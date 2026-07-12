using Cysharp.Threading.Tasks;
using UnityEngine;

public class EnemyBrain : MonoBehaviour, IInput
{
    [SerializeField] private AnimationCurve animationCurve;
    [SerializeField] private float rotationSpeed;
    [SerializeField] private Transform[] patrolPoints;
    
    public AsyncStateMachine Fsm { get; private set; }
    
    public bool InputBlocked { get; set; }
    public Vector2 MoveInput { get; set; }
    public bool JumpInput { get; set; }
    public float RotationYaw { get; set; }
    public bool Attack1 { get; set; }
    public bool Attack2 { get; set; }
    
    public bool HaveControl() => !InputBlocked;
    public void ReleaseControl() => InputBlocked = true;
    public void GainControl() => InputBlocked = false;

    private async void Start()
    {
        await InitializeFsm();
        await Fsm.TransitionTo(Fsm.PatrolState);
        Fsm.Ctx.Activate(true);
    }
    
    private UniTask InitializeFsm()
    {
        Fsm = new AsyncStateMachine(new StateMachineContext(this, transform, 
            animationCurve, rotationSpeed, patrolPoints));
        return UniTask.CompletedTask;
    }
}
