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
    // TransactionManager checks electricityCapacity - electricityUsed >= this value.
    // Windmill buildings contribute to electricityCapacity via ResourceType.Electricity.
    public float electricityRequired = 0f;

    // Phase 3: Minimum Town Hall level required to place this building.
    // Leave 0 (default) for buildings available from the start.
    public int requiredTownHallLevel = 0;

    // Phase 3: Mark this building as a Town Hall.
    // Each placed Town Hall increments Player.townHallLevel by 1,
    // unlocking buildings whose requiredTownHallLevel matches or exceeds the new level.
    public bool isTownHall = false;

    public void Print()
    {
        Debug.Log(name + ": " + description + "\nCosts: " + cost);
    }
}
