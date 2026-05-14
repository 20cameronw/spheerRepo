using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Player : MonoBehaviour
{
    public static Player Instance; //this class is a singleton

    private float dollarsGainedThisSecond;


    [Header("Setup References")]
    [SerializeField] private WorldSpawner worldSpawner;

    [SerializeField] private ShopItemsListSO shopItemsList;

    [SerializeField] private ResearchItemsListSO researchInfo;

    [Space(10)]
    [Header("Modifiable Data")]
    [SerializeField] private bool RWFileData;
    [SerializeField] private float dollars;
    [SerializeField] private float passive;
    [SerializeField] private int cores;
    [SerializeField] private List<int> buildingCount;
    [SerializeField] private List<int> researchCount;

    [SerializeField] private int currentWorld;
    [SerializeField] private float maxHealth;
    [SerializeField] private int currentXPLevel;

    [SerializeField] private int currentXP;
    [SerializeField] private float power = 1;
    [SerializeField] private float productionRateMultiplier = 1;
    [SerializeField] private float xpModifier = 1;

    [SerializeField] private float researchDiscount = 1;
    [SerializeField] private float turretDamageMultiplier = 1;
    [SerializeField] private float turretFireRateMultiplier = 1;
    [SerializeField] private float turretRangeMultiplier = 1;
    [SerializeField] private float sellBackMultiplier = 0;

    [SerializeField] private UIManager uIManager;

    public Transform target;

    public static event System.Action<Transform> OnTargetChanged;

    public void targetThis(Transform target)
    {
        this.target = target;
        OnTargetChanged?.Invoke(target);
    }

    public Transform GetTarget()
    {
        return target;
    }

    public void ClearTarget()
    {
        target = null;
        OnTargetChanged?.Invoke(null);
    }

    public float getTurretRangeMultiplier()
    {
        return turretRangeMultiplier;
    }

    public float getSellBackMultiplier()
    {
        return sellBackMultiplier;
    }

    public float getTurretFireRateMultiplier()
    {
        return turretFireRateMultiplier;
    }

    public float getTurretDamageMultiplier()
    {
        return turretDamageMultiplier;
    }

    public void levelUpXP()
    {
        currentXP = 0;
        currentXPLevel++;
        FindObjectOfType<ShopPanel>()?.LoadCards();
    }

    public float getPower()
    {
        return power;
    }

    public void addXpPoints(int amount)
    {
        currentXP += (int)(amount * xpModifier);
    }

    public int getCurrentXP()
    {
        return currentXP;
    }

    public int getCurrentXPLevel()
    {
        return currentXPLevel;
    }

    public float getDollars()
    {
        return dollars;
    }

    public float getDollarsGainedThisSecond()
    {
        return dollarsGainedThisSecond;
    }

    public float getPassive()
    {
        return passive;
    }

    public void AddDollars(float amount)
    {
        dollars += amount;
    }

    public void AddBuildingCount(int index)
    {
        buildingCount[index]++;
    }

    public void minusBuildingCount(int index)
    {
        buildingCount[index]--;
    }

    public void AddPassive(float bonus)
    {
        passive += bonus;
    }

    public List<int> getBuildingCountList()
    {
        return buildingCount;
    }

    public int getNumberBuildings(int index)
    {
        return buildingCount[index];
    }

    public int getResearchCount(int index)
    {
        return researchCount[index];
    }

    public List<int> getResearchCount()
    {
        return researchCount;
    }

    public int getCurrentWorld()
    {
        return currentWorld;
    }

    public void setCurrentWorld(int index)
    {
        currentWorld = index;
    }

    public int getCores()
    {
        return cores;
    }

    public float getResearchDiscount()
    {
        return researchDiscount;
    }

    private bool autoTargeting = false;

    private float junkMultiplier = 1;



    public void addResearchCount(int index, bool init)
    {
        //if we are loading from file we don't want to increment the research count
        if (!init) researchCount[index]++;
        //but we still want to apply the effects of the research on startup 

        //hard coding the effects for each research item
        switch (index)
        {
            case 0: //x2 click power
                int ower = 1;
                for (int i = 0; i < researchCount[index]; i++)
                    ower *= 2;
                power = ower;
                break;
            case 1: //x2 current cash
                AddDollars(getDollars());
                break;
            case 2: //xp gained +10%
                xpModifier = xpModifier + 0.1f;
                break;
            case 3: //turret damage
                turretDamageMultiplier += 0.05f;
                break;
            case 4: //turret fire rate
                turretFireRateMultiplier += 0.05f;
                break;
            case 5: //turret range
                turretRangeMultiplier += 0.05f;
                break;
            case 6: //sell back rate
                sellBackMultiplier += 0.05f;
                break;
            case 7: //reduce research costs
                researchDiscount -= 0.05f;
                break;
            case 8: //auto targeting
                autoTargeting = true;
                break;
            case 9: //hold to buy
            case 10: //hold to click
            case 11: //junk rewards
                junkMultiplier += 0.1f;
                break;
            case 12: //offline rate
                offlineIncomeMultiplier += 0.05f;
                break;
            case 13: //production rate
                productionRateMultiplier += 0.01f;
                break;
            default:
                Debug.Log("No effect coded in for this research with index " + index);
                break;
        }

    }

    public void resetBuildingCount()
    {
        for (int i = 0; i < buildingCount.Count; i++)
        {
            buildingCount[i] = 0;
        }
        passive = 0;
    }

    public void resetResearchCount()
    {
        Research[] researchItems = researchInfo.researchItemsSO;
        for (int i = 0; i < researchCount.Count; i++)
        {
            if (!researchItems[i].isEpic)
            {
                researchCount[i] = 0;
            }
        }
    }

    public void resetDollars()
    {
        dollars = 0;
    }

    public void addCores(int amount)
    {
        cores += amount;
    }


    private void Awake()
    {
        //if an instance that is not me then delete me
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }
        Application.targetFrameRate = 60;

        LoadPlayerData();

        InvokeRepeating("SaveAndAddPassive", 1f, 1f);
    }


    public float collectSpaceJunk()
    {
        float amount = dollars / 10 *  junkMultiplier;
        dollars += amount;
        return amount;
    }

    public void Start()
    {
        if (offlineEarnings > 0)
        {
            string message = "Earnings while offline: ";
            if (offlineEarnings > 999999999)
            {
                message += offlineEarnings.ToString("0.##E0");
            }
            else
            {
                message += Mathf.Round(offlineEarnings).ToString("N0");
            }
            PopupManager.Instance.ShowPopup(message);
        }
    }

    private int xpPerClick = 1;

    public void MineResource()
    {
        dollars += 1 * power;
        addXpPoints(xpPerClick);
    }

    private float offlineIncomeMultiplier = 0.5f;

    private float offlineEarnings;

    public float CalculateOfflineEarnings(DateTime logOffTime)
    {
        TimeSpan elapsed = DateTime.Now - logOffTime;
        double secondsPassed = elapsed.TotalSeconds;
        float earnings = (float)(secondsPassed * passive * productionRateMultiplier * (1 + (getDMEarningsBonus() / 100))) * offlineIncomeMultiplier;
        return earnings;
    }


    private void LoadPlayerData() //called in Awake
    {
        if (SaveSystem.LoadPlayer() != null && RWFileData)
        {
            //load data from file to object
            PlayerData data = SaveSystem.LoadPlayer();
            //reload data from object to player
            dollars = data.dollars;
            cores = data.cores;
            buildingCount = data.buildingCount;
            researchCount = data.researchCount;
            currentWorld = data.currentWorld;
            darkMatter = data.darkMatter;
            worldSpawner.SetCurrentWorld(currentWorld);
            EnemySpawner.Instance.currentWave = data.currentWave;
            for (int i = 0; i < buildingCount.Count; i++)
            {
                worldSpawner.LoadObjects(buildingCount[i], i);
            }
            for (int i = 0; i < researchCount.Count; i++)
            {
                addResearchCount(i, true);
            }

            currentXP = data.currentXP;
            currentXPLevel = data.currentXPLevel;

            for (int i = 0; i < buildingCount.Count; i++)
            {
                passive += shopItemsList.shopItemsSO[i].bonus * buildingCount[i];
            }

            offlineEarnings = CalculateOfflineEarnings(data.saveTime);
            dollars += offlineEarnings;
        }
        else
        {
            worldSpawner.SetCurrentWorld(0);
        }
    }

    private void SaveAndAddPassive()
    {
        if (RWFileData)
        {
            SaveSystem.SavePlayer(this);
        }
        dollars += passive * productionRateMultiplier * (1 + (getDMEarningsBonus()/100));
    }

    public void removeUpgrade(int index)
    {
        buildingCount[index]--;
        passive -= shopItemsList.shopItemsSO[index].bonus;
    }

    public void resetData()
    {
        currentXP = 0;
        currentXPLevel = 0;
        dollars = 0;
        currentWorld = 0;
        worldSpawner.SetCurrentWorld(currentWorld);
        EnemySpawner.Instance.prestige();
        resetResearchCount();
        resetBuildingCount();
        FindObjectOfType<XPBar>()?.refreshXPLevel();
        FindObjectOfType<StructuresPanel>()?.LoadCards();
        FindObjectOfType<ResearchPanel>()?.LoadCards();
    }

    public DateTime now()
    {
        return DateTime.Now;
    }

    [SerializeField] private int darkMatter = 0; // Prestige currency

    [SerializeField] private float dmMultiplier = 1;

    public int getDarkMatter()
    {
        return darkMatter;
    }

    public float getDMEarningsBonus()
    {
        return darkMatter * dmMultiplier;
    }

    public int getDMAvailable()
    {
        return Mathf.FloorToInt(dollars / 1000000f);
    }


    public void prestige()
    {
        int earnedDarkMatter = getDMAvailable();

        darkMatter += earnedDarkMatter;

        resetData();

        ClearTarget();

        uIManager.ClosePanel();

        string message = "Prestige complete! Earned " + earnedDarkMatter + " Dark Matter. Total: " + darkMatter;
        PopupManager.Instance.ShowPopup(message);
        Debug.Log(message);

    }


    public void giveSomeMoney()
    {
        dollars += 1000000000;
    }

    public void giveSomeXPLevel()
    {
        currentXPLevel += 10;
        FindObjectOfType<XPBar>()?.refreshXPLevel();
        FindObjectOfType<StructuresPanel>()?.LoadCards();
    }

    public void Update()
    {
        if (autoTargeting)
        {
            AutoTarget();
        }
    }
    private void AutoTarget()
    {
        // Find all enemies in the scene
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        // If there are no enemies, clear the target
        if (enemies.Length == 0)
        {
            if (target != null) ClearTarget();
            return;
        }

        // Pick the closest enemy
        GameObject closestEnemy   = enemies[0];
        float      closestDistance = Vector3.Distance(transform.position, closestEnemy.transform.position);

        foreach (GameObject enemy in enemies)
        {
            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance < closestDistance)
            {
                closestEnemy    = enemy;
                closestDistance = distance;
            }
        }

        // Assign the target only if it changed (avoids event spam every frame)
        if (target != closestEnemy.transform)
        {
            target = closestEnemy.transform;
            OnTargetChanged?.Invoke(target);
        }
    }


}
