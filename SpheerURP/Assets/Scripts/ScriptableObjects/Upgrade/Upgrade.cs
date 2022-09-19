using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "New Upgrade", menuName = "Upgrade")]
public class Upgrade : ScriptableObject
{
    public string name;
    public string description;
    public int upgradeIndex;
    public Sprite Icon;
    public int cost;
    public int health;
    public float bonus;

    public void Print()
    {
        Debug.Log(name + ": " + description + "\nCosts: " + cost);
    }
}
