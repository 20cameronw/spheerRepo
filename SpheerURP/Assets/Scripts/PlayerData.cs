using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerData
{
    public float dollars;

    public PlayerData(Player player)
    {
        dollars = Player.Instance.getDollars();
    }
}
