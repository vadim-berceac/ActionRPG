using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class DeathState : AsyncState
{
    private const int DeadLayer = 20;

    public DeathState(AsyncStateMachine stateMachine) : base(stateMachine)
    {
    }
    
    public override async UniTask OnEnter(CancellationToken ct)
    {
        await base.OnEnter(ct);
        StateMachine.Ctx.SetLayer(DeadLayer);
        Debug.Log($"{StateMachine.Ctx.Transform.gameObject.name} is dead");
    }

    public override async UniTask OnExit(CancellationToken ct)
    {
        StateMachine.Ctx.SetLayer(StateMachine.Ctx.DefaultLayer);
        await base.OnExit(ct);
    }

    public override async UniTask OnUpdate(CancellationToken ct)
    {
        await UniTask.WaitUntilCanceled(ct);
    }
}
