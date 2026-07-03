using UnityEngine;

public class EnemyBrain : MonoBehaviour, IInput
{
    public bool InputBlocked { get; set; }

    public Vector2 MoveInput
    {
        get => Vector2.zero;
        set {}
    }

    public Vector2 CameraInput
    {
        get =>  Vector2.zero; 
        set {}
    }

    public bool JumpInput
    {
        get =>  false; 
        set {}
    }

    public bool Attack1
    {
        get =>  false;
        set {}
    }

    public bool Attack2
    {
        get =>  false;
        set {}
    }
    
    public bool HaveControl() => !InputBlocked;
    public void ReleaseControl() => InputBlocked = true;
    public void GainControl() => InputBlocked = false;
}
