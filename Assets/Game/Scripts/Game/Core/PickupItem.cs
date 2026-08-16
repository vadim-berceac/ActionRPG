using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using UnityEngine;
using UnityEngine.Events;
using Zenject;

public class PickupItem : MonoBehaviour, ISaveable
{
    [System.Serializable]
    public class PickupState
    {
        public string SaveKey { get; set; }
        public bool IsPicked { get; set; }
        public bool IsRuntimeSpawned { get; set; }
        public Vector3 Position { get; set; }
        public string ItemName { get; set; }
    }

    [SerializeField] private GameObject root;
    [SerializeField] private InteractOnTrigger trigger;
    [SerializeField] private InteractAnimation interactAnimation;
    [SerializeField] private WeaponData data;
    [SerializeField] private DialogueAdapter dialogueAdapter;
    [SerializeField] private string phraseKey;
    [SerializeField] private float hideDelay = 0.1f;
    [field: SerializeField] public string SaveKey { get; set; }

    private PickupSelectionService _selectionService;
    private PickupPersistenceService _pickupRegistry;
    private bool _isBeingPicked;
    private bool _isInTrigger;
    private bool _isRuntimeSpawned;
    private CancellationTokenSource _cts;

    public bool IsInTrigger => _isInTrigger;
    public string SaveStateItemName => data != null ? data.name : null;
    public bool IsRuntimeSpawned => _isRuntimeSpawned;
    public UnityEvent onPickup;

    private void Awake()
    {
        if (string.IsNullOrEmpty(SaveKey))
        {
            var pos = transform.position;
            SaveKey = $"{gameObject.name}_{pos.x:0.00}_{pos.y:0.00}_{pos.z:0.00}";
        }
    }

    [Inject]
    private void Construct(PickupSelectionService selectionService,
        PickupPersistenceService pickupRegistry)
    {
        _selectionService = selectionService;
        _pickupRegistry = pickupRegistry;

        trigger.OnEnter.AddListener(OnEnter);
        trigger.OnExit.AddListener(OnExit);
        if (interactAnimation)
        {
            interactAnimation.onInteractEnter.AddListener(OnInteract);
        }

        _cts = new CancellationTokenSource();
    }

    private void OnDestroy()
    {
        if (trigger)
        {
            trigger.OnEnter.RemoveListener(OnEnter);
            trigger.OnExit.RemoveListener(OnExit);
        }

        if (interactAnimation)
        {
            interactAnimation.onInteractEnter.RemoveListener(OnInteract);
        }

        _selectionService?.Exit(this);
        _selectionService?.Release(this);

        _cts?.Cancel();
        _cts?.Dispose();
    }

    private void OnEnter(Collider other)
    {
        if (other == null) return;
        if (!other.TryGetComponent<PlayerTag>(out var playerTag)) return;
        _isInTrigger = true;
        _selectionService.Enter(this);
    }

    private void OnExit(Collider other)
    {
        if (other == null) return;
        if (!other.TryGetComponent<PlayerTag>(out var playerTag)) return;
        _isInTrigger = false;
        _selectionService.Exit(this);
    }

    public void ShowTooltip()
    {
        if (!dialogueAdapter) return;
        dialogueAdapter.ActivateCanvasWithTranslatedText(phraseKey);
    }

    public void HideTooltip()
    {
        if (!dialogueAdapter) return;
        dialogueAdapter.DeactivateCanvasWithDelay(hideDelay);
    }

    private void OnInteract(HumanoidController humanoidController)
    {
        if (_isBeingPicked) return;

        var closest = _selectionService.GetClosest(humanoidController.transform.position);
        if (closest != this) return;

        if (!_selectionService.TryClaim(this)) return;

        _isBeingPicked = true;

        _pickupRegistry?.MarkPicked(SaveKey);

        HideTooltip();
        humanoidController.TryGetComponent<Inventory>(out var inventory);
        inventory?.Add(data);

        if (_cts != null)
        {
            DestroyAndNotifyAsync(hideDelay, _cts.Token).Forget();
        }
        else
        {
            onPickup?.Invoke();
            Destroy(root);
        }
    }

    private async UniTaskVoid DestroyAndNotifyAsync(float delay, CancellationToken token)
    {
        try
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: token);
        }
        catch (System.OperationCanceledException)
        {
            return;
        }

        onPickup?.Invoke();

        Destroy(root);
    }

    public void DestroyView()
    {
        if (root)
        {
            Destroy(root);
        }
    }

    public void MarkRuntimeSpawned()
    {
        _isRuntimeSpawned = true;
    }

    public void SetSaveKey(string saveKey)
    {
        if (!string.IsNullOrEmpty(saveKey))
        {
            SaveKey = saveKey;
        }
    }

    public void DestroySelf()
    {
        if (root)
        {
            Destroy(root);
            return;
        }

        Destroy(gameObject);
    }

    public PickupState CaptureRuntimeState()
    {
        return new PickupState
        {
            SaveKey = SaveKey,
            IsPicked = _pickupRegistry?.IsPicked(SaveKey) ?? false,
            IsRuntimeSpawned = true,
            Position = transform.position,
            ItemName = SaveStateItemName
        };
    }

    public object CaptureState()
    {
        return new PickupState
        {
            SaveKey = SaveKey,
            IsPicked = _pickupRegistry?.IsPicked(SaveKey) ?? false,
            IsRuntimeSpawned = _isRuntimeSpawned,
            Position = transform.position,
            ItemName = SaveStateItemName
        };
    }

    public void RestoreState(object state)
    {
        if (state is not PickupState pickupState) return;
        if (!pickupState.IsPicked) return;

        _pickupRegistry?.MarkPicked(SaveKey);
        DestroyView();
    }
}