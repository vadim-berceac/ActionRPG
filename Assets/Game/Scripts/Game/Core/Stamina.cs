using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game
{
    public class Stamina : IDisposable
    {
        public event Action<float> OnMaxStaminaChanged;
        public event Action<float> OnRegenSpeedChanged;
        public event Action<float> OnCurrentStaminaChanged;

        private readonly Damageable _damageable;

        private float _maxStamina = 100f;
        private float _regenSpeed = 10f;
        private float _regenDelay = 2f;
        private float _currentStamina;
        private float _regenDelayTimer;

        private CancellationTokenSource _cts;
        private bool _isDisposed;

        public Stamina(Damageable damageable)
        {
            _damageable = damageable;
            _currentStamina = _maxStamina;

            _cts = new CancellationTokenSource();
            RegenLoopAsync(_cts.Token).Forget();
        }

        public bool HasEnoughStamina(float amount)
        {
            return _currentStamina >= amount;
        }
        
        public float GetMaxStamina()
        {
            return _maxStamina;
        }

        public void SetMaxStamina(float maxStamina)
        {
            _maxStamina = Mathf.Max(0f, maxStamina);

            if (_currentStamina > _maxStamina)
            {
                _currentStamina = _maxStamina;
                OnCurrentStaminaChanged?.Invoke(_currentStamina);
            }

            OnMaxStaminaChanged?.Invoke(_maxStamina);
        }

        public void SetRegenSpeed(float regenSpeed)
        {
            _regenSpeed = Mathf.Max(0f, regenSpeed);
            OnRegenSpeedChanged?.Invoke(_regenSpeed);
        }

        public void SetRegenDelay(float regenDelay)
        {
            _regenDelay = Mathf.Max(0f, regenDelay);
        }

        public float GetCurrentStamina()
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
                _regenDelayTimer = _regenDelay;

                if (_currentStamina < spendAmount)
                {
                    //Debug.Log($"Недостаточно стамины: {_currentStamina} - нужно {spendAmount}");
                    return false;
                }

                //Debug.Log($"Потрачено: {_currentStamina} - {spendAmount}");
                _currentStamina -= spendAmount;
                OnCurrentStaminaChanged?.Invoke(_currentStamina);
                return true;
            }

            if (amount > 0f)
            {
                if (Mathf.Approximately(_currentStamina, _maxStamina))
                {
                    return false;
                }

                _currentStamina = Mathf.Min(_currentStamina + amount, _maxStamina);
                OnCurrentStaminaChanged?.Invoke(_currentStamina);
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

                if (_regenSpeed <= 0f)
                {
                    continue;
                }

                if (Mathf.Approximately(_currentStamina, _maxStamina))
                {
                    continue;
                }

                if (_damageable.currentHitPoints <= 0)
                {
                    continue;
                }

                TryChangeStamina(_regenSpeed * Time.deltaTime);
            }
        }
    }
}