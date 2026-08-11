using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using UnityEngine;
using UnityEngine.Events;
using Zenject;

public class PickupItem : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private InteractOnTrigger trigger;
    [SerializeField] private InteractAnimation interactAnimation;
    [SerializeField] private WeaponData data;
    [SerializeField] private DialogueAdapter dialogueAdapter;
    [SerializeField] private string phraseKey;
    [SerializeField] private float hideDelay = 0.1f;

    private PickupSelectionService _selectionService;
    private bool _isBeingPicked;
    private bool _isInTrigger;
    public bool IsInTrigger => _isInTrigger;
    private CancellationTokenSource _cts;
    
    public UnityEvent onPickup;

    [Inject]
    private void Construct(PickupSelectionService selectionService)
    {
        _selectionService = selectionService;

        trigger.OnEnter.AddListener(OnEnter);
        trigger.OnExit.AddListener(OnExit);
        interactAnimation.onInteractEnter.AddListener(OnInteract);

        _cts = new CancellationTokenSource();
    }

    private void OnDestroy()
    {
        if (trigger)
        {
            trigger.OnEnter.RemoveListener(OnEnter);
            trigger.OnExit.RemoveListener(OnExit);
        }

        interactAnimation.onInteractEnter.RemoveListener(OnInteract);

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

        HideTooltip();
        humanoidController.TryGetComponent<Inventory>(out var inventory);
        inventory?.Add(data);
        
        DestroyAndNotifyAsync(hideDelay, _cts.Token).Forget();
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
}