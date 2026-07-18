using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

public static class AIActions
{
    private struct StuckTracker
    {
        private Vector3 _samplePosition;
        private float _bestDistanceSq;
        private float _stuckElapsed;
        private float _sampleTimer;
        private float _bestHeightDelta;
        private int _stuckAtArriveCount;

        public void Begin(Vector3 position, float distanceSq)
        {
            _samplePosition = position;
            _bestDistanceSq = distanceSq;
            _stuckElapsed = 0f;
            _sampleTimer = 0f;
            _bestHeightDelta = 0f;
            _stuckAtArriveCount = 0;
        }

        public bool Update(Vector3 position, float distanceSq, float heightDelta, float deltaTime)
        {
            _sampleTimer += deltaTime;
            if (_sampleTimer < Constants.StuckSampleInterval)
                return false;

            _sampleTimer = 0f;

            // Track absolute height difference to detect stepped/cliffed terrain
            // where XZ is near destination but Y is blocked.
            var absHeightDelta = Mathf.Abs(heightDelta);
            if (absHeightDelta > _bestHeightDelta)
                _bestHeightDelta = absHeightDelta;

            // Detect "arrived on XZ but not on Y" — staircase deadlock
            if (distanceSq < Constants.StuckArriveXzThresholdSq && absHeightDelta > Constants.ArriveHeightThreshold)
            {
                _stuckAtArriveCount++;
                if (_stuckAtArriveCount >= Constants.StuckArriveMaxCount)
                    return true;
            }

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
        var lastDestination = destination;
        var revisitCount = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            if (transform == null)
                throw new OperationCanceledException();

            var position = transform.position;
            var toTarget = destination - position;
            toTarget.y = 0f;

            var distanceSq = toTarget.sqrMagnitude;
            var heightDelta = destination.y - position.y;
            if (distanceSq <= arriveThresholdSq
                && Mathf.Abs(heightDelta) <= Constants.ArriveHeightThreshold)
                break;

            // ============================================================
            // STEP CLIMB — when XZ is at destination but Y is still offset:
            // continuously apply forward+upward input pressure. This helps
            // climb stepped NavMesh segments even when horizontal motion is
            // zero (the agent is directly under/over the next surface).
            // ============================================================
            if (distanceSq <= arriveThresholdSq && Mathf.Abs(heightDelta) > Constants.ArriveHeightThreshold)
            {
                // Push forward (destination direction) even though XZ ≈ 0.
                // This is critical for staircase/stepped geometry where the
                // next NavMesh polygon is stacked vertically.
                var forwardVector = destination - position;
                forwardVector.y = 0f;
                if (forwardVector.sqrMagnitude < 0.001f)
                    forwardVector = transform.forward;
                else
                    forwardVector.Normalize();

                // Alternate approach: detect the best climb direction via
                // NavMesh.SamplePosition to find the actual walkable surface.
                var climbDirection = forwardVector;
                if (TryFindClimbDirection(position, forwardVector, destination,
                        fsm.Ctx.WalkableAreaMask, out var climbDir))
                {
                    climbDirection = climbDir;
                }

                var climbYaw = Mathf.Atan2(climbDirection.x, climbDirection.z) * Mathf.Rad2Deg;
                input.RotationYaw = climbYaw;

                // Apply both forward and strafe pressure to "scrabble" onto
                // the next step. The strafe helps when the agent is slightly
                // misaligned with the edge.
                var climbPhase = (revisitCount % 3) switch
                {
                    0 => new Vector2(0f, 1f),
                    1 => new Vector2(0.5f, 0.8f),
                    _ => new Vector2(-0.5f, 0.8f),
                };
                input.MoveInput = climbPhase;

                // If we are grounded and haven't jumped, attempt one.
                if (!jumpAttempted && fsm.Ctx.IsGrounded && heightDelta >= Constants.MinJumpHeight)
                {
                    jumpAttempted = true;
                    await TryExecuteJumpAsync(input, transform, fsm.Ctx, ct);
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                if (transform == null)
                    throw new OperationCanceledException();

                if (lastDestination == destination)
                {
                    revisitCount++;
                    if (revisitCount > Constants.StepClimbMaxAttempts)
                        break; // give up on this corner
                }
                else
                {
                    lastDestination = destination;
                    revisitCount = 0;
                }

                continue;
            }

            var requiresJump = IsJumpableHeightDelta(heightDelta);

            // At stacked/stepped NavMesh corners XZ can already match while the next
            // surface is still above the character. Jump before normalizing the zero
            // horizontal direction and keep forward pressure through the transition.
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
                hasTracker = true;
                continue;
            }

            if (!hasTracker)
            {
                stuckTracker.Begin(position, distanceSq);
                hasTracker = true;
            }
            else if (stuckTracker.Update(position, distanceSq, heightDelta, Time.deltaTime))
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

            // ============================================================
            // CORNER ANTI-GOUGE: when approaching a corner of the path,
            // steer toward the next logical position rather than directly at
            // the sharp corner. This prevents the agent from ramming into
            // NavMesh polygon vertices.
            // ============================================================
            var effectiveDestination = SteerAroundCorners(position, destination, distanceSq, fsm.Ctx.WalkableAreaMask, out var steerFound);
            if (steerFound)
            {
                var steerToTarget = effectiveDestination - position;
                steerToTarget.y = 0f;
                distanceSq = steerToTarget.sqrMagnitude;
                toTarget = steerToTarget;
            }

            var distance = Mathf.Sqrt(distanceSq);
            var direction = distance > 0.001f ? toTarget / distance : transform.forward;
            direction.y = 0f;
            direction.Normalize();

            var slideDir = ComputeWallSlide(transform, direction, distance);
            if (slideDir.sqrMagnitude > 0.001f)
            {
                direction = (direction + slideDir * Constants.WallSlideStrength).normalized;
            }

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
        input.JumpInput = false;
    }

    private static bool TryFindClimbDirection(
        Vector3 agentPosition,
        Vector3 fallbackDirection,
        Vector3 destination,
        int areaMask,
        out Vector3 climbDir)
    {
       
        var probeDir = fallbackDirection.normalized;
        var right = Vector3.Cross(Vector3.up, probeDir).normalized;

        for (var angle = -45f; angle <= 45f; angle += 15f)
        {
            var dir = Quaternion.AngleAxis(angle, Vector3.up) * probeDir;
            var probePos = agentPosition + dir * 1.0f;
            probePos.y = destination.y; 

            if (NavMesh.SamplePosition(probePos, out _, Constants.NavMeshSampleRadius, areaMask))
            {
                climbDir = dir;
                return true;
            }
        }

        climbDir = fallbackDirection;
        return false;
    }

    private static Vector3 SteerAroundCorners(
        Vector3 agentPosition,
        Vector3 destination,
        float distanceSq,
        int areaMask,
        out bool found)
    {
        found = false;
        if (distanceSq > Constants.CornerSteerThresholdSq)
            return destination;

        if (!NavMesh.SamplePosition(agentPosition, out var agentHit,
                Constants.NavMeshSampleRadius, areaMask))
            return destination;

        var toDest = destination - agentPosition;
        toDest.y = 0f;
        var dist = toDest.magnitude;
        if (dist < 0.001f)
            return destination;

        var forward = toDest / dist;
        var right = Vector3.Cross(Vector3.up, forward);

        var probeDist = Constants.CornerProbeDistance;
        var bestDot = -1f;
        var bestPos = destination;

        for (var angle = -60f; angle <= 60f; angle += 30f)
        {
            var offsetDir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            var probePos = agentHit.position + offsetDir * probeDist;

            if (!NavMesh.Raycast(agentHit.position, probePos, out _, areaMask))
            {
                var toClear = probePos - agentHit.position;
                toClear.y = 0f;
                toClear.Normalize();
                var dot = Vector3.Dot(toClear, forward);
                if (dot > bestDot)
                {
                    bestDot = dot;
                   
                    var forwardProg = Vector3.Dot(probePos - agentPosition, forward);
                    bestPos = agentPosition + forward * forwardProg + (probePos - agentPosition).magnitude * 0.3f * right;
                }
            }
        }

        if (bestDot > 0.3f)
        {
            found = true;
            return bestPos;
        }

        return destination;
    }

    private static Vector3 ComputeWallSlide(Transform transform, Vector3 desiredDir, float distanceToTarget)
    {
        var pos = transform.position + Vector3.up * 0.5f;
        var right = Vector3.Cross(Vector3.up, desiredDir).normalized;

        var slideProbe = Mathf.Min(Constants.WallSlideProbeDistance, distanceToTarget * 0.5f);
        if (slideProbe < 0.3f)
            return Vector3.zero;

        var hitRight = Physics.Raycast(pos, right, slideProbe, Constants.ObstacleLayerMask);
        var hitLeft = Physics.Raycast(pos, -right, slideProbe, Constants.ObstacleLayerMask);

        if (hitRight && !hitLeft)
            return right * (1f - Constants.WallSlideProbeDistance / slideProbe);
        if (hitLeft && !hitRight)
            return -right * (1f - Constants.WallSlideProbeDistance / slideProbe);

        if (hitRight && hitLeft)
        {
            var distRight = MeasureClearance(pos, right);
            var distLeft = MeasureClearance(pos, -right);
            if (distRight > distLeft)
                return right * Mathf.Clamp01(distRight / slideProbe);
            return -right * Mathf.Clamp01(distLeft / slideProbe);
        }

        return Vector3.zero;
    }

    private static float MeasureClearance(Vector3 origin, Vector3 direction)
    {
        if (Physics.Raycast(origin, direction, out var hit, Constants.WallSlideProbeDistance * 2f,
                Constants.ObstacleLayerMask))
            return hit.distance;
        return Constants.WallSlideProbeDistance * 2f;
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

        var forwardBlocked = false;
        for (var probeIndex = 0; probeIndex < 2; probeIndex++)
        {
            var probeOrigin = transform.position + Vector3.up * (0.3f + probeIndex * 0.5f);
            if (Physics.Raycast(probeOrigin, forward, Constants.StuckProbeDistance,
                    Constants.ObstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                forwardBlocked = true;
                break;
            }
        }

        if (!forwardBlocked)
            return defaultSide;

        var rightClear = true;
        var leftClear = true;

        for (var probeIndex = 0; probeIndex < 2; probeIndex++)
        {
            var probeOrigin = transform.position + Vector3.up * (0.3f + probeIndex * 0.5f);
            if (Physics.Raycast(probeOrigin, right, Constants.StuckProbeDistance,
                    Constants.ObstacleLayerMask, QueryTriggerInteraction.Ignore))
                rightClear = false;
            if (Physics.Raycast(probeOrigin, -right, Constants.StuckProbeDistance,
                    Constants.ObstacleLayerMask, QueryTriggerInteraction.Ignore))
                leftClear = false;
        }

        if (NavMesh.SamplePosition(transform.position, out var navHit, Constants.NavMeshSampleRadius, NavMesh.AllAreas))
        {
            rightClear &= !NavMesh.Raycast(navHit.position,
                navHit.position + right * Constants.StuckProbeDistance, out _, NavMesh.AllAreas);
            leftClear &= !NavMesh.Raycast(navHit.position,
                navHit.position - right * Constants.StuckProbeDistance, out _, NavMesh.AllAreas);
        }

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
