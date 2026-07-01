using System;
using Game;
using UnityEngine;

[RequireComponent(typeof(Inventory))]
[RequireComponent(typeof(HumanoidController))]
public class Equipment : MonoBehaviour
{
    public enum EquipmentType
    {
        Primary,
        Additional,
        Ranged,
        Ammunition,
    }
    private Inventory _inventory;
    private HumanoidController _humanoidController;

    private InventoryItemSlot _primaryWeapon;
    private InventoryItemSlot _additionalWeapon;
    private InventoryItemSlot _rangedWeapon;
    private InventoryItemSlot _ammunition;
    
    public ItemData Primary => _primaryWeapon == null ? null : _primaryWeapon.ItemData;
    public ItemData Additional => _additionalWeapon == null ? null : _additionalWeapon.ItemData;
    public ItemData Ranged => _rangedWeapon == null ? null : _rangedWeapon.ItemData;
    
    public event Action<ItemData, EquipmentType> OnEquip;

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
            
            case WeaponData.WearType.Ranged:
                TryEquipRanged(weapon, amount);
                break;
            
            case WeaponData.WearType.Ammunition:
                TryEquipAmmunition(weapon, amount);
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
            DestroySlot(ref _additionalWeapon);
            OnEquip?.Invoke(null, EquipmentType.Additional);
        }
        OnEquip?.Invoke(weapon, EquipmentType.Primary);
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
        OnEquip?.Invoke(weapon, EquipmentType.Additional);
    }

    private void TryEquipRanged(WeaponData weapon, int amount)
    {
        if (_rangedWeapon != null)
        {
            ReturnToInventory(weapon, amount);
            return;
        }
        
        _rangedWeapon = new InventoryItemSlot(weapon, amount);
        _humanoidController.CreateRangedWeapon(weapon);
        
        OnEquip?.Invoke(weapon, EquipmentType.Ranged);
    }
    
    private void TryEquipAmmunition(WeaponData weapon, int amount)
    {
        if (_ammunition != null)
        {
            ReturnToInventory(weapon, amount);
            return;
        }
        
        _ammunition = new InventoryItemSlot(weapon, amount);
        _humanoidController.CreateAmmunition(weapon);
        
        OnEquip?.Invoke(weapon, EquipmentType.Ammunition);
    }

    private void ReturnToInventory(ItemData item, int amount)
    {
        _inventory.Add(item, amount);
    }

    private void DestroySlot(ref InventoryItemSlot slot)
    {
        if (slot == null)
        {
            return;
        }
        if (slot == _primaryWeapon)
        {
            _humanoidController.CreatePrimaryWeapon(null);
        }

        if (slot == _additionalWeapon)
        {
            _humanoidController.CreateAdditionalWeapon(null);
        }

        if (slot == _rangedWeapon)
        {
            
        }
        ReturnToInventory(slot.ItemData, slot.Amount);
        slot.Dispose();
        slot = null;
    }

    public void DestroyPrimary()
    {
        DestroySlot(ref _primaryWeapon);
    }

    public void DestroyAdditional()
    {
        DestroySlot(ref _additionalWeapon);
    }

    public void DestroyRanged()
    {
        DestroySlot(ref _rangedWeapon);
    }
}
