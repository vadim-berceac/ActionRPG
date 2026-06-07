using Game;
using UnityEngine;
using Zenject;

public class PickupItem : MonoBehaviour
{
   [SerializeField] private GameObject root;
   [SerializeField] private InteractOnTrigger trigger;
   [SerializeField] private WeaponData data;
   
   private PlayerNewInput _playerInput;
   private PlayerController _playerController;
   private bool _canPickup;

   [Inject]
   private void Construct(PlayerNewInput playerInput, PlayerController playerController)
   {
      _playerInput = playerInput;
      _playerController = playerController;
      
      trigger.OnEnter.AddListener(OnInteractZoneOn);
      trigger.OnExit.AddListener(OnInteractZoneLeave);
      
      _playerInput.Interact += OnInteract;
   }
   
   private void OnDestroy()
   {
      trigger.OnEnter.RemoveListener(OnInteractZoneOn);
      trigger.OnExit.RemoveListener(OnInteractZoneLeave);

      if (_playerInput != null)
      {
         _playerInput.Interact -= OnInteract;
      }
   }

   private void OnInteractZoneOn()
   {
      _canPickup = true;
   }
   
   private void OnInteractZoneLeave()
   {
      _canPickup = false;
   }

   private void OnInteract()
   {
      if (!_canPickup)
      {
         return;
      }
      
      _playerController.CreateWeapon(data, true);
      Destroy(root);
   }
}
