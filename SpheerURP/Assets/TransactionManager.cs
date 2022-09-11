using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransactionManager : MonoBehaviour
{
    public static TransactionManager Instance;

    [SerializeField] private float purchaseCostIncreaseMultiplier;

    [SerializeField] private ShopItemsListSO structuresPanelInfo;

    [SerializeField] private WorldSpawner worldSpawner;

    [SerializeField] private List<GameObject> structuresGOList;

    [SerializeField] private ShopPanel structuresPanel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(this);
        else
            Instance = this;

    }

    public void PurchaseSomething(int index)
    {
        float cost = getCostOfUpgradeStructure(index);
        if (cost > Player.Instance.getDollars()) return;
        Player.Instance.AddDollars(-cost);
        Player.Instance.AddBuildingCount(index);
        Player.Instance.AddPassive(structuresPanelInfo.shopItemsSO[index].bonus);
        worldSpawner.spawnObject(structuresGOList[index]);
        structuresPanel.LoadCards();
    }

    public float getCostOfUpgradeStructure(int index)
    {
        int numberBuildings = Player.Instance.getNumberBuildings(index);
        float baseCost = structuresPanelInfo.shopItemsSO[index].cost;
        for (int i = 0; i < numberBuildings; i++)
        {
            baseCost *= purchaseCostIncreaseMultiplier;
        }
        return baseCost;
    }
}
