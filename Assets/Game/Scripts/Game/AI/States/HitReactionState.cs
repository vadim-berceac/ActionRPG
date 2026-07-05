using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class HitReactionState : AsyncState
{
    public HitReactionState(AsyncStateMachine stateMachine) : base(stateMachine)
    {
        
    }
    
    public override async UniTask OnEnter(CancellationToken ct)
    {
        await base.OnEnter(ct);
        Debug.Log("Entering Hit Reaction State...");
    }
    
    public override async UniTask OnUpdate(CancellationToken ct)
    {
        await base.OnUpdate(ct);        
        
        await UniTask.Delay(5000, cancellationToken: ct); 
        
        StateMachine.Ctx.HitReaction(false);
        
        await HandleTransition();
    }
    
    public override async UniTask OnExit(CancellationToken ct)
    {
        await base.OnExit(ct);
        Debug.Log("Exiting Hit Reaction State...");
        await UniTask.CompletedTask;
    }
    
    protected override bool ShouldInterrupt() => !StateMachine.Ctx.IsHitReaction;

    protected override async UniTask HandleTransition()
    {
        if (!StateMachine.Ctx.IsHitReaction)
        {
            await StateMachine.TransitionTo(StateMachine.PatrolState);
        }
    }
}
