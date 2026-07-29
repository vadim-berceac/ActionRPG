using System;
using Cysharp.Threading.Tasks;
using Game;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public class StateMachineContext : IDisposable
{
    public IInput Input { get; private set; }
    public Transform Transform { get; private set; }
    public Damageable Target { get; private set; }
    public float RotationSpeed { get; private set; }
    public Vector3[] PatrolWaypoints { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsGrounded => _humanoidController.IsGrounded;
    public bool HasPrimaryWeapon => _humanoidController.HasPrimaryWeapon;
    public bool HasAdditionalWeapon => _humanoidController.HasAdditionalWeapon;
    public bool HasRangedWeapon => _humanoidController.HasRangeWeapon;
    public float LoadProgressCurve => _humanoidController.LoadProgressCurve;
    public void TriggerRangedAttack() => _humanoidController.TriggerRangedAttack();
    public float LastRangedFireTime { get; set; } = -10f;
    public EnemyBehaviorMode BehaviorMode { get; private set; }
    public PatrolMode PatrolMode { get; private set; }

    public Vector3 GuardPosition => _guardPointTransform != null
        ? _guardPointTransform.position
        : _fixedGuardPosition;

    public float PreferredAttackDistance => _humanoidController.PrimaryWeaponPreferredAttackDistance;

    public readonly int WalkableAreaMask = NavMesh.GetAreaFromName("Walkable") != -1
        ? 1 << NavMesh.GetAreaFromName("Walkable")
        : NavMesh.AllAreas;

    public VisionSystem VisionSystem => _visionSystem;
    public Factions Faction { get; private set; }

    private readonly VisionSystem _visionSystem;
    private readonly Damageable _self;
    private readonly HumanoidController _humanoidController;
    private AsyncStateMachine _fsm;

    private Damageable _lastSeenTarget;
    private int _defaultLayer;
    private Vector3 _fixedGuardPosition;
    private Transform _guardPointTransform;

    /// <summary>
    /// Флаг, устанавливаемый при получении урона. Сбрасывается при входе в AlarmState.
    /// Используется в ChaseState для принудительного перезапуска преследования.
    /// </summary>
    public bool DamageTakenRecently { get; set; }

    public StateMachineContext(IInput input, VisionSystem visionSystem, Damageable self, Transform transform,
        HumanoidController humanoidController, float rotationSpeed, Transform[] patrolWaypoints,
        EnemyBehaviorMode behaviorMode, PatrolMode patrolMode, Factions faction)
    {
        Input = input;
        _visionSystem = visionSystem;
        _self = self;
        _humanoidController = humanoidController;
        Transform = transform;
        RotationSpeed = rotationSpeed;
        BehaviorMode = behaviorMode;
        PatrolMode = patrolMode;
        Faction = faction;
        SetWaypoints(ConvertPath.ToVector(patrolWaypoints));

        if (patrolWaypoints != null && patrolWaypoints.Length > 0 && patrolWaypoints[0] != null)
        {
            _guardPointTransform = patrolWaypoints[0];
            _fixedGuardPosition = transform.position;
        }
        else
        {
            _guardPointTransform = null;
            _fixedGuardPosition = transform.position;
        }

        _visionSystem.OnTargetReached += OnTargetReached;
        _self.OnDeath.AddListener(OnDeath);
        _self.OnDamageAttempted += OnDamageAttempted;
    }

    public void SetFsm(AsyncStateMachine fsm)
    {
        _fsm = fsm;
    }

    public void SetGuardPosition(Vector3 position)
    {
        _guardPointTransform = null;
        _fixedGuardPosition = position;
    }

    /// <summary>
    /// Устанавливает цель. Не позволяет сбросить живую цель в null.
    /// Мёртвая цель сбрасывается.
    /// </summary>
    private void SetTarget(Damageable newTarget)
    {
        // Никогда не сбрасываем живую цель
        if (newTarget == null && Target != null && Target.currentHitPoints > 0)
            return;

        // Если цель умерла — сбрасываем и lastKnown позицию
        if (newTarget == null && Target != null && Target.currentHitPoints <= 0)
        {
            ClearLastKnownTargetPosition();
            _lastSeenTarget = null;
        }

        Target = newTarget;
    }

    private void OnTargetReached(Damageable damageable)
    {
        // В Neutral режиме игнорируем обнаружение целей, пока не вступили в бой
        if (BehaviorMode == EnemyBehaviorMode.Neutral
            && (_fsm == null || (_fsm.CurrentState != _fsm.ChaseState && _fsm.CurrentState != _fsm.AttackState && _fsm.CurrentState != _fsm.ShootState)))
        {
            return;
        }

        SetTarget(damageable);

        if (damageable)
        {
            _lastSeenTarget = damageable;
        }
    }

    private async void OnDamageAttempted(Damageable.DamageMessage message)
    {
        if (IsDead) return;
        if (_humanoidController.IsBlocking) return;

        var damager = message.damager;
        if (!damager) return;

        var damagerDamageable = damager.GetComponentInParent<Damageable>();
        if (!damagerDamageable) return;
        if (damagerDamageable.currentHitPoints <= 0) return;

        _lastSeenTarget = damagerDamageable;
        _visionSystem.SetLastKnownPosition(damagerDamageable, message.damageSource);
        SetTarget(damagerDamageable);

        var damagerCollider = damagerDamageable.GetComponent<Collider>();
        if (damagerCollider && !_visionSystem.HasCandidate(damagerCollider))
        {
            _visionSystem.AddCandidate(damagerCollider, damagerDamageable);
        }

        // Устанавливаем флаг получения урона. ChaseState проверит его и перезапустит
        // преследование с актуальной позицией цели.
        DamageTakenRecently = true;

        // При получении удара всегда переводим врага в AlarmState, если он не в атаке, не стреляет и не мёртв.
        // Это критически важно для "застрявших" врагов: если враг в ChaseState, но не может
        // достигнуть цели (устаревшая lastKnown позиция, физическое выталкивание и т.д.),
        // удар по нему должен "разбудить" его и заставить перезапустить преследование.
        if (_fsm != null
            && _fsm.CurrentState != _fsm.AttackState
            && _fsm.CurrentState != _fsm.ShootState
            && _fsm.CurrentState != _fsm.DeathState)
        {
            await _fsm.TransitionTo(_fsm.AlarmState);
        }
        // Если мы в состоянии атаки и цель видна — уведомляем о контр-атаке
        else if (_fsm != null && _fsm.CurrentState == _fsm.AttackState && _fsm.AttackState is AttackState attackState)
        {
            if (IsTargetVisible(damagerDamageable))
            {
                attackState.OnAttackDetected();
            }
        }
        // Если мы в состоянии стрельбы и цель видна — также можем блокировать
        else if (_fsm != null && _fsm.CurrentState == _fsm.ShootState && _fsm.ShootState is ShootState shootState)
        {
            if (IsTargetVisible(damagerDamageable) && Random.value <= 0.7f)
            {
                // При получении урона во время стрельбы — переключаемся на BlockState
                // (аналогично AttackState.OnAttackDetected)
                _fsm.TransitionTo(_fsm.BlockState).Forget();
            }
        }
    }

    private void OnDeath()
    {
        _defaultLayer = _humanoidController.gameObject.layer;

        IsDead = true;

        _visionSystem.ClearAllLastKnownPositions();
        _lastSeenTarget = null;
        SetTarget(null);
        _visionSystem.enabled = false;
        _humanoidController.gameObject.layer = 20;
        _humanoidController.AdditionalAttackEnd();
        _humanoidController.MeleeAttackEnd();
    }

    private void OnRevive()
    {
        IsDead = false;

        _visionSystem.enabled = true;
        _humanoidController.gameObject.layer = _defaultLayer;
    }

    public void SetWaypoints(Vector3[] waypoints)
    {
        PatrolWaypoints = waypoints;
    }

    public bool IsTargetVisible(Damageable target)
    {
        return _visionSystem.IsTargetVisible(target);
    }

    public bool IsTargetInRange(Damageable target)
    {
        return _visionSystem.IsTargetInRange(target);
    }

    public bool TryGetLastKnownTargetPosition(out Vector3 position)
    {
        // Если _lastSeenTarget сброшен (например, через ClearLastKnownTargetPosition),
        // но Target всё ещё жив — используем Target как lastSeen.
        // Это критически важно для целей, полученных через AlarmState:
        // после ClearLastKnownTargetPosition _lastSeenTarget становится null,
        // TryGetLastKnownTargetPosition возвращает false, и ChaseState переходит
        // в IdleWaitState, хотя цель жива и должна преследоваться.
        if (!_lastSeenTarget)
        {
            if (Target != null && Target.currentHitPoints > 0)
            {
                _lastSeenTarget = Target;
                position = Target.Transform.position;
                return true;
            }

            position = default;
            return false;
        }

        if (_lastSeenTarget.currentHitPoints <= 0)
        {
            ClearLastKnownTargetPosition();
            position = default;
            return false;
        }

        if (_visionSystem.TryGetLastKnownPosition(_lastSeenTarget, out position))
        {
            return true;
        }

        if (_lastSeenTarget.Transform != null)
        {
            position = _lastSeenTarget.Transform.position;
            return true;
        }

        position = default;
        return false;
    }

    public void ClearLastKnownTargetPosition()
    {
        if (!_lastSeenTarget) return;

        _visionSystem.ClearLastKnownPosition(_lastSeenTarget);
    }

    public Damageable GetLastSeenTarget()
    {
        return _lastSeenTarget;
    }

    /// <summary>
    /// Устанавливает цель извне (например, при получении тревоги от другого врага)
    /// </summary>
    public void SetAlarmTarget(Damageable target)
    {
        if (target == null) return;

        _lastSeenTarget = target;
        _visionSystem.SetLastKnownPosition(target, target.Transform.position);

        // Устанавливаем цель, если её нет или если новая цель отличается
        if (Target == null || Target != target)
        {
            SetTarget(target);
        }

        // Важно: добавляем коллайдер цели в VisionSystem, чтобы он мог отслеживать
        // её видимость. Без этого IsTargetVisible всегда будет возвращать false
        // для целей, полученных через AlarmState, и враг не сможет их атаковать.
        var targetCollider = target.GetComponent<Collider>();
        if (targetCollider != null && !_visionSystem.HasCandidate(targetCollider))
        {
            _visionSystem.AddCandidate(targetCollider, target);
        }
    }

    public void Dispose()
    {
        _visionSystem.OnTargetReached -= OnTargetReached;
        _self.OnDeath.RemoveListener(OnDeath);
        _self.OnDamageAttempted -= OnDamageAttempted;
    }
}