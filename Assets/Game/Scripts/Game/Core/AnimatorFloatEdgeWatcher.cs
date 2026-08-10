using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

public class AnimatorFloatEdgeWatcher : MonoBehaviour
{
    [Header("Источник")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _paramName = "Direction";

    [Header("Целевые значения")]
    [Tooltip("Значение параметра, при достижении которого срабатывает 'положительное' событие")]
    [SerializeField] private float _positiveTargetValue = 1f;

    [Tooltip("Значение параметра, при достижении которого срабатывает 'отрицательное' событие")]
    [SerializeField] private float _negativeTargetValue = -1f;

    [Header("Пороги")]
    [Tooltip("Допустимое отклонение от целевого значения, при котором событие считается 'достигнутым'")]
    [SerializeField, Min(0f)] private float _triggerTolerance = 0.05f;

    [Tooltip("На сколько значение должно отступить от целевого назад, чтобы событие могло сработать снова (должно быть больше tolerance)")]
    [SerializeField, Min(0f)] private float _resetHysteresis = 0.15f;

    [Tooltip("0 = проверять каждый кадр. Можно поставить интервал, если точность по кадрам не критична")]
    [SerializeField] private float _pollIntervalSeconds = 0f;

    [Header("События")]
    public UnityEvent OnReachedPositiveOne;
    public UnityEvent OnReachedNegativeOne;

    public event Action ReachedPositiveOne;
    public event Action ReachedNegativeOne;

    private int _paramHash;
    private bool _positiveArmed = true;
    private bool _negativeArmed = true;
    private CancellationTokenSource _cts;

    private void Awake()
    {
        _paramHash = Animator.StringToHash(_paramName);
    }

    private void OnEnable()
    {
        _cts = new CancellationTokenSource();
        WatchLoop(_cts.Token).Forget();
    }

    private void OnDisable()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async UniTaskVoid WatchLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var value = _animator.GetFloat(_paramHash);
            Evaluate(value);

            if (_pollIntervalSeconds > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), cancellationToken: token);
            else
                await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
    }

    private void Evaluate(float value)
    {
        var distToPositive = Mathf.Abs(value - _positiveTargetValue);
        var distToNegative = Mathf.Abs(value - _negativeTargetValue);

        if (distToPositive <= _triggerTolerance)
        {
            if (_positiveArmed)
            {
                _positiveArmed = false; 
                ReachedPositiveOne?.Invoke();
                OnReachedPositiveOne?.Invoke();
            }
        }
        else if (distToPositive >= _resetHysteresis)
        {
            _positiveArmed = true; 
        }

        if (distToNegative <= _triggerTolerance)
        {
            if (_negativeArmed)
            {
                _negativeArmed = false;
                ReachedNegativeOne?.Invoke();
                OnReachedNegativeOne?.Invoke();
            }
        }
        else if (distToNegative >= _resetHysteresis)
        {
            _negativeArmed = true;
        }
    }
}