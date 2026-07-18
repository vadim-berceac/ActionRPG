using System;
using Game;
using UnityEngine;
using UnityEngine.AI;

public class StateMachineContext : IDisposable
{
    public IInput Input { get; private set; }
    public Transform Transform { get; private set; }
    public Damageable Target { get; private set; }
    public float RotationSpeed { get; private set; }
    public Vector3[] PatrolWaypoints { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsGrounded => _humanoidController.IsGrounded;
    public bool HasAdditionalWeapon => _humanoidController.HasAdditionalWeapon;
    public EnemyBehaviorMode BehaviorMode { get; private set; }
    public PatrolMode PatrolMode { get; private set; }

    public Vector3 GuardPosition => _guardPointTransform != null
        ? _guardPointTransform.position
        : _fixedGuardPosition;

    public float PreferredAttackDistance
    {
        get
        {
            var weaponData = _humanoidController.PrimaryWeaponData;
            return weaponData ? weaponData.preferredDistance : 2f;
        }
    }

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
            && (_fsm == null || (_fsm.CurrentState != _fsm.ChaseState && _fsm.CurrentState != _fsm.AttackState)))
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

        // Neutral режим: при получении удара переходим в AlarmState, если ещё не в бою
        if (BehaviorMode == EnemyBehaviorMode.Neutral
            && _fsm != null
            && _fsm.CurrentState != _fsm.AlarmState
            && _fsm.CurrentState != _fsm.ChaseState
            && _fsm.CurrentState != _fsm.AttackState)
        {
            await _fsm.TransitionTo(_fsm.AlarmState);
            return;
        }

        // Если мы в состоянии атаки и цель видна — уведомляем о контр-атаке
        if (_fsm != null && _fsm.CurrentState == _fsm.AttackState && _fsm.AttackState is AttackState attackState)
        {
            if (IsTargetVisible(damagerDamageable))
            {
                attackState.OnAttackDetected();
            }
        }
        // Не дёргаемся, если уже в бою
        else if (_fsm != null
            && _fsm.CurrentState != _fsm.AlarmState
            && _fsm.CurrentState != _fsm.AttackState
            && _fsm.CurrentState != _fsm.ChaseState)
        {
            await _fsm.TransitionTo(_fsm.AlarmState);
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

    public bool TryGetLastKnownTargetPosition(out Vector3 position)
    {
        if (!_lastSeenTarget)
        {
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
    public void SetAlarmTarget(Damageable target, Vector3 lastKnownPosition)
    {
        if (target == null) return;

        _lastSeenTarget = target;
        _visionSystem.SetLastKnownPosition(target, lastKnownPosition);

        // Устанавливаем цель, если её нет или если новая цель отличается
        if (Target == null || Target != target)
        {
            SetTarget(target);
        }
    }

    public void Dispose()
    {
        _visionSystem.OnTargetReached -= OnTargetReached;
        _self.OnDeath.RemoveListener(OnDeath);
        _self.OnDamageAttempted -= OnDamageAttempted;
    }
}