using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "New Upgrade", menuName = "Upgrade")]
public class Upgrade : ScriptableObject
{
    public new string name;
    public string description;
    public int upgradeIndex;
    public Sprite Icon;
    public int cost;
    public int health;
    public float bonus;

    public bool isInOrbit;

    public int requiredXPLevel;

    public void Print()
    {
        Debug.Log(name + ": " + description + "\nCosts: " + cost);
    }
}
