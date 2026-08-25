using UnityEngine;

[CreateAssetMenu(fileName = "CurveConstants", menuName = "Scriptable Objects/CurveConstants")]
public class CurveConstants : ScriptableObject
{
    [field: SerializeField] public AnimationCurve HitStopCurve { get; private set; }
}
