using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

public class IdleWaitState : AsyncState
{
    public IdleWaitState(AsyncStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override async UniTask OnEnter(CancellationToken ct)
    {
        await base.OnEnter(ct);
        Debug.Log("Entering Idle Wait State...");
    }

    public override async UniTask OnUpdate(CancellationToken ct)
    {
        await base.OnUpdate(ct);        
        
        var waitDuration = Random.Range(3f, 15f);
        Debug.Log($"Idle waiting for {waitDuration:F1} seconds...");
        await UniTask.Delay((int)(waitDuration * 1000), cancellationToken: ct);
        
        await HandleTransition();
    }

    public override async UniTask OnExit(CancellationToken ct)
    {
        await base.OnExit(ct);
        Debug.Log("Exiting Idle Wait State...");
        await UniTask.CompletedTask;
    }

    protected override bool ShouldInterrupt() => StateMachine.Ctx.Target || StateMachine.Ctx.IsDead;

    protected override async UniTask HandleTransition()
    {
        if (StateMachine.Ctx.IsDead)
        {
            await StateMachine.TransitionTo(StateMachine.DeathState);
            return;
        }

        if (StateMachine.Ctx.Target && StateMachine.Ctx.TryGetLastKnownTargetPosition(out var destPos)
            && Vector3.Distance(StateMachine.Ctx.Transform.position, destPos) <= StateMachine.Ctx.PreferredAttackDistance)
        {
            await StateMachine.TransitionTo(StateMachine.AttackState);
            return;
        }

        if (StateMachine.Ctx.Target)
        {
            await StateMachine.TransitionTo(StateMachine.ChaseState);
            return;
        }

        if (StateMachine.Ctx.TryGetLastKnownTargetPosition(out _))
        {
            await StateMachine.TransitionTo(StateMachine.ChaseState);
            return;
        }

        await StateMachine.TransitionTo(StateMachine.PatrolState);
    }
}