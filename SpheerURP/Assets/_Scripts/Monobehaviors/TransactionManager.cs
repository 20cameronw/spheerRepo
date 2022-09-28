using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransactionManager : MonoBehaviour
{
    public static TransactionManager Instance;

    [SerializeField] private float purchaseCostIncreaseMultiplier;

    public ShopItemsListSO structuresPanelInfo;

    [SerializeField] private WorldSpawner worldSpawner;

    [SerializeField] private ShopPanel structuresPanel;

    [Range(0, 2)]
    [SerializeField] private float sellBackMultiplier;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(this);
        else
            Instance = this;

    }

    public void PurchaseSomething(int index)
    {
        float passiveEarnings = structuresPanelInfo.shopItemsSO[index].bonus;
        float cost = getCostOfUpgradeStructure(index);
        if (cost > Player.Instance.getDollars()) return;
        Player.Instance.AddDollars(-cost);
        Player.Instance.AddBuildingCount(index);
        
        if (structuresPanelInfo.shopItemsSO[index].isInOrbit)
            worldSpawner.spawnInOrbit(index, passiveEarnings);
        else
            worldSpawner.spawnObject(index, passiveEarnings);

        structuresPanel.LoadCards();
    }

    public void SellStructure(int index)
    {
        int numberBuildings = Player.Instance.getNumberBuildings(index);
        if (numberBuildings == 0)
            return;
        
        Player.Instance.minusBuildingCount(index);
        worldSpawner.removeObject(index);
        Player.Instance.AddDollars(getCostOfUpgradeStructure(index) * sellBackMultiplier);
        structuresPanel.LoadCards();
        Debug.Log("Sold structure");

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
