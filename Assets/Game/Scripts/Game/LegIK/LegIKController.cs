using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace LegIK
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public class LegIKController : MonoBehaviour
    {
        [Header("Animator")]
        [Tooltip("Если не назначен — берётся GetComponent<Animator>() в Awake")]
        [SerializeField] private Animator animator;

        [Header("Общие настройки")]
        [SerializeField] private bool enableIK = true;

        [Tooltip("Общий множитель веса IK — удобно для плавного включения/выключения системы извне (например, при приземлении или рэгдолле)")]
        [Range(0f, 1f)]
        [SerializeField] private float globalWeight = 1f;

        [Header("Ноги")]
        [SerializeField]
        private LegIKFootSettings[] feet =
        {
            new LegIKFootSettings(AvatarIKGoal.LeftFoot, AvatarIKHint.LeftKnee),
            new LegIKFootSettings(AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee)
        };

        [Header("Подстройка таза (Pelvis / Hips)")]
        [Tooltip("Опускать/поднимать таз, чтобы обе ноги доставали до земли на склонах и ступенях")]
        [SerializeField] private bool adjustPelvis = true;

        [Range(0f, 1f)]
        [SerializeField] private float pelvisWeight = 1f;

        [Tooltip("Время сглаживания вертикального смещения таза")]
        [Range(0.001f, 0.5f)]
        [SerializeField] private float pelvisSmoothTime = 0.1f;

        [Tooltip("Максимум, на который таз может опуститься (метры)")]
        [Range(0f, 1f)]
        [SerializeField] private float maxPelvisLower = 0.4f;

        [Tooltip("Максимум, на который таз может подняться (метры)")]
        [Range(0f, 1f)]
        [SerializeField] private float maxPelvisRaise = 0.2f;

        [Header("Character Spring (squash & stretch)")]
        [Tooltip("Модель со скелетом (дочерний объект Character Transform) — на неё применяется squash&stretch деформация")]
        [SerializeField] private Transform deformationBody;

        [SerializeField] private CharacterSpringSolver characterSpring = new();

        [Tooltip("Насколько наклон тела от пружины передаётся в таз/ноги через animator.bodyRotation. 0 = ноги не наклоняются вместе с телом (только верх тела через анимацию/другие средства), 1 = полный наклон, как посчитала пружина.")]
        [Range(0f, 1f)]
        [SerializeField] private float bodyLeanWeight = 1f;

        [Header("Debug / Gizmos")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private Color gizmoRayColor = new Color(1f, 0.85f, 0f);
        [SerializeField] private Color gizmoHitColor = Color.green;
        [SerializeField] private Color gizmoNoHitColor = Color.red;
        [SerializeField] private Color gizmoTargetColor = Color.cyan;

        private NativeArray<FootIKJobInput> _inputs;
        private NativeArray<FootIKJobState> _states;
        private NativeArray<FootIKJobOutput> _outputs;

        private Vector3[] _debugRayOrigin;
        private Vector3[] _debugHitPoint;
        private bool[] _debugHasHit;

        private float[] _unleanedFootY;

        private float _pelvisOffsetCurrent;
        private float _pelvisOffsetVelocity;

        public float GlobalWeight
        {
            get => globalWeight;
            set => globalWeight = Mathf.Clamp01(value);
        }

        public bool EnableIK
        {
            get => enableIK;
            set => enableIK = value;
        }

        public void SetGlobalWeight(float weight)
        {
            globalWeight = Mathf.Clamp01(weight);
        }

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }

        private void OnEnable()
        {
            AllocateNative();

            if (deformationBody != null)
                characterSpring.Initialize(transform, deformationBody);
        }

        private void OnDisable()
        {
            DisposeNative();
            characterSpring.Dispose();
        }

        private void OnAnimatorMove()
        {
            characterSpring.Tick(Time.deltaTime);
        }

        private void AllocateNative()
        {
            DisposeNative();

            var count = feet?.Length ?? 0;
            if (count == 0)
            {
                return;
            }

            _inputs = new NativeArray<FootIKJobInput>(count, Allocator.Persistent);
            _states = new NativeArray<FootIKJobState>(count, Allocator.Persistent);
            _outputs = new NativeArray<FootIKJobOutput>(count, Allocator.Persistent);

            for (var i = 0; i < count; i++)
            {
                _states[i] = FootIKJobState.Default();
            }

            _debugRayOrigin = new Vector3[count];
            _debugHitPoint = new Vector3[count];
            _debugHasHit = new bool[count];
            _unleanedFootY = new float[count];
        }

        private void DisposeNative()
        {
            if (_inputs.IsCreated) _inputs.Dispose();
            if (_states.IsCreated) _states.Dispose();
            if (_outputs.IsCreated) _outputs.Dispose();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!enableIK || animator == null || feet == null || feet.Length == 0)
            {
                return;
            }

            if (!_inputs.IsCreated || _inputs.Length != feet.Length)
            {
                AllocateNative();
                if (!_inputs.IsCreated)
                {
                    return;
                }
            }

            for (var i = 0; i < feet.Length; i++)
            {
                _unleanedFootY[i] = animator.GetIKPosition(feet[i].ikGoal).y;
            }

            if (bodyLeanWeight > 0f)
            {
                var lean = characterSpring.LeanEulerAngles * (globalWeight * bodyLeanWeight);
                if (lean != Vector3.zero)
                {
                    animator.bodyRotation *= Quaternion.Euler(lean);
                }
            }

            var dt = Time.deltaTime;

            for (var i = 0; i < feet.Length; i++)
            {
                var foot = feet[i];

                var animatedPos = animator.GetIKPosition(foot.ikGoal);
                var animatedRot = animator.GetIKRotation(foot.ikGoal);

                var rayOrigin = animatedPos + Vector3.up * foot.raycastUpOffset;
                var rayLength = foot.raycastUpOffset + foot.raycastDownDistance;

                bool hasHit;
                RaycastHit hit;

                if (foot.useSphereCast)
                {
                    hasHit = Physics.SphereCast(rayOrigin, foot.sphereCastRadius, Vector3.down, out hit,
                        rayLength, foot.groundMask, QueryTriggerInteraction.Ignore);
                }
                else
                {
                    hasHit = Physics.Raycast(rayOrigin, Vector3.down, out hit,
                        rayLength, foot.groundMask, QueryTriggerInteraction.Ignore);
                }

                _debugRayOrigin[i] = rayOrigin;
                _debugHasHit[i] = hasHit;
                _debugHitPoint[i] = hasHit ? hit.point : rayOrigin + Vector3.down * rayLength;

                _inputs[i] = new FootIKJobInput
                {
                    animatedPosition = animatedPos,
                    animatedRotation = animatedRot,
                    unleanedAnimatedPositionY = _unleanedFootY[i],
                    hitPoint = hasHit ? hit.point : animatedPos,
                    hitNormal = hasHit ? hit.normal : Vector3.up,
                    hasHit = (byte)(hasHit ? 1 : 0),
                    footOffset = foot.footOffset,
                    soleThickness = foot.soleThickness,
                    maxRotationAngleRad = foot.maxRotationAngle * Mathf.Deg2Rad,
                    maxSlopeAngleRad = foot.maxSlopeAngle * Mathf.Deg2Rad,
                    groundedSpeedThreshold = foot.groundedSpeedThreshold,
                    liftSpeedThreshold = foot.liftSpeedThreshold,
                    positionSmoothTime = foot.positionSmoothTime,
                    rotationSmoothTime = foot.rotationSmoothTime,
                    weightSmoothTime = foot.weightSmoothTime,
                    maxPositionWeight = foot.maxPositionWeight * globalWeight,
                    maxRotationWeight = foot.maxRotationWeight * globalWeight,
                    deltaTime = dt
                };
            }

            var job = new FootIKSolverJob
            {
                inputs = _inputs,
                states = _states,
                outputs = _outputs
            };
            job.Schedule(feet.Length, 4).Complete();

            var lowestOffset = 0f;
            var highestOffset = 0f;

            for (var i = 0; i < feet.Length; i++)
            {
                var foot = feet[i];
                var output = _outputs[i];

                animator.SetIKPositionWeight(foot.ikGoal, output.positionWeight);
                animator.SetIKRotationWeight(foot.ikGoal, output.rotationWeight);
                animator.SetIKPosition(foot.ikGoal, output.targetPosition);
                animator.SetIKRotation(foot.ikGoal, output.targetRotation);

                if (foot.useHint)
                {
                    var hintPos = animator.GetIKHintPosition(foot.ikHint) + transform.forward * foot.kneeForwardOffset;
                    animator.SetIKHintPosition(foot.ikHint, hintPos);
                    animator.SetIKHintPositionWeight(foot.ikHint, foot.hintWeight * globalWeight * output.positionWeight);
                }

                var weightedOffset = output.heightOffset * output.positionWeight;
                lowestOffset = Mathf.Min(lowestOffset, weightedOffset);
                highestOffset = Mathf.Max(highestOffset, weightedOffset);
            }

            if (!adjustPelvis)
            {
               return;
            }

            var targetPelvisOffset = lowestOffset < 0f ? lowestOffset : highestOffset;
            targetPelvisOffset = Mathf.Clamp(targetPelvisOffset, -maxPelvisLower, maxPelvisRaise);

            _pelvisOffsetCurrent = Mathf.SmoothDamp(_pelvisOffsetCurrent, targetPelvisOffset,
                ref _pelvisOffsetVelocity, Mathf.Max(pelvisSmoothTime, 0.0001f));

            var bodyPos = animator.bodyPosition;
            bodyPos.y += _pelvisOffsetCurrent * pelvisWeight * globalWeight;
            animator.bodyPosition = bodyPos;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || _debugRayOrigin == null)
            {
                return;
            }

            for (var i = 0; i < _debugRayOrigin.Length; i++)
            {
                var origin = _debugRayOrigin[i];
                var hasHit = _debugHasHit[i];
                var point = _debugHitPoint[i];

                Gizmos.color = gizmoRayColor;
                Gizmos.DrawLine(origin, point);

                Gizmos.color = hasHit ? gizmoHitColor : gizmoNoHitColor;
                Gizmos.DrawWireSphere(point, 0.04f);

                if (hasHit && _outputs.IsCreated && i < _outputs.Length)
                {
                    Gizmos.color = gizmoTargetColor;
                    Gizmos.DrawWireSphere(_outputs[i].targetPosition, 0.03f);
                }
            }
        }
    }
}