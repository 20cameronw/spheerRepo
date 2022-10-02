using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "New Research Item", menuName = "Research")]
public class Research : ScriptableObject
{
    public new string name;
    public string bonus;
    public int upgradeIndex;
    public Sprite Icon;
    public int cost;
}
