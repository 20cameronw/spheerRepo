using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public static EnemySpawner Instance;

    [Header("World Reference")]
    [SerializeField] private Transform worldCenter;

    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject[] smallEnemyPrefabs;
    [SerializeField] private GameObject[] bigEnemyPrefabs;

    [Header("Distances")]
    [SerializeField] private float spawnOffScreenRadius = 28f;
    [SerializeField] private float stagingRadius        = 14f;
    [SerializeField] private float attackYOffset        = 5f;
    [SerializeField] private float attackSweepWidth     = 8f;
    [SerializeField] private float leaveDistance        = 32f;

    [Header("Wave Scaling")]
    [SerializeField] private float baseEnemyCount      = 2f;
    [SerializeField] private float enemiesPerWave      = 0.5f;
    [SerializeField] private float enemiesPerXPLevel   = 0.3f;
    [SerializeField] private float incomeScaleDivisor  = 10000f;
    [SerializeField] private float baseSpawnRate       = 2f;
    [SerializeField] private float minSpawnRate        = 0.4f;
    [SerializeField] private float baseWaveDelay       = 5f;
    [SerializeField] private float minWaveDelay        = 2f;

    public delegate void WaveEventHandler(int waveIndex);
    public static event WaveEventHandler OnWaveStarted;
    public static event WaveEventHandler OnWaveCompleted;

    private int currentEnemiesKilled   = 0;
    public  int currentWave            = 0;
    private bool betweenWaves          = true;
    private int  currentWaveEnemyCount = 0;
    private Coroutine waveCoroutine;

    private Vector3 Center => worldCenter != null ? worldCenter.position : Vector3.zero;

    void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;
    }

    void Start()
    {
        StartWave();
    }

    public int getEnemiesRemaining()
    {
        return betweenWaves ? 0 : currentWaveEnemyCount - currentEnemiesKilled;
    }

    // ── Procedural position helpers ──────────────────────────────────────────

    /// <summary>Off-screen spawn point. ySide = +1 above, -1 below.</summary>
    public Vector3 GetOffScreenSpawnPoint(float ySide)
    {
        return EnemyPathGenerator.GenerateOffScreenSpawnPoint(Center, spawnOffScreenRadius, ySide);
    }

    /// <summary>Neutral staging area near the planet perimeter (Y ≈ 0).</summary>
    public Vector3 GetStagingPoint()
    {
        return EnemyPathGenerator.GenerateStagingPoint(Center, stagingRadius);
    }

    /// <summary>
    /// Horizontal sweep path across the top (+yOffset) or bottom (-yOffset) of the planet.
    /// </summary>
    public Vector3[] GetAttackSweepPath(float yOffset)
    {
        return EnemyPathGenerator.GenerateAttackSweepPath(Center, 3, attackSweepWidth, yOffset);
    }

    /// <summary>The Y offset used for attack positions (positive = above).</summary>
    public float AttackYOffset => attackYOffset;

    /// <summary>Point far from the planet that the enemy retreats to.</summary>
    public Vector3 GetDynamicLeavePoint()
    {
        return EnemyPathGenerator.GenerateLeavePoint(Center, leaveDistance);
    }

    // ── Wave lifecycle ───────────────────────────────────────────────────────

    public void handleAlienDeath()
    {
        currentEnemiesKilled++;
        if (currentEnemiesKilled >= currentWaveEnemyCount)
        {
            betweenWaves = true;
            OnWaveCompleted?.Invoke(currentWave + 1);
            currentEnemiesKilled = 0;
            currentWave++;
            StartWave();
        }
    }

    public void DeleteChildrenStartingWithUFO()
    {
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("ufo", System.StringComparison.OrdinalIgnoreCase))
                Destroy(child.gameObject);
        }
    }

    public void prestige()
    {
        if (waveCoroutine != null) StopCoroutine(waveCoroutine);
        DeleteChildrenStartingWithUFO();
        currentEnemiesKilled = 0;
        currentWave = 0;
        StartWave();
    }

    /// <summary>
    /// Suspend all wave activity while an enemy-base attack is in progress.
    /// All active UFOs are immediately removed so the screen is clear during the assault.
    /// </summary>
    public void PauseForAttack()
    {
        if (waveCoroutine != null) { StopCoroutine(waveCoroutine); waveCoroutine = null; }
        DeleteChildrenStartingWithUFO();
        betweenWaves = true;
        // Clear any UFO target the player may have had
        if (Player.Instance != null) Player.Instance.ClearTarget();
    }

    /// <summary>
    /// Resume wave spawning after an enemy-base attack ends.
    /// </summary>
    public void ResumeFromAttack()
    {
        StartWave();
    }

    // ── Procedural wave generation ───────────────────────────────────────────

    private struct WaveData
    {
        public List<(GameObject prefab, int count)> enemies;
        public float spawnRate;
        public float waveDelay;
    }

    private WaveData GenerateWave(int wave)
    {
        int   xpLevel     = Player.Instance != null ? Player.Instance.getCurrentXPLevel() : 0;
        float income      = Player.Instance != null ? Player.Instance.getPassive()        : 0f;
        float incomeScale = Mathf.Log10(Mathf.Max(income, 1f) / Mathf.Max(incomeScaleDivisor, 1f) + 1f);

        int totalEnemies = Mathf.Max(1, Mathf.RoundToInt(
            baseEnemyCount
            + (wave    * enemiesPerWave)
            + (xpLevel * enemiesPerXPLevel)
            + (incomeScale * 2f)));

        float bigRatio = Mathf.Clamp01((wave / 20f) + (incomeScale * 0.2f));
        int   bigCount   = Mathf.RoundToInt(totalEnemies * bigRatio);
        int   smallCount = totalEnemies - bigCount;

        float spawnRate = Mathf.Max(minSpawnRate, baseSpawnRate - (wave * 0.04f));
        float waveDelay = Mathf.Max(minWaveDelay, baseWaveDelay  - (wave * 0.08f));

        var data = new WaveData
        {
            enemies   = new List<(GameObject, int)>(),
            spawnRate = spawnRate,
            waveDelay = waveDelay
        };

        if (smallCount > 0 && smallEnemyPrefabs != null && smallEnemyPrefabs.Length > 0)
            data.enemies.Add((smallEnemyPrefabs[wave % smallEnemyPrefabs.Length], smallCount));

        if (bigCount > 0 && bigEnemyPrefabs != null && bigEnemyPrefabs.Length > 0)
            data.enemies.Add((bigEnemyPrefabs[wave % bigEnemyPrefabs.Length], bigCount));

        if (data.enemies.Count == 0)
        {
            if (smallEnemyPrefabs != null && smallEnemyPrefabs.Length > 0)
                data.enemies.Add((smallEnemyPrefabs[0], totalEnemies));
            else if (bigEnemyPrefabs != null && bigEnemyPrefabs.Length > 0)
                data.enemies.Add((bigEnemyPrefabs[0], totalEnemies));
        }

        return data;
    }

    private void spawnEnemy(GameObject prefab)
    {
        // Spawn at the spawner's position; the enemy's ApproachingState.Awake will
        // immediately reposition it to the correct off-screen spawn point.
        GameObject enemy = Instantiate(prefab, transform.position, Quaternion.identity);
        enemy.transform.SetParent(this.transform, true);
    }

    public void StartWave()
    {
        waveCoroutine = StartCoroutine(SpawnWave(currentWave));
    }

    private IEnumerator SpawnWave(int wave)
    {
        WaveData data = GenerateWave(wave);

        currentWaveEnemyCount = 0;
        foreach (var (_, count) in data.enemies)
            currentWaveEnemyCount += count;

        if (currentWaveEnemyCount == 0)
        {
            currentWave++;
            StartWave();
            yield break;
        }

        yield return new WaitForSeconds(data.waveDelay);
        betweenWaves = false;
        OnWaveStarted?.Invoke(wave + 1);

        foreach (var (prefab, count) in data.enemies)
        {
            for (int i = 0; i < count; i++)
            {
                if (prefab != null)
                    spawnEnemy(prefab);
                yield return new WaitForSeconds(data.spawnRate);
            }
        }
    }
}
