using UnityEngine;

[CreateAssetMenu(fileName = "CharacterParamsSettings", menuName = "Scriptable Objects/CharacterParamsSettings")]
public class CharacterParamsSettings : ScriptableObject
{
    [field: SerializeField] public CharacterParams CharacterParams { get; private set; }
}

[System.Serializable]
public struct CharacterParams
{
    [field: Header("Movement")]
    [field: SerializeField] public float MaxForwardSpeed { get; private set; }
    [field: SerializeField] public float Gravity { get; private set; }
    [field: SerializeField] public float JumpSpeed { get; private set; }
    [field: SerializeField] public float MinTurnSpeed { get; private set; }
    [field: SerializeField] public float MaxTurnSpeed { get; private set; }
    [field: SerializeField] public float CombatTurnSpeed { get; private set; }
    [field: SerializeField] public float AimTurnSpeed { get; private set; }
    [field: SerializeField] public float IdleTimeout { get; private set; }
    [field: SerializeField] public float GroundedTurnSmoothTime { get; private set; }
    [field: SerializeField] public float CoyoteTime { get; private set; }
    [field: SerializeField] public float MinFallHeightForAirborneAnim { get; private set; } 
    
    [field: Header("Stamina Costs")]
    [field: SerializeField] public float Attack1StaminaCost { get; private set; }
    [field: SerializeField] public float Attack2StaminaCost { get; private set; }
    [field: SerializeField] public float BlockStaminaCost { get; private set; }
    [field: SerializeField] public float ShootStaminaCost { get; private set; }
    [field: SerializeField] public float BlockHoldStaminaCostPerSecond { get; private set; }
    [field: SerializeField] public float AimHoldStaminaCostPerSecond { get; private set; }
    [field: SerializeField] public float MaxStamina { get; private set; }
    [field: SerializeField] public float RegenSpeed { get; private set; }
    [field: SerializeField] public float RegenDelay { get; private set; }
}
