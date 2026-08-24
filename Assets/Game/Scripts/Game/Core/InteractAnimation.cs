using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game;
using UnityEngine;
using UnityEngine.Events;
using Zenject;

public class InteractAnimation : MonoBehaviour
{
   [SerializeField] private InteractOnTrigger trigger;
   [Tooltip("Выбор персонажа напрямую")]
   [SerializeField] private HumanoidController controller;
   [SerializeField] private AnimationClipSettings enterClip;
   [SerializeField] private AnimationClipSettings[] clips;
   [SerializeField] private AnimationClipSettings exitClip;
   [SerializeField] private bool canBeInterrupted;
   public UnityEvent<HumanoidController> onInteractEnter, onInteractExit;
   public UnityEvent<AnimationClip, float> onClipStarted;

   private ICharacterInput _input;
   private Collider _currentCollider;
   private HumanoidController _currentController;
   private bool _isPlaying;
   private bool _interruptRequested;
   
   public HumanoidController CurrentController => _currentController;

   [Inject]
   private void Construct(PlayerNewInput playerInput)
   {
      _input = playerInput;
   }

   private void OnEnable()
   {
      if (trigger)
      {
         trigger.OnEnter.AddListener(OnEnter);
         trigger.OnExit.AddListener(OnExit);
      }

      if (controller)
      {
         _currentController = controller;
      }
      
      _input.Interact += OnInteractEnter;
   }

   private void OnDisable()
   {
      if (trigger)
      {
         trigger.OnEnter.RemoveListener(OnEnter);
         trigger.OnExit.RemoveListener(OnExit);
      }
      
      if (controller)
      {
         _currentController = null;
      }

      if (_input != null)
      {
         _input.Interact -= OnInteractEnter;
      }
   }

   private void OnEnter(Collider other)
   {
      if (!trigger)
      {
         return;
      }
      _currentCollider = other;
   }

   private void OnExit(Collider other)
   {
      if (!trigger)
      {
         return;
      }
      
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

      HumanoidController targetController = null;

      if (controller)
      {
         if (_currentCollider &&
             _currentCollider.gameObject.TryGetComponent(out HumanoidController other) &&
             other != controller)
         {
            targetController = controller;
         }
      }
      else if (_currentCollider)
      {
         if (!_currentCollider.gameObject.TryGetComponent(out targetController))
            return;
      }

      if (!targetController)
      {
         return;
      }

      _currentController = targetController;

      onInteractEnter?.Invoke(_currentController);

      if ((clips == null || clips.Length == 0) && !enterClip.Clip && !exitClip.Clip)
         return;

      _currentController.SetInteracting(true);
      _isPlaying = true;
      _interruptRequested = false;

      if (canBeInterrupted)
         _input.Interact += Interrupt;

      PlaySequence().Forget();
   }

   private void OnInteractExit()
   {
      if (!_currentController)
      {
         return;
      }
      
      onInteractExit?.Invoke(_currentController);

      _currentController.SetInteracting(false);
      var exitBlend = exitClip.Clip ? exitClip.EnterBlendLength : 0.2f;
      _currentController.StopInteractClip(exitBlend);
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
      var mainSequence = BuildMainSequence();
      var hasExitClip = exitClip.Clip;
      AnimationClipSettings? lastPlayed = null;

      for (var i = 0; i < mainSequence.Count; i++)
      {
         var current = mainSequence[i];

         var next = (i + 1 < mainSequence.Count)
            ? mainSequence[i + 1]
            : (hasExitClip ? exitClip : (AnimationClipSettings?)null);

         var exitOverlap = GetBlendOverlap(current, next);

         if (current.Clip.isLooping)
         {
            await PlayLoopedClip(current, exitOverlap);
         }
         else
         {
            await PlayBlendedClip(current, exitOverlap);
         }

         lastPlayed = current;

         if (_interruptRequested)
         {
            break;
         }
      }

      if (hasExitClip)
      {
         await PlayBlendedClip(exitClip, 0f);
      }

      _isPlaying = false;
      _interruptRequested = false;
      OnInteractExit();
   }

   private List<AnimationClipSettings> BuildMainSequence()
   {
      var sequence = new List<AnimationClipSettings>();

      if (enterClip.Clip)
      {
         sequence.Add(enterClip);
      }

      if (clips != null)
      {
         foreach (var clip in clips)
         {
            if (clip.Clip)
            {
               sequence.Add(clip);
            }
         }
      }

      return sequence;
   }

   private static float GetBlendOverlap(AnimationClipSettings current, AnimationClipSettings? next)
   {
      if (!next.HasValue)
      {
         return 0f;
      }

      var overlap = Mathf.Min(current.ExitBlendLength, next.Value.EnterBlendLength);

      return Mathf.Clamp(overlap, 0f, current.Clip.length);
   }

   private async UniTask PlayBlendedClip(AnimationClipSettings settings, float exitOverlap)
   {
      onClipStarted?.Invoke(settings.Clip, settings.EnterBlendLength);

      _currentController.PlayInteractClip(settings.Clip, settings.EnterBlendLength, settings.Mask, settings.IsAdditive);

      var waitTime = settings.Clip.length - exitOverlap;

      if (waitTime > 0f)
      {
         await UniTask.WaitForSeconds(waitTime);
      }
   }

   private async UniTask PlayLoopedClip(AnimationClipSettings settings, float exitOverlap)
   {
      onClipStarted?.Invoke(settings.Clip, settings.EnterBlendLength);

      _currentController.PlayInteractClip(settings.Clip, settings.EnterBlendLength, settings.Mask, settings.IsAdditive);

      var mainWait = Mathf.Max(settings.Clip.length - exitOverlap, 0f);

      do
      {
         if (mainWait > 0f)
         {
            await UniTask.WaitForSeconds(mainWait);
         }

         if (_interruptRequested)
         {
            break;
         }

         if (exitOverlap > 0f)
         {
            await UniTask.WaitForSeconds(exitOverlap);
         }
      }
      while (!_interruptRequested);
   }
}

[System.Serializable]
public struct AnimationClipSettings
{
   [field: SerializeField] public AnimationClip Clip { get; private set; }
   [field: SerializeField, Range(0, 1)] public float EnterBlendLength { get; private set; }
   [field: SerializeField, Range(0, 1)] public float ExitBlendLength { get; private set; }
   [field: SerializeField] public AvatarMask Mask { get; private set; }
   [field: SerializeField] public bool IsAdditive { get; private set; }
}