using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ChaseState : AsyncState
{
    // Сколько секунд суммарно AI пытается достроить путь до lastKnown позиции,
    // прежде чем признать цель недостижимой и сбросить преследование.
    private const float PathFailureTimeBudget = 4f;
    private const float PathFailureRetryDelayMs = 250f;

    private float _pathFailureTimer;

    public ChaseState(AsyncStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override async UniTask OnEnter(CancellationToken ct)
    {
        await base.OnEnter(ct);
        _pathFailureTimer = 0f;
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

        // Если цель в радиусе обнаружения И видна — преследуем напрямую.
        // Если цель скрыта препятствием — идём по NavMesh к lastKnown позиции,
        // чтобы обойти препятствие и найти прямую видимость.
        if (StateMachine.Ctx.Target != null
            && StateMachine.Ctx.IsTargetInRange(StateMachine.Ctx.Target)
            && StateMachine.Ctx.IsTargetVisible(StateMachine.Ctx.Target))
        {
            await DirectChase(ct);
            await HandleTransition();
            return;
        }

        _pathFailureTimer = 0f;

        while (!IsCancelled
               && StateMachine.Ctx.TryGetLastKnownTargetPosition(out var destination)
               && !IsWithinStopDistance(destination))
        {
            // Пока идём к lastKnown позиции, проверяем, не появилась ли цель в радиусе триггера
            // и не стала ли она видимой (обошли препятствие)
            if (StateMachine.Ctx.Target != null
                && StateMachine.Ctx.IsTargetInRange(StateMachine.Ctx.Target)
                && StateMachine.Ctx.IsTargetVisible(StateMachine.Ctx.Target))
            {
                await DirectChase(ct);
                await HandleTransition();
                return;
            }

            var hasPath = StateMachine.Ctx.Transform.position.TryGetPathTo(
                destination, StateMachine.Ctx.WalkableAreaMask, out var corners);

            // Вырожденный путь (true, но только стартовая точка) трактуем как "путь не найден" —
            // иначе for ниже не выполнится ни разу, не будет ни одного await, и мы получим
            // синхронный бесконечный цикл (зависание редактора без единой ошибки).
            var pathIsUsable = hasPath && corners.Length >= 2;

            if (!pathIsUsable)
            {
                // Путь до реальной lastKnown позиции пока не строится — не подменяем цель
                // фейковой "ближайшей точкой" (это давало вырожденные пути в паре сантиметров
                // от агента и AI зависал в микро-топтании на месте). Просто ждём: пока мы ждём,
                // lastKnown позиция может обновиться (цель движется) и путь станет проходимым.
                _pathFailureTimer += PathFailureRetryDelayMs / 1000f;

                if (_pathFailureTimer >= PathFailureTimeBudget)
                {
                    // Бюджет исчерпан — осознанно теряем цель, а не зависаем
                    // и не топчемся на месте бесконечно.
                    StateMachine.Ctx.ClearLastKnownTargetPosition();
                    StopInput();
                    break;
                }

                StopInput();

                await UniTask.Delay((int)PathFailureRetryDelayMs, cancellationToken: CancellationTokenSource.Token)
                    .Timeout(TimeSpan.FromSeconds(1))
                    .SuppressCancellationThrow();
                continue;
            }

            _pathFailureTimer = 0f;

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

            var stopDistance = GetStopDistance();
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
        _pathFailureTimer = 0f;
        Debug.Log("Interrupted chase routine.");
        await UniTask.CompletedTask;
    }

    private bool IsWithinStopDistance(Vector3 point)
    {
        return IsWithinStopDistance(point, GetStopDistance());
    }

    private bool IsWithinStopDistance(Vector3 point, float stopDistance)
    {
        var distance = Vector3.Distance(StateMachine.Ctx.Transform.position, point);
        return distance <= stopDistance;
    }

    private float GetStopDistance()
    {
        var hasLineOfSight = StateMachine.Ctx.Target != null
            && StateMachine.Ctx.Target.currentHitPoints > 0
            && StateMachine.Ctx.IsTargetVisible(StateMachine.Ctx.Target);

        if (hasLineOfSight)
        {
            if (StateMachine.Ctx.HasRangedWeapon)
                return Constants.PreferredShootDistance;

            return StateMachine.Ctx.PreferredAttackDistance;
        }

        return Constants.ArriveThreshold;
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
        // Мёртвая цель не должна удерживать AI в боевом цикле
        StateMachine.Ctx.ClearDeadTarget();

        if (StateMachine.Ctx.IsDead)
        {
            await StateMachine.TransitionTo(StateMachine.DeathState);
            return;
        }

        var target = StateMachine.Ctx.Target;
        if (target != null && target.currentHitPoints > 0)
        {
            var distance = Vector3.Distance(StateMachine.Ctx.Transform.position, target.Transform.position);
            var hasLineOfSight = StateMachine.Ctx.IsTargetVisible(target);

            if (StateMachine.Ctx.IsTargetInRange(target))
            {
                if (hasLineOfSight)
                {
                    if (StateMachine.Ctx.HasRangedWeapon && distance > StateMachine.Ctx.PreferredAttackDistance * 1.5f)
                    {
                        await StateMachine.TransitionTo(StateMachine.ShootState);
                        return;
                    }

                    if (IsWithinStopDistance(target.Transform.position))
                    {
                        await StateMachine.TransitionTo(StateMachine.AttackState);
                        return;
                    }
                }
                else
                {
                    return;
                }
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