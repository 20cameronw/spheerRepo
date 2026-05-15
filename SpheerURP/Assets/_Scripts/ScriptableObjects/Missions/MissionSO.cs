using UnityEngine;

public enum MissionType
{
    ReachWave,
    EarnTotalMoney,
    KillTotalEnemies,
    CompleteWaves,
    ReachXPLevel,
    PurchaseBuildings,
    CompleteResearch,
    ReachPassiveIncome,
    Prestige
}

[CreateAssetMenu(fileName = "New Mission", menuName = "Mission")]
public class MissionSO : ScriptableObject
{
    public new string name;
    [TextArea] public string description;
    public MissionType type;
    public float targetValue;
    public Sprite icon;
    public string rewardDescription;
}
