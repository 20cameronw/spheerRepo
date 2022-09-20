using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Shop Panel Item List", menuName = "List of type Shop Items and Cards")]
public class ShopItemsListSO : ScriptableObject
{
    public Upgrade[] shopItemsSO;
    public GameObject cardTemplate;
}
