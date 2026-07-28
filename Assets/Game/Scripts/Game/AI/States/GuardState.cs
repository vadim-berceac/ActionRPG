using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

public class GuardState : AsyncState
{
    private const float GuardExtraStopDistance = 0.5f;
    private Vector3? _fallbackGuardPosition;
    private bool _hasFallbackPosition;
    private Vector3 _lastGuardPosition;

    public GuardState(AsyncStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override async UniTask OnEnter(CancellationToken ct)
    {
        await base.OnEnter(ct);
        _fallbackGuardPosition = null;
        _hasFallbackPosition = false;
        _lastGuardPosition = StateMachine.Ctx.GuardPosition;
        Debug.Log("Entering Guard State...");
    }

    public override async UniTask OnUpdate(CancellationToken ct)
    {
        await base.OnUpdate(ct);

        // Если появился враг — выходим, HandleTransition решит куда переключиться
        if (StateMachine.Ctx.Target)
        {
            StopInput();
            await HandleTransition();
            return;
        }

        // Получаем актуальную позицию точки охраны каждый кадр
        var guardPosition = StateMachine.Ctx.GuardPosition;
        var targetPosition = ResolveTargetPosition(guardPosition);

        // Если не можем идти к цели или уже на месте — стоим
        if (targetPosition == null || IsWithinStopDistance(targetPosition.Value))
        {
            StopInput();
            return;
        }

        // Прямое движение к цели — без блокирующих вызовов MoveTowardsAsync
        // Каждый кадр пересчитываем направление
        var targetPos = targetPosition.Value;
        var currentPos = StateMachine.Ctx.Transform.position;
        var toTarget = targetPos - currentPos;
        toTarget.y = 0f;
        var distance = toTarget.magnitude;

        if (distance <= 0.01f)
        {
            StopInput();
            return;
        }

        // Пытаемся построить путь NavMesh, чтобы не упереться в стены
        if (currentPos.TryGetPathTo(targetPos, StateMachine.Ctx.WalkableAreaMask, out var corners) && corners.Length > 1)
        {
            // Идём к самому дальнему видимому углу
            var targetCorner = corners[corners.Length - 1];
            for (var i = corners.Length - 1; i >= 1; i--)
            {
                if (!NavMesh.Raycast(currentPos, corners[i], out _, StateMachine.Ctx.WalkableAreaMask))
                {
                    targetCorner = corners[i];
                    break;
                }
            }

            toTarget = targetCorner - currentPos;
            toTarget.y = 0f;
            distance = toTarget.magnitude;
        }

        var yaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
        StateMachine.Ctx.Input.RotationYaw = yaw;

        var speedFactor = Mathf.Clamp(distance / 2f, 0.3f, 1f);
        StateMachine.Ctx.Input.MoveInput = new Vector2(0f, speedFactor);
    }

    /// <summary>
    /// Возвращает целевую позицию для движения.
    /// Если оригинальная точка охраны недостижима — ищет ближайшую NavMesh позицию.
    /// При изменении оригинальной guardPosition сбрасывает закешированный fallback.
    /// </summary>
    private Vector3? ResolveTargetPosition(Vector3 guardPosition)
    {
        // Если guardPosition изменилась — сбрасываем fallback и пересчитываем
        if (guardPosition != _lastGuardPosition)
        {
            _hasFallbackPosition = false;
            _fallbackGuardPosition = null;
            _lastGuardPosition = guardPosition;
        }

        if (_hasFallbackPosition && _fallbackGuardPosition.HasValue)
            return _fallbackGuardPosition.Value;

        var currentPos = StateMachine.Ctx.Transform.position;

        if (currentPos.TryGetPathTo(guardPosition, StateMachine.Ctx.WalkableAreaMask, out _))
        {
            _hasFallbackPosition = false;
            _fallbackGuardPosition = null;
            return guardPosition;
        }

        var closestNavPos = FindClosestNavMeshPosition(guardPosition);
        if (closestNavPos.HasValue)
        {
            _fallbackGuardPosition = closestNavPos.Value;
            _hasFallbackPosition = true;
            return closestNavPos.Value;
        }

        return null;
    }

    public override async UniTask OnExit(CancellationToken ct)
    {
        await base.OnExit(ct);
        StopInput();
        Debug.Log("Interrupted guard routine.");
        await UniTask.CompletedTask;
    }

    private void StopInput()
    {
        StateMachine.Ctx.Input.MoveInput = Vector2.zero;
        StateMachine.Ctx.Input.JumpInput = false;
    }

    /// <summary>
    /// Считаем точку охраны достигнутой, если находимся в радиусе
    /// preferredAttackDistance + небольшой запас. Это не даёт AI заходить
    /// внутрь объекта, привязанного к точке, и бегать вокруг него.
    /// </summary>
    private bool IsWithinStopDistance(Vector3 point)
    {
        var stopDistance = StateMachine.Ctx.PreferredAttackDistance + GuardExtraStopDistance;
        var distance = Vector3.Distance(StateMachine.Ctx.Transform.position, point);
        return distance <= stopDistance;
    }

    private Vector3? FindClosestNavMeshPosition(Vector3 targetPos)
    {
        var sampleRadius = Constants.NavMeshSampleRadius;
        const float maxRadius = 20f;
        var step = 2f;

        while (sampleRadius <= maxRadius)
        {
            if (NavMesh.SamplePosition(targetPos, out var hit, sampleRadius,
                    StateMachine.Ctx.WalkableAreaMask))
            {
                return hit.position;
            }

            sampleRadius += step;
            step *= 1.5f;
        }

        return null;
    }

    protected override bool ShouldInterrupt() =>
        StateMachine.Ctx.Target;

    protected override async UniTask HandleTransition()
    {
        if (StateMachine.Ctx.IsDead)
        {
            await StateMachine.TransitionTo(StateMachine.DeathState);
            return;
        }

        if (StateMachine.Ctx.Target
            && StateMachine.Ctx.TryGetLastKnownTargetPosition(out var destPos))
        {
            var distance = Vector3.Distance(StateMachine.Ctx.Transform.position, destPos);

            // Если есть дальнобойное оружие и цель дальше melee-дистанции — стреляем
            if (StateMachine.Ctx.HasRangedWeapon && distance > StateMachine.Ctx.PreferredAttackDistance * 1.5f)
            {
                await StateMachine.TransitionTo(StateMachine.ShootState);
                return;
            }

            if (distance <= StateMachine.Ctx.PreferredAttackDistance)
            {
                await StateMachine.TransitionTo(StateMachine.AttackState);
                return;
            }
        }

        if (StateMachine.Ctx.Target)
        {
            await StateMachine.TransitionTo(StateMachine.AlarmState);
            return;
        }

        if (StateMachine.Ctx.TryGetLastKnownTargetPosition(out _))
        {
            await StateMachine.TransitionTo(StateMachine.AlarmState);
            return;
        }

        await StateMachine.TransitionTo(StateMachine.GuardState);
    }
}