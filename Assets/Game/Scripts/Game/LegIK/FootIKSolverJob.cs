using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace LegIK
{
    /// <summary>
    /// Входные данные на кадр для одной ноги. Заполняются на главном потоке
    /// (raycast не бёрстуется), передаются в job как read-only.
    /// </summary>
    public struct FootIKJobInput
    {
        public float3 animatedPosition;
        public quaternion animatedRotation;

        public float3 hitPoint;
        public float3 hitNormal;
        public byte hasHit;

        public float3 footOffset;
        public float soleThickness;

        public float maxRotationAngleRad;
        public float maxSlopeAngleRad;

        public float groundedHeight;
        public float liftHeight;

        public float positionSmoothTime;
        public float rotationSmoothTime;
        public float weightSmoothTime;

        public float maxPositionWeight;
        public float maxRotationWeight;

        public float deltaTime;
    }

    /// <summary>
    /// Состояние ноги, которое живёт между кадрами (скорости демпфирования и т.п.).
    /// Хранится персистентно в NativeArray на контроллере.
    /// </summary>
    public struct FootIKJobState
    {
        public float3 currentPosition;
        public float3 positionVelocity;
        public quaternion currentRotation;
        public float currentWeight;
        public byte initialized;

        public static FootIKJobState Default()
        {
            return new FootIKJobState
            {
                currentPosition = float3.zero,
                positionVelocity = float3.zero,
                currentRotation = quaternion.identity,
                currentWeight = 0f,
                initialized = 0
            };
        }
    }

    /// <summary>
    /// Результат солвинга — то, что контроллер применяет к Animator через SetIKPosition/Rotation/Weight.
    /// </summary>
    public struct FootIKJobOutput
    {
        public float3 targetPosition;
        public quaternion targetRotation;
        public float positionWeight;
        public float rotationWeight;

        /// <summary>Смещение по Y относительно анимированной позиции — используется контроллером для подстройки таза.</summary>
        public float heightOffset;
    }

    /// <summary>
    /// Основная математика Leg IK: проверка уклона, выравнивание стопы по нормали земли,
    /// демпфированное сглаживание позиции/поворота/веса. Полностью Burst-совместимо —
    /// никаких managed-типов, только blittable-структуры и Unity.Mathematics.
    /// </summary>
    [BurstCompile]
    public struct FootIKSolverJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<FootIKJobInput> inputs;
        public NativeArray<FootIKJobState> states;
        [WriteOnly] public NativeArray<FootIKJobOutput> outputs;

        public void Execute(int index)
        {
            FootIKJobInput input = inputs[index];
            FootIKJobState state = states[index];

            float3 desiredPos = input.animatedPosition;
            quaternion desiredRot = input.animatedRotation;
            float targetWeight = 0f;

            if (input.hasHit == 1)
            {
                float slopeAngle = AngleBetween(input.hitNormal, math.up());
                bool withinSlope = slopeAngle <= input.maxSlopeAngleRad;

                if (withinSlope)
                {
                    desiredPos = input.hitPoint + input.footOffset + math.up() * input.soleThickness;

                    // Важно: берём "up" именно текущей анимированной ориентации стопы, а не мировой up.
                    // Так поворот выравнивает то направление, которое кость и правда считает "подошвой вниз",
                    // а не абстрактный мировой верх — иначе на риге, где локальный up кости не совпадает
                    // с мировым, стопа доворачивается с ошибкой (в т.ч. на ~90 градусов).
                    float3 currentFootUp = math.mul(input.animatedRotation, math.up());
                    quaternion normalRot = FromToRotationClamped(currentFootUp, input.hitNormal, input.maxRotationAngleRad);
                    desiredRot = math.mul(normalRot, input.animatedRotation);

                    // Вес IK зависит от того, насколько анимированная стопа сейчас приподнята над землёй.
                    // Сравниваем не с сырой точкой луча, а с точкой, где стопа окажется в состоянии полного
                    // контакта с землёй (с учётом footOffset и soleThickness) — иначе пороги groundedHeight/
                    // liftHeight означали бы разное в зависимости от того, какой offset задан на ноге.
                    float restY = input.hitPoint.y + input.footOffset.y + input.soleThickness;
                    float heightAboveGround = input.animatedPosition.y - restY;
                    float liftRange = math.max(input.liftHeight - input.groundedHeight, 0.0001f);
                    float liftT = math.saturate((heightAboveGround - input.groundedHeight) / liftRange);
                    targetWeight = 1f - liftT;
                }
            }

            float3 newPos;
            quaternion newRot;
            float newWeight;

            if (state.initialized == 0)
            {
                // Первый кадр — не сглаживаем, чтобы не было прыжка из нуля.
                newPos = desiredPos;
                newRot = desiredRot;
                newWeight = targetWeight;
                state.positionVelocity = float3.zero;
                state.initialized = 1;
            }
            else
            {
                newPos = SmoothDamp(state.currentPosition, desiredPos, ref state.positionVelocity,
                    math.max(input.positionSmoothTime, 0.0001f), input.deltaTime);

                float rotT = 1f - math.exp(-input.deltaTime / math.max(input.rotationSmoothTime, 0.0001f));
                newRot = math.nlerp(state.currentRotation, desiredRot, rotT);

                float weightT = 1f - math.exp(-input.deltaTime / math.max(input.weightSmoothTime, 0.0001f));
                newWeight = math.lerp(state.currentWeight, targetWeight, weightT);
            }

            state.currentPosition = newPos;
            state.currentRotation = newRot;
            state.currentWeight = newWeight;
            states[index] = state;

            outputs[index] = new FootIKJobOutput
            {
                targetPosition = newPos,
                targetRotation = newRot,
                positionWeight = newWeight * input.maxPositionWeight,
                rotationWeight = newWeight * input.maxRotationWeight,
                heightOffset = newPos.y - input.animatedPosition.y
            };
        }

        private static float AngleBetween(float3 a, float3 b)
        {
            float d = math.clamp(math.dot(math.normalize(a), math.normalize(b)), -1f, 1f);
            return math.acos(d);
        }

        private static quaternion FromToRotationClamped(float3 from, float3 to, float maxAngleRad)
        {
            float angle = AngleBetween(from, to);
            if (angle < 1e-5f)
            {
                return quaternion.identity;
            }

            float3 axis = math.cross(from, to);
            float axisLen = math.length(axis);
            if (axisLen < 1e-6f)
            {
                return quaternion.identity;
            }

            axis /= axisLen;
            float clampedAngle = math.min(angle, maxAngleRad);
            return quaternion.AxisAngle(axis, clampedAngle);
        }

        /// <summary>
        /// float3-версия Mathf.SmoothDamp (та же формула — Game Programming Gems 4, 1.10),
        /// переписанная под Unity.Mathematics, чтобы быть Burst-совместимой.
        /// </summary>
        private static float3 SmoothDamp(float3 current, float3 target, ref float3 velocity, float smoothTime, float dt)
        {
            float omega = 2f / smoothTime;
            float x = omega * dt;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);

            float3 change = current - target;
            float3 temp = (velocity + omega * change) * dt;
            velocity = (velocity - omega * temp) * exp;
            return target + (change + temp) * exp;
        }
    }
}