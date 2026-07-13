using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ChaseState : AsyncState
{
    private const float RepathDistance = 1.5f;
    private const float ArrivalTolerance = 0.5f;

    public ChaseState(AsyncStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override async UniTask OnEnter(CancellationToken ct)
    {
        await base.OnEnter(ct);
        Debug.Log("Entering Chase State...");
    }

    public override async UniTask OnUpdate(CancellationToken ct)
    {
        await base.OnUpdate(ct);

        while (!CancellationTokenSource.IsCancellationRequested
               && StateMachine.Ctx.TryGetLastKnownTargetPosition(out var destination)
               && !IsWithinStopDistance(destination))
        {
            var corners = StateMachine.Ctx.Transform.position.GetPathTo(destination, StateMachine.Ctx.WalkableAreaMask);

            var moveResult = false;
            var needsRepath = false;

            for (var i = 1; i < corners.Length; i++)
            {
                if (CancellationTokenSource.IsCancellationRequested) break;

                if (!StateMachine.Ctx.TryGetLastKnownTargetPosition(out var currentDestination))
                {
                    // цель никогда не была видна / позицию уже сбросили извне
                    needsRepath = false;
                    moveResult = false;
                    break;
                }

                if (IsWithinStopDistance(currentDestination)) break;

                if (Vector3.Distance(currentDestination, destination) > RepathDistance)
                {
                    needsRepath = true;
                    break;
                }

                var corner = corners[i];

                moveResult = await AIActions.MoveTowardsAsync(corner, CancellationTokenSource.Token, StateMachine)
                    .SuppressCancellationThrow();

                if (moveResult) break;
            }

            StopInput();

            if (moveResult) break;
            if (needsRepath) continue;

            // Достигли последней известной точки, но условие выхода из while ещё не перепроверилось —
            // цикл сам корректно завершится на следующей итерации проверки.
        }

        StopInput();

        // Если добрались до последней известной точки и цели там нет — считаем поиск оконченным.
        if (!StateMachine.Ctx.Target && StateMachine.Ctx.TryGetLastKnownTargetPosition(out var reachedPos)
            && IsWithinStopDistance(reachedPos))
        {
            StateMachine.Ctx.ClearLastKnownTargetPosition();
        }

        await HandleTransition();
    }

    public override async UniTask OnExit(CancellationToken ct)
    {
        await base.OnExit(ct);
        StopInput();
        Debug.Log("Interrupted chase routine.");
        await UniTask.CompletedTask;
    }

    private bool IsWithinStopDistance(Vector3 point)
    {
        var distance = Vector3.Distance(StateMachine.Ctx.Transform.position, point);
        return distance <= Constants.StopDistance;
    }

    private void StopInput()
    {
        StateMachine.Ctx.Input.MoveInput = Vector2.zero;
    }

    protected override bool ShouldInterrupt() =>
        StateMachine.Ctx.IsHitReaction;

    protected override async UniTask HandleTransition()
    {
        if (StateMachine.Ctx.IsDead)
        {
            await StateMachine.TransitionTo(StateMachine.DeathState);
            return;
        }

        if (StateMachine.Ctx.IsHitReaction)
        {
            await StateMachine.TransitionTo(StateMachine.HitReactionState);
            return;
        }

        if (StateMachine.Ctx.Target && IsWithinStopDistance(StateMachine.Ctx.Target.Transform.position))
        {
            await StateMachine.TransitionTo(StateMachine.AttackState);
            return;
        }

        if (!StateMachine.Ctx.TryGetLastKnownTargetPosition(out _))
        {
            await StateMachine.TransitionTo(StateMachine.PatrolState);
        }
    }
}