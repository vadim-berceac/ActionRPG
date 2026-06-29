using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class InventoryFiller : MonoBehaviour
{
    [field: SerializeField] public GameObject InventoryCellPrefab { get; set; }
    [field: SerializeField] public Transform InventoryCellsParent { get; set; }
    
    private readonly List<InventoryCellView> _inventoryCells = new ();
    
    private Inventory _inventory;

    [Inject]
    private void Construct(PlayerTag playerTag)
    {
        _inventory = playerTag.PlayerInventory;
    }

    private void OnEnable()
    {
       ClearCells();
       FillCells();
    }

    private void ClearCells()
    {
        foreach (var item in _inventoryCells)
        {
            Destroy(item.gameObject);
        }
        _inventoryCells.Clear();
    }

    private void FillCells()
    {
        foreach (var item in _inventory.InventoryItemSlots)
        {
            var instance = Instantiate(InventoryCellPrefab, InventoryCellsParent).GetComponent<InventoryCellView>();
            _inventoryCells.Add(instance);
            instance.Initialize(item);
        }
    }
}
