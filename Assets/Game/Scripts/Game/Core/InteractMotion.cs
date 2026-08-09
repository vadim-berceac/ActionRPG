using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game;
using UnityEngine;

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

    [SerializeField] private InteractAnimation trigger;
    [SerializeField] private float enterDelay;
    [SerializeField] private float enterTime;
    [SerializeField] private float exitTime;
    [SerializeField] private Transform footTarget;
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

    private void Awake()
    {
        if (!interactableModel)
        {
            return;
        }
        _interactableColliders = interactableModel
            ? interactableModel.GetComponentsInChildren<Collider>(true)
            : Array.Empty<Collider>();
    }

    private void OnEnable()
    {
        trigger.onInteractEnter.AddListener(OnEnter);
        trigger.onInteractExit.AddListener(OnExit);
    }

    private void OnDisable()
    {
        trigger.onInteractEnter.RemoveListener(OnEnter);
        trigger.onInteractExit.RemoveListener(OnExit);

        Cancel();
    }

    private void OnDestroy()
    {
        Cancel();
    }

    private void OnEnter(HumanoidController controller)
    {
        if (!footTarget)
        {
            return;
        }

        _controllerInitialPosition = controller.transform.position;
        _controllerInitialRotation = controller.transform.rotation;

        SetCollisionIgnored(controller, true);

        var rotation = GetEnterRotation(controller.transform);

        Play(enterTime, footTarget.position, rotation, controller).Forget();
    }

    private Quaternion GetEnterRotation(Transform controllerTransform)
    {
        if (motionType != MotionType.RotateToFootTarget)
        {
            return footTarget.rotation;
        }

        var direction = footTarget.position - controllerTransform.position;
        direction.y = 0f;

        return direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction, controllerTransform.up)
            : controllerTransform.rotation;
    }

    private void OnExit(HumanoidController controller)
    {
        ExitAsync(controller).Forget();
    }

    private async UniTaskVoid ExitAsync(HumanoidController controller)
    {
        try
        {
            switch (exitType)
            {
                case ExitType.ReturnToInitialPosition:
                    await Play(exitTime, _controllerInitialPosition, controller.transform.rotation, controller);
                    break;

                case ExitType.ReturnToInitialPositionAndRotation:
                    await Play(exitTime,_controllerInitialPosition, _controllerInitialRotation, controller);
                    break;

                case ExitType.StayOnFootPosition:
                    break;
                
                case ExitType.MoveToExitPosition:
                    if (!exitTarget)
                    {
                        break;
                    }
                    await Play(exitTime,exitTarget.position, exitTarget.rotation, controller);
                    break;
            }
        }
        finally
        {
            SetCollisionIgnored(controller, false);
        }
    }

    private async UniTask Play(float time, Vector3 position, Quaternion rotation, HumanoidController controller)
    {
        Cancel(restoreCollision: false);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        var token = _cts.Token;

        if (enterDelay > 0f)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(enterDelay), cancellationToken: token);
        }

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
        {
            return;
        }

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
        {
            _sequence.Kill();
        }

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