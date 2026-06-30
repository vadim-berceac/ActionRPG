using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

public class InventoryFiller : MonoBehaviour
{
    [field: SerializeField] public GameObject InventoryCellPrefab { get; set; }
    [field: SerializeField] public Transform InventoryCellsParent { get; set; }
    [field: SerializeField] public AudioSource Audio { get; set; }
    [field: SerializeField] public AudioClip ClickClip { get; set; }
    
    private readonly List<InventoryCellView> _inventoryCells = new ();
    
    private Inventory _inventory;

    [Inject]
    private void Construct(PlayerTag playerTag)
    {
        _inventory = playerTag.PlayerInventory;
    }

    public void PlayClickSound()
    {
        Audio.PlayOneShot(ClickClip);
    }
    
    public async UniTask RemoveCell(InventoryCellView cell)
    {
        if (cell == null || !_inventoryCells.Contains(cell))
        {
            return;
        }
        
        _inventoryCells.Remove(cell);
        
        await UniTask.Yield(PlayerLoopTiming.Update);
    }

    private async void OnEnable()
    {
        await ClearCells();
        await FillCells();

        _inventory.OnSlotCreated += OnCellCreated;
    }

    private void OnDisable()
    {
        _inventory.OnSlotCreated -= OnCellCreated;
    }

    private async void OnCellCreated(InventoryItemSlot cell)
    {
        await ClearCells();
        await FillCells();
    }

    private async UniTask ClearCells()
    {
        foreach (var item in _inventoryCells)
        {
            Destroy(item.gameObject);
        }
        _inventoryCells.Clear();
        
        await UniTask.Yield(PlayerLoopTiming.Update);
    }

    private async UniTask FillCells()
    {
        foreach (var item in _inventory.InventoryItemSlots)
        {
            var instance = Instantiate(InventoryCellPrefab, InventoryCellsParent).GetComponent<InventoryCellView>();
            if (instance == null)
            {
                continue;
            }
            _inventoryCells.Add(instance);
            instance.Initialize(_inventory, this, item);
        }
        
        await UniTask.Yield(PlayerLoopTiming.Update);
    }
}
