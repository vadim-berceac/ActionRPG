using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class AsyncStateMachine : IDisposable
{
    public IAsyncState CurrentState { get; private set; }
    public StateMachineContext Ctx { get; private set; }

    public PatrolState PatrolState { get; private set; }
    public GuardState GuardState { get; private set; }
    public ChaseState ChaseState { get; private set; }
    public AttackState AttackState { get; private set; }
    public BlockState BlockState { get; private set; }
    public DeathState DeathState { get; private set; }
    public IdleWaitState IdleWaitState { get; private set; }

    private CancellationTokenSource _stateTokenSource;
    private bool _isTransitioning;
    private bool _isDisposed;

    public AsyncStateMachine(StateMachineContext ctx)
    {
        Ctx = ctx;

        PatrolState = new PatrolState(this);
        GuardState = new GuardState(this);
        ChaseState = new ChaseState(this);
        AttackState = new AttackState(this);
        BlockState = new BlockState(this);
        DeathState = new DeathState(this);
        IdleWaitState = new IdleWaitState(this);
    }

    public async UniTask TransitionTo(IAsyncState newState)
    {
        if (_isDisposed) return; 
        if (_isTransitioning || CurrentState == newState) return;
        _isTransitioning = true;

        try
        {
            if (_stateTokenSource != null)
            {
                _stateTokenSource.Cancel();
                _stateTokenSource.Dispose();
                _stateTokenSource = null;
            }

            if (_isDisposed) return;

            _stateTokenSource = new CancellationTokenSource();
            var token = _stateTokenSource.Token;

            if (CurrentState != null)
            {
                await CurrentState.OnExit(token);
            }

            if (_isDisposed) return; 

            CurrentState = newState;
            await CurrentState.OnEnter(token);

            if (_isDisposed) return;

            _ = RunUpdateLoop(CurrentState, token);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("State transition was cancelled smoothly.");
        }
        catch (ObjectDisposedException)
        {
            Debug.Log("State transition skipped: state machine already disposed.");
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
            while (!_isDisposed && !token.IsCancellationRequested && CurrentState == state)
            {
                await state.OnUpdate(token);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        catch (OperationCanceledException) { /* Handled silently */ }
        catch (ObjectDisposedException) { /* machine disposed mid-update, ignore */ }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        Ctx.Dispose();
        _stateTokenSource?.Cancel();
        _stateTokenSource?.Dispose();
        _stateTokenSource = null;
    }
}