using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using UnityEngine;

public class VisionSystem : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private HumanoidController humanoidController;
    [SerializeField] private Transform owner;
    [SerializeField] private Transform eyePoint;

    [Header("Vision Settings")]
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private float visionCheckInterval = 0.05f; 
    [SerializeField] private int hysteresisChecks = 1; 

    [Header("Detection")]
    [SerializeField] private float triggerRadius = 10f;
    [SerializeField] private float closeRangeRadius = 2f;

    public event Action<Damageable> OnTargetReached;

    private readonly Dictionary<Collider, Damageable> _candidates = new();
    private readonly HashSet<Collider> _inTriggerZone = new();
    private readonly HashSet<Collider> _closeRangeThisTick = new();

    private readonly HashSet<Damageable> _visibleTargets = new();
    private readonly Dictionary<Damageable, int> _visibleStreak = new();
    private readonly Dictionary<Damageable, int> _hiddenStreak = new();
    private readonly Dictionary<Damageable, Vector3> _lastKnownPositions = new();

    private readonly Collider[] _closeRangeBuffer = new Collider[8];
    private readonly List<Collider> _pendingRemoval = new();
    private readonly List<KeyValuePair<Collider, Damageable>> _candidatesSnapshot = new();

    private CancellationTokenSource _cts;

    private void OnEnable()
    {
        _cts = new CancellationTokenSource();
        VisionLoop(_cts.Token).Forget();
    }

    private void OnDisable()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _candidates.Clear();
        _inTriggerZone.Clear();
        _closeRangeThisTick.Clear();
        _visibleTargets.Clear();
        _visibleStreak.Clear();
        _hiddenStreak.Clear();
        _lastKnownPositions.Clear();
        _pendingRemoval.Clear();
        _candidatesSnapshot.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsValidTarget(other, out var damageable)) return;

        _inTriggerZone.Add(other);
        _candidates[other] = damageable;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!IsValidTarget(other, out var damageable)) return;

        _inTriggerZone.Add(other);
        _candidates[other] = damageable;
    }

    private void OnTriggerExit(Collider other)
    {
        _inTriggerZone.Remove(other);
    }

    private bool IsValidTarget(Collider other, out Damageable damageable)
    {
        damageable = null;

        if (other.transform == owner) return false;
        if (((1 << other.gameObject.layer) & humanoidController.TargetLayer.value) == 0) return false;
        if (!other.TryGetComponent(out damageable)) return false;
        if (damageable.currentHitPoints <= 0) return false;

        return true;
    }

    private async UniTaskVoid VisionLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                UpdateCloseRangeCandidates();
                RemoveStaleCandidates();
                CheckVisibility();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VisionSystem] Error in vision loop: {ex}");
            }

            await UniTask.Delay(TimeSpan.FromSeconds(visionCheckInterval), cancellationToken: token);
        }
    }

    private void UpdateCloseRangeCandidates()
    {
        _closeRangeThisTick.Clear();

        var count = Physics.OverlapSphereNonAlloc(owner.position, closeRangeRadius, _closeRangeBuffer,
            humanoidController.TargetLayer, QueryTriggerInteraction.Ignore);

        for (var i = 0; i < count; i++)
        {
            var col = _closeRangeBuffer[i];
            if (col == null) continue;
            if (!IsValidTarget(col, out var damageable)) continue;

            _closeRangeThisTick.Add(col);

            if (!_candidates.ContainsKey(col))
                _candidates[col] = damageable;
        }
    }

    private void RemoveStaleCandidates()
    {
        _pendingRemoval.Clear();

        foreach (var kvp in _candidates)
        {
            var col = kvp.Key;
            var damageable = kvp.Value;

            var isDead = damageable == null || damageable.currentHitPoints <= 0;
            var isPresent = col != null && (_inTriggerZone.Contains(col) || _closeRangeThisTick.Contains(col));

            if (isDead || !isPresent)
            {
                _pendingRemoval.Add(col);
            }
        }

        foreach (var col in _pendingRemoval)
        {
            _candidates.TryGetValue(col, out var damageable);

            _candidates.Remove(col);
            _inTriggerZone.Remove(col);

            _visibleStreak.Remove(damageable);
            _hiddenStreak.Remove(damageable);

            if (_visibleTargets.Remove(damageable))
                OnTargetReached?.Invoke(null);
        }
    }

    private void CheckVisibility()
    {
        _candidatesSnapshot.Clear();
        _candidatesSnapshot.AddRange(_candidates);

        foreach (var (col, damageable) in _candidatesSnapshot)
        {
            if (col == null) continue;

            var targetPoint = col.bounds.center;
            var wasVisible = _visibleTargets.Contains(damageable);

            var isVisibleNow = !Physics.Linecast(eyePoint.position, targetPoint, obstacleMask, QueryTriggerInteraction.Ignore);

            if (!isVisibleNow)
            {
                var distanceToOwner = Vector3.Distance(owner.position, targetPoint);
                if (distanceToOwner <= closeRangeRadius)
                {
                    if (!Physics.Linecast(owner.position, targetPoint, obstacleMask, QueryTriggerInteraction.Ignore))
                    {
                        isVisibleNow = true;
                    }
                }
            }

            if (isVisibleNow)
            {
                _lastKnownPositions[damageable] = targetPoint;

                _hiddenStreak[damageable] = 0;
                _visibleStreak.TryGetValue(damageable, out var streak);
                streak++;
                _visibleStreak[damageable] = streak;

                if (!wasVisible && streak >= hysteresisChecks)
                {
                    _visibleTargets.Add(damageable);
                    OnTargetReached?.Invoke(damageable);
                }
            }
            else
            {
                _visibleStreak[damageable] = 0;
                _hiddenStreak.TryGetValue(damageable, out var streak);
                streak++;
                _hiddenStreak[damageable] = streak;

                if (wasVisible && streak >= hysteresisChecks)
                {
                    _visibleTargets.Remove(damageable);
                    OnTargetReached?.Invoke(null);
                }
            }
        }
    }

    public bool IsTargetInRange(Damageable target)
    {
        if (!target) return false;
        if (!target.Transform) return false;

        var distance = Vector3.Distance(owner.position, target.Transform.position);
        return distance <= triggerRadius;
    }

    public bool IsTargetVisible(Damageable target)
    {
        return target && _visibleTargets.Contains(target);
    }

    public bool HasCandidate(Collider col)
    {
        return _candidates.ContainsKey(col);
    }

    public void AddCandidate(Collider col, Damageable damageable)
    {
        if (!col || !damageable) return;
        if (_candidates.ContainsKey(col)) return;

        _candidates[col] = damageable;
        _inTriggerZone.Add(col);
    }

    public bool TryGetLastKnownPosition(Damageable target, out Vector3 position)
    {
        return _lastKnownPositions.TryGetValue(target, out position);
    }

    public void SetLastKnownPosition(Damageable target, Vector3 position)
    {
        _lastKnownPositions[target] = position;
    }

    public void ClearLastKnownPosition(Damageable target)
    {
        _lastKnownPositions.Remove(target);
    }

    public void ClearAllLastKnownPositions()
    {
        _lastKnownPositions.Clear();
    }
}