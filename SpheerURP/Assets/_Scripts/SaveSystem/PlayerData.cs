using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[System.Serializable]
public class PlayerData
{
    public float dollars;

    public List<int> buildingCount;

    public List<int> researchCount;
    public int currentWorld;

    public int currentXPLevel;
    public int currentXP;

    public int cores;

    public int currentWave;

    public int darkMatter;

    public DateTime saveTime;



    public PlayerData(Player player)
    {
        dollars = Player.Instance.getDollars();
        buildingCount = Player.Instance.getBuildingCountList();
        researchCount = Player.Instance.getResearchCount();
        currentWorld = Player.Instance.getCurrentWorld();
        currentXPLevel = Player.Instance.getCurrentXPLevel();
        currentXP = Player.Instance.getCurrentXP();
        cores = Player.Instance.getCores();
        currentWave = EnemySpawner.Instance.currentWave;
        darkMatter = Player.Instance.getDarkMatter();
        saveTime = Player.Instance.now();
    }
}
