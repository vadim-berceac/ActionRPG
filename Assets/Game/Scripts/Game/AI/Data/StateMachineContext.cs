using System;
using Game;
using UnityEngine;
using UnityEngine.AI;

public class StateMachineContext : IDisposable
{
    public IInput Input { get; private set; }
    public Transform Transform  { get; private set; }
    public Damageable Target { get; private set; }
    public float RotationSpeed { get; private set; }
    public Vector3[] PatrolWaypoints { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsGrounded => _humanoidController.IsGrounded;
    public bool HasAdditionalWeapon => _humanoidController.HasAdditionalWeapon;
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
    
    private readonly VisionSystem _visionSystem;
    private readonly Damageable _self;
    private readonly HumanoidController _humanoidController;
    private AsyncStateMachine _fsm;

    private Damageable _lastSeenTarget;
    private int _defaultLayer;
    
    public StateMachineContext(IInput input, VisionSystem visionSystem, Damageable self, Transform transform,
        HumanoidController humanoidController, float rotationSpeed, Transform[] patrolWaypoints)
    {
        Input = input;
        _visionSystem = visionSystem;
        _self = self;
        _humanoidController = humanoidController;
        Transform = transform;
        RotationSpeed = rotationSpeed;
        SetWaypoints(ConvertPath.ToVector(patrolWaypoints));
        
        _visionSystem.OnTargetReached += OnTargetReached;
        _self.OnDeath.AddListener(OnDeath);

        _self.OnDamageAttempted += OnDamageAttempted;
    }

    public void SetFsm(AsyncStateMachine fsm)
    {
        _fsm = fsm;
    }
    
    private void OnTargetReached(Damageable damageable)
    {
        Target = damageable;

        if (damageable != null)
        {
            _lastSeenTarget = damageable;
        }
    }

    private async void OnDamageAttempted(Damageable.DamageMessage message)
    {
        if (IsDead) return;

        var damager = message.damager;
        if (damager == null) return;

        var damagerDamageable = damager.GetComponentInParent<Damageable>();
        if (damagerDamageable == null) return;

        if (damagerDamageable.currentHitPoints <= 0)
            return;

        _lastSeenTarget = damagerDamageable;
        _visionSystem.SetLastKnownPosition(damagerDamageable, message.damageSource);
        Target = damagerDamageable;

        var damagerCollider = damagerDamageable.GetComponent<Collider>();
        if (damagerCollider != null && !_visionSystem.HasCandidate(damagerCollider))
        {
            _visionSystem.AddCandidate(damagerCollider, damagerDamageable);
        }

        if (_fsm != null)
        {
            await _fsm.TransitionTo(_fsm.ChaseState);
        }
    }

    private void OnDeath()
    {
        _defaultLayer = _humanoidController.gameObject.layer;
        
        IsDead = true;

        _visionSystem.ClearAllLastKnownPositions();
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
        if (_lastSeenTarget == null)
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

        return _visionSystem.TryGetLastKnownPosition(_lastSeenTarget, out position);
    }

    public void ClearLastKnownTargetPosition()
    {
        if (_lastSeenTarget == null) return;

        _visionSystem.ClearLastKnownPosition(_lastSeenTarget);
        _lastSeenTarget = null;
    }

    public void Dispose()
    {
        _visionSystem.OnTargetReached -= OnTargetReached;
        _self.OnDeath.RemoveListener(OnDeath);
        _self.OnDamageAttempted -= OnDamageAttempted;
    }
}
