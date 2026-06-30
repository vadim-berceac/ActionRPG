using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class EquipmentFiller : MonoBehaviour
{
   [SerializeField] private Image primary;
   [SerializeField] private Image additional;
   [SerializeField] private Image ranged;
   
   private Equipment _equipment;

   [Inject]
   private void Construct(PlayerTag playerTag)
   {
      _equipment = playerTag.PlayerEquipment;
   }

   private void Awake()
   {
      if (_equipment.Primary)
      {
         primary.sprite = _equipment.Primary.Icon;
         primary.gameObject.SetActive(true);
      }

      if (_equipment.Additional)
      {
         additional.sprite = _equipment.Additional.Icon;
         additional.gameObject.SetActive(true);
      }

      if (_equipment.Ranged)
      {
         ranged.sprite = _equipment.Ranged.Icon;
         ranged.gameObject.SetActive(true);
      }
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
            if (itemData)
            {
               primary.sprite = itemData.Icon;
               primary.gameObject.SetActive(true);
            }
            else
            {
               ClearPrimary();
            }
            
            break;
         
         case Equipment.EquipmentType.Additional:
            if (itemData)
            {
               additional.sprite = itemData.Icon;
               additional.gameObject.SetActive(true);
            }
            else
            {
               ClearAdditional();
            }
            
            break;
         
         case Equipment.EquipmentType.Ranged:
            if (itemData)
            {
               ranged.sprite = itemData.Icon;
               ranged.gameObject.SetActive(true);
            }
            else
            {
               ClearRanged();
            }
           
            break;
      }
   }

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
}
