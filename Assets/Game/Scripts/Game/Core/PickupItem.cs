using Game;
using UnityEngine;
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

    [Inject]
    private void Construct(PickupSelectionService selectionService)
    {
        _selectionService = selectionService;

        trigger.OnEnter.AddListener(OnEnter);
        trigger.OnExit.AddListener(OnExit);
        interactAnimation.onInteractEnter.AddListener(OnInteract);
    }

    private void OnDestroy()
    {
        if (trigger != null)
        {
            trigger.OnEnter.RemoveListener(OnEnter);
            trigger.OnExit.RemoveListener(OnExit);
        }

        interactAnimation.onInteractEnter.RemoveListener(OnInteract);

        _selectionService?.Exit(this);
        _selectionService?.Release(this);
    }

    private void OnEnter(Collider other) => _selectionService.Enter(this);
    private void OnExit(Collider other) => _selectionService.Exit(this);

    public void ShowTooltip()
    {
        if (dialogueAdapter == null) return;
        dialogueAdapter.ActivateCanvasWithTranslatedText(phraseKey);
    }

    public void HideTooltip()
    {
        if (dialogueAdapter == null) return;
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
        Destroy(root);
    }
}