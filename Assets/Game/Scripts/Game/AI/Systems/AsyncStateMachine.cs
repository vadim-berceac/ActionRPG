using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class AsyncStateMachine
{
    public IAsyncState CurrentState { get; private set; }
    public StateMachineContext Ctx { get; private set; }
    
    //States
    public WaitForOrdersState WaitForOrdersState { get; private set; }
    public PatrolState PatrolState { get; private set; }
    public HitReactionState HitReactionState { get; private set; }
    
    private CancellationTokenSource _stateTokenSource;
    private bool _isTransitioning;

    public AsyncStateMachine(StateMachineContext ctx)
    {
        Ctx = ctx;
        
        WaitForOrdersState = new WaitForOrdersState(this);
        PatrolState = new PatrolState(this);
        HitReactionState = new HitReactionState(this);
    }

    public async UniTask TransitionTo(IAsyncState newState)
    {
        if (_isTransitioning || CurrentState == newState) return;
        _isTransitioning = true;

        try
        {
            if (_stateTokenSource != null)
            {
                _stateTokenSource.Cancel();
                _stateTokenSource.Dispose();
            }

            _stateTokenSource = new CancellationTokenSource();
            var token = _stateTokenSource.Token;
            
            if (CurrentState != null)
            {
                await CurrentState.OnExit(token);
            }

            CurrentState = newState;
            await CurrentState.OnEnter(token);

            _ = RunUpdateLoop(CurrentState, token);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("State transition was cancelled smoothly.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during state transition: {ex}");
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    private async UniTask RunUpdateLoop(IAsyncState state, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && CurrentState == state)
            {
                await state.OnUpdate(token);
            }
        }
        catch (OperationCanceledException) { /* Handled silently */ }
    }

    public void Shutdown()
    {
        _stateTokenSource?.Cancel();
        _stateTokenSource?.Dispose();
    }
}
