using UnityEngine;

/// <summary>
/// Static utility that generates procedural Vector3 positions for enemy movement.
/// Positions are computed relative to a world center (the planet).
/// </summary>
public static class EnemyPathGenerator
{
    /// <summary>
    /// Spawn point well outside the camera view.
    /// ySide = +1 → above the planet, -1 → below.
    /// </summary>
    public static Vector3 GenerateOffScreenSpawnPoint(Vector3 center, float spawnRadius, float ySide)
    {
        float angle  = Random.Range(0f, Mathf.PI * 2f);
        float yOff   = ySide * spawnRadius * 0.6f;
        return center + new Vector3(
            Mathf.Cos(angle) * spawnRadius,
            yOff,
            Mathf.Sin(angle) * spawnRadius
        );
    }

    /// <summary>
    /// A neutral staging area near the planet perimeter (Y ≈ 0) that the fly-in targets.
    /// </summary>
    public static Vector3 GenerateStagingPoint(Vector3 center, float stagingRadius)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        return center + new Vector3(
            Mathf.Cos(angle) * stagingRadius,
            0f,
            Mathf.Sin(angle) * stagingRadius
        );
    }

    /// <summary>
    /// Horizontal left-to-right (or right-to-left) sweep path across the top or bottom
    /// of the planet.  All points share the same Y (center.y + yOffset) and Z (center.z),
    /// with X sweeping from -sweepWidth to +sweepWidth or vice-versa.
    /// </summary>
    public static Vector3[] GenerateAttackSweepPath(Vector3 center, int count,
                                                     float sweepWidth, float yOffset)
    {
        Vector3[] path    = new Vector3[count];
        bool      reverse = Random.value > 0.5f;
        float     startX  = center.x + (reverse ? sweepWidth  : -sweepWidth);
        float     endX    = center.x + (reverse ? -sweepWidth :  sweepWidth);

        for (int i = 0; i < count; i++)
        {
            float t  = count > 1 ? (float)i / (count - 1) : 0f;
            path[i]  = new Vector3(Mathf.Lerp(startX, endX, t),
                                   center.y + yOffset,
                                   center.z);
        }
        return path;
    }

    /// <summary>
    /// Get a leave position far from the center, aimed off-screen.
    /// </summary>
    public static Vector3 GenerateLeavePoint(Vector3 center, float distance)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float ySign = Random.value > 0.5f ? 1f : -1f;
        return center + new Vector3(
            Mathf.Cos(angle) * distance,
            ySign * distance * 0.4f,
            Mathf.Sin(angle) * distance
        );
    }
}

