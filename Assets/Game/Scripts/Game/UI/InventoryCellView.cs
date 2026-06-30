using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryCellView : MonoBehaviour
{
   [SerializeField] private Button button;
   [SerializeField] private TextMeshProUGUI amountText;
   [SerializeField] private Image image;
   
   private InventoryItemSlot _slot;
   private InventoryFiller _filler;
   private Inventory _inventory;

   public static event Action<string> ObDescriptionChanged;

   public void Initialize(Inventory inventory, InventoryFiller filler,  InventoryItemSlot slot)
   {
      _inventory = inventory;
      _filler = filler;
      _slot = slot;
      amountText.text = _slot.Amount.ToString();
      image.sprite = _slot.ItemData.Icon == null ? image.sprite : _slot.ItemData.Icon;
      
      _slot.OnDestroy += OnSlotDestroyed;
      _slot.OnAmountChanged += OnAmountChanged;
   }

   private void OnDestroy()
   {
      _slot.OnDestroy -= OnSlotDestroyed;
      _slot.OnAmountChanged -= OnAmountChanged;
   }

   private async void OnSlotDestroyed()
   {
      await _filler.RemoveCell(this);
      Destroy(gameObject);
   }

   private void OnAmountChanged(int newAmount)
   {
      amountText.text = newAmount.ToString();
   }

   public void OnSelect()
   {
      ObDescriptionChanged?.Invoke(_slot.ItemData.Description);
   }
   
   public void OnAnyPointerClick(BaseEventData eventData)
   {
      var pointer = eventData as PointerEventData;

      switch (pointer.button)
      {
         case PointerEventData.InputButton.Left:
            OnLeftClick();
            break;

         case PointerEventData.InputButton.Right:
            OnRightClick();
            break;
      }
   }

   private void OnRightClick()
   {
      _inventory.TryDrop(_slot.ItemData, 1);
   }

   private void OnLeftClick()
   {
      _inventory.TryTransfer(_slot.ItemData, 1);
   }
}