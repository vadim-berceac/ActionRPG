using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

public class GameFlowController : MonoBehaviour
{
    [field: SerializeField] public float Mass { get; private set; } // временно - потом в дату вынести
    
    [SerializeField] private AnimationCurve animationCurve;
    [SerializeField] private float rotationSpeed;
    [SerializeField] private Transform[] patrolPoints;

    //private ClickHandler _clickHandler;
    public AsyncStateMachine Fsm { get; private set; }

    // [Inject]
    // private void Construct(ClickHandler clickHandler)
    // {
    //     _clickHandler = clickHandler;
    // }

    private async void Start()
    {
        await SubscribeInput();
        await InitializeFsm();
        await Fsm.TransitionTo(Fsm.PatrolState);
        Fsm.Ctx.Activate(true);
    }

    private void OnClickPerformed(GameObject obj)
    {
        if (obj != gameObject)
        {
            return;
        }
        
        Debug.Log($"GameFlowController::OnClickPerformed: {obj}");
        
        if (Fsm.CurrentState == Fsm.WaitForOrdersState)
        {
            Fsm.Ctx.Activate(true);
        }
        else
        {
            Fsm.Ctx.Activate(false);
        }
    }

    private UniTask SubscribeInput()
    {
        //_clickHandler.OnObjectSelected += OnClickPerformed;
        return UniTask.CompletedTask;
    }

    private UniTask InitializeFsm()
    {
        Fsm = new AsyncStateMachine(new StateMachineContext(transform, animationCurve, rotationSpeed, patrolPoints));
        return UniTask.CompletedTask;
    }

    private void OnDestroy()
    {
        //_clickHandler.OnObjectSelected -= OnClickPerformed;
        Fsm?.Shutdown();
    }
}