using UnityEngine;

namespace Game
{
    public class WeaponEffect : StateMachineBehaviour
    {
        public int effectIndex;
        
        private PlayerController _player;

        // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
        override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (_player == null)
            {
                _player = animator.GetComponent<PlayerController>();
            }

            //_player.m_MeleeWeapon.effects[effectIndex].Activate();
        }

    } 
}
