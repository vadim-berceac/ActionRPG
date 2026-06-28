using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    [field: SerializeField] public List<InventoryItemSlot> InventoryItemSlots { get; private set; }

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
            InventoryItemSlots.Add(new InventoryItemSlot(itemData, amount));
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
            }
            
            return true;
        }
        
        return false;
    }
}

[System.Serializable]
public class InventoryItemSlot
{
    [field: SerializeField] public ItemData ItemData { get; private set; }
    [field: SerializeField] public int Amount { get; private set; }
    [field: SerializeField] public bool EquippedOnStart { get; private set; }

    public InventoryItemSlot(ItemData itemData, int amount = 1)
    {
        ItemData = itemData;
        Amount = amount;
        EquippedOnStart = false;
    }

    public void SetAmount(int amount)
    {
        Amount = amount;
    }
}
