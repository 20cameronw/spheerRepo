using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public static EnemySpawner Instance;
    [SerializeField] private Transform leavePoint;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private Transform spawnPoint;

    [SerializeField] private List<GameObject> enemyPrefabs;

    private GameObject enemy;


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

        InvokeRepeating("spawnEnemyIfNotExists", 5f, 5f);
    }

    private void spawnEnemy()
    {
        enemy = Instantiate(enemyPrefabs[0], spawnPoint);
        enemy.transform.SetParent(this.transform, true);
    }


    private void spawnEnemyIfNotExists()
    {
        if (enemy == null && Player.Instance.getCurrentXPLevel() > 3)
        {
            spawnEnemy();
        }
    }




}
