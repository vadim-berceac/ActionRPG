using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class Inventory : MonoBehaviour, ISaveable
{
    public class InventoryState
    {
        public List<InventorySlotState> Slots { get; set; }
    }

    public class InventorySlotState
    {
        public string ItemName { get; set; }
        public int Amount { get; set; }
        public bool EquippedOnStart { get; set; }
    }
    
    [field: SerializeField] public List<InventoryItemSlot> InventoryItemSlots { get; private set; }

    public event Action<ItemData, int> OnTransfer;
    public event Action<InventoryItemSlot> OnSlotCreated;

    private DiContainer _container;
    private IItemDatabase _itemDatabase;

    public string SaveKey => "inventory";

    [Inject]
    private void Construct(DiContainer container, IItemDatabase itemDatabase)
    {
        _container = container;
        _itemDatabase = itemDatabase;
    }

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        foreach (var itemSlot in InventoryItemSlots.ToArray())
        {
            if (!itemSlot.EquippedOnStart || !itemSlot.ItemData.CanBeEquipped || itemSlot.Amount <= 0)
            {
                continue;
            }

            TryTransfer(itemSlot.ItemData, 1);
        }
    }
    
    public object CaptureState()
    {
        var state = new InventoryState { Slots = new List<InventorySlotState>(InventoryItemSlots.Count) };

        foreach (var slot in InventoryItemSlots)
        {
            state.Slots.Add(new InventorySlotState { ItemName = slot.ItemData.name, Amount = slot.Amount, EquippedOnStart = slot.EquippedOnStart });
        }

        return state;
    }

    public void RestoreState(object state)
    {
        var s = (InventoryState)state;

        foreach (var slot in InventoryItemSlots)
        {
            slot.Dispose();
        }
        InventoryItemSlots.Clear();

        foreach (var slotState in s.Slots)
        {
            var itemData = _itemDatabase.GetByName(slotState.ItemName);
            if (itemData == null)
            {
                Debug.LogWarning($"Item with id '{slotState.ItemName}' not found in database, skipping.");
                continue;
            }

            var slot = new InventoryItemSlot(itemData, slotState.EquippedOnStart, slotState.Amount);
            InventoryItemSlots.Add(slot);
            OnSlotCreated?.Invoke(slot);
        }
    }
    
    public WeaponData GetWeaponData(WeaponData.WearType type)
    {
        foreach (var itemSlot in InventoryItemSlots)
        {
            if (!itemSlot.EquippedOnStart || !itemSlot.ItemData.CanBeEquipped || itemSlot.Amount <= 0)
            {
                continue;
            }
            
            var item = (WeaponData) itemSlot.ItemData;

            if (item.Wear == type)
            {
                return item;
            }
        }
        return null;
    }

    public void Add(ItemData itemData, int amount = 1)
    {
        var slotFind = false;
        
        foreach (var itemSlot in InventoryItemSlots)
        {
            if (itemSlot.ItemData != itemData)
            {
                continue;
            }
            slotFind = true;
            itemSlot.SetAmount(itemSlot.Amount + amount);
        }

        if (!slotFind)
        {
            var newCell = new InventoryItemSlot(itemData, false, amount);
            InventoryItemSlots.Add(newCell);
            OnSlotCreated?.Invoke(newCell);
        }
    }

    public bool TryRemove(ItemData itemData, int amount = 1)
    {
        foreach (var itemSlot in InventoryItemSlots)
        {
            if (itemSlot.ItemData != itemData)
            {
                continue;
            }

            if (itemSlot.Amount < amount)
            {
                return false;
            }
            
            itemSlot.SetAmount(itemSlot.Amount - amount);

            if (itemSlot.Amount <= 0)
            {
                InventoryItemSlots.Remove(itemSlot);
                itemSlot.Dispose();
            }
            
            return true;
        }
        
        return false;
    }

    public bool TryDrop(ItemData itemData, int amount = 1)
    {
        if (TryRemove(itemData, amount))
        {
            itemData.GetGroundInstance(transform, _container);
            return true;
        }
        return false;
    }

    public bool TryTransfer(ItemData itemData, int amount = 1)
    {
        if (TryRemove(itemData, amount))
        {
            OnTransfer?.Invoke(itemData, amount);
            return true;
        }
        return false;
    }
}

[System.Serializable]
public class InventoryItemSlot : IDisposable
{
    [field: SerializeField] public ItemData ItemData { get; private set; }
    [field: SerializeField] public int Amount { get; private set; }
    [field: SerializeField] public bool EquippedOnStart { get; private set; }
    
    public event Action OnDestroy;
    public event Action<int> OnAmountChanged;

    public InventoryItemSlot(ItemData itemData, bool equippedOnStart, int amount = 1)
    {
        ItemData = itemData;
        Amount = amount;
        EquippedOnStart = equippedOnStart;
    }

    public void SetAmount(int amount)
    {
        Amount = amount;
        OnAmountChanged?.Invoke(Amount);
    }

    public void Dispose()
    {
        OnDestroy?.Invoke();
    }
}
