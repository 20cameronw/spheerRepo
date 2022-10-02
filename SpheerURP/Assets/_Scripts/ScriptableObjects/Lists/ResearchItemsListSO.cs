using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Research Item List", menuName = "List of Research Items")]
public class ResearchItemsListSO : ScriptableObject
{
    public Research[] researchItemsSO;
    public GameObject cardTemplate;
}

