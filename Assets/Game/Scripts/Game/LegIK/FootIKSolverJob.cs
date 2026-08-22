using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace LegIK
{
    public struct FootIKJobInput
    {
        public float3 animatedPosition;
        public quaternion animatedRotation;
        
        public float unleanedAnimatedPositionY;

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

    public struct FootIKJobOutput
    {
        public float3 targetPosition;
        public quaternion targetRotation;
        public float positionWeight;
        public float rotationWeight;
        public float heightOffset;
    }

   
    [BurstCompile]
    public struct FootIKSolverJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<FootIKJobInput> inputs;
        public NativeArray<FootIKJobState> states;
        [WriteOnly] public NativeArray<FootIKJobOutput> outputs;

        public void Execute(int index)
        {
            var input = inputs[index];
            var state = states[index];

            var desiredPos = input.animatedPosition;
            var desiredRot = input.animatedRotation;
            var targetWeight = 0f;

            if (input.hasHit == 1)
            {
                var slopeAngle = AngleBetween(input.hitNormal, math.up());
                var withinSlope = slopeAngle <= input.maxSlopeAngleRad;

                if (withinSlope)
                {
                    desiredPos = input.hitPoint + input.footOffset + math.up() * input.soleThickness;

                    var currentFootUp = math.mul(input.animatedRotation, math.up());
                    var normalRot = FromToRotationClamped(currentFootUp, input.hitNormal, input.maxRotationAngleRad);
                    desiredRot = math.mul(normalRot, input.animatedRotation);

                    var restY = input.hitPoint.y + input.footOffset.y + input.soleThickness;
                    var heightAboveGround = input.unleanedAnimatedPositionY - restY;
                    var liftRange = math.max(input.liftHeight - input.groundedHeight, 0.0001f);
                    var liftT = math.saturate((heightAboveGround - input.groundedHeight) / liftRange);
                    targetWeight = 1f - liftT;
                }
            }

            float3 newPos;
            quaternion newRot;
            float newWeight;

            if (state.initialized == 0)
            {
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

                var rotT = 1f - math.exp(-input.deltaTime / math.max(input.rotationSmoothTime, 0.0001f));
                newRot = math.nlerp(state.currentRotation, desiredRot, rotT);

                var weightT = 1f - math.exp(-input.deltaTime / math.max(input.weightSmoothTime, 0.0001f));
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
            var d = math.clamp(math.dot(math.normalize(a), math.normalize(b)), -1f, 1f);
            return math.acos(d);
        }

        private static quaternion FromToRotationClamped(float3 from, float3 to, float maxAngleRad)
        {
            var angle = AngleBetween(from, to);
            if (angle < 1e-5f)
            {
                return quaternion.identity;
            }

            var axis = math.cross(from, to);
            var axisLen = math.length(axis);
            if (axisLen < 1e-6f)
            {
                return quaternion.identity;
            }

            axis /= axisLen;
            var clampedAngle = math.min(angle, maxAngleRad);
            return quaternion.AxisAngle(axis, clampedAngle);
        }

      
        private static float3 SmoothDamp(float3 current, float3 target, ref float3 velocity, float smoothTime, float dt)
        {
            var omega = 2f / smoothTime;
            var x = omega * dt;
            var exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);

            var change = current - target;
            var temp = (velocity + omega * change) * dt;
            velocity = (velocity - omega * temp) * exp;
            return target + (change + temp) * exp;
        }
    }
}