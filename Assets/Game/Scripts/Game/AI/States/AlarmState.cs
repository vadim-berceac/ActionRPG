using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using UnityEngine;

public class AlarmState : AsyncState
{
    // Буфер для OverlapSphereNonAlloc чтобы избежать выделения памяти
    private static readonly Collider[] _overlapBuffer = new Collider[32];

    public AlarmState(AsyncStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override async UniTask OnEnter(CancellationToken ct)
    {
        await base.OnEnter(ct);
        Debug.Log("Entering Alarm State...");

        // Получаем последнюю известную позицию цели
        if (StateMachine.Ctx.TryGetLastKnownTargetPosition(out var alarmPosition))
        {
            var targetToShare = StateMachine.Ctx.GetLastSeenTarget();

            // Выполняем OverlapSphereNonAlloc для поиска других врагов с той же фракцией
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
                    // Проверяем, что у другого врага нет текущей цели
                    var otherFsm = otherEnemyBrain.Fsm;
                    if (otherFsm != null)
                    {
                        var otherCtx = otherFsm.Ctx;
                        if (otherCtx.Target == null && !otherCtx.TryGetLastKnownTargetPosition(out _))
                        {
                            // Устанавливаем цель и позицию для другого врага
                            otherCtx.SetAlarmTarget(targetToShare, alarmPosition);

                            // Переводим другого врага в AlarmState (не ждем завершения, чтобы не блокировать цикл)
                            if (otherFsm.CurrentState != otherFsm.AlarmState)
                            {
                                _ = otherFsm.TransitionTo(otherFsm.AlarmState);
                            }
                        }
                    }
                }
            }
        }

        // Переходим в ChaseState для преследования цели
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

        // Если цель найдена и видна - переходим в ChaseState
        if (StateMachine.Ctx.Target != null)
        {
            await StateMachine.TransitionTo(StateMachine.ChaseState);
            return;
        }

        // Если есть последняя известная позиция цели - переходим в ChaseState
        if (StateMachine.Ctx.TryGetLastKnownTargetPosition(out _))
        {
            await StateMachine.TransitionTo(StateMachine.ChaseState);
            return;
        }

        // Если цели нет - возвращаемся в предыдущее состояние
        if (StateMachine.Ctx.PatrolMode == PatrolMode.Guard)
        {
            await StateMachine.TransitionTo(StateMachine.GuardState);
        }
        else
        {
            await StateMachine.TransitionTo(StateMachine.PatrolState);
        }
    }
}