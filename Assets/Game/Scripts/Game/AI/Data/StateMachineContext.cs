using System;
using Game;
using UnityEngine;
using UnityEngine.AI;

public class StateMachineContext : IDisposable
{
    public IInput Input { get; private set; }
    public Transform Transform  { get; private set; }
    public float RotationSpeed { get; private set; }
    public Vector3[] PatrolWaypoints { get; private set; }
    public bool IsStarted { get; private set; }
    public bool IsHitReaction { get; private set; }

    public readonly int WalkableAreaMask = NavMesh.GetAreaFromName("Walkable") != -1
        ? 1 << NavMesh.GetAreaFromName("Walkable")
        : NavMesh.AllAreas;
    
    private readonly VisionSystem _visionSystem;
    
    public StateMachineContext(IInput input, VisionSystem visionSystem, Transform transform, float rotationSpeed, Transform[] patrolWaypoints)
    {
        Input = input;
        _visionSystem = visionSystem;
        Transform = transform;
        RotationSpeed = rotationSpeed;
        SetWaypoints(ConvertPath.ToVector(patrolWaypoints));
        
        _visionSystem.OnTargetReached += OnTargetReached;
    }
    
    private static void OnTargetReached(Damageable damageable)
    {
        if (damageable == null)
        {
            Debug.LogWarning("Цель потеряна");
            return;
        }
        
        Debug.Log($"Обнаружена цель {damageable.gameObject.name}");
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

    public void Dispose()
    {
        _visionSystem.OnTargetReached -= OnTargetReached;
    }
}
