using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WaterVolume : MonoBehaviour
{
    [Header("Пороги глубины (метры ниже поверхности воды)")]
    [Tooltip("На такой глубине плавание ВКЛЮЧАЕТСЯ")]
    [SerializeField] private float enterSwimDepth = 0.6f;

    [Tooltip("На такой глубине плавание ВЫКЛЮЧАЕТСЯ. Должно быть меньше enterSwimDepth (гистерезис)")]
    [SerializeField] private float exitSwimDepth = 0.3f;

    private Collider _waterCollider;
    private readonly Dictionary<HumanoidController, SwimmerState> _swimmers = new();

    private CancellationTokenSource _cts;

    private class SwimmerState
    {
        public Transform DepthPoint;
        public bool IsSwimming;
    }

    private void Awake()
    {
        _waterCollider = GetComponent<Collider>();
        _waterCollider.isTrigger = true;

        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (exitSwimDepth >= enterSwimDepth)
            Debug.LogWarning($"{nameof(WaterVolume)}: exitSwimDepth должен быть меньше enterSwimDepth, иначе гистерезис не работает.", this);
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

        foreach (var pair in _swimmers)
        {
            if (pair.Value.IsSwimming)
                pair.Key.SetSwim(false);
        }
        _swimmers.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        var controller = other.GetComponentInParent<HumanoidController>();
        if (controller == null || _swimmers.ContainsKey(controller)) return;

        _swimmers.Add(controller, new SwimmerState
        {
            DepthPoint = ResolveDepthPoint(controller, other),
            IsSwimming = false
        });
    }

    private void OnTriggerExit(Collider other)
    {
        var controller = other.GetComponentInParent<HumanoidController>();
        if (controller == null) return;

        if (_swimmers.TryGetValue(controller, out var state))
        {
            if (state.IsSwimming)
                controller.SetSwim(false);

            _swimmers.Remove(controller);
        }
    }

    private async UniTask DepthCheckLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_swimmers.Count > 0)
            {
                CheckDepths();
            }

            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, token);
        }
    }

    private void CheckDepths()
    {
        var surfaceY = _waterCollider.bounds.max.y;

        foreach (var pair in _swimmers)
        {
            var controller = pair.Key;
            var state = pair.Value;

            var depth = surfaceY - state.DepthPoint.position.y;

            if (depth > enterSwimDepth)
            {
                var excess = depth - enterSwimDepth;
                controller.transform.position += Vector3.up * excess;
                depth = enterSwimDepth;
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
            }
        }
    }

    private static Transform ResolveDepthPoint(HumanoidController controller, Collider enteredCollider)
    {
        var marker = controller.GetComponentInChildren<SwimDepthPoint>();
        return marker != null ? marker.transform : enteredCollider.transform;
    }
}

public class SwimDepthPoint : MonoBehaviour { }