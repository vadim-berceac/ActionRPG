using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WaterVolume : MonoBehaviour
{
    [Header("Пороги глубины (метры ниже поверхности воды)")]
    [SerializeField] private float enterSwimDepth = 0.6f;
    [SerializeField] private float exitSwimDepth = 0.3f;

    [Header("Выталкивание")]
    [SerializeField] private float pushOutDelay = 0.3f;
    [SerializeField] private float pushOutSpeed = 5f;
    [SerializeField] private float minSplashVerticalSpeed = 2f;

    [Header("Защита от ложных срабатываний на старте / при спавне")]
    [Tooltip("Сколько секунд после входа в триггер игнорировать проверки глубины (даём аниматору/физике устаканиться)")]
    [SerializeField] private float settleDelayAfterEnter = 0.15f;

    [Tooltip("На сколько метров нужно подняться выше enterSwimDepth, чтобы событие взвелось заново")]
    [SerializeField] private float pushEventRearmMargin = 0.7f;

    [Tooltip("Сколько кадров подряд глубина должна подтверждаться, прежде чем считать погружение реальным (защита от джиттера на границе поверхности)")]
    [SerializeField] private int requiredConsecutiveBelowFrames = 3;

    [Header("Защита от флапа триггера (Enter/Exit подряд на одном и том же физическом входе)")]
    [Tooltip("Если Exit и повторный Enter для одного контроллера произошли быстрее этого времени — считаем это флапом, а не реальным выходом, и не сбрасываем состояние")]
    [SerializeField] private float reEnterGraceWindow = 0.2f;

    [Tooltip("Минимальный интервал между событиями PushOutDelayStarted для одного контроллера — страховка от спама вне зависимости от причины")]
    [SerializeField] private float minTimeBetweenEvents = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    public event Action<HumanoidController, Vector3> PushOutDelayStarted;

    private Collider _waterCollider;
    private readonly Dictionary<HumanoidController, SwimmerState> _swimmers = new();

    private CancellationTokenSource _cts;

    private class SwimmerState
    {
        public Transform DepthPoint;
        public bool IsSwimming;
        public bool IsBelowEnterDepth;
        public float PushOutTimer;
        public bool PushEventArmed = true;
        public float TimeSinceEnter;
        public int ConsecutiveBelowFrames;

        public float LastEventTime = -999f;

        public float LastExitTime = -999f;
        public bool PendingRemoval;
    }

    private void Awake()
    {
        _waterCollider = GetComponent<Collider>();
        _waterCollider.isTrigger = true;

        if (exitSwimDepth >= enterSwimDepth)
            Debug.LogWarning($"{nameof(WaterVolume)}: exitSwimDepth должен быть меньше enterSwimDepth.", this);
    }

    private void OnEnable()
    {
        _cts = new CancellationTokenSource();
        DepthCheckLoop(_cts.Token).SuppressCancellationThrow().Forget();
    }

    private void OnDisable()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        
        _swimmers.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        var controller = other.GetComponentInParent<HumanoidController>();
        if (controller == null) return;

        if (_swimmers.TryGetValue(controller, out var existing))
        {
            existing.PendingRemoval = false;
            if (debugLog) Debug.Log($"[WaterVolume] Re-enter (already tracked): {controller.name}");
            return;
        }

        var state = new SwimmerState
        {
            DepthPoint = other.transform,
            IsSwimming = false,
            IsBelowEnterDepth = false,
            PushOutTimer = 0f,
            PushEventArmed = true,
            TimeSinceEnter = 0f,
            ConsecutiveBelowFrames = 0
        };

        if (_recentlyRemoved.TryGetValue(controller, out var removedState)
            && Time.time - removedState.LastExitTime <= reEnterGraceWindow)
        {
            state.PushEventArmed = removedState.PushEventArmed;
            state.LastEventTime = removedState.LastEventTime;
            state.TimeSinceEnter = removedState.TimeSinceEnter; 
            state.IsBelowEnterDepth = removedState.IsBelowEnterDepth;
            state.ConsecutiveBelowFrames = removedState.ConsecutiveBelowFrames;

            if (debugLog) Debug.Log($"[WaterVolume] Flap detected, carrying over state: {controller.name}");

            _recentlyRemoved.Remove(controller);
        }

        _swimmers.Add(controller, state);

        if (debugLog) Debug.Log($"[WaterVolume] Enter: {controller.name}");
    }

    private void OnTriggerExit(Collider other)
    {
        var controller = other.GetComponentInParent<HumanoidController>();
        if (controller == null) return;

        if (_swimmers.TryGetValue(controller, out var state))
        {
            if (state.IsSwimming)
            {
                controller.SetSwim(false);
                controller.SetVerticalSpeed(0f);
            }

            state.LastExitTime = Time.time;
            _recentlyRemoved[controller] = state;
            _swimmers.Remove(controller);

            if (debugLog) Debug.Log($"[WaterVolume] Exit: {controller.name}");
        }
    }

    private readonly Dictionary<HumanoidController, SwimmerState> _recentlyRemoved = new();

    private async UniTask DepthCheckLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_swimmers.Count > 0)
            {
                CheckDepths();
            }

            CleanupRecentlyRemoved();

            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, token);
        }
    }

    private void CleanupRecentlyRemoved()
    {
        if (_recentlyRemoved.Count == 0) return;

        List<HumanoidController> toRemove = null;
        foreach (var pair in _recentlyRemoved)
        {
            if (Time.time - pair.Value.LastExitTime > reEnterGraceWindow)
            {
                toRemove ??= new List<HumanoidController>();
                toRemove.Add(pair.Key);
            }
        }

        if (toRemove == null) return;
        foreach (var controller in toRemove)
            _recentlyRemoved.Remove(controller);
    }

    private void CheckDepths()
    {
    var surfaceY = _waterCollider.bounds.max.y;
    var deltaTime = Time.deltaTime;

    foreach (var pair in _swimmers)
    {
        var controller = pair.Key;
        var state = pair.Value;

        state.TimeSinceEnter += deltaTime;
        var isSettled = state.TimeSinceEnter >= settleDelayAfterEnter;

        var depth = surfaceY - state.DepthPoint.position.y;
        var excess = depth - enterSwimDepth;

        if (debugLog)
            Debug.Log($"[WaterVolume] {controller.name}: depth={depth:F3} vSpeed={controller.VerticalSpeed:F2} armed={state.PushEventArmed} below={state.IsBelowEnterDepth} consec={state.ConsecutiveBelowFrames} settled={isSettled}");

        if (excess > 0f)
        {
            if (!state.IsBelowEnterDepth)
            {
                state.IsBelowEnterDepth = true;
                state.PushOutTimer = 0f;
            }

            state.PushOutTimer += deltaTime;

            if (state.PushOutTimer >= pushOutDelay)
            {
                controller.SetVerticalSpeed(pushOutSpeed);
            }

            if (isSettled)
            {
                state.ConsecutiveBelowFrames++;

                if (state.PushEventArmed
                    && state.ConsecutiveBelowFrames >= requiredConsecutiveBelowFrames
                    && Mathf.Abs(controller.VerticalSpeed) >= minSplashVerticalSpeed
                    && Time.time - state.LastEventTime >= minTimeBetweenEvents)
                {
                    state.PushEventArmed = false;
                    state.LastEventTime = Time.time;
                    PushOutDelayStarted?.Invoke(controller, state.DepthPoint.position);

                    if (debugLog) Debug.Log($"[WaterVolume] {controller.name}: EVENT FIRED");
                }
            }
            else
            {
                state.ConsecutiveBelowFrames = Mathf.Max(state.ConsecutiveBelowFrames, 0);
            }
        }
        else
        {
            state.ConsecutiveBelowFrames = 0;

            if (state.IsBelowEnterDepth)
            {
                state.IsBelowEnterDepth = false;
                state.PushOutTimer = 0f;

                if (state.IsSwimming)
                    controller.SetVerticalSpeed(0f);
            }

            if (!state.PushEventArmed && depth <= enterSwimDepth - pushEventRearmMargin)
            {
                state.PushEventArmed = true;
            }
        }

        if (!state.IsSwimming && depth >= enterSwimDepth)
        {
            state.IsSwimming = true;
            controller.SetSwim(true);
        }
        else if (state.IsSwimming && depth <= exitSwimDepth)
        {
            state.IsSwimming = false;
            controller.SetSwim(false);
            controller.SetVerticalSpeed(0f);
        }
    }
}
}