using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class CollisionDetector : MonoBehaviour
{
    [SerializeField] private GameFlowController gameFlowController;
    [SerializeField] private LayerMask collisionLayerMask;
    [SerializeField] private Collider selfCollider;
    [SerializeField] private float resetCollisionDelay = 0.5f;

    public event Action<Collider> OnLastHit;
    public bool IsOverlapping { get; private set; }

    private Collider _lastHitCollider;
    private GameFlowController _lastHitFlow;
    private readonly Collider[] _results = new Collider[16];
    private CancellationTokenSource _cts;
    private CancellationTokenSource _resetCts;

    private void OnEnable()
    {
        _cts = new CancellationTokenSource();
        RunOverlapLoopAsync(_cts.Token).Forget();

        OnLastHit += OnHit;
    }

    private void OnDisable()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _resetCts?.Cancel();
        _resetCts?.Dispose();
        _resetCts = null;

        OnLastHit -= OnHit;
    }

    private async UniTaskVoid RunOverlapLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var count = Physics.OverlapBoxNonAlloc(selfCollider.bounds.center, selfCollider.bounds.extents,
                _results, transform.rotation, collisionLayerMask);

            var wasOverlapping = IsOverlapping;
            IsOverlapping = false;

            for (var i = 0; i < count; i++)
            {
                if (_results[i] == selfCollider) continue;

                IsOverlapping = true;
                OnLastHit?.Invoke(_results[i]);
                break;
            }

            if (wasOverlapping && !IsOverlapping)
                OnLastHit?.Invoke(null);

            await UniTask.Yield(PlayerLoopTiming.FixedUpdate, ct);
        }
    }

    private async UniTaskVoid ResetCollisionDelay(CancellationToken ct)
    {
        await UniTask.WaitForSeconds(resetCollisionDelay, true, cancellationToken: ct);
        _lastHitCollider = null;
        _lastHitFlow = null;
    }

    private void OnHit(Collider col)
    {
        if (col == null || col == _lastHitCollider) return;

        _lastHitCollider = col;
        _lastHitFlow = col.GetComponent<GameFlowController>();

        _resetCts?.Cancel();
        _resetCts?.Dispose();
        _resetCts = new CancellationTokenSource();
        ResetCollisionDelay(_resetCts.Token).Forget();

        CheckDamage();
    }

    private void CheckDamage()
    {
        if (_lastHitFlow == null) return;

        if (_lastHitFlow.Mass < gameFlowController.Mass) return;

        gameFlowController.Fsm.Ctx.HitReaction(true);
        Debug.Log("Boom!");
    }
}