using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerData
{
    public float dollars;

    public List<int> buildingCount;

    public List<int> researchCount;

    public int currentWorld;

    public int currentXPLevel;
    public int currentXP;

    public PlayerData(Player player)
    {
        dollars = Player.Instance.getDollars();
        buildingCount = Player.Instance.getBuildingCountList();
        researchCount = Player.Instance.getResearchCount();
        currentWorld = Player.Instance.getCurrentWorld();
        currentXPLevel = Player.Instance.getCurrentXPLevel();
        currentXP = Player.Instance.getCurrentXP();
    }
}
