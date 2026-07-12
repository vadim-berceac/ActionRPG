using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class AIMove
{
    public static async UniTask MoveTowardsAsync(Vector3 destination, CancellationToken ct, AsyncStateMachine fsm)
    {
        var input = fsm.Ctx.Input;
        var transform = fsm.Ctx.Transform;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            if (transform == null)
                throw new OperationCanceledException();

            var toTarget = destination - transform.position;
            toTarget.y = 0f;

            var distance = toTarget.magnitude;
            if (distance <= Constants.ArriveThreshold) break;

            var direction = toTarget / distance;
            var targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            input.RotationYaw = targetYaw;

            var angleDiff = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, targetYaw));

            var distanceFactor = Mathf.Clamp(distance / Constants.SlowRadius, Constants.MinThrottle, 1f);
            var angleFactor = Mathf.Clamp01(1f - angleDiff / Constants.MaxTurnAngleForFullSpeed);

            var speedFactor = Mathf.Max(Constants.MinThrottle, Mathf.Min(distanceFactor, angleFactor));
            input.MoveInput = new Vector2(0f, speedFactor);

            await UniTask.Yield(PlayerLoopTiming.Update, ct);

            if (transform == null)
                throw new OperationCanceledException();
        }

        input.MoveInput = Vector2.zero;
    }
}
