using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New World List", menuName = "List of Worlds Info")]
public class WorldsListSO : ScriptableObject
{
    public World[] worldsList;
    public GameObject cardTemplate;
}
