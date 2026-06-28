using Game;
using UnityEngine;

public class WeaponTrail : StateMachineBehaviour
{
    private HumanoidController _humanoid;
    
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (_humanoid == null)
        {
            _humanoid = animator.GetComponent<HumanoidController>();
        }

        //_humanoid.PrimaryWeaponInstance.effects[effectIndex].Activate();
    }
}
