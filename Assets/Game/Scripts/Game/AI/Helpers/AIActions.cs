using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class AIActions
{
    private struct StuckTracker
    {
        private Vector3 _samplePosition;
        private float _bestDistanceSq;
        private float _stuckElapsed;
        private float _sampleTimer;

        public void Begin(Vector3 position, float distanceSq)
        {
            _samplePosition = position;
            _bestDistanceSq = distanceSq;
            _stuckElapsed = 0f;
            _sampleTimer = 0f;
        }

        public bool Update(Vector3 position, float distanceSq, float deltaTime)
        {
            _sampleTimer += deltaTime;
            if (_sampleTimer < Constants.StuckSampleInterval)
                return false;

            _sampleTimer = 0f;

            if (_bestDistanceSq - distanceSq > Constants.StuckMinProgressSq)
            {
                _bestDistanceSq = distanceSq;
                _stuckElapsed = 0f;
                _samplePosition = position;
                return false;
            }

            var delta = position - _samplePosition;
            delta.y = 0f;
            var movedSq = delta.sqrMagnitude;
            _samplePosition = position;

            if (movedSq < Constants.StuckMinMoveSq)
                _stuckElapsed += Constants.StuckSampleInterval;
            else
                _stuckElapsed += Constants.StuckSampleInterval * 0.5f;

            return _stuckElapsed >= Constants.StuckTimeThreshold;
        }

        public void Reset(Vector3 position, float distanceSq) => Begin(position, distanceSq);
    }

    public static async UniTask MoveTowardsAsync(Vector3 destination, CancellationToken ct, AsyncStateMachine fsm)
    {
        var input = fsm.Ctx.Input;
        var transform = fsm.Ctx.Transform;
        var stuckTracker = new StuckTracker();
        var unstuckAttempts = 0;
        var jumpAttempted = false;
        var arriveThresholdSq = Constants.ArriveThreshold * Constants.ArriveThreshold;
        var jumpTriggerDistanceSq = Constants.JumpTriggerDistance * Constants.JumpTriggerDistance;
        var hasTracker = false;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            if (transform == null)
                throw new OperationCanceledException();

            var position = transform.position;
            var toTarget = destination - position;
            toTarget.y = 0f;

            var distanceSq = toTarget.sqrMagnitude;
            if (distanceSq <= arriveThresholdSq)
                break;

            var heightDelta = destination.y - position.y;
            var requiresJump = IsJumpableHeightDelta(heightDelta);

            if (!hasTracker)
            {
                stuckTracker.Begin(position, distanceSq);
                hasTracker = true;
            }
            else if (stuckTracker.Update(position, distanceSq, Time.deltaTime))
            {
                if (requiresJump && !jumpAttempted && fsm.Ctx.IsGrounded)
                {
                    var jumped = await TryExecuteJumpAsync(input, transform, fsm.Ctx, ct);
                    jumpAttempted = true;

                    if (jumped)
                    {
                        if (transform == null)
                            throw new OperationCanceledException();

                        stuckTracker.Reset(transform.position, (destination - transform.position).Flatten().sqrMagnitude);
                        continue;
                    }
                }

                if (unstuckAttempts >= Constants.MaxUnstuckAttempts)
                    break;

                await ExecuteUnstuckManeuverAsync(transform, input, destination, unstuckAttempts, ct);
                unstuckAttempts++;

                if (transform == null)
                    throw new OperationCanceledException();

                stuckTracker.Reset(transform.position, (destination - transform.position).Flatten().sqrMagnitude);
                continue;
            }

            var distance = Mathf.Sqrt(distanceSq);
            var direction = toTarget / distance;
            var targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            input.RotationYaw = targetYaw;

            var angleDiff = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, targetYaw));
            var distanceFactor = Mathf.Clamp(distance / Constants.SlowRadius, Constants.MinThrottle, 1f);
            var angleFactor = Mathf.Clamp01(1f - angleDiff / Constants.MaxTurnAngleForFullSpeed);
            var speedFactor = Mathf.Max(Constants.MinThrottle, Mathf.Min(distanceFactor, angleFactor));
            input.MoveInput = new Vector2(0f, speedFactor);

            if (requiresJump
                && !jumpAttempted
                && fsm.Ctx.IsGrounded
                && distanceSq <= jumpTriggerDistanceSq)
            {
                jumpAttempted = true;
                await TryExecuteJumpAsync(input, transform, fsm.Ctx, ct);

                if (transform == null)
                    throw new OperationCanceledException();

                stuckTracker.Reset(transform.position, (destination - transform.position).Flatten().sqrMagnitude);
                continue;
            }

            await UniTask.Yield(PlayerLoopTiming.Update, ct);

            if (transform == null)
                throw new OperationCanceledException();
        }

        input.MoveInput = Vector2.zero;
        input.JumpInput = false;
    }

    private static bool IsJumpableHeightDelta(float heightDelta) =>
        heightDelta >= Constants.MinJumpHeight && heightDelta <= Constants.MaxJumpHeight;

    private static async UniTask<bool> TryExecuteJumpAsync(
        IInput input,
        Transform transform,
        StateMachineContext ctx,
        CancellationToken ct)
    {
        if (!ctx.IsGrounded)
            return false;

        input.MoveInput = new Vector2(0f, 1f);

        for (var i = 0; i < Constants.JumpInputFrames; i++)
        {
            ct.ThrowIfCancellationRequested();
            input.JumpInput = true;
            await UniTask.Yield(PlayerLoopTiming.FixedUpdate, ct);
        }

        input.JumpInput = false;

        var elapsed = 0f;
        while (!ctx.IsGrounded && elapsed < Constants.JumpLandingTimeout)
        {
            ct.ThrowIfCancellationRequested();

            if (transform == null)
                throw new OperationCanceledException();

            input.MoveInput = new Vector2(0f, 1f);
            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        input.MoveInput = Vector2.zero;
        return ctx.IsGrounded;
    }

    private static async UniTask ExecuteUnstuckManeuverAsync(
        Transform transform,
        IInput input,
        Vector3 destination,
        int attempt,
        CancellationToken ct)
    {
        var side = ResolveUnstuckSide(transform, destination, attempt);
        var phase = attempt % 3;
        var elapsed = 0f;

        while (elapsed < Constants.UnstuckManeuverDuration)
        {
            ct.ThrowIfCancellationRequested();

            if (transform == null)
                throw new OperationCanceledException();

            switch (phase)
            {
                case 0:
                    input.MoveInput = new Vector2(side * Constants.UnstuckStrafeInput, Constants.UnstuckForwardInput);
                    input.RotationYaw = transform.eulerAngles.y + side * Constants.UnstuckYawOffset;
                    break;
                case 1:
                    input.MoveInput = new Vector2(-side * Constants.UnstuckStrafeInput, Constants.UnstuckForwardInput);
                    input.RotationYaw = transform.eulerAngles.y;
                    break;
                default:
                    input.MoveInput = new Vector2(side * Constants.UnstuckStrafeInput * 0.5f, -Constants.UnstuckBackwardInput);
                    input.RotationYaw = transform.eulerAngles.y - side * Constants.UnstuckYawOffset;
                    break;
            }

            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        input.MoveInput = Vector2.zero;
        input.JumpInput = false;
    }

    private static float ResolveUnstuckSide(Transform transform, Vector3 destination, int attempt)
    {
        var defaultSide = (transform.GetInstanceID() & 1) == 0 ? 1f : -1f;
        if (attempt > 0)
            return attempt % 2 == 0 ? defaultSide : -defaultSide;

        var toTarget = destination - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
            return defaultSide;

        var forward = toTarget.normalized;
        var right = Vector3.Cross(Vector3.up, forward);
        var origin = transform.position + Vector3.up;

        var forwardBlocked = Physics.Raycast(
            origin, forward, Constants.StuckProbeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        if (!forwardBlocked)
            return defaultSide;

        var rightClear = !Physics.Raycast(
            origin, right, Constants.StuckProbeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        var leftClear = !Physics.Raycast(
            origin, -right, Constants.StuckProbeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        if (rightClear && !leftClear) return 1f;
        if (leftClear && !rightClear) return -1f;

        return defaultSide;
    }

    private static Vector3 Flatten(this Vector3 vector)
    {
        vector.y = 0f;
        return vector;
    }

    public static async UniTask AttackAsync(CancellationToken ct, AsyncStateMachine fsm)
    {
        var input = fsm.Ctx.Input;
        var transform = fsm.Ctx.Transform;

        ct.ThrowIfCancellationRequested();

        if (transform == null)
            throw new OperationCanceledException();

        var useSecondary = fsm.Ctx.HasAdditionalWeapon && UnityEngine.Random.value < 0.33f;

        if (useSecondary)
            input.Attack2 = true;
        else
            input.Attack1 = true;

        await UniTask.Delay(5, cancellationToken: ct);

        if (useSecondary)
            input.Attack2 = false;
        else
            input.Attack1 = false;
    }
}
