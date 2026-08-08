using Cysharp.Threading.Tasks;
using Game;
using UnityEngine;
using UnityEngine.Events;
using Zenject;

public class InteractAnimation : MonoBehaviour
{
   [SerializeField] private InteractOnTrigger trigger;
   [SerializeField] private AnimationClip enterClip;
   [SerializeField] private AnimationClip[] clips;
   [SerializeField] private AnimationClip exitClip;
   [SerializeField] private bool canBeInterrupted;
   public UnityEvent<HumanoidController> onInteractEnter, onInteractExit;

   private ICharacterInput _input;
   private Collider _currentCollider;
   private HumanoidController _currentController;
   private bool _isPlaying;
   private bool _interruptRequested;

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
      if (_isPlaying)
      {
         return;
      }

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

      if ((clips == null || clips.Length == 0) && !enterClip && !exitClip)
      {
         return;
      }

      _currentController.SetInteracting(true);
      _isPlaying = true;
      _interruptRequested = false;

      if (canBeInterrupted)
      {
         _input.Interact += Interrupt;
      }

      PlaySequence().Forget();
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

      if (canBeInterrupted)
      {
         _input.Interact -= Interrupt;
      }
   }

   private void Interrupt()
   {
      if (!_isPlaying)
      {
         return;
      }

      _interruptRequested = true;
   }

   private async UniTask PlaySequence()
   {
      if (enterClip)
      {
         await PlayClip(enterClip);
      }

      if (clips != null)
      {
         foreach (var clip in clips)
         {
            if (!clip)
            {
               continue;
            }

            if (clip.isLooping)
            {
               await PlayLoopedClip(clip);
            }
            else
            {
               await PlayClip(clip);
            }

            if (_interruptRequested)
            {
               break;
            }
         }
      }

      if (exitClip)
      {
         await PlayClip(exitClip);
      }

      _isPlaying = false;
      _interruptRequested = false;
      OnInteractExit();
   }

   private async UniTask PlayClip(AnimationClip clip)
   {
      if (!clip)
      {
         return;
      }

      _currentController.PlayInteractClip(clip);
      await UniTask.WaitForSeconds(clip.length);
   }

   private async UniTask PlayLoopedClip(AnimationClip clip)
   {
      _currentController.PlayInteractClip(clip);

      do
      {
         await UniTask.WaitForSeconds(clip.length);
      }
      while (!_interruptRequested);
   }
}