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

    [SerializeField] private StructuresPanel structuresPanel;

    [SerializeField] private ResearchPanel researchPanel;

    [SerializeField] private UIManager uIManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(this);
        else
            Instance = this;

    }

    public bool PurchaseWorld(int index)
    {
        if (Player.Instance.getDollars() >= worldsPanelInfo.worldsList[index].cost && index > Player.Instance.getCurrentWorld())
        {
            worldSpawner.SetCurrentWorld(index);
            Player.Instance.setCurrentWorld(index);
            Player.Instance.resetBuildingCount();
            Player.Instance.resetResearchCount();
            Player.Instance.resetDollars();
            Player.Instance.addCores(worldsPanelInfo.worldsList[index-1].cores);
            structuresPanel.LoadCards();
            researchPanel.LoadCards();
            return true;
        }
        return false;
    }

    public bool PurchaseSomething(int index)
    {
        float passiveEarnings = structuresPanelInfo.shopItemsSO[index].bonus;
        float cost = getCostOfUpgradeStructure(index);

        bool isOrbit = structuresPanelInfo.shopItemsSO[index].isInOrbit;

        if (isOrbit)
        {
            // Orbit buildings: check funds and place immediately.
            if (cost > Player.Instance.getDollars()) return false;

            AudioManager.Instance.Play("Place Building");
            Player.Instance.AddDollars(-cost);
            Player.Instance.AddBuildingCount(index);
            worldSpawner.spawnInOrbit(index, passiveEarnings);
            Player.Instance.AddPassive(passiveEarnings);
            structuresPanel.LoadCards();

            string message = "-" + cost.ToString("F2");
            uIManager.CreateAnimatedText(message, Color.red, 1f);
        }
        else
        {
            // Surface buildings: enter placement mode WITHOUT deducting money yet.
            // Funds are checked and deducted only when the player confirms.
            if (cost > Player.Instance.getDollars())
            {
                PopupManager.Instance.ShowPopup("Not enough money to buy this building.");
                return false;
            }

            int slotSize = Mathf.Max(1, structuresPanelInfo.shopItemsSO[index].slotSize);
            if (worldSpawner.GetSlotsAvailable() < slotSize)
            {
                PopupManager.Instance.ShowPopup("Not enough building slots on this world. Some worlds have more space.");
                return false;
            }

            if (PlacementManager.Instance == null)
            {
                Debug.LogWarning("[TransactionManager] PlacementManager is missing in scene.");
                PopupManager.Instance.ShowPopup("Building placement is unavailable. Please ensure PlacementManager is configured in this scene.");
                return false;
            }

            // Enter placement mode — money is only taken when the player confirms.
            PlacementManager.Instance.EnterPlacementMode(index);
        }

        return true;
    }

    public void PurchaseResearch(int upgradeIndex, bool isEpic)
    {
        float cost = getCostOfResearchUpgrade(upgradeIndex);

        // Enforce XP level requirement for common (non-epic) research
        if (!isEpic)
        {
            int required = researchPanelInfo.researchItemsSO[upgradeIndex].requiredXPLevel;
            if (Player.Instance.getCurrentXPLevel() < required)
            {
                Debug.Log("Player XP level too low for this research (requires " + required + ")");
                return;
            }
        }

        if (isEpic) {
            if (Player.Instance.getCores() < cost)
            {
                Debug.Log("Not enough cores to purchase research");
                return;
            }
            Player.Instance.addResearchCount(upgradeIndex, false);
            Player.Instance.addCores((int)-cost);
        } else {
            if (Player.Instance.getDollars() < cost)
            {
                Debug.Log("Not enough dollars to purchase research");
                return;
            }
            Player.Instance.addResearchCount(upgradeIndex, false);
            Player.Instance.AddDollars(-cost);
        }
        researchPanel.LoadCards();
    }

    private float getSellBackRate()
    {
        return sellBackMultiplier + Player.Instance.getSellBackMultiplier();
    }

    public void SellStructure(int index)
    {
        int numberBuildings = Player.Instance.getNumberBuildings(index);
        if (numberBuildings <= 0)
        {
            return;
        }

        Player.Instance.removeUpgrade(index);
        worldSpawner.removeObject(index);
        float add = getCostOfUpgradeStructure(index) * sellBackMultiplier;
        Player.Instance.AddDollars(add);
        structuresPanel.LoadCards();
        string message = "+" + add.ToString("F2");
        uIManager.CreateAnimatedText(message, Color.green, 1f);
        Debug.Log("Sold structure");
    }

    public float getCostOfUpgradeStructure(int index)
    {
        int numberBuildings = Player.Instance.getNumberBuildings(index);
        return GetCostAtBuildingCount(index, numberBuildings);
    }

    /// <summary>
    /// Returns the purchase cost of building <paramref name="index"/> assuming the player
    /// already owns exactly <paramref name="count"/> of them.  Used by PlacementManager to
    /// preview the incremental cost of each additional slot selection.
    /// </summary>
    public float GetCostAtBuildingCount(int index, int count)
    {
        float baseCost = structuresPanelInfo.shopItemsSO[index].cost;
        for (int i = 0; i < count; i++)
            baseCost *= purchaseCostIncreaseMultiplier;
        return baseCost;
    }

    public float getCostOfResearchUpgrade(int index)
    {
        int countPurchased = Player.Instance.getResearchCount(index);
        float baseCost = researchPanelInfo.researchItemsSO[index].cost;
        for (int i = 0; i < countPurchased; i++)
        {
            baseCost = (int)(baseCost * researchCostIncreaseMultiplier);
        }
        
        if (!researchPanelInfo.researchItemsSO[index].isEpic)
        {
            baseCost *= Player.Instance.getResearchDiscount();
        }
        
        return baseCost;
    }
}
