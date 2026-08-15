using System;
using Game;
using UnityEngine;
using Zenject;

[RequireComponent(typeof(Inventory))]
[RequireComponent(typeof(HumanoidController))]
public class Equipment : MonoBehaviour, ISaveable
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
    private IItemDatabase _itemDatabase;

    private InventoryItemSlot _primaryWeapon;
    private InventoryItemSlot _additionalWeapon;
    private InventoryItemSlot _rangedWeapon;
    private InventoryItemSlot _ammunition;

    public ItemData Primary => _primaryWeapon == null ? null : _primaryWeapon.ItemData;
    public ItemData Additional => _additionalWeapon == null ? null : _additionalWeapon.ItemData;
    public ItemData Ranged => _rangedWeapon == null ? null : _rangedWeapon.ItemData;

    public event Action<ItemData, EquipmentType> OnEquip;

    public string SaveKey => "equipment";

    [Inject]
    private void Construct(IItemDatabase itemDatabase)
    {
        _itemDatabase = itemDatabase;
    }

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

        _primaryWeapon = new InventoryItemSlot(weapon, false, amount);
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

        _additionalWeapon = new InventoryItemSlot(weapon, false, amount);
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

        _rangedWeapon = new InventoryItemSlot(weapon, false, amount);
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

        _ammunition = new InventoryItemSlot(weapon, false, amount);
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
            _humanoidController.CreateRangedWeapon(null);
        }
        if (slot == _ammunition)
        {
            _humanoidController.CreateAmmunition(null);
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

    private class EquipmentState
    {
        public SlotState Primary { get; set; }
        public SlotState Additional { get; set; }
        public SlotState Ranged { get; set; }
        public SlotState Ammunition { get; set; }
    }

    private class SlotState
    {
        public string ItemName { get; set; }
        public int Amount { get; set; }
    }

    public object CaptureState()
    {
        return new EquipmentState
        {
            Primary = ToSlotState(_primaryWeapon),
            Additional = ToSlotState(_additionalWeapon),
            Ranged = ToSlotState(_rangedWeapon),
            Ammunition = ToSlotState(_ammunition)
        };
    }

    private static SlotState ToSlotState(InventoryItemSlot slot)
    {
        return slot == null ? null : new SlotState { ItemName = slot.ItemData.name, Amount = slot.Amount };
    }

    public void RestoreState(object state)
    {
        var s = (EquipmentState)state;

        DestroySlot(ref _primaryWeapon);
        DestroySlot(ref _additionalWeapon);
        DestroySlot(ref _rangedWeapon);
        DestroySlot(ref _ammunition);

        RestoreSlot(s.Primary, ref _primaryWeapon, _humanoidController.CreatePrimaryWeapon, EquipmentType.Primary);
        RestoreSlot(s.Additional, ref _additionalWeapon, _humanoidController.CreateAdditionalWeapon, EquipmentType.Additional);
        RestoreSlot(s.Ranged, ref _rangedWeapon, _humanoidController.CreateRangedWeapon, EquipmentType.Ranged);
        RestoreSlot(s.Ammunition, ref _ammunition, _humanoidController.CreateAmmunition, EquipmentType.Ammunition);
    }

    private void RestoreSlot(SlotState slotState, ref InventoryItemSlot slot, Action<WeaponData> createFn, EquipmentType type)
    {
        if (slotState == null || string.IsNullOrEmpty(slotState.ItemName))
        {
            return;
        }

        var itemData = _itemDatabase.GetByName(slotState.ItemName);
        if (itemData is not WeaponData weapon)
        {
            Debug.LogWarning($"Item '{slotState.ItemName}' not found or not a WeaponData, skipping equip slot.");
            return;
        }

        slot = new InventoryItemSlot(weapon, false, slotState.Amount);
        createFn(weapon);
        OnEquip?.Invoke(weapon, type);
    }
}