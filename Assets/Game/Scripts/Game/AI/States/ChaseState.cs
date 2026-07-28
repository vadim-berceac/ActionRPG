using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ChaseState : AsyncState
{
    public ChaseState(AsyncStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override async UniTask OnEnter(CancellationToken ct)
    {
        await base.OnEnter(ct);
        Debug.Log("Entering Chase State...");
    }

    public override async UniTask OnUpdate(CancellationToken ct)
    {
        await base.OnUpdate(ct);

        // Если враг получил удар — принудительно перезапускаем преследование.
        if (StateMachine.Ctx.DamageTakenRecently)
        {
            StateMachine.Ctx.DamageTakenRecently = false;
        }

        // Всегда обновляем lastKnown позицию из Target, если он жив.
        if (StateMachine.Ctx.Target != null && StateMachine.Ctx.Target.currentHitPoints > 0)
        {
            StateMachine.Ctx.VisionSystem.SetLastKnownPosition(
                StateMachine.Ctx.Target,
                StateMachine.Ctx.Target.Transform.position);
        }

        // Если цель в радиусе обнаружения — преследуем напрямую.
        // Не ждём IsTargetVisible (line-of-sight + гистерезис),
        // так как цель могла быть получена через AlarmState.
        if (StateMachine.Ctx.Target != null && StateMachine.Ctx.IsTargetInRange(StateMachine.Ctx.Target))
        {
            await DirectChase(ct);
            await HandleTransition();
            return;
        }

        while (!IsCancelled
               && StateMachine.Ctx.TryGetLastKnownTargetPosition(out var destination)
               && !IsWithinStopDistance(destination))
        {
            // Пока идём к lastKnown позиции, проверяем, не появилась ли цель в радиусе триггера
            if (StateMachine.Ctx.Target != null && StateMachine.Ctx.IsTargetInRange(StateMachine.Ctx.Target))
            {
                await DirectChase(ct);
                await HandleTransition();
                return;
            }

            if (!StateMachine.Ctx.Transform.position.TryGetPathTo(
                    destination, StateMachine.Ctx.WalkableAreaMask, out var corners))
            {
                StopInput();
                
                await UniTask.Delay(250, cancellationToken: CancellationTokenSource.Token)
                    .Timeout(TimeSpan.FromSeconds(1))
                    .SuppressCancellationThrow();
                continue;
            }

            var moveResult = false;
            var needsRepath = false;

            for (var i = 1; i < corners.Length; i++)
            {
                if (IsCancelled) break;

                if (!StateMachine.Ctx.TryGetLastKnownTargetPosition(out var currentDestination))
                {
                    break;
                }

                if (IsWithinStopDistance(currentDestination)) break;

                if (Vector3.Distance(currentDestination, destination) > Constants.PathTargetMoveThreshold)
                {
                    needsRepath = true;
                    break;
                }

                var cornerIndex = FindFurthestVisibleCorner(corners, i);
                var corner = corners[cornerIndex];
                i = cornerIndex;

                moveResult = await AIActions.MoveTowardsAsync(corner, CancellationTokenSource.Token, StateMachine)
                    .SuppressCancellationThrow();

                if (moveResult) break;
            }

            StopInput();

            if (moveResult) break;
            if (needsRepath) continue;
        }

        StopInput();
        
        if (!StateMachine.Ctx.Target && StateMachine.Ctx.TryGetLastKnownTargetPosition(out var reachedPos)
            && IsWithinStopDistance(reachedPos))
        {
            StateMachine.Ctx.ClearLastKnownTargetPosition();
        }

        await HandleTransition();
    }

    private async UniTask DirectChase(CancellationToken ct)
    {
        while (!IsCancelled)
        {
            var target = StateMachine.Ctx.Target;
            if (target == null || target.currentHitPoints <= 0) break;

            var targetPosition = target.Transform.position;

            if (IsWithinStopDistance(targetPosition)) break;

            // Обновляем lastKnown позицию цели, чтобы другие враги, получившие
            // тревогу через AlarmState, могли идти к актуальной позиции
            StateMachine.Ctx.VisionSystem.SetLastKnownPosition(target, targetPosition);

            var toTarget = targetPosition - StateMachine.Ctx.Transform.position;
            toTarget.y = 0f;
            var distance = toTarget.magnitude;
            if (distance <= 0.01f) break;

            // Используем ту же stop distance что и IsWithinStopDistance, чтобы
            // DirectChase остановился на правильной дистанции (35 для ranged оружия)
            var stopDistance = StateMachine.Ctx.HasRangedWeapon
                ? Constants.PreferredShootDistance
                : StateMachine.Ctx.PreferredAttackDistance;
            var moveTarget = targetPosition - toTarget.normalized * stopDistance;
            var toMove = moveTarget - StateMachine.Ctx.Transform.position;
            toMove.y = 0f;

            if (toMove.sqrMagnitude <= Constants.ArriveThreshold * Constants.ArriveThreshold)
                break;

            var yaw = Mathf.Atan2(toMove.x, toMove.z) * Mathf.Rad2Deg;
            StateMachine.Ctx.Input.RotationYaw = yaw;
            StateMachine.Ctx.Input.MoveInput = new Vector2(0f, 1f);

            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        StopInput();
    }

    public override async UniTask OnExit(CancellationToken ct)
    {
        await base.OnExit(ct);
        StopInput();
        Debug.Log("Interrupted chase routine.");
        await UniTask.CompletedTask;
    }

    private bool IsWithinStopDistance(Vector3 point)
    {
        var distance = Vector3.Distance(StateMachine.Ctx.Transform.position, point);
        // Если есть дальнобойное оружие — останавливаемся на дистанции стрельбы
        var stopDistance = StateMachine.Ctx.HasRangedWeapon
            ? Constants.PreferredShootDistance
            : StateMachine.Ctx.PreferredAttackDistance;
        return distance <= stopDistance;
    }

    private void StopInput()
    {
        StateMachine.Ctx.Input.MoveInput = Vector2.zero;
        StateMachine.Ctx.Input.JumpInput = false;
    }

    private int FindFurthestVisibleCorner(Vector3[] corners, int firstIndex)
    {
        var position = StateMachine.Ctx.Transform.position;
        if (!UnityEngine.AI.NavMesh.SamplePosition(position, out var hit,
                Constants.NavMeshSampleRadius, StateMachine.Ctx.WalkableAreaMask))
            return firstIndex;

        var isNarrowPassage = IsNarrowPassage(hit.position, corners, firstIndex,
            StateMachine.Ctx.WalkableAreaMask);

        for (var i = corners.Length - 1; i > firstIndex; i--)
        {
            if (Mathf.Abs(corners[i].y - position.y) > Constants.CornerVisibilityHeightTolerance)
                continue;

            if (isNarrowPassage && i > firstIndex + 1)
                continue;

            if (!UnityEngine.AI.NavMesh.Raycast(hit.position, corners[i], out _,
                    StateMachine.Ctx.WalkableAreaMask))
                return i;
        }

        return firstIndex;
    }

    
    private bool IsNarrowPassage(Vector3 navPosition, Vector3[] corners, int firstIndex, int areaMask)
    {
        if (firstIndex + 1 >= corners.Length)
            return false;

        var from = corners[firstIndex];
        var to = corners[Mathf.Min(firstIndex + 2, corners.Length - 1)];
        var dir = to - from;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f)
            return false;

        dir.Normalize();
        var right = Vector3.Cross(Vector3.up, dir);

        var probeDist = Constants.NarrowPassageProbeDistance;
        var leftClear = !UnityEngine.AI.NavMesh.Raycast(navPosition,
            navPosition - right * probeDist, out _, areaMask);
        var rightClear = !UnityEngine.AI.NavMesh.Raycast(navPosition,
            navPosition + right * probeDist, out _, areaMask);

        return !leftClear || !rightClear;
    }

    protected override bool ShouldInterrupt() => false;

    protected override async UniTask HandleTransition()
    {
        if (StateMachine.Ctx.IsDead)
        {
            await StateMachine.TransitionTo(StateMachine.DeathState);
            return;
        }

        var target = StateMachine.Ctx.Target;
        if (target != null && target.currentHitPoints > 0)
        {
            var distance = Vector3.Distance(StateMachine.Ctx.Transform.position, target.Transform.position);

            // Если цель в радиусе атаки или в радиусе обнаружения — атакуем.
            // AttackState сам решит, когда наносить удар (AdjustApproach подведёт ближе).
            if (IsWithinStopDistance(target.Transform.position) || StateMachine.Ctx.IsTargetInRange(target))
            {
                // Если есть дальнобойное оружие и цель дальше melee-дистанции — стреляем
                if (StateMachine.Ctx.HasRangedWeapon && distance > StateMachine.Ctx.PreferredAttackDistance * 1.5f)
                {
                    await StateMachine.TransitionTo(StateMachine.ShootState);
                    return;
                }

                await StateMachine.TransitionTo(StateMachine.AttackState);
                return;
            }
        }

        if (target != null && target.currentHitPoints <= 0)
        {
            StateMachine.Ctx.ClearLastKnownTargetPosition();
        }

        if (!StateMachine.Ctx.TryGetLastKnownTargetPosition(out _))
        {
            await StateMachine.TransitionTo(StateMachine.IdleWaitState);
        }
    }
}