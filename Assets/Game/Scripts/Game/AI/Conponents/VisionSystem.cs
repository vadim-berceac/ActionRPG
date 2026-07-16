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
    [SerializeField] private float visionCheckInterval = 0.15f;
    [SerializeField] private int hysteresisChecks = 2;

    [Header("Close-Range Failsafe")]
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
        _visibleTargets.Clear();
        _visibleStreak.Clear();
        _hiddenStreak.Clear();
        _lastKnownPositions.Clear();
    }

    private void OnTriggerEnter(Collider other)
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

        return other.TryGetComponent(out damageable);
    }

    private async UniTaskVoid VisionLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            UpdateCloseRangeCandidates();
            CheckVisibility();
            RemoveStaleCandidates();
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

        foreach (var col in _candidates.Keys)
        {
            if (col != null && (_inTriggerZone.Contains(col) || _closeRangeThisTick.Contains(col)))
                continue;

            _pendingRemoval.Add(col);
        }

        foreach (var col in _pendingRemoval)
        {
            _candidates.Remove(col, out var damageable);

            _visibleStreak.Remove(damageable);
            _hiddenStreak.Remove(damageable);

            if (_visibleTargets.Remove(damageable))
                OnTargetReached?.Invoke(null);
        }
    }

    private void CheckVisibility()
    {
        foreach (var (col, damageable) in _candidates)
        {
            if (col == null) continue;

            var targetPoint = col.bounds.center;
            var isVisibleNow = !Physics.Linecast(eyePoint.position, targetPoint, obstacleMask, QueryTriggerInteraction.Ignore);
            var wasVisible = _visibleTargets.Contains(damageable);

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

    public bool IsTargetVisible(Damageable target)
    {
        return target != null && _visibleTargets.Contains(target);
    }

    public bool HasCandidate(Collider col)
    {
        return _candidates.ContainsKey(col);
    }

    public void AddCandidate(Collider col, Damageable damageable)
    {
        if (col == null || damageable == null) return;
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