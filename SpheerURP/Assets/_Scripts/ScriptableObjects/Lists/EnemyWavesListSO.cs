using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Enemy Waves List", menuName = "List of Enemy Waves info")]
public class EnemyWavesListSO : ScriptableObject
{
    public Wave[] wavesList;

}

