using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public static EnemySpawner Instance;
    [SerializeField] private Transform leavePoint;
    [SerializeField] private Transform[] attackPoints;

    [SerializeField] private Transform[] attackPointsBelow;

    [SerializeField] private Transform bigAttackPointAbove;

    [SerializeField] private Transform bigAttackPointBelow;
    [SerializeField] private Transform spawnPoint;

    [SerializeField] private EnemyWavesListSO enemyWavesList;

    public delegate void WaveEventHandler(int waveIndex);

    public static event WaveEventHandler OnWaveStarted;
    public static event WaveEventHandler OnWaveCompleted;

    private int currentEnemiesKilled = 0;

    public int currentWave = 0;

    private bool betweenWaves = true;

    private Coroutine coroutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }
    }

    void Start() {
        StartWave();
    }

    public int getEnemiesRemaining() {
        int count = (betweenWaves) ? 0 : countEnemiesInWave(currentWave) - currentEnemiesKilled;
        return count;
    }

    public Transform getAttackPoint() {
        int random = Random.Range(0, 2);
        Transform point = (random == 0) ? bigAttackPointAbove : bigAttackPointBelow;
        return point;
    }

    public void handleAlienDeath() {
        currentEnemiesKilled++;
        if (currentEnemiesKilled == countEnemiesInWave(currentWave)) {
            betweenWaves = true;
            OnWaveCompleted?.Invoke(currentWave + 1);
            currentEnemiesKilled = 0;
            if (currentWave + 1 < enemyWavesList.wavesList.Length) {
                currentWave++;
                StartWave();
            }
        }
    }
    
    public void DeleteChildrenStartingWithUFO()
    {
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("ufo", System.StringComparison.OrdinalIgnoreCase))
            {
                Destroy(child.gameObject);
            }
        }
    }

    public void prestige()
    {
        StopCoroutine(coroutine);
        DeleteChildrenStartingWithUFO();
        currentEnemiesKilled = 0;
        currentWave = 0;
        StartWave();
    }

    private int countEnemiesInWave(int wave)
    {
        int count = 0;
        foreach (EnemyInfo ef in enemyWavesList.wavesList[wave].enemyInfoList)
        {
            count += ef.numberOfEnemies;
        }
        return count;
    }

    private void spawnEnemy(GameObject prefab)
    {
        GameObject enemy = Instantiate(prefab, spawnPoint);
        enemy.transform.SetParent(this.transform, true);
    }

    public void StartWave()
    {
        coroutine = StartCoroutine(SpawnWave(enemyWavesList.wavesList[currentWave]));
    }

    private IEnumerator SpawnWave(Wave wave)
    {
        yield return new WaitForSeconds(enemyWavesList.wavesList[currentWave].waveDelay);
        betweenWaves = false;
        OnWaveStarted?.Invoke(currentWave + 1);
        foreach (var enemyInfo in wave.enemyInfoList)
        {
            for (int i = 0; i < enemyInfo.numberOfEnemies; i++)
            {
                spawnEnemy(enemyInfo.enemyPrefab);
                yield return new WaitForSeconds(wave.spawnRate);
            }
        }
    }

    public Transform[] getAttackPath() {
        // Choose a random attack path
        int random = Random.Range(0, 2); // 0 or 1
        Transform[] selectedPath = (random == 0) ? attackPoints : attackPointsBelow;

        // Create a new array to store the randomized order
        Transform[] randomizedPath = new Transform[selectedPath.Length];
        List<int> availableIndices = new List<int>();

        // Fill the list with available indices
        for (int i = 0; i < selectedPath.Length; i++) {
            availableIndices.Add(i);
        }

        // Assign elements in random order
        for (int i = 0; i < selectedPath.Length; i++) {
            int randomIndex = Random.Range(0, availableIndices.Count);
            randomizedPath[i] = selectedPath[availableIndices[randomIndex]];
            availableIndices.RemoveAt(randomIndex); // Remove the used index
        }

        return randomizedPath;
    }

    public Transform getLeavePoint() {
        return leavePoint;
    }
}
