using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

public class GuardState : AsyncState
{
    private const float GuardExtraStopDistance = 0.5f;
    private Vector3? _fallbackGuardPosition;
    private bool _hasFallbackPosition;
    private Vector3 _lastGuardPosition;

    public GuardState(AsyncStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override async UniTask OnEnter(CancellationToken ct)
    {
        await base.OnEnter(ct);
        _fallbackGuardPosition = null;
        _hasFallbackPosition = false;
        _lastGuardPosition = StateMachine.Ctx.GuardPosition;
        Debug.Log("Entering Guard State...");
    }

    public override async UniTask OnUpdate(CancellationToken ct)
    {
        await base.OnUpdate(ct);

        if (!StateMachine.Ctx.Transform)
        {
            return;
        }

        if (ShouldInterrupt())
        {
            StopInput();
            await HandleTransition();
            return;
        }

        var guardPosition = StateMachine.Ctx.GuardPosition;
        var targetPosition = ResolveTargetPosition(guardPosition);

        if (targetPosition == null || IsWithinStopDistance(targetPosition.Value))
        {
            StopInput();
            return;
        }

        var targetPos = targetPosition.Value;
        var currentPos = StateMachine.Ctx.Transform.position;
        var toTarget = targetPos - currentPos;
        toTarget.y = 0f;
        var distance = toTarget.magnitude;

        if (distance <= 0.01f)
        {
            StopInput();
            return;
        }

        if (currentPos.TryGetPathTo(targetPos, StateMachine.Ctx.WalkableAreaMask, out var corners) && corners.Length > 1)
        {
            var targetCorner = corners[corners.Length - 1];
            for (var i = corners.Length - 1; i >= 1; i--)
            {
                if (!NavMesh.Raycast(currentPos, corners[i], out _, StateMachine.Ctx.WalkableAreaMask))
                {
                    targetCorner = corners[i];
                    break;
                }
            }

            toTarget = targetCorner - currentPos;
            toTarget.y = 0f;
            distance = toTarget.magnitude;
        }

        var yaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
        StateMachine.Ctx.Input.RotationYaw = yaw;

        var speedFactor = Mathf.Clamp(distance / 2f, 0.3f, 1f);
        StateMachine.Ctx.Input.MoveInput = new Vector2(0f, speedFactor);
    }

    private Vector3? ResolveTargetPosition(Vector3 guardPosition)
    {
        if (!StateMachine.Ctx.Transform)
        {
            return null;
        }

        if (guardPosition != _lastGuardPosition)
        {
            _hasFallbackPosition = false;
            _fallbackGuardPosition = null;
            _lastGuardPosition = guardPosition;
        }

        if (_hasFallbackPosition && _fallbackGuardPosition.HasValue)
            return _fallbackGuardPosition.Value;

        var currentPos = StateMachine.Ctx.Transform.position;

        if (currentPos.TryGetPathTo(guardPosition, StateMachine.Ctx.WalkableAreaMask, out _))
        {
            _hasFallbackPosition = false;
            _fallbackGuardPosition = null;
            return guardPosition;
        }

        var closestNavPos = FindClosestNavMeshPosition(guardPosition);
        if (closestNavPos.HasValue)
        {
            _fallbackGuardPosition = closestNavPos.Value;
            _hasFallbackPosition = true;
            return closestNavPos.Value;
        }

        return null;
    }

    public override async UniTask OnExit(CancellationToken ct)
    {
        await base.OnExit(ct);
        StopInput();
        Debug.Log("Interrupted guard routine.");
        await UniTask.CompletedTask;
    }

    private void StopInput()
    {
        StateMachine.Ctx.Input.MoveInput = Vector2.zero;
        StateMachine.Ctx.Input.JumpInput = false;
    }

    private bool IsWithinStopDistance(Vector3 point)
    {
        if (!StateMachine.Ctx.Transform)
        {
            return true;
        }

        var stopDistance = StateMachine.Ctx.PreferredAttackDistance + GuardExtraStopDistance;
        var distance = Vector3.Distance(StateMachine.Ctx.Transform.position, point);
        return distance <= stopDistance;
    }

    private Vector3? FindClosestNavMeshPosition(Vector3 targetPos)
    {
        var sampleRadius = Constants.NavMeshSampleRadius;
        const float maxRadius = 20f;
        var step = 2f;

        while (sampleRadius <= maxRadius)
        {
            if (NavMesh.SamplePosition(targetPos, out var hit, sampleRadius,
                    StateMachine.Ctx.WalkableAreaMask))
            {
                return hit.position;
            }

            sampleRadius += step;
            step *= 1.5f;
        }

        return null;
    }

    protected override bool ShouldInterrupt() =>
        StateMachine.Ctx.Target != null && StateMachine.Ctx.Target.currentHitPoints > 0 &&
        StateMachine.Ctx.IsTargetVisible(StateMachine.Ctx.Target);

    protected override async UniTask HandleTransition()
    {
        StateMachine.Ctx.ClearDeadTarget();

        if (StateMachine.Ctx.IsDead)
        {
            await StateMachine.TransitionTo(StateMachine.DeathState);
            return;
        }

        if (StateMachine.Ctx.Target != null && StateMachine.Ctx.Target.currentHitPoints > 0)
        {
            await StateMachine.TransitionTo(StateMachine.AlarmState);
            return;
        }

        if (StateMachine.Ctx.TryGetLastKnownTargetPosition(out _))
        {
            await StateMachine.TransitionTo(StateMachine.AlarmState);
            return;
        }

        await StateMachine.TransitionTo(StateMachine.GuardState);
    }
}