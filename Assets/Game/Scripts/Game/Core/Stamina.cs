using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game
{
    public class Stamina : IUIUpdater, IDisposable
    {
        public event Action<float> OnMaxValueChanged;
        public event Action<float> OnRegenSpeedChanged;
        public event Action<float> OnCurrentValueChanged;

        private readonly Damageable _damageable;
        private readonly CharacterParams  _characterParams;
        
        private float _currentStamina;
        private float _regenDelayTimer;

        private CancellationTokenSource _cts;
        private bool _isDisposed;

        public Stamina(Damageable damageable, CharacterParams characterParams)
        {
            _damageable = damageable;
            _characterParams = characterParams;
            _currentStamina = _characterParams.MaxStamina;

            _cts = new CancellationTokenSource();
            RegenLoopAsync(_cts.Token).Forget();
        }

        public bool HasEnoughStamina(float amount)
        {
            return _currentStamina >= amount;
        }
        
        public float GetMaxValue()
        {
            return _characterParams.MaxStamina;
        }

        public float GetCurrentValue()
        {
            return _currentStamina;
        }

        public bool TryChangeStamina(float amount)
        {
            if (_isDisposed)
            {
                return false;
            }

            if (amount < 0f)
            {
                var spendAmount = -amount;
                _regenDelayTimer = _characterParams.RegenDelay;

                if (_currentStamina < spendAmount)
                {
                    return false;
                }

                _currentStamina -= spendAmount;
                OnCurrentValueChanged?.Invoke(_currentStamina);
                return true;
            }

            if (amount > 0f)
            {
                if (Mathf.Approximately(_currentStamina, _characterParams.MaxStamina))
                {
                    return false;
                }

                _currentStamina = Mathf.Min(_currentStamina + amount, _characterParams.MaxStamina);
                OnMaxValueChanged?.Invoke(_currentStamina);
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async UniTaskVoid RegenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var canceled = await UniTask.Yield(PlayerLoopTiming.Update, token)
                    .SuppressCancellationThrow();

                if (canceled || _isDisposed)
                {
                    break;
                }

                if (_regenDelayTimer > 0f)
                {
                    _regenDelayTimer -= Time.deltaTime;
                    continue;
                }

                if (_characterParams.RegenSpeed <= 0f)
                {
                    continue;
                }

                if (Mathf.Approximately(_currentStamina, _characterParams.MaxStamina))
                {
                    continue;
                }

                if (_damageable.currentHitPoints <= 0)
                {
                    continue;
                }

                TryChangeStamina(_characterParams.RegenSpeed * Time.deltaTime);
            }
        }
    }
}

public interface IUIUpdater
{
    public event Action<float> OnMaxValueChanged;
    public event Action<float> OnRegenSpeedChanged;
    public event Action<float> OnCurrentValueChanged;

    public float GetMaxValue();

    public float GetCurrentValue();
}