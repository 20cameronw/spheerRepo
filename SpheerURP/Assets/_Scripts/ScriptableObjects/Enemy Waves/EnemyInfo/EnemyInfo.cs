using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Enemy Info", menuName = "Enemy info")]
public class EnemyInfo : ScriptableObject
{
    public GameObject enemyPrefab;

    public int numberOfEnemies;
}
