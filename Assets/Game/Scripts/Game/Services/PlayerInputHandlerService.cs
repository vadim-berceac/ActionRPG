using UnityEngine;
using Zenject;

public class PlayerInputHandlerService : IInput
{
    public bool InputBlocked { get; set; }

    private PlayerNewInput _input;

    public Vector2 MoveInput
    {
        get => InputBlocked ? Vector2.zero : _input.Move;
        set {}
    }

    public Vector2 CameraInput
    {
        get => InputBlocked ? Vector2.zero : _input.Look; 
        set {}
    }

    public bool JumpInput
    {
        get =>  !InputBlocked && _input.Jump; 
        set {}
    }

    public bool Attack1
    {
        get =>  !InputBlocked && _input.Attack1; 
        set {}
    }

    public bool Attack2
    {
        get =>  !InputBlocked && _input.Attack2; 
        set {}
    }

    [Inject]
    public void Construct (PlayerNewInput input)
    {
        _input = input;
    }
    
    public bool HaveControl() => !InputBlocked;
    public void ReleaseControl() => InputBlocked = true;
    public void GainControl() => InputBlocked = false;
}
