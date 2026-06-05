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

    public int darkMatter;

    // Phase 2 — Multi-resource pools
    public float plasma;
    public float electricity;
    public float voidCrystal;

    public DateTime saveTime;

    // Lifetime stats (never reset on prestige)
    public int lifetimeTotalEnemiesKilled;
    public int lifetimeTotalWavesCompleted;
    public float lifetimeTotalMoneyEarned;
    public int lifetimeHighestWave;
    public int lifetimePrestigeCount;
    public float lifetimeRecordPeakPassive;
    public int lifetimeRecordHighestXPLevel;
    public List<int> completedMissionIndices;
    public bool hasSeenTutorial;



    public PlayerData(Player player)
    {
        dollars = Player.Instance.getDollars();
        buildingCount = Player.Instance.getBuildingCountList();
        researchCount = Player.Instance.getResearchCount();
        currentWorld = Player.Instance.getCurrentWorld();
        currentXPLevel = Player.Instance.getCurrentXPLevel();
        currentXP = Player.Instance.getCurrentXP();
        cores = Player.Instance.getCores();
        darkMatter = Player.Instance.getDarkMatter();
        plasma      = Player.Instance.getPlasma();
        electricity = Player.Instance.getElectricity();
        voidCrystal = Player.Instance.getVoidCrystal();
        saveTime = Player.Instance.now();

        lifetimeTotalEnemiesKilled = Player.Instance.getLifetimeEnemiesKilled();
        lifetimeTotalWavesCompleted = Player.Instance.getLifetimeWavesCompleted();
        lifetimeTotalMoneyEarned = Player.Instance.getLifetimeTotalMoneyEarned();
        lifetimeHighestWave = Player.Instance.getLifetimeHighestWave();
        lifetimePrestigeCount = Player.Instance.getLifetimePrestigeCount();
        lifetimeRecordPeakPassive = Player.Instance.getLifetimeRecordPeakPassive();
        lifetimeRecordHighestXPLevel = Player.Instance.getLifetimeRecordHighestXPLevel();
        completedMissionIndices = Player.Instance.getCompletedMissionIndices();
        hasSeenTutorial = Player.Instance.getHasSeenTutorial();
    }
}
