using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class DeathState : AsyncState
{
    public DeathState(AsyncStateMachine stateMachine) : base(stateMachine)
    {
    }
    
    public override async UniTask OnEnter(CancellationToken ct)
    {
        await base.OnEnter(ct);
        Debug.Log($"{StateMachine.Ctx.Transform.gameObject.name} is dead");
    }
}
