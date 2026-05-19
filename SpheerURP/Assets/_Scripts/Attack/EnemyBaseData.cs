using System.Collections.Generic;

/// <summary>
/// Plain-data snapshot of a procedurally generated enemy base.
/// Populated by <see cref="AttackManager.GenerateEnemyBase"/> before the attack
/// scene begins.  Later this can be replaced by real player-base data fetched
/// from a backend without changing the rest of the attack flow.
/// </summary>
[System.Serializable]
public class EnemyBaseData
{
    /// <summary>Display name shown on the attack screen.</summary>
    public string baseName;

    /// <summary>Index into the world prefab list used for the visual globe.</summary>
    public int worldPrefabIndex;

    /// <summary>Approximate XP level used to scale building health and resource reward.</summary>
    public int simulatedXPLevel;

    /// <summary>Number of buildings on the surface (mirrors the player's build count).</summary>
    public List<int> buildingCounts = new List<int>();

    /// <summary>Total dollar resources available to loot if the attack succeeds.</summary>
    public float stolenResources;

    /// <summary>Total core resources available to loot if the attack succeeds.</summary>
    public int stolenCores;
}
