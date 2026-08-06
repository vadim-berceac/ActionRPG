using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ShootState : AsyncState
{
    private const float AimDuration = 3f;
    private const float ShootCooldown = 0.5f;

    private float _aimStartTime;
    private float _approachThrottle;

    public ShootState(AsyncStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override async UniTask OnEnter(CancellationToken ct)
    {
        await base.OnEnter(ct);
        Debug.Log("Entering Shoot State...");

        StateMachine.Ctx.UpdateRangedTargetPosition();

        StateMachine.Ctx.Input.Shoot = true;
        _aimStartTime = Time.time;
        AimAtTarget();
    }

    public override async UniTask OnUpdate(CancellationToken ct)
    {
        await base.OnUpdate(ct);

        var weaponSwitchTimeout = Time.time + Constants.WeaponSwitchTimeout;
        while (!StateMachine.Ctx.IsRangedWeaponEquipped && Time.time < weaponSwitchTimeout)
        {
            await UniTask.Yield(PlayerLoopTiming.FixedUpdate, ct);
        }

        while (!IsCancelled && !StateMachine.Ctx.IsDead)
        {
            if (!StateMachine.Ctx.Target || StateMachine.Ctx.Target.currentHitPoints <= 0)
                break;

            if (!StateMachine.Ctx.IsTargetVisible(StateMachine.Ctx.Target))
                break;

            AimAtTarget();

            var distance = GetDistanceToTarget();

            if (IsOutOfRange())
                break;

            if (distance <= StateMachine.Ctx.PreferredAttackDistance)
                break;

            AdjustApproach();

            StateMachine.Ctx.Input.Shoot = true;

            var now = Time.time;

            var aimReady = StateMachine.Ctx.IsRangedWeaponEquipped
                && StateMachine.Ctx.IsShootPressed
                && now - _aimStartTime >= AimDuration;
            var cooldownReady = now - StateMachine.Ctx.LastRangedFireTime >= ShootCooldown;

            if (aimReady && cooldownReady)
            {
                StateMachine.Ctx.LastRangedFireTime = now;
                _aimStartTime = now;

                StateMachine.Ctx.TriggerRangedAttack();
                await UniTask.Delay(50, cancellationToken: CancellationTokenSource.Token)
                    .SuppressCancellationThrow();
                StateMachine.Ctx.Input.Attack1 = false;

                await UniTask.Delay(100, cancellationToken: CancellationTokenSource.Token)
                    .SuppressCancellationThrow();
            }

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

    private void AimAtTarget()
    {
        if (!StateMachine.Ctx.Target) return;

        var toTarget = StateMachine.Ctx.Target.Transform.position - StateMachine.Ctx.Transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        var yaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
        StateMachine.Ctx.Input.RotationYaw = yaw;
    }

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

        var preferredShootDistance = StateMachine.Ctx.RangeWeaponPreferredDistance;
        var deadZoneMin = preferredShootDistance * 0.75f;
        var deadZoneMax = preferredShootDistance * 1.25f;

        if (distance >= deadZoneMin && distance <= deadZoneMax)
        {
            if (_approachThrottle != 0f)
            {
                _approachThrottle = 0f;
                StateMachine.Ctx.Input.MoveInput = Vector2.zero;
            }
            return;
        }

        var moveTarget = targetPos - toTarget.normalized * preferredShootDistance;
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
            Mathf.Clamp01(toMove.magnitude / preferredShootDistance));
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

        return distance > StateMachine.Ctx.RangeWeaponPreferredDistance;
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
        StateMachine.Ctx.ClearDeadTarget();

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

        var distance = GetDistanceToTarget();

        if (!StateMachine.Ctx.IsTargetVisible(StateMachine.Ctx.Target))
        {
            await StateMachine.TransitionTo(StateMachine.ChaseState);
            return;
        }

        if (distance <= StateMachine.Ctx.PreferredAttackDistance)
        {
            await StateMachine.TransitionTo(StateMachine.AttackState);
            return;
        }

        if (IsOutOfRange())
        {
            await StateMachine.TransitionTo(StateMachine.ChaseState);
            return;
        }
    }
}