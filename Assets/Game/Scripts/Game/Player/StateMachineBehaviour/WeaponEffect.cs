using UnityEngine;

namespace Game
{
    public class WeaponEffect : StateMachineBehaviour
    {
        public int effectIndex;
        
        private HumanoidController _humanoid;

        // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
        override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (_humanoid == null)
            {
                _humanoid = animator.GetComponent<HumanoidController>();
            }

            //_humanoid.PrimaryWeaponInstance.effects[effectIndex].Activate();
        }

    } 
}
