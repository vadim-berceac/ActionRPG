using Cysharp.Threading.Tasks;
using Game;
using UnityEngine;
using UnityEngine.Events;
using Zenject;

public class InteractAnimation : MonoBehaviour
{
   [SerializeField] private InteractOnTrigger  trigger;
   [SerializeField] private AnimationClip test;
   public UnityEvent<HumanoidController> onInteractEnter, onInteractExit;
   
   private ICharacterInput _input;
   private Collider _currentCollider;
   private HumanoidController _currentController;

   [Inject]
   private void Construct(PlayerNewInput playerInput)
   {
      _input = playerInput;
      
      trigger.OnEnter.AddListener(OnEnter);
      trigger.OnExit.AddListener(OnExit);
      _input.Interact += OnInteractEnter;
   }

   private void OnDestroy()
   {
      if (trigger)
      {
         trigger.OnEnter.RemoveListener(OnEnter);
         trigger.OnExit.RemoveListener(OnExit);
      }

      if (_input != null)
      {
         _input.Interact -= OnInteractEnter;
      }
   }

   private void OnEnter(Collider other)
   {
      _currentCollider = other;
   }

   private void OnExit(Collider other)
   {
      if (_currentCollider == other)
      {
         _currentCollider = null;
      }
   }
   
   private void OnInteractEnter()
   {
      if (!_currentCollider)
      {
         return;
      }
      
      _currentCollider.gameObject.TryGetComponent(out _currentController);
      
      if (!_currentController)
      {
         return;
      }
      
      Debug.Log($"Interacting with {_currentController.name}");
      onInteractEnter?.Invoke(_currentController);

      if (!test)
      {
         return;
      }
      
      _currentController.SetInteracting(true);
      _currentController.PlayInteractClip(test);
      Timer(test.length).Forget();
   }

   private void OnInteractExit()
   {
      if (!_currentController)
      {
         return;
      }
      
      Debug.Log($"Interacting with {_currentController.name} end!");
      onInteractExit?.Invoke(_currentController);
      _currentController.StopInteractClip();
      _currentController.SetInteracting(false);
      _currentController = null;
   }

   private async UniTask Timer(float t)
   {
      await UniTask.WaitForSeconds(t);
      OnInteractExit();
   }
}