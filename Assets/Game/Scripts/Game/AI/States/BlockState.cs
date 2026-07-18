using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

public class BlockState : AsyncState
{
    // Диапазон времени для блокирования (в секундах)
    private const float BLOCK_DURATION_MIN = 2f;
    private const float BLOCK_DURATION_MAX = 5f;

    public BlockState(AsyncStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override async UniTask OnEnter(CancellationToken ct)
    {
        await base.OnEnter(ct);
        StopInput();
        Debug.Log("Entering Block State...");
    }

    public override async UniTask OnUpdate(CancellationToken ct)
    {
        await base.OnUpdate(ct);

        // Активируем блокирование
        StateMachine.Ctx.Input.Block = true;

        // Случайное время блокирования
        var blockDuration = Random.Range(BLOCK_DURATION_MIN, BLOCK_DURATION_MAX);
        var blockEndTime = Time.time + blockDuration;
        
        var blockTimeout = TimeSpan.FromSeconds(8);
        var blockStartTime = DateTime.UtcNow;

        while (!IsCancelled && Time.time < blockEndTime && !StateMachine.Ctx.IsDead)
        {
            // Защита от зависания в блоке
            if (DateTime.UtcNow - blockStartTime > blockTimeout)
            {
                Debug.LogWarning("BlockState timeout - forcing exit");
                break;
            }

            // Если цель потеряна или умерла - выходим из блокирования
            if (!StateMachine.Ctx.Target || StateMachine.Ctx.IsDead)
            {
                break;
            }

            // Если цель вышла из зоны атаки - переходим в Chase
            if (IsOutOfRange())
            {
                break;
            }

            await UniTask.Yield(ct);
        }

        StopInput();
        await HandleTransition();
    }

    public override async UniTask OnExit(CancellationToken ct)
    {
        await base.OnExit(ct);
        StateMachine.Ctx.Input.Block = false;
        StopInput();
        await UniTask.CompletedTask;
    }

    private bool IsOutOfRange()
    {
        if (!StateMachine.Ctx.Target) return false;

        var distance = Vector3.Distance(StateMachine.Ctx.Transform.position, StateMachine.Ctx.Target.Transform.position);
        return distance > StateMachine.Ctx.PreferredAttackDistance * 1.5f;
    }

    private void StopInput()
    {
        StateMachine.Ctx.Input.MoveInput = Vector2.zero;
        StateMachine.Ctx.Input.Attack1 = false;
        StateMachine.Ctx.Input.Attack2 = false;
        StateMachine.Ctx.Input.JumpInput = false;
    }

    protected override bool ShouldInterrupt() =>
        !StateMachine.Ctx.Target || !StateMachine.Ctx.IsTargetVisible(StateMachine.Ctx.Target);

    protected override async UniTask HandleTransition()
    {
        if (StateMachine.Ctx.IsDead)
        {
            await StateMachine.TransitionTo(StateMachine.DeathState);
            return;
        }

        if (!StateMachine.Ctx.Target)
        {
            await StateMachine.TransitionTo(StateMachine.IdleWaitState);
            return;
        }

        if (!StateMachine.Ctx.IsTargetVisible(StateMachine.Ctx.Target))
        {
            await StateMachine.TransitionTo(StateMachine.ChaseState);
            return;
        }

        if (IsOutOfRange())
        {
            await StateMachine.TransitionTo(StateMachine.ChaseState);
            return;
        }

        // Если цель в зоне атаки и видна - переходим в атаку
        await StateMachine.TransitionTo(StateMachine.AttackState);
    }
}