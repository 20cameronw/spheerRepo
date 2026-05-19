using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central controller for the base-attack feature.
/// Assign this MonoBehaviour to the Game Managers GameObject.
///
/// Responsibilities:
///   1. Procedurally generate a random enemy base (or later, a real player base).
///   2. Animate the generated world in from below while pushing the player's
///      world to a configurable "parked" position.
///   3. Hide the HUD (top bar + shop buttons) during the attack.
///   4. Award looted resources when the attack succeeds.
///   5. Restore the scene to its pre-attack state when the battle ends.
///
/// Extend by adding new <see cref="IOffenseWeapon"/> implementations; this manager
/// stays weapon-type agnostic.
/// </summary>
public class AttackManager : MonoBehaviour
{
    public static AttackManager Instance { get; private set; }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when an attack sequence begins.</summary>
    public static event Action<EnemyBaseData> OnAttackStarted;

    /// <summary>Fired when an attack sequence ends. Bool = player won.</summary>
    public static event Action<bool> OnAttackEnded;

    // ── Inspector fields ──────────────────────────────────────────────────────

    [Header("Scene References")]
    [SerializeField] private WorldSpawner worldSpawner;

    [Tooltip("Root GameObject of the top bar UI. Will be hidden during an attack.")]
    [SerializeField] private GameObject topBarUI;

    [Tooltip("Root GameObject(s) of shop / action buttons. Will be hidden during an attack.")]
    [SerializeField] private List<GameObject> shopButtonObjects = new List<GameObject>();

    [Header("World Positions")]
    [Tooltip("The player's world will be moved to this transform's position when an attack starts.")]
    [SerializeField] private Transform playerWorldParkedTransform;

    [Tooltip("The enemy world is instantiated here (e.g. below the screen) before animating in.")]
    [SerializeField] private Transform enemyWorldStartTransform;

    [Tooltip("The enemy world animates to this transform's position when an attack starts.")]
    [SerializeField] private Transform enemyWorldEndTransform;

    [Header("Enemy World Generation")]
    [Tooltip("World prefabs the generator can pick from when creating an enemy base.")]
    [SerializeField] private List<GameObject> worldPrefabs = new List<GameObject>();

    [Tooltip("Health per building on the generated base (scaled by simulatedXPLevel).")]
    [SerializeField] private float baseBuildingHealth = 80f;

    [Tooltip("Percentage of the enemy's simulated passive income available to loot (0–1).")]
    [SerializeField] [Range(0f, 1f)] private float lootPercentage = 0.3f;

    [Header("Enemy World Rotation")]
    [Tooltip("Minimum rotation speed (degrees/second) per axis applied to the spawned enemy world.")]
    [SerializeField] private Vector3 enemyWorldRotateMin = new Vector3(0f, 5f, 0f);

    [Tooltip("Maximum rotation speed (degrees/second) per axis applied to the spawned enemy world.")]
    [SerializeField] private Vector3 enemyWorldRotateMax = new Vector3(0f, 20f, 5f);

    [Header("Enemy World Building Fallback")]
    [Tooltip("Minimum number of buildings to place when the player has bought none yet.")]
    [SerializeField] private int minEnemyBuildingsMin = 3;

    [Tooltip("Maximum number of buildings to place when the player has bought none yet.")]
    [SerializeField] private int minEnemyBuildingsMax = 7;

    [Header("Animation")]
    [Tooltip("Seconds the slide-in / slide-out animations take.")]
    [SerializeField] private float animationDuration = 1.2f;

    [Tooltip("LeanTween ease type used for the world entrance animation.")]
    [SerializeField] private LeanTweenType enterEase = LeanTweenType.easeOutBack;

    [Tooltip("LeanTween ease type used for the world exit animation.")]
    [SerializeField] private LeanTweenType exitEase = LeanTweenType.easeInBack;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private bool isAttacking;
    private EnemyBaseData currentEnemyBase;
    private GameObject spawnedEnemyWorldGO;
    private AttackWorldView currentAttackWorldView;

    private Vector3 playerWorldOriginalPosition;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        // Destroy the duplicate component only (not the GameObject) so the shared
        // "Game Managers" object stays alive — consistent with Player.cs and
        // TransactionManager.cs singleton patterns in this project.
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        AttackWorldView.OnBaseDestroyed += HandleBaseDestroyed;
    }

    private void OnDestroy()
    {
        AttackWorldView.OnBaseDestroyed -= HandleBaseDestroyed;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called from the Attack button in scene.
    /// Generates a random enemy base, hides the HUD, slides the player world up,
    /// and animates the enemy world in from below.
    /// </summary>
    public void StartAttack()
    {
        if (isAttacking)
        {
            Debug.LogWarning("[AttackManager] Attack already in progress.");
            return;
        }

        currentEnemyBase = GenerateEnemyBase();
        StartCoroutine(AttackSequenceEnter(currentEnemyBase));
    }

    /// <summary>
    /// Manually end the current attack (e.g. from a Retreat button).
    /// </summary>
    public void RetreatFromAttack()
    {
        if (!isAttacking) return;
        StartCoroutine(AttackSequenceExit(playerWon: false));
    }

    /// <summary>
    /// Returns whether an attack is currently in progress.
    /// </summary>
    public bool IsAttacking() => isAttacking;

    /// <summary>
    /// Returns the <see cref="AttackWorldView"/> for the currently spawned enemy world,
    /// or null when no attack is active.
    /// </summary>
    public AttackWorldView GetCurrentAttackWorldView() => currentAttackWorldView;

    // ── Enemy Base Generation ─────────────────────────────────────────────────

    /// <summary>
    /// Builds a procedural <see cref="EnemyBaseData"/> that mirrors the player's
    /// current progression level.  Replace or extend this method when real
    /// player-base data is available from a backend.
    /// </summary>
    private EnemyBaseData GenerateEnemyBase()
    {
        EnemyBaseData data = new EnemyBaseData();
        data.baseName = GenerateRandomBaseName();

        // Pick a random world prefab
        data.worldPrefabIndex = (worldPrefabs.Count > 0)
            ? UnityEngine.Random.Range(0, worldPrefabs.Count)
            : 0;

        // Mirror the player's approximate level ± some variance for interest
        int playerLevel = Player.Instance != null ? Player.Instance.getCurrentXPLevel() : 1;
        int variance = UnityEngine.Random.Range(-2, 3);
        data.simulatedXPLevel = Mathf.Max(1, playerLevel + variance);

        // Mirror building counts (randomised slightly)
        if (Player.Instance != null)
        {
            List<int> playerBuildings = Player.Instance.getBuildingCountList();
            foreach (int count in playerBuildings)
            {
                int generated = Mathf.Max(0, count + UnityEngine.Random.Range(-1, 2));
                data.buildingCounts.Add(generated);
            }
        }

        // Calculate lootable resources: simulate a passive income * some time window
        float simulatedPassive = EstimatePassiveFromBuildings(data);
        data.stolenResources = simulatedPassive * 60f * lootPercentage;  // ~60 s worth
        data.stolenCores     = UnityEngine.Random.Range(0, data.simulatedXPLevel / 5 + 1);

        return data;
    }

    private float EstimatePassiveFromBuildings(EnemyBaseData data)
    {
        if (data.buildingCounts.Count == 0) return 100f;
        float total = 0f;
        foreach (int count in data.buildingCounts)
            total += count * 5f;  // rough per-building income estimate
        return Mathf.Max(total, 50f);
    }

    private string GenerateRandomBaseName()
    {
        string[] prefixes = { "Iron", "Storm", "Dark", "Solar", "Void", "Nova", "Ember", "Frost" };
        string[] suffixes = { "Fortress", "Citadel", "Outpost", "Keep", "Bastion", "Hold", "Station" };
        return prefixes[UnityEngine.Random.Range(0, prefixes.Length)] + " "
             + suffixes[UnityEngine.Random.Range(0, suffixes.Length)];
    }

    // ── Attack sequence coroutines ────────────────────────────────────────────

    private IEnumerator AttackSequenceEnter(EnemyBaseData data)
    {
        isAttacking = true;

        // 1. Hide HUD
        SetHUDVisible(false);

        // 2. Remember and slide the player's world to the parked position
        if (worldSpawner != null && worldSpawner.CurrentWorld != null)
        {
            playerWorldOriginalPosition = worldSpawner.transform.position;
            Vector3 parkedPos = playerWorldParkedTransform != null
                ? playerWorldParkedTransform.position
                : playerWorldOriginalPosition + new Vector3(0f, 20f, 0f);

            LeanTween.move(worldSpawner.gameObject, parkedPos, animationDuration)
                     .setEase(enterEase);
        }

        // 3. Spawn and animate the enemy world in from its start position
        if (worldPrefabs.Count > 0)
        {
            GameObject prefab = worldPrefabs[data.worldPrefabIndex];
            Vector3 startPos = enemyWorldStartTransform != null
                ? enemyWorldStartTransform.position
                : transform.position + new Vector3(0f, -30f, 0f);
            Vector3 finalPos = enemyWorldEndTransform != null
                ? enemyWorldEndTransform.position
                : transform.position;

            spawnedEnemyWorldGO = Instantiate(prefab, startPos, Quaternion.identity);

            // Add rotation with random speeds within the configured range
            Rotate rotComp = spawnedEnemyWorldGO.AddComponent<Rotate>();
            rotComp.SetSpeeds(
                UnityEngine.Random.Range(enemyWorldRotateMin.x, enemyWorldRotateMax.x),
                UnityEngine.Random.Range(enemyWorldRotateMin.y, enemyWorldRotateMax.y),
                UnityEngine.Random.Range(enemyWorldRotateMin.z, enemyWorldRotateMax.z)
            );

            // Attach AttackWorldView if the prefab doesn't already have one
            currentAttackWorldView = spawnedEnemyWorldGO.GetComponent<AttackWorldView>()
                                  ?? spawnedEnemyWorldGO.AddComponent<AttackWorldView>();

            // Place buildings on the surface
            SpawnBuildingsOnEnemyWorld(data);
            currentAttackWorldView.Initialise(data);

            // Add WorldAttackTarget to every Collider child of the world sphere so that
            // tapping bare sphere surface routes through Player.targetThis() just like
            // tapping an alien UFO does. Buildings already received it in SpawnSingleBuilding.
            foreach (Collider col in spawnedEnemyWorldGO.GetComponentsInChildren<Collider>())
            {
                if (col.GetComponent<WorldAttackTarget>() == null)
                    col.gameObject.AddComponent<WorldAttackTarget>();
            }

            LeanTween.move(spawnedEnemyWorldGO, finalPos, animationDuration)
                     .setEase(enterEase);
        }

        yield return new WaitForSeconds(animationDuration);

        OnAttackStarted?.Invoke(data);
    }

    private IEnumerator AttackSequenceExit(bool playerWon)
    {
        if (playerWon && currentEnemyBase != null)
            AwardLoot(currentEnemyBase);

        // Slide the enemy world back to its start position
        if (spawnedEnemyWorldGO != null)
        {
            Vector3 exitPos = enemyWorldStartTransform != null
                ? enemyWorldStartTransform.position
                : spawnedEnemyWorldGO.transform.position + new Vector3(0f, -30f, 0f);
            LeanTween.move(spawnedEnemyWorldGO, exitPos, animationDuration)
                     .setEase(exitEase);
        }

        // Slide the player's world back to its original position
        if (worldSpawner != null)
        {
            LeanTween.move(worldSpawner.gameObject, playerWorldOriginalPosition, animationDuration)
                     .setEase(exitEase);
        }

        yield return new WaitForSeconds(animationDuration);

        // Clean up enemy world
        if (spawnedEnemyWorldGO != null)
        {
            Destroy(spawnedEnemyWorldGO);
            spawnedEnemyWorldGO     = null;
            currentAttackWorldView  = null;
        }

        // Restore HUD
        SetHUDVisible(true);

        isAttacking      = false;
        currentEnemyBase = null;

        OnAttackEnded?.Invoke(playerWon);
    }

    // ── Building placement ────────────────────────────────────────────────────

    private void SpawnBuildingsOnEnemyWorld(EnemyBaseData data)
    {
        List<GameObject> structures = worldSpawner != null
            ? worldSpawner.GetStructuresList()
            : new List<GameObject>();
        if (structures == null || structures.Count == 0) return;

        float surfaceRadius = worldSpawner != null ? worldSpawner.GetSurfaceRadius() : 5f;
        Vector3 sphereCenter = spawnedEnemyWorldGO.transform.position;
        SphereCollider sc = spawnedEnemyWorldGO.GetComponentInChildren<SphereCollider>();
        if (sc != null)
        {
            sphereCenter  = sc.bounds.center;
            surfaceRadius = sc.bounds.extents.x;
        }

        float buildingHealth = baseBuildingHealth * Mathf.Max(1, data.simulatedXPLevel * 0.5f);

        // Count total buildings; guarantee a minimum so the enemy world is never empty.
        int totalCount = 0;
        foreach (int c in data.buildingCounts) totalCount += c;

        if (totalCount == 0)
        {
            int minBuildings = UnityEngine.Random.Range(minEnemyBuildingsMin, minEnemyBuildingsMax + 1);
            for (int i = 0; i < minBuildings; i++)
            {
                int typeIdx = UnityEngine.Random.Range(0, structures.Count);
                SpawnSingleBuilding(structures[typeIdx], sphereCenter, surfaceRadius, buildingHealth);
            }
            return;
        }

        for (int typeIdx = 0; typeIdx < data.buildingCounts.Count && typeIdx < structures.Count; typeIdx++)
        {
            for (int n = 0; n < data.buildingCounts[typeIdx]; n++)
                SpawnSingleBuilding(structures[typeIdx], sphereCenter, surfaceRadius, buildingHealth);
        }
    }

    private void SpawnSingleBuilding(GameObject prefab, Vector3 sphereCenter, float surfaceRadius, float health)
    {
        Vector3 worldPos = sphereCenter + UnityEngine.Random.onUnitSphere * surfaceRadius;
        GameObject bgo   = Instantiate(prefab, worldPos, Quaternion.identity);
        bgo.transform.SetParent(spawnedEnemyWorldGO.transform, worldPositionStays: true);
        bgo.transform.LookAt(spawnedEnemyWorldGO.transform.position);
        // Rotate -90° on X so the building's "up" axis points away from the sphere center
        // (matches the same adjustment used in WorldSpawner.spawnOnSurface).
        bgo.transform.Rotate(-90f, 0f, 0f);

        AttackBuildingView abv = bgo.GetComponent<AttackBuildingView>()
                               ?? bgo.AddComponent<AttackBuildingView>();
        abv.Initialise(health, defense: 0f);

        // Ensure the building can receive raycasts — add a small sphere collider
        // if the prefab doesn't already supply any collider.
        if (bgo.GetComponentInChildren<Collider>() == null)
        {
            SphereCollider fallback = bgo.AddComponent<SphereCollider>();
            fallback.radius = 0.5f;
        }

        // Add WorldAttackTarget so tapping this building routes through Player.targetThis()
        // and the existing turret / lazer / missile system fires at it automatically.
        if (bgo.GetComponent<WorldAttackTarget>() == null)
            bgo.AddComponent<WorldAttackTarget>();
    }

    // ── Loot distribution ─────────────────────────────────────────────────────

    private void AwardLoot(EnemyBaseData data)
    {
        if (Player.Instance == null) return;

        Player.Instance.AddDollars(data.stolenResources);
        Player.Instance.addCores(data.stolenCores);

        Debug.Log($"[AttackManager] Loot awarded — ${data.stolenResources:N0}, {data.stolenCores} cores.");
    }

    // ── HUD control ───────────────────────────────────────────────────────────

    private void SetHUDVisible(bool visible)
    {
        if (topBarUI != null)
            topBarUI.SetActive(visible);

        foreach (GameObject btn in shopButtonObjects)
            if (btn != null) btn.SetActive(visible);
    }

    // ── Callbacks ─────────────────────────────────────────────────────────────

    private void HandleBaseDestroyed()
    {
        if (!isAttacking) return;
        StartCoroutine(AttackSequenceExit(playerWon: true));
    }
}
