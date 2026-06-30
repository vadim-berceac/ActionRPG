using Game;
using UnityEngine;

[RequireComponent(typeof(Inventory))]
[RequireComponent(typeof(HumanoidController))]
public class Equipment : MonoBehaviour
{
    private Inventory _inventory;
    private HumanoidController _humanoidController;

    private InventoryItemSlot _primaryWeapon;
    private InventoryItemSlot _additionalWeapon;
    private InventoryItemSlot _rangedWeapon;

    private void Awake()
    {
        _inventory = GetComponent<Inventory>();
        _humanoidController = GetComponent<HumanoidController>();
        
        _inventory.OnTransfer += OnTransfer;
    }

    private void OnDestroy()
    {
        _inventory.OnTransfer -= OnTransfer;
    }

    private void OnTransfer(ItemData item, int amount)
    {
        if (item is not WeaponData weapon)
        {
            ReturnToInventory(item, amount);
            return;
        }

        switch (weapon.Wear)
        {
            case WeaponData.WearType.OneHanded:
            case WeaponData.WearType.TwoHanded:
                TryEquipPrimary(weapon, amount);
                break;

            case WeaponData.WearType.Additional:
                TryEquipAdditional(weapon, amount);
                break;

            default:
                ReturnToInventory(item, amount);
                break;
        }
    }

    private void TryEquipPrimary(WeaponData weapon, int amount)
    {
        if (_primaryWeapon != null)
        {
            ReturnToInventory(weapon, amount);
            return;
        }

        _primaryWeapon = new InventoryItemSlot(weapon, amount);
        _humanoidController.CreatePrimaryWeapon(weapon);

        if (weapon.Wear == WeaponData.WearType.TwoHanded && _additionalWeapon != null)
        {
            ReturnToInventory(_additionalWeapon.ItemData, _additionalWeapon.Amount);
            DestroySlot(ref _additionalWeapon);
            _humanoidController.CreateAdditionalWeapon(null);
        }
    }

    private void TryEquipAdditional(WeaponData weapon, int amount)
    {
        var primaryIsTwoHanded = _primaryWeapon != null
                                 && ((WeaponData)_primaryWeapon.ItemData).Wear == WeaponData.WearType.TwoHanded;

        if (_additionalWeapon != null || primaryIsTwoHanded)
        {
            ReturnToInventory(weapon, amount);
            return;
        }

        _additionalWeapon = new InventoryItemSlot(weapon, amount);
        _humanoidController.CreateAdditionalWeapon(weapon);
    }

    private void ReturnToInventory(ItemData item, int amount)
    {
        _inventory.Add(item, amount);
    }

    public void DestroySlot(ref InventoryItemSlot slot)
    {
        ReturnToInventory(slot.ItemData, slot.Amount);
        slot.Dispose();
        slot = null;
    }
}
