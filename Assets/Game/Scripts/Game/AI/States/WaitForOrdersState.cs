using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class WaitForOrdersState : AsyncState
{
    public WaitForOrdersState(AsyncStateMachine stateMachine) : base(stateMachine)
    {
        
    }
    
    public override async UniTask OnEnter(CancellationToken ct)
    {
        await base.OnEnter(ct);
        Debug.Log("Entering Wait For Orders State...");
        await UniTask.Delay(2000, cancellationToken: ct); 
    }

    public override async UniTask OnUpdate(CancellationToken ct)
    {
        await base.OnUpdate(ct);
        await HandleTransition();
    }

    public override async UniTask OnExit(CancellationToken ct)
    {
        await base.OnExit(ct);
        Debug.Log("Exiting Wait For Orders State...");
        await UniTask.CompletedTask;
    }
    
    protected override bool ShouldInterrupt() => StateMachine.Ctx.IsStarted;

    protected override async UniTask HandleTransition()
    {
        if (StateMachine.Ctx.IsStarted)
        {
            await StateMachine.TransitionTo(StateMachine.PatrolState);
        }
    }
}