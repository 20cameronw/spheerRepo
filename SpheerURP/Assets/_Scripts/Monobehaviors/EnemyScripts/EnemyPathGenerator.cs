using UnityEngine;

/// <summary>
/// Static utility that generates procedural Vector3 positions for enemy movement.
/// Positions are computed relative to a world center (the planet).
/// </summary>
public static class EnemyPathGenerator
{
    /// <summary>
    /// Generate waypoints arranged in a circular arc around a center point.
    /// </summary>
    /// <param name="center">World-space center (planet position).</param>
    /// <param name="count">Number of waypoints.</param>
    /// <param name="radius">Orbit radius.</param>
    /// <param name="yOffset">Vertical offset from center (positive = above, negative = below).</param>
    /// <param name="sweepAngleDegrees">Total arc covered by the path (360 = full circle).</param>
    public static Vector3[] GenerateOrbitPath(Vector3 center, int count, float radius,
                                               float yOffset, float sweepAngleDegrees = 360f)
    {
        Vector3[] path = new Vector3[count];
        float startAngle = Random.Range(0f, Mathf.PI * 2f);
        float sweepRad   = sweepAngleDegrees * Mathf.Deg2Rad;

        for (int i = 0; i < count; i++)
        {
            float t     = count > 1 ? (float)i / (count - 1) : 0f;
            float angle = startAngle + t * sweepRad;
            float yJitter = Random.Range(-0.5f, 0.5f);
            path[i] = center + new Vector3(
                Mathf.Cos(angle) * radius,
                yOffset + yJitter,
                Mathf.Sin(angle) * radius
            );
        }
        return path;
    }

    /// <summary>
    /// Get a single point at the given radius and Y offset, at a random angle.
    /// </summary>
    public static Vector3 GenerateApproachPoint(Vector3 center, float radius, float yOffset)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        return center + new Vector3(
            Mathf.Cos(angle) * radius,
            yOffset,
            Mathf.Sin(angle) * radius
        );
    }

    /// <summary>
    /// Get a leave position far from the center in a random direction.
    /// </summary>
    public static Vector3 GenerateLeavePoint(Vector3 center, float distance)
    {
        float angle  = Random.Range(0f, Mathf.PI * 2f);
        float ySign  = Random.value > 0.5f ? 1f : -1f;
        return center + new Vector3(
            Mathf.Cos(angle) * distance,
            ySign * distance * 0.4f,
            Mathf.Sin(angle) * distance
        );
    }
}
