using Game;
using UnityEngine;

public class WeaponIndex : StateMachineBehaviour
{
    [SerializeField] private Mode setMode;
    [SerializeField] private bool onStart;
    
    private HumanoidController _humanoid;
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
        if(_humanoid == null) _humanoid = animator.gameObject.GetComponent<HumanoidController>();
        
        if(_humanoid == null) return;
        
        if (setMode == Mode.Zero)
        {
            animator.SetFloat(m_HashWeaponIndex, 0);
            return;
        }
        
        animator.SetFloat(m_HashWeaponIndex, _humanoid.primaryWeaponData ? _humanoid.primaryWeaponData.AnimationSetIndex : 0);
    }
}

public enum Mode
{
    Zero,
    WeaponIndex
}
