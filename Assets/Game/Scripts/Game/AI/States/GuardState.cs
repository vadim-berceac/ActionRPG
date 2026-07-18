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

    public GuardState(AsyncStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override async UniTask OnEnter(CancellationToken ct)
    {
        await base.OnEnter(ct);
        _fallbackGuardPosition = null;
        _hasFallbackPosition = false;
        Debug.Log("Entering Guard State...");
    }

    public override async UniTask OnUpdate(CancellationToken ct)
    {
        await base.OnUpdate(ct);

        var updateTimeout = TimeSpan.FromSeconds(15);
        var startTime = DateTime.UtcNow;

        while (!IsCancelled)
        {
            // Защита от зависания в общем цикле
            if (DateTime.UtcNow - startTime > updateTimeout)
            {
                Debug.LogWarning("GuardState update timeout - forcing transition");
                break;
            }

            // Получаем актуальную позицию точки охраны каждый кадр
            var guardPosition = StateMachine.Ctx.GuardPosition;
            var targetPosition = ResolveTargetPosition(guardPosition);

            // Если не можем идти к цели — стоим и ждём
            if (targetPosition == null)
            {
                StopInput();
                await WaitForTargetOrLeave(ct);
                continue;
            }

            // Если мы уже на комфортном расстоянии от точки — ждём, но следим за её движением
            if (IsWithinStopDistance(targetPosition.Value))
            {
                StopInput();
                await WaitForTargetOrLeave(ct);
                // После ожидания — возвращаемся в начало цикла и перепроверяем позицию
                continue;
            }

            // Идём к точке
            await MoveToTarget(ct, targetPosition.Value);

            StopInput();
        }

        await HandleTransition();
    }

    /// <summary>
    /// Ожидание цели, с постоянной проверкой актуальной позиции точки охраны.
    /// Если точка ушла за пределы stopDistance — выходим.
    /// </summary>
    private async UniTask WaitForTargetOrLeave(CancellationToken ct)
    {
        var waitTimeout = TimeSpan.FromSeconds(10);
        var waitStartTime = DateTime.UtcNow;
        
        while (!IsCancelled)
        {
            // Защита от зависания при ожидании
            if (DateTime.UtcNow - waitStartTime > waitTimeout)
            {
                Debug.LogWarning("WaitForTargetOrLeave timeout");
                break;
            }

            if (StateMachine.Ctx.Target) break;

            // Получаем актуальную позицию точки охраны каждый кадр
            var currentGuardPos = StateMachine.Ctx.GuardPosition;

            // Проверяем, не ушла ли точка охраны слишком далеко
            if (!IsWithinStopDistance(currentGuardPos))
                break;

            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }
    }

    private async UniTask MoveToTarget(CancellationToken ct, Vector3 target)
    {
        var moveTimeout = TimeSpan.FromSeconds(20);
        var moveStartTime = DateTime.UtcNow;
        
        while (!IsCancelled && !IsWithinStopDistance(target))
        {
            // Защита от зависания при движении к цели
            if (DateTime.UtcNow - moveStartTime > moveTimeout)
            {
                Debug.LogWarning("MoveToTarget timeout - cannot reach target");
                break;
            }

            if (StateMachine.Ctx.Target) break;

            var currentPos = StateMachine.Ctx.Transform.position;

            if (!currentPos.TryGetPathTo(target, StateMachine.Ctx.WalkableAreaMask, out var corners))
            {
                StopInput();
                await UniTask.Delay(250, cancellationToken: CancellationTokenSource.Token)
                    .Timeout(TimeSpan.FromSeconds(1))
                    .SuppressCancellationThrow();
                return;
            }

            var moveResult = false;
            for (var i = 1; i < corners.Length; i++)
            {
                if (IsCancelled) break;
                if (StateMachine.Ctx.Target) break;
                if (IsWithinStopDistance(target)) break;

                var corner = corners[i];
                moveResult = await AIActions.MoveTowardsAsync(
                    corner, CancellationTokenSource.Token, StateMachine)
                    .SuppressCancellationThrow();

                if (moveResult) break;
            }

            StopInput();
            if (moveResult) break;
        }
    }

    /// <summary>
    /// Возвращает целевую позицию для движения.
    /// Если оригинальная точка охраны недостижима — ищет ближайшую NavMesh позицию.
    /// </summary>
    private Vector3? ResolveTargetPosition(Vector3 guardPosition)
    {
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

    /// <summary>
    /// Считаем точку охраны достигнутой, если находимся в радиусе
    /// preferredAttackDistance + небольшой запас. Это не даёт AI заходить
    /// внутрь объекта, привязанного к точке, и бегать вокруг него.
    /// </summary>
    private bool IsWithinStopDistance(Vector3 point)
    {
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
        StateMachine.Ctx.Target;

    protected override async UniTask HandleTransition()
    {
        if (StateMachine.Ctx.IsDead)
        {
            await StateMachine.TransitionTo(StateMachine.DeathState);
            return;
        }

        if (StateMachine.Ctx.Target
            && StateMachine.Ctx.TryGetLastKnownTargetPosition(out var destPos)
            && Vector3.Distance(StateMachine.Ctx.Transform.position, destPos) <= StateMachine.Ctx.PreferredAttackDistance)
        {
            await StateMachine.TransitionTo(StateMachine.AttackState);
            return;
        }

        if (StateMachine.Ctx.Target)
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