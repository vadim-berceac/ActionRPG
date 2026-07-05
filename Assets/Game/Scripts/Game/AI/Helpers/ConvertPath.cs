using UnityEngine;

public static class ConvertPath
{
    public static Vector3[] ToVector(Transform[] patrolWaypoints)
    {
        var path = new Vector3[patrolWaypoints.Length];

        for (var i = 0; i < patrolWaypoints.Length; i++)
        {
            path[i] = patrolWaypoints[i].position;
        }
        
        return path;
    }
}
