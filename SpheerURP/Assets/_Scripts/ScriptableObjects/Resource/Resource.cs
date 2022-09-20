using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Resource", menuName = "Resource")]
public class Resource : ScriptableObject
{
    public new string name;
    public string description;

    public float cost;
    public int gemsCost;
    public float clickerBonus;
    public float bonus;

    public Sprite Icon;

    public int index;

    public Vector3 rotation;

    public void Print()
    {
        Debug.Log(name + ": " + description + "\nCosts: " + gemsCost + " gems");
    }

}
