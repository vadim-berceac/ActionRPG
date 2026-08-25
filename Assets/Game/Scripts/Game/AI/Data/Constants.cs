
using UnityEngine;

public static class Constants
{
    public const float WeaponStuckTime = 0.2f;
    public const float ArriveThreshold = 0.2f;
    public const float ArriveHeightThreshold = 0.35f;
    public const float NavMeshSampleRadius = 1.5f;
    public const float PathTargetMoveThreshold = 0.75f;
    public const float CornerVisibilityHeightTolerance = 0.65f;
    public const float SlowRadius = 3.5f;    
    public const float MinThrottle = 0.15f;
    public const float MaxTurnAngleForFullSpeed = 60f;
    public const float ExitDistanceBuffer = 0.1f;
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
    public const float StuckArriveXzThresholdSq = 0.5f * 0.5f;
    public const int StuckArriveMaxCount = 3;
    public const int StepClimbMaxAttempts = 30;

    public const float MinJumpHeight = 0.2f;
    public const float MaxJumpHeight = 1.8f;
    public const float JumpTriggerDistance = 1.25f;
    public const float JumpLandingTimeout = 2f;
    public const int JumpInputFrames = 2;
    public const float NarrowPassageProbeDistance = 1.5f;
    public const float CornerSteerThresholdSq = 2.5f * 2.5f;
    public const float CornerProbeDistance = 2.0f;
    public const float WallSlideStrength = 0.6f;
    public const float WallSlideProbeDistance = 1.0f;
    public const float PreferredShootDistance = 35f;
    public const float WeaponSwitchTimeout = 0.5f;
    public static readonly int ObstacleLayerMask = Physics.DefaultRaycastLayers;
    
    public const float AirborneTurnSpeedProportion = 5.4f;
    public const float GroundedRayDistance = 1f;
    public const float JumpAbortSpeed = 10f;
    public const float InverseOneEighty = 1f / 180f;
    public const float StickingGravityProportion = 0.3f;
    public const float GroundAcceleration = 20f;
    public const float GroundDeceleration = 25f;
    public const float KnockbackDeceleration = 15f;
}
