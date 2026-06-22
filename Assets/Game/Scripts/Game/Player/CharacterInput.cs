using System;
using UnityEngine;
using UnityEngine.InputSystem;

public interface ICharacterInput
{
    public Vector2 Move { get; set; }
    public Vector2 Look { get; set; }
    public bool Jump { get; set; }
    public bool Attack1 { get; set; }
}

public class PlayerNewInput : ICharacterInput, IDisposable
{
    public Vector2 Move { get; set; }
    public Vector2 Look { get; set; }
    public bool Jump { get; set; }
    public bool Attack1 { get; set; }
    public bool Attack2 { get; set; }

    public event Action Interact;
    public event Action Pause;
    
    private readonly InputAction _moveAction;
    private readonly InputAction _lookAction;
    private readonly InputAction _jumpAction;
    private readonly InputAction _attack1Action;
    private readonly InputAction _attack2Action;
    private readonly InputAction _interactAction;
    private readonly InputAction _pauseAction;

    private PlayerNewInput(InputActionAsset actionAsset)
    {
        _moveAction = actionAsset.FindAction("Move");
        _lookAction = actionAsset.FindAction("Look");
        _jumpAction = actionAsset.FindAction("Jump");
        _attack1Action = actionAsset.FindAction("Attack1");
        _attack2Action = actionAsset.FindAction("Attack2");
        _interactAction = actionAsset.FindAction("Interact");
        _pauseAction = actionAsset.FindAction("Pause");
        
        Subscribe();
        
        actionAsset.Enable();
    }

    private void Subscribe()
    {
        _moveAction.performed += OnMove;
        _moveAction.canceled += OnMoveCanceled;
        
        _lookAction.performed += OnLook;
        _lookAction.canceled += OnLookCanceled;
        
        _jumpAction.started += OnJump;
        _jumpAction.canceled += OnJumpCanceled;

        _attack1Action.started += OnAttack1;
        _attack1Action.canceled += OnAttack1Canceled;
        
        _attack2Action.started += OnAttack2;
        _attack2Action.canceled += OnAttack2Canceled;
        
        _interactAction.started += OnInteract;
        _pauseAction.performed += OnPause;
    }

    private void Unsubscribe()
    {
        _moveAction.performed -= OnMove;
        _moveAction.canceled -= OnMoveCanceled;
        
        _lookAction.performed -= OnLook;
        _lookAction.canceled -= OnLookCanceled;
        
        _jumpAction.started -= OnJump;
        _jumpAction.canceled -= OnJumpCanceled;

        _attack1Action.started -= OnAttack1;
        _attack1Action.canceled -= OnAttack1Canceled;
        
        _attack2Action.started -= OnAttack2;
        _attack2Action.canceled -= OnAttack2Canceled;
        
        _interactAction.started -= OnInteract;
        _pauseAction.performed -= OnPause;
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        Move = context.ReadValue<Vector2>();
    }
    
    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        Move = Vector2.zero;
    }

    private void OnLook(InputAction.CallbackContext context)
    {
        Look = context.ReadValue<Vector2>();
    }

    private void OnLookCanceled(InputAction.CallbackContext context)
    {
        Look = Vector2.zero;
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        Jump = true;
    }

    private void OnJumpCanceled(InputAction.CallbackContext context)
    {
        Jump = false;
    }

    private void OnAttack1(InputAction.CallbackContext context)
    {
        Attack1 = true;
    }
    
    private void OnAttack1Canceled(InputAction.CallbackContext context)
    {
        Attack1 = false;
    }
    
    private void OnAttack2(InputAction.CallbackContext context)
    {
        Attack2 = true;
    }
    
    private void OnAttack2Canceled(InputAction.CallbackContext context)
    {
        Attack2 = false;
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        Interact?.Invoke();
    }

    private void OnPause(InputAction.CallbackContext context)
    {
        Pause?.Invoke();
    }

    public void Dispose()
    {
        Unsubscribe();
    }
}
