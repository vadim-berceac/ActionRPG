using System;
using Cysharp.Threading.Tasks;
using Game;
using UnityEngine;

public enum EnemyBehaviorMode
{
    Aggressive,
    Neutral
}

public enum AIMode
{
    Patrol,
    Guard
}

public class EnemyBrain : MonoBehaviour, IInput, ISaveable
{
    public class EnemyBrainState
    {
        public string StateName { get; set; }
    }
    
    public string SaveKey => "EnemyBrain";
    
    public object CaptureState()
    {
        return new EnemyBrainState { StateName = Fsm?.CurrentState?.GetType().Name };
    }
    
    public void RestoreState(object state)
    {
        var s = (EnemyBrainState)state;
        if (Fsm == null || string.IsNullOrEmpty(s.StateName)) return;

        IAsyncState target = s.StateName switch
        {
            nameof(PatrolState) => Fsm.PatrolState,
            nameof(GuardState) => Fsm.GuardState,
            nameof(ChaseState) => Fsm.ChaseState,
            nameof(AttackState) => Fsm.AttackState,
            nameof(ShootState) => Fsm.ShootState,
            nameof(BlockState) => Fsm.BlockState,
            nameof(DeathState) => Fsm.DeathState,
            nameof(IdleWaitState) => Fsm.IdleWaitState,
            nameof(AlarmState) => Fsm.AlarmState,
            _ => null
        };

        if (target != null && Fsm.CurrentState != target)
        {
            Fsm.TransitionTo(target).Forget();
        }
    }
    
    [SerializeField] private VisionSystem visionSystem;
    [SerializeField] private HumanoidController humanoidController;
    [SerializeField] private Damageable damageable;
    [SerializeField] private float rotationSpeed;
    [SerializeField] private Factions faction;
    [SerializeField] private EnemyBehaviorMode behaviorMode = EnemyBehaviorMode.Aggressive;
    [SerializeField] private AIMode aiMode = AIMode.Patrol;
    [SerializeField] private Transform[] patrolPoints;

    public AsyncStateMachine Fsm { get; private set; }
    public EnemyBehaviorMode BehaviorMode => behaviorMode;
    public AIMode AIMode => aiMode;
    public Factions Faction => faction;
    
    public bool InputBlocked { get; set; }
    public Vector2 MoveInput { get; set; }
    public bool JumpInput { get; set; }
    public float RotationYaw { get; set; }
    public bool Attack1 { get; set; }
    public bool Attack2 { get; set; }
    public bool Block { get; set; }
    public bool Shoot { get; set; }
    
    public bool HaveControl() => !InputBlocked;
    public void ReleaseControl() => InputBlocked = true;
    public void GainControl() => InputBlocked = false;

    private async void Start()
    {
        try
        {
            await InitializeFsm();
            Fsm.Ctx.SetFsm(Fsm);
            
            if (aiMode == AIMode.Guard)
            {
                await Fsm.TransitionTo(Fsm.GuardState);
            }
            else
            {
                await Fsm.TransitionTo(Fsm.PatrolState);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to initialize enemy brain: {ex}");
        }
    }

    private void OnDisable()
    {
        Fsm?.Dispose();
        Fsm = null;
    }
    
    private UniTask InitializeFsm()
    {
        Fsm = new AsyncStateMachine(new StateMachineContext(this, visionSystem, damageable,
            transform, humanoidController, rotationSpeed, patrolPoints, behaviorMode, aiMode, faction));
        return UniTask.CompletedTask;
    }
}