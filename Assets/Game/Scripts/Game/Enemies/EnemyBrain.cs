using Cysharp.Threading.Tasks;
using Game;
using UnityEngine;

public enum EnemyBehaviorMode
{
    Aggressive,
    Neutral
}

public class EnemyBrain : MonoBehaviour, IInput
{
    [SerializeField] private VisionSystem visionSystem;
    [SerializeField] private HumanoidController humanoidController;
    [SerializeField] private Damageable damageable;
    [SerializeField] private float rotationSpeed;
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private EnemyBehaviorMode behaviorMode = EnemyBehaviorMode.Aggressive;

    public AsyncStateMachine Fsm { get; private set; }
    public EnemyBehaviorMode BehaviorMode => behaviorMode;
    
    public bool InputBlocked { get; set; }
    public Vector2 MoveInput { get; set; }
    public bool JumpInput { get; set; }
    public float RotationYaw { get; set; }
    public bool Attack1 { get; set; }
    public bool Attack2 { get; set; }
    public bool Block { get; set; }
    
    public bool HaveControl() => !InputBlocked;
    public void ReleaseControl() => InputBlocked = true;
    public void GainControl() => InputBlocked = false;

    private async void Start()
    {
        await InitializeFsm();
        Fsm.Ctx.SetFsm(Fsm);
        await Fsm.TransitionTo(Fsm.PatrolState);
    }

    private void OnDisable()
    {
        Fsm?.Dispose();
        Fsm = null;
    }
    
    private UniTask InitializeFsm()
    {
        Fsm = new AsyncStateMachine(new StateMachineContext(this, visionSystem, damageable,
            transform, humanoidController, rotationSpeed, patrolPoints, behaviorMode));
        return UniTask.CompletedTask;
    }
}
