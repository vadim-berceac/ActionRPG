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
        Debug.Log("Entering Attack State...");
    }

    public override async UniTask OnUpdate(CancellationToken ct)
    {
        await base.OnUpdate(ct);

        var weaponSwitchTimeout = Time.time + Constants.WeaponSwitchTimeout;
        while (!StateMachine.Ctx.IsMeleeWeaponEquipped && Time.time < weaponSwitchTimeout)
        {
            await UniTask.Yield(PlayerLoopTiming.FixedUpdate, ct);
        }

        while (!IsCancelled && !StateMachine.Ctx.IsDead)
        {
            if (!StateMachine.Ctx.Target || StateMachine.Ctx.Target.currentHitPoints <= 0)
                break;

            if (IsOutOfRange())
                break;

            AdjustApproach();

            await TryExecuteAttack(CancellationTokenSource.Token)
                .SuppressCancellationThrow();

            if (IsCancelled) break;

            await UniTask.Delay(200, cancellationToken: CancellationTokenSource.Token)
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

    private async UniTask<bool> TryExecuteAttack(CancellationToken ct)
    {
        await AIActions.AttackAsync(ct, StateMachine);
        return true;
    }

    public void OnAttackDetected()
    {
        if (Random.value <= 0.7f && StateMachine.Ctx.IsTargetVisible(StateMachine.Ctx.Target))
        {
            StateMachine.TransitionTo(StateMachine.BlockState).Forget();
        }
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

        var preferredDistance = StateMachine.Ctx.PreferredAttackDistance;
        var deadZoneMin = preferredDistance * 0.75f;
        var deadZoneMax = preferredDistance * 1.25f;

        if (distance >= deadZoneMin && distance <= deadZoneMax)
        {
            if (_approachThrottle != 0f)
            {
                _approachThrottle = 0f;
                StateMachine.Ctx.Input.MoveInput = Vector2.zero;
            }
            return;
        }

        var moveTarget = targetPos - toTarget.normalized * preferredDistance;
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
            Mathf.Clamp01(toMove.magnitude / preferredDistance));
        _approachThrottle = Mathf.MoveTowards(_approachThrottle, targetThrottle, 3f * Time.deltaTime);
        StateMachine.Ctx.Input.MoveInput = new Vector2(0f, _approachThrottle);
    }

    private bool IsOutOfRange()
    {
        if (!StateMachine.Ctx.Target) return false;

        var distance = Vector3.Distance(StateMachine.Ctx.Transform.position, StateMachine.Ctx.Target.Transform.position);
        return distance > StateMachine.Ctx.PreferredAttackDistance;
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

        if (IsOutOfRange())
        {
            var distance = Vector3.Distance(
                StateMachine.Ctx.Transform.position,
                StateMachine.Ctx.Target.Transform.position);

            if (!StateMachine.Ctx.IsTargetVisible(StateMachine.Ctx.Target))
            {
                await StateMachine.TransitionTo(StateMachine.ChaseState);
                return;
            }

            if (StateMachine.Ctx.HasRangedWeapon
                && distance <= StateMachine.Ctx.RangeWeaponPreferredDistance)
            {
                await StateMachine.TransitionTo(StateMachine.ShootState);
                return;
            }

            await StateMachine.TransitionTo(StateMachine.ChaseState);
        }
    }
}