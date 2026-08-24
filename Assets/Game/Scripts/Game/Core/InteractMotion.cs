using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game;
using UnityEngine;
using UnityEngine.Events;

public class InteractMotion : MonoBehaviour
{
    private enum MotionType
    {
        RotateToFootTarget,
        MoveToFootTarget,
        MoveToAndRotateToFootTarget,
    }

    private enum ExitType
    {
        StayOnFootPosition,
        ReturnToInitialPosition,
        ReturnToInitialPositionAndRotation,
        MoveToExitPosition,
    }

    private enum TargetSource
    {
        FixedFootTarget,         
        FromInteractAnimation,   
        FromEventController,     
    }

    private enum ControllerSource
    {
        FromEvent,               
        FixedController,         
        FromInteractAnimation,    
    }

    [SerializeField] private InteractAnimation trigger;
    [SerializeField] private float enterDelay;
    [SerializeField] private float enterTime;
    [SerializeField] private float exitTime;

    [Header("Who to move/rotate")]
    [SerializeField] private ControllerSource controllerSource = ControllerSource.FromEvent;
    [SerializeField] private HumanoidController fixedController;
    [SerializeField] private InteractAnimation controllerInteractAnimation;

    [Header("Target Source (mutually exclusive)")]
    [SerializeField] private TargetSource targetSource = TargetSource.FixedFootTarget;
    [SerializeField] private Transform footTarget;
    [SerializeField] private InteractAnimation targetInteractAnimation;

    [Tooltip("Используется только при MoveToExitPosition, опционально")]
    [SerializeField] private Transform exitTarget;
    [SerializeField] private Transform interactableModel;
    [SerializeField] private MotionType motionType;
    [SerializeField] private ExitType exitType;

    private CancellationTokenSource _cts;
    private Sequence _sequence;
    private Vector3 _controllerInitialPosition;
    private Quaternion _controllerInitialRotation;

    private Collider[] _interactableColliders;
    private CharacterController _activeCharacterController;
    private bool _collisionDisabled;

    private Transform _resolvedFootTarget;
    private HumanoidController _movedController;

    public UnityEvent onEnterMotionEnd;
    public UnityEvent onExitMotionStart;

    private void Awake()
    {
        if (!interactableModel)
            return;

        _interactableColliders = interactableModel.GetComponentsInChildren<Collider>(true);
    }

    private void OnEnable()
    {
        if (trigger)
        {
            trigger.onInteractEnter.AddListener(OnEnter);
            trigger.onInteractExit.AddListener(OnExit);
        }
    }

    private void OnDisable()
    {
        if (trigger)
        {
            trigger.onInteractEnter.RemoveListener(OnEnter);
            trigger.onInteractExit.RemoveListener(OnExit);
        }

        Cancel();
    }

    private void OnDestroy()
    {
        Cancel();
    }

    private void OnEnter(HumanoidController eventController)
    {
        var controllerToMove = ResolveController(eventController);
        if (!controllerToMove)
            return;

        _resolvedFootTarget = ResolveFootTarget(eventController);
        if (!_resolvedFootTarget)
            return;

        _movedController = controllerToMove;
        _controllerInitialPosition = controllerToMove.transform.position;
        _controllerInitialRotation = controllerToMove.transform.rotation;
        
        controllerToMove.SetInteracting(true);

        SetCollisionIgnored(controllerToMove, true);

        var rotation = GetEnterRotation(controllerToMove.transform);
        EnterAsync(rotation, controllerToMove).Forget();
    }

    private HumanoidController ResolveController(HumanoidController eventController)
    {
        switch (controllerSource)
        {
            case ControllerSource.FromEvent:
                return eventController;

            case ControllerSource.FixedController:
                return fixedController;

            case ControllerSource.FromInteractAnimation:
                return controllerInteractAnimation
                    ? controllerInteractAnimation.CurrentController
                    : null;

            default:
                return null;
        }
    }

    private Transform ResolveFootTarget(HumanoidController eventController)
    {
        switch (targetSource)
        {
            case TargetSource.FixedFootTarget:
                return footTarget;

            case TargetSource.FromInteractAnimation:
                return targetInteractAnimation
                    ? targetInteractAnimation.transform
                    : null;

            case TargetSource.FromEventController:
                return eventController
                    ? eventController.transform
                    : null;

            default:
                return null;
        }
    }

    private async UniTaskVoid EnterAsync(Quaternion rotation, HumanoidController controller)
    {
        await Play(enterTime, _resolvedFootTarget.position, rotation, controller);
        onEnterMotionEnd?.Invoke();
    }

    private void OnExit(HumanoidController eventController)
    {
        var controller = _movedController ? _movedController : eventController;
        if (!controller)
            return;

        ExitAsync(controller).Forget();
    }

    private async UniTaskVoid ExitAsync(HumanoidController controller)
    {
        onExitMotionStart?.Invoke();

        try
        {
            switch (exitType)
            {
                case ExitType.ReturnToInitialPosition:
                    await Play(exitTime, _controllerInitialPosition, controller.transform.rotation, controller);
                    break;

                case ExitType.ReturnToInitialPositionAndRotation:
                    await Play(exitTime, _controllerInitialPosition, _controllerInitialRotation, controller);
                    break;

                case ExitType.StayOnFootPosition:
                    break;

                case ExitType.MoveToExitPosition:
                    if (exitTarget)
                        await Play(exitTime, exitTarget.position, exitTarget.rotation, controller);
                    break;
            }
        }
        finally
        {
            controller.SetInteracting(false);   
            SetCollisionIgnored(controller, false);
            _resolvedFootTarget = null;
            _movedController = null;
        }
    }

    private async UniTask Play(float time, Vector3 position, Quaternion rotation, HumanoidController controller, UnityEvent onMotionStart = null)
    {
        Cancel(restoreCollision: false);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        var token = _cts.Token;

        if (enterDelay > 0f)
            await UniTask.Delay(TimeSpan.FromSeconds(enterDelay), cancellationToken: token);

        onMotionStart?.Invoke();

        var controllerTransform = controller.transform;
        _sequence = DOTween.Sequence().SetTarget(controllerTransform);

        switch (motionType)
        {
            case MotionType.RotateToFootTarget:
                _sequence.Join(controllerTransform.DORotateQuaternion(rotation, time));
                break;

            case MotionType.MoveToFootTarget:
                _sequence.Join(controllerTransform.DOMove(position, time));
                break;

            case MotionType.MoveToAndRotateToFootTarget:
                _sequence.Join(controllerTransform.DOMove(position, time));
                _sequence.Join(controllerTransform.DORotateQuaternion(rotation, time));
                break;
        }

        await AwaitSequence(_sequence, token);
    }

    private Quaternion GetEnterRotation(Transform controllerTransform)
    {
        if (!_resolvedFootTarget)
            return controllerTransform.rotation;

        if (motionType != MotionType.RotateToFootTarget)
            return _resolvedFootTarget.rotation;

        var direction = _resolvedFootTarget.position - controllerTransform.position;
        direction.y = 0f;

        return direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction, controllerTransform.up)
            : controllerTransform.rotation;
    }

    private static UniTask AwaitSequence(Sequence sequence, CancellationToken token)
    {
        var tcs = new UniTaskCompletionSource();

        sequence.OnComplete(() => tcs.TrySetResult());
        sequence.OnKill(() => tcs.TrySetResult());

        CancellationTokenRegistration registration = default;
        registration = token.Register(() =>
        {
            if (sequence.IsActive())
                sequence.Kill();

            registration.Dispose();
        });

        return tcs.Task;
    }

    private void SetCollisionIgnored(HumanoidController controller, bool ignore)
    {
        var characterController = controller.GetComponent<CharacterController>();
        if (!characterController || _interactableColliders == null || _interactableColliders.Length == 0)
            return;

        foreach (var col in _interactableColliders)
        {
            if (!col) continue;
            Physics.IgnoreCollision(col, characterController, ignore);
        }

        _collisionDisabled = ignore;
        _activeCharacterController = ignore ? characterController : null;
    }

    private void Cancel(bool restoreCollision = true)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_sequence != null && _sequence.IsActive())
            _sequence.Kill();

        _sequence = null;

        if (restoreCollision && _collisionDisabled && _activeCharacterController)
        {
            foreach (var col in _interactableColliders)
            {
                if (!col) continue;
                Physics.IgnoreCollision(col, _activeCharacterController, false);
            }

            _collisionDisabled = false;
            _activeCharacterController = null;
        }
    }
}