using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerData
{
    public float dollars;

    public List<int> buildingCount;

    public List<int> researchCount;



    public PlayerData(Player player)
    {
        dollars = Player.Instance.getDollars();
        buildingCount = Player.Instance.getBuildingCountList();
        researchCount = Player.Instance.getResearchCount();
    }
}
