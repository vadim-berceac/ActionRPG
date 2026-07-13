
public static class Constants
{
    public const float ArriveThreshold = 1.0f; 
    public const float SlowRadius = 3.5f;    
    public const float MinThrottle = 0.15f;
    public const float MaxTurnAngleForFullSpeed = 60f;
    public const float StopDistance = 2f;
    public const float ExitDistanceBuffer = 0.5f;

    public const float StuckSampleInterval = 0.2f;
    public const float StuckTimeThreshold = 0.6f;
    public const float StuckMinProgressSq = 0.05f * 0.05f;
    public const float StuckMinMoveSq = 0.02f * 0.02f;
    public const int MaxUnstuckAttempts = 3;
    public const float UnstuckManeuverDuration = 0.35f;
    public const float StuckProbeDistance = 1.2f;
    public const float UnstuckStrafeInput = 0.85f;
    public const float UnstuckForwardInput = 0.4f;
    public const float UnstuckBackwardInput = 0.45f;
    public const float UnstuckYawOffset = 35f;

    public const float MinJumpHeight = 0.2f;
    public const float MaxJumpHeight = 1.8f;
    public const float JumpTriggerDistance = 1.25f;
    public const float JumpLandingTimeout = 2f;
    public const int JumpInputFrames = 2;
}
