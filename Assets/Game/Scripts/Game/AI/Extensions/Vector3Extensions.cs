using UnityEngine;
using UnityEngine.AI;

public static class Vector3Extensions
{
    public static bool TryGetPathTo(this Vector3 start, Vector3 end, int areaMask, out Vector3[] corners)
    {
        corners = null;

        if (!NavMesh.SamplePosition(start, out var startHit, Constants.NavMeshSampleRadius, areaMask)
            || !NavMesh.SamplePosition(end, out var endHit, Constants.NavMeshSampleRadius, areaMask))
            return false;

        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(startHit.position, endHit.position, areaMask, path)
            || path.status != NavMeshPathStatus.PathComplete
            || path.corners == null
            || path.corners.Length < 2)
            return false;

        corners = path.corners;
        return true;
    }

    public static Vector3[] GetPathTo(this Vector3 start, Vector3 end, int areaMask)
    {
        return start.TryGetPathTo(end, areaMask, out var corners)
            ? corners
            : System.Array.Empty<Vector3>();
    }

    public static Vector3[] GetRandomPath(this Vector3 start, int size, float radius, int areaMask)
    {
        var path = new Vector3[size];
        path[0] = start;

        for (var i = 1; i < path.Length; i++)
        {
            var randomDirection = Random.insideUnitSphere * radius;
            randomDirection += path[i - 1];

            if (NavMesh.SamplePosition(randomDirection, out var hit, radius, areaMask))
                path[i] = hit.position;
            else
                path[i] = path[i - 1];
        }

        return path;
    }
}
