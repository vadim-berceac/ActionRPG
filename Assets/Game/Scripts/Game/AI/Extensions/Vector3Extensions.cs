using UnityEngine;
using UnityEngine.AI;

public static class Vector3Extensions
{
    public static Vector3[] GetPathTo(this Vector3 start, Vector3 end, int areaMask)
    {
        var path = new NavMeshPath();
        NavMesh.CalculatePath(start, end, areaMask, path);

        return path.corners;
    }

    public static Vector3[] GetRandomPath(this Vector3 start, int size, float radius, int areaMask)
    {
        var path = new Vector3[size];
        path[0] = start;

        for (var i = 1; i < path.Length; i++)
        {
            var randomDirection = Random.insideUnitSphere * radius;
            randomDirection += path[i - 1];

            NavMesh.SamplePosition(randomDirection, out var hit, radius, areaMask);
            path[i] = hit.position;
        }

        return path;
    }
}
