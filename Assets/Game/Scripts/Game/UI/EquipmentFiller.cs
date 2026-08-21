using System;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class EquipmentFiller : MonoBehaviour
{
   [SerializeField] private Image primary;
   [SerializeField] private Image additional;
   [SerializeField] private Image ranged;
   [SerializeField] private Image ammunition;
   [SerializeField] private Image armor;
   [SerializeField] private Image helmet;
   
   private Equipment _equipment;

   [Inject]
   private void Construct(PlayerTag playerTag)
   {
      _equipment = playerTag.PlayerEquipment;
   }

   private void Awake()
   {
      UpdateIcon(ref primary, _equipment.Primary, ClearPrimary);
      UpdateIcon(ref additional, _equipment.Additional, ClearAdditional);
      UpdateIcon(ref ranged, _equipment.Ranged, ClearRanged);
      UpdateIcon(ref ammunition, _equipment.Ammunition, ClearAmmunition);
   }

   private void OnEnable()
   {
      _equipment.OnEquip += FillEquipment;
   }

   private void OnDisable()
   {
      _equipment.OnEquip -= FillEquipment;
   }

   private void FillEquipment(ItemData itemData, Equipment.EquipmentType equipmentType)
   {
      switch (equipmentType)
      {
         case Equipment.EquipmentType.Primary:
            UpdateIcon(ref primary, itemData, ClearPrimary);
            break;
         
         case Equipment.EquipmentType.Additional:
            UpdateIcon(ref additional, itemData, ClearAdditional);
            break;
         
         case Equipment.EquipmentType.Ranged:
            UpdateIcon(ref ranged, itemData, ClearRanged);
            break;
         
         case Equipment.EquipmentType.Ammunition:
            UpdateIcon(ref ammunition, itemData, ClearAmmunition);
            break;
      }
   }

   private static void UpdateIcon(ref Image icon, ItemData itemData, Action clearAction)
   {
      if (itemData)
      {
         icon.sprite = itemData.Icon;
         icon.gameObject.SetActive(true);
      }
      else
      {
         clearAction.Invoke();
      }
   }
   
   //buttons

   public void ClearPrimary()
   {
      _equipment.DestroyPrimary();
      primary.gameObject.SetActive(false);
   }

   public void ClearAdditional()
   {
      _equipment.DestroyAdditional();
      additional.gameObject.SetActive(false);
   }

   public void ClearRanged()
   {
      _equipment.DestroyRanged();
      ranged.gameObject.SetActive(false);
   }
   
   public void ClearAmmunition()
   {
      _equipment.DestroyAmmunition();
      ammunition.gameObject.SetActive(false);
   }
   
   public void ClearArmor()
   {
      //_equipment.DestroyRanged();
      //armor.gameObject.SetActive(false);
   }
   
   public void ClearHelmet()
   {
      //_equipment.DestroyRanged();
      //helmet.gameObject.SetActive(false);
   }
}
