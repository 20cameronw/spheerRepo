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

}
