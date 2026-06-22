using Game;
using UnityEngine;

public class WeaponIndex : StateMachineBehaviour
{
    [SerializeField] private Mode setMode;
    [SerializeField] private bool onStart;
    
    private PlayerController _player;
    readonly int m_HashWeaponIndex = Animator.StringToHash("WeaponIndex");
    
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
        
        if (setMode == Mode.Zero)
        {
            animator.SetFloat(m_HashWeaponIndex, 0);
            return;
        }
        
        animator.SetFloat(m_HashWeaponIndex, _player.primaryWeaponData ? _player.primaryWeaponData.AnimationSetIndex : 0);
    }
}

public enum Mode
{
    Zero,
    WeaponIndex
}
