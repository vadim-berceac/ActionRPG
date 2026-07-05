using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class PatrolState : AsyncState
{
    public PatrolState(AsyncStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override async UniTask OnEnter(CancellationToken ct)
    {
        await base.OnEnter(ct);
        
        if (StateMachine.Ctx.PatrolWaypoints == null || StateMachine.Ctx.PatrolWaypoints.Length == 0)
        {
            StateMachine.Ctx.SetWaypoints(StateMachine.Ctx.Transform.position.GetRandomPath(Random.Range(3, 10),
                50f, StateMachine.Ctx.WalkableAreaMask));
        }
        await UniTask.CompletedTask;
    }

    public override async UniTask OnUpdate(CancellationToken ct)
    {
        await base.OnUpdate(ct);
        
        foreach (var point in StateMachine.Ctx.PatrolWaypoints)
        {
            if (CancellationTokenSource.IsCancellationRequested) break;

            var corners = StateMachine.Ctx.Transform.position.GetPathTo(point, StateMachine.Ctx.WalkableAreaMask);

            var moveResult = false;

            for (var i = 1; i < corners.Length; i++)
            {
                if (CancellationTokenSource.IsCancellationRequested) break;

                var corner = corners[i];

                // moveResult = await StateMachine.Ctx.Transform.MoveAsync(
                //         StateMachine.Ctx.AnimationCurve,
                //         corner,
                //         CancellationTokenSource.Token,
                //         true,
                //         StateMachine.Ctx.RotationSpeed)
                //     .SuppressCancellationThrow();

                if (moveResult) break;
            }

            if (moveResult) break;
            if (CancellationTokenSource.IsCancellationRequested) break;

            Debug.Log("Scanning surroundings...");
            var delayResult = await UniTask.Delay(1000, cancellationToken: CancellationTokenSource.Token)
                .SuppressCancellationThrow();

            if (delayResult) break;
        }

        await HandleTransition();
    }

    public override async UniTask OnExit(CancellationToken ct)
    {
        await base.OnExit(ct);
        Debug.Log("Interrupted patrol routine.");
        await UniTask.CompletedTask;
    }

    protected override bool ShouldInterrupt() =>
        !StateMachine.Ctx.IsStarted ||
        StateMachine.Ctx.IsHitReaction;

    protected override async UniTask HandleTransition()
    {
        if (!StateMachine.Ctx.IsStarted)
        {
            await StateMachine.TransitionTo(StateMachine.WaitForOrdersState);
            return;
        }

        if (StateMachine.Ctx.IsHitReaction)
        {
            await StateMachine.TransitionTo(StateMachine.HitReactionState);
        }
    }
}