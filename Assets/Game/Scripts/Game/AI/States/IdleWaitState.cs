using System;
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
        
        var waitDuration = Random.Range(5f, 15f);
        var elapsed = 0f;
        Debug.Log($"Idle waiting for {waitDuration:F1} seconds...");
      
        while (elapsed < waitDuration && !IsCancelled)
        {
            if (StateMachine.Ctx.Target != null && StateMachine.Ctx.Target.currentHitPoints > 0) break;
            StateMachine.Ctx.ClearDeadTarget();
            
            var step = Mathf.Min(0.1f, waitDuration - elapsed);
            await UniTask.Delay((int)(step * 1000), cancellationToken: CancellationTokenSource.Token)
                .Timeout(TimeSpan.FromSeconds(2))
                .SuppressCancellationThrow();
            elapsed += step;
        }
        
        await HandleTransition();
    }

    public override async UniTask OnExit(CancellationToken ct)
    {
        await base.OnExit(ct);
        Debug.Log("Exiting Idle Wait State...");
        await UniTask.CompletedTask;
    }

    protected override bool ShouldInterrupt() =>
        (StateMachine.Ctx.Target != null && StateMachine.Ctx.Target.currentHitPoints > 0)
        || StateMachine.Ctx.IsDead;

    protected override async UniTask HandleTransition()
    {
        // Мёртвая цель не должна удерживать AI в боевом цикле — сбрасываем её,
        // чтобы AI вернулся к патрулированию/охране, а не залипал между
        // IdleWait -> Alarm -> Chase -> IdleWait.
        StateMachine.Ctx.ClearDeadTarget();

        if (StateMachine.Ctx.IsDead)
        {
            await StateMachine.TransitionTo(StateMachine.DeathState);
            return;
        }

        if (StateMachine.Ctx.Target || StateMachine.Ctx.TryGetLastKnownTargetPosition(out _))
        {
            await StateMachine.TransitionTo(StateMachine.AlarmState);
            return;
        }

        if (StateMachine.Ctx.AIMode == AIMode.Guard)
        {
            await StateMachine.TransitionTo(StateMachine.GuardState);
        }
        else
        {
            await StateMachine.TransitionTo(StateMachine.PatrolState);
        }
    }
}