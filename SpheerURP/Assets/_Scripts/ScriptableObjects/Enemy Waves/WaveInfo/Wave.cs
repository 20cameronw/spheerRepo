using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Enemy Wave", menuName = "Enemy Wave info")]
public class Wave : ScriptableObject
{
    public float spawnRate;

    public float waveDelay;
    public List<EnemyInfo> enemyInfoList;
    
}
