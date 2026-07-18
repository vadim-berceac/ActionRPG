using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

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
            if (IsCancelled) break;

            if (!StateMachine.Ctx.Transform.position.TryGetPathTo(
                    point, StateMachine.Ctx.WalkableAreaMask, out var corners))
            {
                StopInput();
                continue;
            }

            var moveResult = false;

            for (var i = 1; i < corners.Length; i++)
            {
                if (IsCancelled) break;

                var corner = corners[i];

                moveResult = await AIActions.MoveTowardsAsync(corner, CancellationTokenSource.Token, StateMachine)
                    .SuppressCancellationThrow();

                if (moveResult) break;
            }

            StopInput();

            if (moveResult) break;
            if (IsCancelled) break;

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
        StopInput();
        Debug.Log("Interrupted patrol routine.");
        await UniTask.CompletedTask;
    }
    
    private void StopInput()
    {
        StateMachine.Ctx.Input.MoveInput = Vector2.zero;
        StateMachine.Ctx.Input.JumpInput = false;
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
        
        if (StateMachine.Ctx.Target)
        {
            await StateMachine.TransitionTo(StateMachine.ChaseState);
            return;
        }

        await StateMachine.TransitionTo(StateMachine.IdleWaitState);
    }
}