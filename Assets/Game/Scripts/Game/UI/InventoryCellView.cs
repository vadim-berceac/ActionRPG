using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryCellView : MonoBehaviour
{
   [SerializeField] private Button button;
   [SerializeField] private TextMeshProUGUI amountText;
   [SerializeField] private Image image;
   
   private InventoryItemSlot _slot;

   public static event Action<string> ObDescriptionChanged;

   public void Initialize(InventoryItemSlot slot)
   {
      _slot = slot;
      amountText.text = _slot.Amount.ToString();
      image.sprite = _slot.ItemData.Icon == null ? image.sprite : _slot.ItemData.Icon;
   }

   public void OnSelect()
   {
      ObDescriptionChanged?.Invoke(_slot.ItemData.Description);
   }
}
