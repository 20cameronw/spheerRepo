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

    public WorldsListSO worldsPanelInfo;

    [Space(10)]
    [Header("Setup References")]
    [SerializeField] private WorldSpawner worldSpawner;

    [SerializeField] private ShopPanel structuresPanel;

    [SerializeField] private ResearchPanel researchPanel;

    private AudioManager audioManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(this);
        else
            Instance = this;

        audioManager = FindObjectOfType<AudioManager>();

    }

    public void PurchaseWorld(int index)
    {
        if (Player.Instance.getDollars() >= worldsPanelInfo.worldsList[index].cost && index > Player.Instance.getCurrentWorld())
        {
            Player.Instance.AddDollars(-worldsPanelInfo.worldsList[index].cost);
            worldSpawner.SetCurrentWorld(index);
            Player.Instance.setCurrentWorld(index);
            Player.Instance.resetBuildingCount();
            Player.Instance.addCores(worldsPanelInfo.worldsList[Player.Instance.getCurrentWorld()].cores);
        }
    }

    public bool PurchaseSomething(int index)
    {
        float passiveEarnings = structuresPanelInfo.shopItemsSO[index].bonus;
        float cost = getCostOfUpgradeStructure(index);
        if (cost > Player.Instance.getDollars()) return false;
        audioManager.Play("Place Building");
        Player.Instance.AddDollars(-cost);
        Player.Instance.AddBuildingCount(index);

        if (structuresPanelInfo.shopItemsSO[index].isInOrbit)
            worldSpawner.spawnInOrbit(index, passiveEarnings);
        else
            worldSpawner.spawnObject(index, passiveEarnings);

        structuresPanel.LoadCards();

        return true;
    }

    public void PurchaseResearch(int upgradeIndex)
    {
        int cost = getCostOfResearchUpgrade(upgradeIndex);
        if (Player.Instance.getCores() < cost)
        {
            Debug.Log("Not enough cores to purchase research");
            return;
        }
        Player.Instance.addResearchCount(upgradeIndex, false);
        Player.Instance.addCores(-cost);

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

    public int getCostOfResearchUpgrade(int index)
    {
        int countPurchased = Player.Instance.getResearchCount(index);
        int baseCost = researchPanelInfo.researchItemsSO[index].cost;
        for (int i = 0; i < countPurchased; i++)
        {
            baseCost = (int)(baseCost * researchCostIncreaseMultiplier);
        }
        return baseCost;
    }
}
