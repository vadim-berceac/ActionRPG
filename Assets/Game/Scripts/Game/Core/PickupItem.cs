using Game;
using UnityEngine;
using Zenject;

public class PickupItem : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private InteractOnTrigger trigger;
    [SerializeField] private WeaponData data;

    private PlayerNewInput _playerInput;
    private HumanoidController _humanoidController;
    private PickupSelectionService _selectionService;

    [Inject]
    private void Construct(PlayerNewInput playerInput, HumanoidController humanoidController, PickupSelectionService selectionService)
    {
        _playerInput = playerInput;
        _humanoidController = humanoidController;
        _selectionService = selectionService;

        trigger.OnEnter.AddListener(OnEnter);
        trigger.OnExit.AddListener(OnExit);
        _playerInput.Interact += OnInteract;
    }

    private void OnDestroy()
    {
        if (trigger != null)
        {
            trigger.OnEnter.RemoveListener(OnEnter);
            trigger.OnExit.RemoveListener(OnExit);
        }

        if (_playerInput != null)
            _playerInput.Interact -= OnInteract;

        _selectionService?.Deselect(this);
    }

    private void OnEnter() => _selectionService.Select(this);
    private void OnExit()  => _selectionService.Deselect(this);

    private void OnInteract()
    {
        if (!_selectionService.IsSelected(this)) return;

        if (data.Wear == WeaponData.WearType.Additional)
        {
            if (_humanoidController.primaryWeaponData == null ||
                _humanoidController.primaryWeaponData.Wear == WeaponData.WearType.OneHanded)
            {
                _humanoidController.CreateAdditionalWeapon(data, true);
                Destroy(root);
            }
        }
        else
        {
            if (data.Wear == WeaponData.WearType.OneHanded || _humanoidController.additionalWeaponData == null)
            {
                _humanoidController.CreatePrimaryWeapon(data, true);
            }
            else
            {
                _humanoidController.CreatePrimaryWeapon(data, true, true);
            }
            Destroy(root);
        }
        
    }
}