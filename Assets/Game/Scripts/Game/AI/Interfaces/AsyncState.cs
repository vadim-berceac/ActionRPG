using System;
using System.Threading;
using Cysharp.Threading.Tasks;

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

    protected AsyncState(AsyncStateMachine stateMachine)
    {
        StateMachine = stateMachine;
    }
    
    public virtual async UniTask OnEnter(CancellationToken ct)
    {
        CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await UniTask.Yield(); 
    }

    public virtual async UniTask OnUpdate(CancellationToken ct)
    {
        _ = UniTask.RunOnThreadPool(
                () => WatchAndCancelAsync(ShouldInterrupt, CancellationTokenSource, ct),
                cancellationToken: ct)
            .SuppressCancellationThrow();
        
        await UniTask.Yield(); 
    }

    public virtual async UniTask OnExit(CancellationToken ct)
    {
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
        await UniTask.WaitUntil(shouldCancel, cancellationToken: ct)
            .SuppressCancellationThrow();

        if (!linkedCts.IsCancellationRequested)
            linkedCts.Cancel();
    }

}
