using UnityEngine;

[CreateAssetMenu(fileName = "New Missions List", menuName = "List of Missions")]
public class MissionsListSO : ScriptableObject
{
    public MissionSO[] missions;
}
