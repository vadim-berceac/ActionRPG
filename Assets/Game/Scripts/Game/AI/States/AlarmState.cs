using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class AlarmState : AsyncState
{
    private static readonly Collider[] _overlapBuffer = new Collider[32];

    public AlarmState(AsyncStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override async UniTask OnEnter(CancellationToken ct)
    {
        await base.OnEnter(ct);
        Debug.Log("Entering Alarm State...");

        // Сбрасываем флаг получения урона — AlarmState обработал его
        StateMachine.Ctx.DamageTakenRecently = false;

        var targetToShare = StateMachine.Ctx.GetLastSeenTarget();

        var colliderCount = Physics.OverlapSphereNonAlloc(
            StateMachine.Ctx.Transform.position,
            20f,
            _overlapBuffer
        );

        for (int i = 0; i < colliderCount; i++)
        {
            var collider = _overlapBuffer[i];
            if (collider == null) continue;

            var otherEnemyBrain = collider.GetComponentInParent<EnemyBrain>();
            if (otherEnemyBrain != null &&
                otherEnemyBrain != StateMachine.Ctx.Input as EnemyBrain &&
                otherEnemyBrain.Faction == StateMachine.Ctx.Faction)
            {
                var otherFsm = otherEnemyBrain.Fsm;
                if (otherFsm != null)
                {
                    var otherCtx = otherFsm.Ctx;
                    if (otherCtx.Target == null && !otherCtx.TryGetLastKnownTargetPosition(out _))
                    {
                        otherCtx.SetAlarmTarget(targetToShare);

                        if (otherFsm.CurrentState != otherFsm.AlarmState && !otherFsm.IsTransitioning())
                        {
                            // Запускаем переход асинхронно, не блокируя текущий AlarmState
                            TransitionWithErrorHandling(otherFsm, otherFsm.AlarmState).Forget();
                        }
                    }
                }
            }
        }

        await StateMachine.TransitionTo(StateMachine.ChaseState);
    }

    public override async UniTask OnUpdate(CancellationToken ct)
    {
        await base.OnUpdate(ct);
        await HandleTransition();
    }

    public override async UniTask OnExit(CancellationToken ct)
    {
        await base.OnExit(ct);
        Debug.Log("Exiting Alarm State...");
        await UniTask.CompletedTask;
    }

    protected override bool ShouldInterrupt() => StateMachine.Ctx.IsDead;

    protected override async UniTask HandleTransition()
    {
        if (StateMachine.Ctx.IsDead)
        {
            await StateMachine.TransitionTo(StateMachine.DeathState);
            return;
        }

        if (StateMachine.Ctx.Target != null)
        {
            await StateMachine.TransitionTo(StateMachine.ChaseState);
            return;
        }

        if (StateMachine.Ctx.TryGetLastKnownTargetPosition(out _))
        {
            await StateMachine.TransitionTo(StateMachine.ChaseState);
            return;
        }

        if (StateMachine.Ctx.PatrolMode == PatrolMode.Guard)
        {
            await StateMachine.TransitionTo(StateMachine.GuardState);
        }
        else
        {
            await StateMachine.TransitionTo(StateMachine.PatrolState);
        }
    }

     private async UniTask TransitionWithErrorHandling(AsyncStateMachine fsm, IAsyncState targetState)
     {
         try
         {
             await fsm.TransitionTo(targetState);
         }
         catch (Exception ex)
         {
             Debug.LogWarning($"Failed to transition enemy to alarm state: {ex.Message}");
         }
     }
}
