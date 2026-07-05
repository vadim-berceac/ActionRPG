using UnityEngine;
using UnityEngine.AI;

public class StateMachineContext
{
    public Transform Transform  { get; private set; }
    public AnimationCurve AnimationCurve  { get; private set; }
    public float RotationSpeed { get; private set; }
    public Vector3[] PatrolWaypoints { get; private set; }
    public bool IsStarted { get; private set; }
    public bool IsHitReaction { get; private set; }

    public readonly int WalkableAreaMask = NavMesh.GetAreaFromName("Walkable") != -1
        ? 1 << NavMesh.GetAreaFromName("Walkable")
        : NavMesh.AllAreas;
    public StateMachineContext(Transform transform, AnimationCurve animationCurve, 
        float rotationSpeed, Transform[] patrolWaypoints)
    {
        Transform = transform;
        AnimationCurve = animationCurve;
        RotationSpeed = rotationSpeed;
        SetWaypoints(ConvertPath.ToVector(patrolWaypoints));
    }

    public void SetWaypoints(Vector3[] waypoints)
    {
        PatrolWaypoints = waypoints;
    }

    public void Activate(bool value)
    {
        IsStarted = value;
        Debug.Log(IsStarted);
    }

    public void HitReaction(bool value)
    {
        IsHitReaction = value;
    }
}
