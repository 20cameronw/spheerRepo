using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New World", menuName = "World")]
public class World : ScriptableObject
{
    public new string name;
    public string description;
    public float cost;
    public Sprite Icon;
    public int index;

    public int cores;

    // Maximum number of building slots available on this world's surface.
    // Bigger/higher-tier worlds should have more slots.
    public int maxBuildingSlots = 20;

}
