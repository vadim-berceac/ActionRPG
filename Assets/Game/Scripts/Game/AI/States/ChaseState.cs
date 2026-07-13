using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ChaseState : AsyncState
{
    private const float RepathDistance = 1.5f;

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
               && StateMachine.Ctx.Target
               && !IsWithinStopDistance())
        {
            var targetPosAtPathStart = StateMachine.Ctx.Target.Transform.position;
            var corners = StateMachine.Ctx.Transform.position.GetPathTo(targetPosAtPathStart, StateMachine.Ctx.WalkableAreaMask);

            var moveResult = false;
            var needsRepath = false;

            for (var i = 1; i < corners.Length; i++)
            {
                if (CancellationTokenSource.IsCancellationRequested) break;
                if (!StateMachine.Ctx.Target) break;
                if (IsWithinStopDistance()) break;

                if (Vector3.Distance(StateMachine.Ctx.Target.Transform.position, targetPosAtPathStart) > RepathDistance)
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
        }
        
        StopInput();

        await HandleTransition();
    }

    public override async UniTask OnExit(CancellationToken ct)
    {
        await base.OnExit(ct);
        StopInput();
        Debug.Log("Interrupted chase routine.");
        await UniTask.CompletedTask;
    }

    private bool IsWithinStopDistance()
    {
        if (!StateMachine.Ctx.Target) return false;

        var distance = Vector3.Distance(StateMachine.Ctx.Transform.position, StateMachine.Ctx.Target.Transform.position);
        return distance <= Constants.StopDistance;
    }

    private void StopInput()
    {
        StateMachine.Ctx.Input.MoveInput = Vector2.zero;
    }

    protected override bool ShouldInterrupt() =>
        !StateMachine.Ctx.Target ||
        StateMachine.Ctx.IsHitReaction;

    protected override async UniTask HandleTransition()
    {
        if (StateMachine.Ctx.IsHitReaction)
        {
            await StateMachine.TransitionTo(StateMachine.HitReactionState);
            return;
        }

        if (!StateMachine.Ctx.Target)
        {
            await StateMachine.TransitionTo(StateMachine.PatrolState);
            return;
        }

        if (IsWithinStopDistance())
        {
            await StateMachine.TransitionTo(StateMachine.AttackState);
        }
    }
}