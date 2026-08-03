using UnityEngine;

public class BowViewAnimationController : StateMachineBehaviour
{
    [SerializeField] private ActionConditions condition;
    [SerializeField] private BowActions bowAction;
    
    private BowView _bowView;
    
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (condition != ActionConditions.OnEnter)
        {
            return;
        }
        
        DoAction(animator);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (condition != ActionConditions.OnExit)
        {
            return;
        }
        
        DoAction(animator);
    }

    private void DoAction(Animator animator)
    {
        if(!_bowView)
        {
            _bowView = animator.GetComponentInChildren<BowView>();
        }
                
        if(bowAction == BowActions.Load)
        {
            _bowView?.LoadBow();
        }
        else
        {
            _bowView?.ReleaseBow();
        }
    }
}

public enum ActionConditions
{
    OnEnter,
    OnExit
}

public enum BowActions
{
    Load,
    Release,
    Cancel
}
