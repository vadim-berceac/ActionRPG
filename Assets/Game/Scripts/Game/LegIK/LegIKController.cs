using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace LegIK
{
    /// <summary>
    /// Система IK ног, работающая через Animator IK Goals (OnAnimatorIK), без прокидывания
    /// трансформов костей напрямую. Требует Humanoid-риг с включённым "IK Pass" на слое Animator,
    /// на котором вызывается этот компонент (Base Layer -> Animator Controller -> Layers -> IK Pass).
    ///
    /// Математика сглаживания/демпфирования считается в Burst job (FootIKSolverJob).
    /// Raycast остаётся на главном потоке, т.к. Physics API не бёрстуется.
    /// </summary>
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

        [Header("Debug / Gizmos")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private Color gizmoRayColor = new Color(1f, 0.85f, 0f);
        [SerializeField] private Color gizmoHitColor = Color.green;
        [SerializeField] private Color gizmoNoHitColor = Color.red;
        [SerializeField] private Color gizmoTargetColor = Color.cyan;

        private NativeArray<FootIKJobInput> _inputs;
        private NativeArray<FootIKJobState> _states;
        private NativeArray<FootIKJobOutput> _outputs;

        // Кэш для гизмо — не бёрстовое, чисто для отладки в редакторе.
        private Vector3[] _debugRayOrigin;
        private Vector3[] _debugHitPoint;
        private bool[] _debugHasHit;

        private float _pelvisOffsetCurrent;
        private float _pelvisOffsetVelocity;

        /// <summary>Позволяет извне плавно включать/выключать IK (например, DOTween.To(() => GlobalWeight, ...)).</summary>
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
        }

        private void OnDisable()
        {
            DisposeNative();
        }

        private void AllocateNative()
        {
            DisposeNative();

            int count = feet?.Length ?? 0;
            if (count == 0)
            {
                return;
            }

            _inputs = new NativeArray<FootIKJobInput>(count, Allocator.Persistent);
            _states = new NativeArray<FootIKJobState>(count, Allocator.Persistent);
            _outputs = new NativeArray<FootIKJobOutput>(count, Allocator.Persistent);

            for (int i = 0; i < count; i++)
            {
                _states[i] = FootIKJobState.Default();
            }

            _debugRayOrigin = new Vector3[count];
            _debugHitPoint = new Vector3[count];
            _debugHasHit = new bool[count];
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

            float dt = Time.deltaTime;

            // 1. Raycast на главном потоке (не бёрстуется) + заполнение входа для job.
            for (int i = 0; i < feet.Length; i++)
            {
                LegIKFootSettings foot = feet[i];

                Vector3 animatedPos = animator.GetIKPosition(foot.ikGoal);
                Quaternion animatedRot = animator.GetIKRotation(foot.ikGoal);

                Vector3 rayOrigin = animatedPos + Vector3.up * foot.raycastUpOffset;
                float rayLength = foot.raycastUpOffset + foot.raycastDownDistance;

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
                    hitPoint = hasHit ? hit.point : animatedPos,
                    hitNormal = hasHit ? hit.normal : Vector3.up,
                    hasHit = (byte)(hasHit ? 1 : 0),
                    footOffset = foot.footOffset,
                    soleThickness = foot.soleThickness,
                    maxRotationAngleRad = foot.maxRotationAngle * Mathf.Deg2Rad,
                    maxSlopeAngleRad = foot.maxSlopeAngle * Mathf.Deg2Rad,
                    groundedHeight = foot.groundedHeightThreshold,
                    liftHeight = foot.liftHeightThreshold,
                    positionSmoothTime = foot.positionSmoothTime,
                    rotationSmoothTime = foot.rotationSmoothTime,
                    weightSmoothTime = foot.weightSmoothTime,
                    maxPositionWeight = foot.maxPositionWeight * globalWeight,
                    maxRotationWeight = foot.maxRotationWeight * globalWeight,
                    deltaTime = dt
                };
            }

            // 2. Burst job — вся математика сглаживания/выравнивания.
            // Ног обычно 2 (реже 4), поэтому Schedule+Complete в тот же кадр — это дёшево
            // и снимает необходимость городить двухфреймовую задержку ради джобы на 2 элемента.
            var job = new FootIKSolverJob
            {
                inputs = _inputs,
                states = _states,
                outputs = _outputs
            };
            job.Schedule(feet.Length, 4).Complete();

            // 3. Применение результата к Animator + подстройка таза.
            float lowestOffset = 0f;
            float highestOffset = 0f;

            for (int i = 0; i < feet.Length; i++)
            {
                LegIKFootSettings foot = feet[i];
                FootIKJobOutput output = _outputs[i];

                animator.SetIKPositionWeight(foot.ikGoal, output.positionWeight);
                animator.SetIKRotationWeight(foot.ikGoal, output.rotationWeight);
                animator.SetIKPosition(foot.ikGoal, output.targetPosition);
                animator.SetIKRotation(foot.ikGoal, output.targetRotation);

                if (foot.useHint)
                {
                    Vector3 hintPos = animator.GetIKHintPosition(foot.ikHint) + transform.forward * foot.kneeForwardOffset;
                    animator.SetIKHintPosition(foot.ikHint, hintPos);
                    animator.SetIKHintPositionWeight(foot.ikHint, foot.hintWeight * globalWeight * output.positionWeight);
                }

                lowestOffset = Mathf.Min(lowestOffset, output.heightOffset);
                highestOffset = Mathf.Max(highestOffset, output.heightOffset);
            }

            if (adjustPelvis)
            {
                // Приоритет — опускание таза (безопаснее, чем пропороть ногой землю снизу);
                // подъём применяется, только если ни одна нога не просит опускания.
                float targetPelvisOffset = lowestOffset < 0f ? lowestOffset : highestOffset;
                targetPelvisOffset = Mathf.Clamp(targetPelvisOffset, -maxPelvisLower, maxPelvisRaise);

                _pelvisOffsetCurrent = Mathf.SmoothDamp(_pelvisOffsetCurrent, targetPelvisOffset,
                    ref _pelvisOffsetVelocity, Mathf.Max(pelvisSmoothTime, 0.0001f));

                Vector3 bodyPos = animator.bodyPosition;
                bodyPos.y += _pelvisOffsetCurrent * pelvisWeight * globalWeight;
                animator.bodyPosition = bodyPos;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || _debugRayOrigin == null)
            {
                return;
            }

            for (int i = 0; i < _debugRayOrigin.Length; i++)
            {
                Vector3 origin = _debugRayOrigin[i];
                bool hasHit = _debugHasHit[i];
                Vector3 point = _debugHitPoint[i];

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