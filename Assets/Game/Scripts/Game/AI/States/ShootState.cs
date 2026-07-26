using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ShootState : AsyncState
{
    /// <summary>
    /// Время прицеливания перед выстрелом (сек).
    /// </summary>
    private const float AimDuration = 3f;

    /// <summary>
    /// Задержка между выстрелами (сек).
    /// </summary>
    private const float ShootCooldown = 0.5f;

    public ShootState(AsyncStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override async UniTask OnEnter(CancellationToken ct)
    {
        await base.OnEnter(ct);
        Debug.Log("Entering Shoot State...");

        // Включаем анимацию прицеливания (Shoot = true)
        StateMachine.Ctx.Input.Shoot = true;
    }

    public override async UniTask OnUpdate(CancellationToken ct)
    {
        await base.OnUpdate(ct);

        var lastFireTime = -ShootCooldown - AimDuration;

        while (!IsCancelled && !StateMachine.Ctx.IsDead)
        {
            // Если цель мертва или отсутствует — выходим
            if (!StateMachine.Ctx.Target || StateMachine.Ctx.Target.currentHitPoints <= 0)
                break;

            var distance = GetDistanceToTarget();

            // Если цель слишком далеко (за пределами видимости или > preferred * 1.5) — преследуем
            if (IsOutOfRange())
                break;

            // Если цель близко (в радиусе melee атаки) — переключаемся на AttackState
            if (distance <= StateMachine.Ctx.PreferredAttackDistance * 1.5f)
                break;

            AdjustApproach();

            // Если Shoot сбросился (анимация прервана внешне) — выходим
            if (!StateMachine.Ctx.Input.Shoot)
                break;

            var now = Time.time;

            // Ждём пока пройдёт AimDuration с последнего выстрела (прицеливание)
            if (now - lastFireTime >= ShootCooldown + AimDuration)
            {
                lastFireTime = now;

                // TriggerRangedAttack включает canAttack и Input.Attack1.
                // ProcessAttack в следующем FixedUpdate подхватит Attack1 и вызовет TriggerAttack1.
                // Затем сбрасываем Input.Attack1, чтобы не держать его зажатым.
                StateMachine.Ctx.TriggerRangedAttack();
                await UniTask.Delay(50, cancellationToken: CancellationTokenSource.Token)
                    .SuppressCancellationThrow();
                StateMachine.Ctx.Input.Attack1 = false;

                // Небольшая пауза перед следующим циклом прицеливания
                await UniTask.Delay(100, cancellationToken: CancellationTokenSource.Token)
                    .SuppressCancellationThrow();
            }

            // Небольшая задержка между проверками
            await UniTask.Delay(50, cancellationToken: CancellationTokenSource.Token)
                .SuppressCancellationThrow();
        }

        StopInput();
        await HandleTransition();
    }

    public override async UniTask OnExit(CancellationToken ct)
    {
        await base.OnExit(ct);
        StopInput();
        await UniTask.CompletedTask;
    }

    private float _approachThrottle;

    private void AdjustApproach()
    {
        if (!StateMachine.Ctx.Target) return;

        var targetPos = StateMachine.Ctx.Target.Transform.position;
        var myPos = StateMachine.Ctx.Transform.position;

        var toTarget = targetPos - myPos;
        toTarget.y = 0f;
        var distance = toTarget.magnitude;
        if (distance >= 0.01f)
        {
            var yaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            StateMachine.Ctx.Input.RotationYaw = yaw;
        }

        if (distance < 0.01f) return;

        var deadZoneMin = Constants.PreferredShootDistance * 0.75f;
        var deadZoneMax = Constants.PreferredShootDistance * 1.25f;

        if (distance >= deadZoneMin && distance <= deadZoneMax)
        {
            if (_approachThrottle != 0f)
            {
                _approachThrottle = 0f;
                StateMachine.Ctx.Input.MoveInput = Vector2.zero;
            }
            return;
        }

        var moveTarget = targetPos - toTarget.normalized * Constants.PreferredShootDistance;
        var toMove = myPos - moveTarget;
        toMove.y = 0f;

        if (toMove.sqrMagnitude <= Constants.ArriveThreshold * Constants.ArriveThreshold)
        {
            if (_approachThrottle != 0f)
            {
                _approachThrottle = 0f;
                StateMachine.Ctx.Input.MoveInput = Vector2.zero;
            }
            return;
        }

        var targetThrottle = Mathf.Lerp(0.3f, 0.7f,
            Mathf.Clamp01(toMove.magnitude / Constants.PreferredShootDistance));
        _approachThrottle = Mathf.MoveTowards(_approachThrottle, targetThrottle, 3f * Time.deltaTime);
        StateMachine.Ctx.Input.MoveInput = new Vector2(0f, _approachThrottle);
    }

    private float GetDistanceToTarget()
    {
        if (!StateMachine.Ctx.Target) return float.MaxValue;
        return Vector3.Distance(
            StateMachine.Ctx.Transform.position,
            StateMachine.Ctx.Target.Transform.position);
    }

    private bool IsOutOfRange()
    {
        if (!StateMachine.Ctx.Target) return true;

        var distance = GetDistanceToTarget();

        // Если цель вне зоны видимости — считаем что нужно преследовать
        if (!StateMachine.Ctx.IsTargetVisible(StateMachine.Ctx.Target)
            && !StateMachine.Ctx.IsTargetInRange(StateMachine.Ctx.Target))
            return true;

        // Если цель дальше preferred * 1.5 — преследуем
        return distance > Constants.PreferredShootDistance * 1.5f;
    }

    private void StopInput()
    {
        StateMachine.Ctx.Input.MoveInput = Vector2.zero;
        StateMachine.Ctx.Input.Shoot = false;
        StateMachine.Ctx.Input.Attack1 = false;
        StateMachine.Ctx.Input.Attack2 = false;
        StateMachine.Ctx.Input.JumpInput = false;
    }

    protected override bool ShouldInterrupt() =>
        !StateMachine.Ctx.Target;

    protected override async UniTask HandleTransition()
    {
        if (StateMachine.Ctx.IsDead)
        {
            await StateMachine.TransitionTo(StateMachine.DeathState);
            return;
        }

        // Если цель мертва — сбрасываем и переходим в ожидание
        if (!StateMachine.Ctx.Target || StateMachine.Ctx.Target.currentHitPoints <= 0)
        {
            StateMachine.Ctx.ClearLastKnownTargetPosition();
            await StateMachine.TransitionTo(StateMachine.IdleWaitState);
            return;
        }

        var distance = GetDistanceToTarget();

        // Если цель близко — переключаемся на melee атаку
        if (distance <= StateMachine.Ctx.PreferredAttackDistance * 1.5f)
        {
            await StateMachine.TransitionTo(StateMachine.AttackState);
            return;
        }

        // Если цель вышла за пределы — преследуем
        if (IsOutOfRange())
        {
            await StateMachine.TransitionTo(StateMachine.ChaseState);
            return;
        }

        // Иначе остаёмся в ShootState (OnUpdate продолжится)
    }
}