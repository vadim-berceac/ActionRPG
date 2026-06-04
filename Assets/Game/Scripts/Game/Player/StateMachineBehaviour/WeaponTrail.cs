using Game;
using UnityEngine;

public class WeaponTrail : StateMachineBehaviour
{
    private PlayerController _player;
    
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (_player == null)
        {
            _player = animator.GetComponent<PlayerController>();
        }

        //_player.m_MeleeWeapon.effects[effectIndex].Activate();
    }
}
