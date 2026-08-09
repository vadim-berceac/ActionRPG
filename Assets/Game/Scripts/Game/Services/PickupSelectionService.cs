using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

public class PickupSelectionService : IInitializable, IDisposable
{
    private const int RefreshIntervalMs = 100;

    private readonly HashSet<PickupItem> _candidates = new();
    private readonly PlayerTag _playerTag;
    private readonly CancellationTokenSource _cts = new();

    private PickupItem _claimed;
    private PickupItem _displayed;

    public PickupSelectionService(PlayerTag playerTag)
    {
        _playerTag = playerTag;
    }

    public void Initialize()
    {
        RefreshLoop(_cts.Token).Forget();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    private async UniTaskVoid RefreshLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_playerTag != null)
                RefreshDisplay(_playerTag.transform.position);

            await UniTask.Delay(RefreshIntervalMs, cancellationToken: token);
        }
    }

    public void Enter(PickupItem item) => _candidates.Add(item);

    public void Exit(PickupItem item)
    {
        _candidates.Remove(item);
        if (_displayed != item) return;

        _displayed.HideTooltip();
        _displayed = null;
    }

    public PickupItem GetClosest(Vector3 origin)
    {
        PickupItem best = null;
        var bestSqrDist = float.MaxValue;

        foreach (var candidate in _candidates)
        {
            if (candidate == null) continue;

            var sqrDist = (candidate.transform.position - origin).sqrMagnitude;
            if (sqrDist >= bestSqrDist) continue;

            bestSqrDist = sqrDist;
            best = candidate;
        }

        return best;
    }

    private void RefreshDisplay(Vector3 origin)
    {
        var closest = GetClosest(origin);
        if (closest == _displayed) return;

        _displayed?.HideTooltip();
        _displayed = closest;
        _displayed?.ShowTooltip();
    }

    public bool TryClaim(PickupItem item)
    {
        if (_claimed != null) return false;
        if (!_candidates.Contains(item)) return false;

        _claimed = item;
        _candidates.Remove(item);
        return true;
    }

    public void Release(PickupItem item)
    {
        if (_claimed == item) _claimed = null;
    }
}