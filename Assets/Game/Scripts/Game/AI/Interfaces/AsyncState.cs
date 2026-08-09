using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public interface IAsyncState
{
    public UniTask OnEnter(CancellationToken ct);
   
    public UniTask OnUpdate(CancellationToken ct);
   
    public UniTask OnExit(CancellationToken ct);
}

public abstract class AsyncState : IAsyncState
{
    protected AsyncStateMachine StateMachine { get; set; }
    protected CancellationTokenSource CancellationTokenSource { get; set; }
    protected bool IsCancelled => CancellationTokenSource == null || CancellationTokenSource.Token.IsCancellationRequested;

    protected AsyncState(AsyncStateMachine stateMachine)
    {
        StateMachine = stateMachine;
    }
    
    public virtual async UniTask OnEnter(CancellationToken ct)
    {
        CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
        WatchAndCancelAsync(ShouldInterrupt, CancellationTokenSource, ct)
            .SuppressCancellationThrow()
            .Forget();
        await UniTask.Yield();
    }

    public virtual async UniTask OnUpdate(CancellationToken ct)
    {
        await UniTask.Yield();
    }

    public virtual async UniTask OnExit(CancellationToken ct)
    {
        CancellationTokenSource?.Cancel();
        CancellationTokenSource?.Dispose();
        CancellationTokenSource = null;
        await UniTask.Yield(); 
    }
   
    protected virtual async UniTask HandleTransition()
    {
        await UniTask.Yield(); 
    }

    protected virtual bool ShouldInterrupt()
    {
        return false;
    }
    
    private static async UniTask WatchAndCancelAsync(Func<bool> shouldCancel, CancellationTokenSource linkedCts, CancellationToken ct)
    {
        try
        {
            await UniTask.WaitUntil(shouldCancel, cancellationToken: ct)
                .Timeout(TimeSpan.FromSeconds(30))
                .SuppressCancellationThrow();
        }
        catch (TimeoutException)
        {
            //Debug.LogWarning("WatchAndCancelAsync timed out - ShouldInterrupt never returned true");
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }

        try
        {
            if (!linkedCts.Token.IsCancellationRequested)
                linkedCts.Cancel();
        }
        catch (ObjectDisposedException) { }
    }
}
