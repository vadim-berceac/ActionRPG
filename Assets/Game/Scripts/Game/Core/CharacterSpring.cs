using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;

public class CharacterSpring : MonoBehaviour
{
    private Transform _character;
    private Transform _deformationBody;

    [SerializeField] private Vector3 upScale = new (0.8f, 1.2f, 0.8f);
    [SerializeField] private Vector3 downScale = new (1.2f, 0.8f, 1.2f);

    [SerializeField] private float scaleFactor = 1f;
    [SerializeField] private float rotationFactor = 1f;

    [Header("Spring simulation (заменяет ConfigurableJoint)")]
    [SerializeField] private float stiffness = 400f;
    [SerializeField] private float damping = 20f;
    [SerializeField] private float mass = 1f;

    [Tooltip("Сила раскачки при 'распрямлении' пружины. 0 = раскачки нет вообще (критическое демпфирование). 1 = раскачка полностью в соответствии с damping/stiffness/mass.")]
    [SerializeField, Range(0f, 1f)] private float bounciness = 1f;

    [Header("Smoothing")]
    [Tooltip("Частота сглаживания позиции персонажа. Убирает высокочастотное дрожание от CharacterController и root motion анимации.")]
    [SerializeField, Range(1f, 30f)] private float positionSmoothingHz = 6f;
    [Tooltip("Частота сглаживания поворота персонажа. Убирает дрожание от поворота.")]
    [SerializeField, Range(1f, 30f)] private float rotationSmoothingHz = 6f;

    private Transform _springTransform;
    private float3 _restLocalOffset;

    private NativeArray<SpringState> _state;
    private NativeArray<float3> _resultSpringPosition;
    private NativeArray<float3> _resultScale;
    private NativeArray<float3> _resultRotation;

    public void Initialize(Transform character, Transform deformationBody)
    {
        _springTransform = transform;
        _character = character;
        _deformationBody = deformationBody;

        name = _character.name + "_Spring";

        var invCharRot = math.inverse((quaternion)_character.rotation);
        _restLocalOffset = math.mul(invCharRot, (float3)_springTransform.position - (float3)_character.position);

        _state = new NativeArray<SpringState>(1, Allocator.Persistent);
        _resultSpringPosition = new NativeArray<float3>(1, Allocator.Persistent);
        _resultScale = new NativeArray<float3>(1, Allocator.Persistent);
        _resultRotation = new NativeArray<float3>(1, Allocator.Persistent);

        var startPos = (float3)_character.position;
        var startRot = (quaternion)_character.rotation;

        _state[0] = new SpringState
        {
            currentPosition = startPos + math.mul(startRot, _restLocalOffset),
            velocity = float3.zero,
            smoothedPosition = startPos,
            smoothedRotation = startRot
        };

        _resultSpringPosition[0] = _state[0].currentPosition;
        _resultScale[0] = new float3(1f, 1f, 1f);
        _resultRotation[0] = float3.zero;
    }

    private void LateUpdate()
    {
        var job = new SpringUpdateJob
        {
            rawCharacterPosition = _character.position,
            rawCharacterRotation = _character.rotation,
            restLocalOffset = _restLocalOffset,
            dt = Time.deltaTime,

            positionSmoothingHz = positionSmoothingHz,
            rotationSmoothingHz = rotationSmoothingHz,

            stiffness = stiffness,
            damping = damping,
            mass = mass,
            bounciness = bounciness,

            upScale = upScale,
            downScale = downScale,
            scaleFactor = scaleFactor,
            rotationFactor = rotationFactor,

            state = _state,
            resultSpringPosition = _resultSpringPosition,
            resultScale = _resultScale,
            resultRotation = _resultRotation
        };

        job.Run();

        _springTransform.position = _resultSpringPosition[0];
        _deformationBody.localScale = _resultScale[0];
        _deformationBody.localEulerAngles = _resultRotation[0];
    }

    private void OnDisable()
    {
        if (_state.IsCreated) _state.Dispose();
        if (_resultSpringPosition.IsCreated) _resultSpringPosition.Dispose();
        if (_resultScale.IsCreated) _resultScale.Dispose();
        if (_resultRotation.IsCreated) _resultRotation.Dispose();
    }

    private struct SpringState
    {
        public float3 currentPosition;
        public float3 velocity;
        public float3 smoothedPosition;
        public quaternion smoothedRotation;
    }

    [BurstCompile]
    private struct SpringUpdateJob : IJob
    {
        public float3 rawCharacterPosition;
        public quaternion rawCharacterRotation;
        public float3 restLocalOffset;
        public float dt;

        public float positionSmoothingHz;
        public float rotationSmoothingHz;

        public float stiffness;
        public float damping;
        public float mass;
        public float bounciness;

        public float3 upScale;
        public float3 downScale;
        public float scaleFactor;
        public float rotationFactor;

        public NativeArray<SpringState> state;
        public NativeArray<float3> resultSpringPosition;
        public NativeArray<float3> resultScale;
        public NativeArray<float3> resultRotation;

        public void Execute()
        {
            var s = state[0];

            if (dt > 0f)
            {
                var posAlpha = 1f - math.exp(-dt * positionSmoothingHz);
                var rotAlpha = 1f - math.exp(-dt * rotationSmoothingHz);

                s.smoothedPosition = math.lerp(s.smoothedPosition, rawCharacterPosition, posAlpha);
                s.smoothedRotation = math.slerp(s.smoothedRotation, rawCharacterRotation, rotAlpha);

                var targetPosition = s.smoothedPosition + math.mul(s.smoothedRotation, restLocalOffset);

                var w = math.sqrt(stiffness / mass);
                var baseZeta = damping / (2f * math.sqrt(stiffness * mass));
                var zeta = math.lerp(1f, baseZeta, bounciness);

                SpringDamperExact(ref s.currentPosition, ref s.velocity, targetPosition, w, zeta, dt);
            }

            state[0] = s;
            resultSpringPosition[0] = s.currentPosition;

            var relativePosition = math.mul(math.inverse(s.smoothedRotation), s.currentPosition - s.smoothedPosition);

            var interpolant = relativePosition.y * scaleFactor;
            var currentScale = Lerp3(downScale, new float3(1f, 1f, 1f), upScale, interpolant);
            var rotation = new float3(relativePosition.z, 0f, -relativePosition.x) * rotationFactor;

            resultScale[0] = currentScale;
            resultRotation[0] = math.degrees(rotation);
        }

        private static void SpringDamperExact(ref float3 pos, ref float3 vel, float3 target,
            float w, float zeta, float dt)
        {
            var x0 = pos - target;
            var v0 = vel;

            float3 x1, v1;

            const float epsilon = 0.0001f;

            if (math.abs(zeta - 1f) < epsilon)
            {
                var ex = math.exp(-w * dt);
                x1 = (x0 + (v0 + w * x0) * dt) * ex;
                v1 = (v0 - (v0 + w * x0) * w * dt) * ex;
            }
            else if (zeta < 1f)
            {
                var wd = w * math.sqrt(1f - zeta * zeta);
                var ex = math.exp(-zeta * w * dt);

                var c1 = x0;
                var c2 = (v0 + zeta * w * x0) / wd;

                var cosWd = math.cos(wd * dt);
                var sinWd = math.sin(wd * dt);

                x1 = ex * (c1 * cosWd + c2 * sinWd);
                v1 = ex * (-zeta * w * (c1 * cosWd + c2 * sinWd) + wd * (-c1 * sinWd + c2 * cosWd));
            }
            else
            {
                var wd = w * math.sqrt(zeta * zeta - 1f);
                var r1 = -zeta * w + wd;
                var r2 = -zeta * w - wd;

                var c2 = (v0 - r1 * x0) / (r2 - r1);
                var c1 = x0 - c2;

                var e1 = math.exp(r1 * dt);
                var e2 = math.exp(r2 * dt);

                x1 = c1 * e1 + c2 * e2;
                v1 = c1 * r1 * e1 + c2 * r2 * e2;
            }

            pos = target + x1;
            vel = v1;
        }

        private static float3 Lerp3(float3 a, float3 b, float3 c, float t)
        {
            return t < 0f
                ? math.lerp(a, b, t + 1f)
                : math.lerp(b, c, t);
        }
    }
}