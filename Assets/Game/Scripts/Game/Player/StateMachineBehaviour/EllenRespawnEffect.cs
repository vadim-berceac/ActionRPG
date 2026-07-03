using UnityEngine;

namespace Game
{
    public class EllenRespawnEffect : StateMachineBehaviour
    {
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            var controller = animator.GetComponent<HumanoidController>();

            if (controller.IsPlayer)
            {
                controller.Respawn();
            }
        }
    } 
}