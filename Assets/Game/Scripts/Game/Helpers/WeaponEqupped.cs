using Game;
using UnityEngine;

public class WeaponEqupped : StateMachineBehaviour
{
   [SerializeField] private bool value;
   [SerializeField] private bool onStart;
   
   private HumanoidController _humanoid;

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
      if(_humanoid == null) _humanoid = animator.gameObject.GetComponent<HumanoidController>();
      
      if(_humanoid == null) return;
      
      _humanoid?.SetIsWeaponEquipped(value);
   }
}
