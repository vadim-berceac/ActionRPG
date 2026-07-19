using UnityEngine;

public interface IInput
{
    public bool InputBlocked { get; set; }

    public Vector2 MoveInput { get; set; }
    public float RotationYaw { get; set; }
    public bool JumpInput { get; set; }
    public bool Attack1 { get; set; }
    public bool Attack2 { get; set; }
    public bool Block { get; set; }
    public bool Shoot { get; set; }
    
    public bool HaveControl() => !InputBlocked;
    public void ReleaseControl() => InputBlocked = true;
    public void GainControl() => InputBlocked = false;
}
