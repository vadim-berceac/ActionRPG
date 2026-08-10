using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using UnityEngine;

[System.Serializable]
public struct ClipIKGoal
{
    [field: SerializeField] public AvatarIKGoal Goal { get; private set; }
    [field: SerializeField] public Transform Target { get; private set; }
    [Tooltip("Опционально: точка сгиба локтя/колена. Если не задана — контроллер сам берёт направление из текущей позы клипа (см. GetAutoHintPosition)")]
    [field: SerializeField] public Transform Hint { get; private set; }
    [field: SerializeField, Range(0, 1)] public float Weight { get; private set; }
}

[System.Serializable]
public struct ClipIKMapping
{
    [field: SerializeField] public AnimationClip Clip { get; private set; }
    [field: SerializeField] public ClipIKGoal[] Goals { get; private set; }

    [Header("Look At (опционально)")]
    [Tooltip("Точка на предмете, куда персонаж должен смотреть/разворачивать корпус на этом клипе. Пусто — LookAt не применяется")]
    [field: SerializeField] public Transform LookAtTarget { get; private set; }
    [field: SerializeField, Range(0, 1)] public float LookAtWeight { get; private set; }
}

public class InteractIK : MonoBehaviour
{
    [SerializeField] private InteractAnimation trigger;
    [SerializeField] private ClipIKMapping[] mappings;
    [Tooltip("Бленд-время, если у клипа не задан явный EnterBlendLength")]
    [SerializeField] private float fallbackBlendTime = 0.15f;

    private Dictionary<AnimationClip, ClipIKMapping> _lookup;
    private HumanoidController _controller;
    private CancellationTokenSource _cts;

    private ClipIKGoal[] _activeGoals = System.Array.Empty<ClipIKGoal>();
    private Transform _activeLookAtTarget;
    private float _activeLookAtWeight;

    private void Awake()
    {
        _lookup = mappings
            .Where(m => m.Clip)
            .ToDictionary(m => m.Clip, m => m);
    }

    private void OnEnable()
    {
        trigger.onInteractEnter.AddListener(OnInteractEnter);
        trigger.onInteractExit.AddListener(OnInteractExit);
        trigger.onClipStarted.AddListener(OnClipStarted);
    }

    private void OnDisable()
    {
        trigger.onInteractEnter.RemoveListener(OnInteractEnter);
        trigger.onInteractExit.RemoveListener(OnInteractExit);
        trigger.onClipStarted.RemoveListener(OnClipStarted);

        Cancel();
        _controller = null;
    }

    private void OnInteractEnter(HumanoidController controller)
    {
        _controller = controller;
    }

    private void OnInteractExit(HumanoidController controller)
    {
        BlendTo(default, fallbackBlendTime).Forget();
    }

    private void OnClipStarted(AnimationClip clip, float clipBlendLength)
    {
        if (!_controller)
        {
            return;
        }

        var mapping = _lookup.TryGetValue(clip, out var mapped) ? mapped : default;
        var duration = clipBlendLength > 0f ? clipBlendLength : fallbackBlendTime;

        BlendTo(mapping, duration).Forget();
    }

    private async UniTaskVoid BlendTo(ClipIKMapping targetMapping, float duration)
    {
        Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var fromGoals = _activeGoals;
        var toGoals = targetMapping.Goals ?? System.Array.Empty<ClipIKGoal>();
        _activeGoals = toGoals;

        var fromLookAt = _activeLookAtTarget;
        var fromLookAtWeight = _activeLookAtWeight;
        var toLookAt = targetMapping.LookAtTarget;
        var toLookAtWeight = targetMapping.LookAtWeight;
        _activeLookAtTarget = toLookAt;
        _activeLookAtWeight = toLookAtWeight;

        if (!_controller)
        {
            return;
        }

        var elapsed = 0f;
        var d = Mathf.Max(duration, 0.0001f);

        while (elapsed < d)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / d);

            ApplyGoalsBlend(fromGoals, toGoals, t);
            ApplyLookAtBlend(fromLookAt, fromLookAtWeight, toLookAt, toLookAtWeight, t);

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
        }

        ApplyGoalsBlend(fromGoals, toGoals, 1f);
        ApplyLookAtBlend(fromLookAt, fromLookAtWeight, toLookAt, toLookAtWeight, 1f);
    }

    private void ApplyGoalsBlend(ClipIKGoal[] from, ClipIKGoal[] to, float t)
    {
        foreach (var g in from)
        {
            if (!System.Array.Exists(to, x => x.Goal == g.Goal))
            {
                _controller.SetIKGoal(g.Goal, g.Target, g.Hint, Mathf.Lerp(g.Weight, 0f, t));
            }
        }

        foreach (var g in to)
        {
            var prevWeight = 0f;
            var prev = System.Array.Find(from, x => x.Goal == g.Goal);
            if (prev.Target)
            {
                prevWeight = prev.Weight;
            }

            _controller.SetIKGoal(g.Goal, g.Target, g.Hint, Mathf.Lerp(prevWeight, g.Weight, t));
        }
    }

    private void ApplyLookAtBlend(Transform fromTarget, float fromWeight, Transform toTarget, float toWeight, float t)
    {
        var target = toTarget ? toTarget : fromTarget;
        var startWeight = fromTarget ? fromWeight : 0f;
        var endWeight = toTarget ? toWeight : 0f;

        _controller.SetLookAt(target, Mathf.Lerp(startWeight, endWeight, t));
    }

    private void Cancel()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}