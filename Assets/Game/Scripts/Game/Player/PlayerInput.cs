using UnityEngine;
using Zenject;

public class CharacterInput : MonoBehaviour
{
    public static CharacterInput Instance { get; private set; }

    public bool InputBlocked { get; set; }

    private PlayerNewInput _input;

    public Vector2 MoveInput => InputBlocked ? Vector2.zero : _input.Move;
    public Vector2 CameraInput => InputBlocked ? Vector2.zero : _input.Look;
    public bool JumpInput => !InputBlocked && _input.Jump;
    public bool Attack1 => !InputBlocked && _input.Attack1;
    public bool Attack2 => !InputBlocked && _input.Attack2;

    [Inject]
    public void Construct (PlayerNewInput input)
    {
        _input = input;
        
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
            throw new UnityException("More than one CharacterInput: " + Instance.name + " and " + name);
    }
    
    public bool HaveControl() => !InputBlocked;
    public void ReleaseControl() => InputBlocked = true;
    public void GainControl() => InputBlocked = false;
}