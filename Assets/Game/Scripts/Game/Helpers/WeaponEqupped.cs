using Game;
using UnityEngine;

public class WeaponEqupped : StateMachineBehaviour
{
   [SerializeField] private bool value;
   [SerializeField] private bool onStart;
   
   private PlayerController _player;

   public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
   {
      if(!onStart) return;
      
      SetValue(animator);
   }
   
   public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
   {
      if(onStart) return;
      
      SetValue(animator);
   }

   private void SetValue(Animator animator)
   {
      if(_player == null) _player = animator.gameObject.GetComponent<PlayerController>();
      
      if(_player == null) return;
      
      _player?.SetIsWeaponEquipped(value);
   }
}
