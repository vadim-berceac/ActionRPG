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

    public event Action<Damageable> OnTargetReached;

    private readonly Dictionary<Collider, Damageable> _candidates = new();
    private readonly HashSet<Damageable> _visibleTargets = new();
    private readonly Dictionary<Damageable, int> _visibleStreak = new();
    private readonly Dictionary<Damageable, int> _hiddenStreak = new();
    private readonly Dictionary<Damageable, Vector3> _lastKnownPositions = new();

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
        _visibleTargets.Clear();
        _visibleStreak.Clear();
        _hiddenStreak.Clear();
        _lastKnownPositions.Clear();
    }

    private void OnTriggerEnter(Collider other) => TryRegister(other);

    private void OnTriggerExit(Collider other) => TryUnregister(other);

    private void TryRegister(Collider other)
    {
        if (other.transform == owner) return;
        if (((1 << other.gameObject.layer) & humanoidController.TargetLayer.value) == 0) return;

        if (!other.TryGetComponent<Damageable>(out var damageable)) return;

        _candidates[other] = damageable;
    }

    private void TryUnregister(Collider other)
    {
        if (!_candidates.Remove(other, out var damageable)) return;

        _visibleStreak.Remove(damageable);
        _hiddenStreak.Remove(damageable);

        if (_visibleTargets.Remove(damageable))
            OnTargetReached?.Invoke(null);
    }

    private async UniTaskVoid VisionLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            CheckVisibility();
            await UniTask.Delay(TimeSpan.FromSeconds(visionCheckInterval), cancellationToken: token);
        }
    }

    private void CheckVisibility()
    {
        foreach (var (col, damageable) in _candidates)
        {
            if (col == null) continue;

            var targetPoint = col.bounds.center;
            var isVisibleNow = !Physics.Linecast(eyePoint.position, targetPoint, obstacleMask);
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

    public bool TryGetLastKnownPosition(Damageable target, out Vector3 position)
    {
        return _lastKnownPositions.TryGetValue(target, out position);
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