using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class AttackState : AsyncState
{
    public AttackState(AsyncStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override async UniTask OnEnter(CancellationToken ct)
    {
        await base.OnEnter(ct);
        StopInput();
        await TryExecuteAttack(CancellationTokenSource.Token)
            .SuppressCancellationThrow();
        Debug.Log("Entering Attack State...");
    }

    public override async UniTask OnUpdate(CancellationToken ct)
    {
        await base.OnUpdate(ct);

        while (!IsCancelled
               && StateMachine.Ctx.Target
               && !IsOutOfRange() && !StateMachine.Ctx.IsDead)
        {
            FaceTarget();

            var attackResult = await TryExecuteAttack(CancellationTokenSource.Token)
                .SuppressCancellationThrow();

            if (attackResult) break;

            if (IsCancelled) break;
            if (!StateMachine.Ctx.Target) break;
            if (IsOutOfRange()) break;

            var delayResult = await UniTask.Delay(200, cancellationToken: CancellationTokenSource.Token)
                .SuppressCancellationThrow();

            if (delayResult) break;
        }

        await HandleTransition();
    }

    public override async UniTask OnExit(CancellationToken ct)
    {
        await base.OnExit(ct);
        StopInput();
        await UniTask.CompletedTask;
    }

    private async UniTask TryExecuteAttack(CancellationToken ct)
    {
        await AIActions.AttackAsync(ct, StateMachine);
    }

    private void FaceTarget()
    {
        var direction = (StateMachine.Ctx.Target.Transform.position - StateMachine.Ctx.Transform.position);
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f) return;

        StateMachine.Ctx.Transform.rotation = Quaternion.LookRotation(direction.normalized);
    }

    private bool IsOutOfRange()
    {
        if (!StateMachine.Ctx.Target) return false;

        var distance = Vector3.Distance(StateMachine.Ctx.Transform.position, StateMachine.Ctx.Target.Transform.position);
        return distance > Constants.StopDistance + Constants.ExitDistanceBuffer;
    }

    private void StopInput()
    {
        StateMachine.Ctx.Input.MoveInput = Vector2.zero;
        StateMachine.Ctx.Input.Attack1 = false;
        StateMachine.Ctx.Input.Attack2 = false;
    }

    protected override bool ShouldInterrupt() =>
        !StateMachine.Ctx.Target ||
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

        if (!StateMachine.Ctx.Target)
        {
            await StateMachine.TransitionTo(StateMachine.PatrolState);
            return;
        }

        if (IsOutOfRange())
        {
            await StateMachine.TransitionTo(StateMachine.ChaseState);
        }
    }
}