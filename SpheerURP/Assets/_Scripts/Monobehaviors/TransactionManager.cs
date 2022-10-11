using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransactionManager : MonoBehaviour
{
    public static TransactionManager Instance;

    [Header("Multipliers")]
    [Range(0, 4)]
    [SerializeField] private float purchaseCostIncreaseMultiplier;
    
    [Range(0, 4)]
    [SerializeField] private float researchCostIncreaseMultiplier;

    [Range(0, 2)]
    [SerializeField] private float sellBackMultiplier;

    [Space(10)]
    [Header("Scriptable Object Lists")]
    public ShopItemsListSO structuresPanelInfo;

    public ResearchItemsListSO researchPanelInfo;

    [Space(10)]
    [Header("Setup References")]
    [SerializeField] private WorldSpawner worldSpawner;

    [SerializeField] private ShopPanel structuresPanel;

    [SerializeField] private ResearchPanel researchPanel;

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

    public void PurchaseResearch(int upgradeIndex)
    {
        Debug.Log(upgradeIndex);
        Player.Instance.addResearchCount(upgradeIndex);

        //hard coding the effects for each research item
        switch (upgradeIndex)
        {
            case 0:
                Player.Instance.resetPower();
                break;
            case 1:
                sellBackMultiplier += .1f;
                break;
            default:
                Debug.Log("No effect coded in for this research");
                break;
        }
        researchPanel.LoadCards();
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

    public float getCostOfResearchUpgrade(int index)
    {
        int countPurchased = Player.Instance.getResearchCount(index);
        float baseCost = researchPanelInfo.researchItemsSO[index].cost;
        for (int i = 0; i < countPurchased; i++)
        {
            baseCost *= researchCostIncreaseMultiplier;
        }
        return baseCost;
    }
}
