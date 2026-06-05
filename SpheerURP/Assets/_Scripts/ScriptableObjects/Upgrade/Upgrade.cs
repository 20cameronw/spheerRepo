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

    // How many surface slots this building occupies when placed.
    // Larger/more powerful buildings should cost more slots.
    public int slotSize = 1;

    // Which resource this building produces each second.
    // Defaults to Nebulite so existing assets remain compatible without modification.
    public ResourceType resourceProduced = ResourceType.Nebulite;

    // How much Electricity (capacity) this building requires to operate.
    // Set > 0 for advanced buildings that need power from Windmills.
    // Phase 3 will enforce this gate; wired here so assets can be configured now.
    public float electricityRequired = 0f;

    public void Print()
    {
        Debug.Log(name + ": " + description + "\nCosts: " + cost);
    }
}
