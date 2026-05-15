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

    [SerializeField] private float lazerDamageMultiplier = 1f;
    [SerializeField] private float lazerRangeMultiplier = 1f;
    [SerializeField] private float spinBonus = 0f;
    [SerializeField] private float prestigeDMMultiplier = 1f;
    [SerializeField] private int xpPerClickBonus = 0;

    [SerializeField] private UIManager uIManager;

    // Lifetime stats (persist across prestiges)
    private int lifetimeTotalEnemiesKilled = 0;
    private int lifetimeTotalWavesCompleted = 0;
    private float lifetimeTotalMoneyEarned = 0f;
    private int lifetimeHighestWave = 0;
    private int lifetimePrestigeCount = 0;
    private float lifetimeRecordPeakPassive = 0f;
    private int lifetimeRecordHighestXPLevel = 0;
    private List<int> completedMissionIndices = new List<int>();

    public int getLifetimeEnemiesKilled()           => lifetimeTotalEnemiesKilled;
    public int getLifetimeWavesCompleted()          => lifetimeTotalWavesCompleted;
    public float getLifetimeTotalMoneyEarned()      => lifetimeTotalMoneyEarned;
    public int getLifetimeHighestWave()             => lifetimeHighestWave;
    public int getLifetimePrestigeCount()           => lifetimePrestigeCount;
    public float getLifetimeRecordPeakPassive()     => lifetimeRecordPeakPassive;
    public int getLifetimeRecordHighestXPLevel()    => lifetimeRecordHighestXPLevel;
    public List<int> getCompletedMissionIndices()   => completedMissionIndices;

    public void recordEnemyKilled()
    {
        lifetimeTotalEnemiesKilled++;
    }

    public void recordWaveCompleted(int wave)
    {
        lifetimeTotalWavesCompleted++;
        if (wave > lifetimeHighestWave) lifetimeHighestWave = wave;
    }

    public void recordMoneyEarned(float amount)
    {
        if (amount > 0) lifetimeTotalMoneyEarned += amount;
    }

    public void updateRecordPassive(float currentPassive)
    {
        if (currentPassive > lifetimeRecordPeakPassive) lifetimeRecordPeakPassive = currentPassive;
    }

    public void completeMission(int missionIndex)
    {
        if (!completedMissionIndices.Contains(missionIndex))
            completedMissionIndices.Add(missionIndex);
    }

    public bool isMissionComplete(int missionIndex)
    {
        return completedMissionIndices.Contains(missionIndex);
    }

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

    public float getLazerDamageMultiplier()
    {
        return lazerDamageMultiplier;
    }

    public float getLazerRangeMultiplier()
    {
        return lazerRangeMultiplier;
    }

    public float getSpinBonus()
    {
        return spinBonus;
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
        EnsureResearchCountSize();
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
            case 1: //x2 current cash — one-time effect; never re-apply on load
                if (!init)
                    AddDollars(getDollars());
                break;
            case 2: //xp gained +10%
                if (init)
                    xpModifier = 1f + 0.1f * researchCount[index];
                else
                    xpModifier += 0.1f;
                break;
            case 3: //turret damage
                if (init)
                    turretDamageMultiplier = 1f + 0.05f * researchCount[index];
                else
                    turretDamageMultiplier += 0.05f;
                break;
            case 4: //turret fire rate
                if (init)
                    turretFireRateMultiplier = 1f + 0.05f * researchCount[index];
                else
                    turretFireRateMultiplier += 0.05f;
                break;
            case 5: //turret range
                if (init)
                    turretRangeMultiplier = 1f + 0.05f * researchCount[index];
                else
                    turretRangeMultiplier += 0.05f;
                break;
            case 6: //sell back rate
                if (init)
                    sellBackMultiplier = 0.05f * researchCount[index];
                else
                    sellBackMultiplier += 0.05f;
                break;
            case 7: //reduce research costs
                if (init)
                    researchDiscount = 1f - 0.05f * researchCount[index];
                else
                    researchDiscount -= 0.05f;
                break;
            case 8: //auto targeting
                if (researchCount[index] > 0) autoTargeting = true;
                break;
            case 9: //hold to buy
            case 10: //hold to click
            case 11: //junk rewards
                if (init)
                    junkMultiplier = 1f + 0.1f * researchCount[index];
                else
                    junkMultiplier += 0.1f;
                break;
            case 12: //offline rate
                if (init)
                    offlineIncomeMultiplier = 0.5f + 0.1f * researchCount[index];
                else
                    offlineIncomeMultiplier += 0.1f;
                break;
            case 13: //production rate (Core Extraction)
                RecalculateProductionRate();
                break;
            case 14: //lazer damage
                if (init)
                    lazerDamageMultiplier = 1f + 0.10f * researchCount[index];
                else
                    lazerDamageMultiplier += 0.10f;
                break;
            case 15: //lazer range
                if (init)
                    lazerRangeMultiplier = 1f + 0.10f * researchCount[index];
                else
                    lazerRangeMultiplier += 0.10f;
                break;
            case 16: //spin bonus (Mineral Density)
                if (init)
                    spinBonus = 0.5f * researchCount[index];
                else
                    spinBonus += 0.5f;
                break;
            case 17: //prestige DM multiplier (Dark Matter Mastery)
                if (init)
                    prestigeDMMultiplier = 1f + 0.20f * researchCount[index];
                else
                    prestigeDMMultiplier += 0.20f;
                break;
            case 18: //wind turbine production boost (Wind Power)
                RecalculateProductionRate();
                break;
            case 19: //drill automation production boost (Drill Automation)
                RecalculateProductionRate();
                break;
            case 20: //quantum capacitor — DM income bonus
                if (init)
                    dmMultiplier = 1f + 0.05f * researchCount[index];
                else
                    dmMultiplier += 0.05f;
                break;
            case 21: //neural interface — +1 XP per click per level
                if (init)
                    xpPerClickBonus = researchCount[index];
                else
                    xpPerClickBonus++;
                break;
            case 22: //orbital relay — +5% offline income per level
                if (init)
                    offlineIncomeMultiplier += 0.05f * researchCount[index];
                else
                    offlineIncomeMultiplier += 0.05f;
                break;
            case 23: //neutron drill — +2% production rate per level (Neutron Drill)
                RecalculateProductionRate();
                break;
            case 24: //warp core — one-time +50% cash boost
                // Effect is immediate: dollars are updated and persisted in the save.
                // On reload (init=true) we skip this because the boosted amount is
                // already stored in the saved 'dollars' field — re-applying would be a double-grant.
                if (!init)
                    AddDollars(getDollars() * 0.5f);
                break;
            case 25: //singularity drive — +0.5 spin bonus (additive with case 16)
                if (init)
                    spinBonus += 0.5f * researchCount[index];
                else
                    spinBonus += 0.5f;
                break;
            case 26: //ion burst — +10% laser damage per level (additive with case 14)
                if (init)
                    lazerDamageMultiplier += 0.10f * researchCount[index];
                else
                    lazerDamageMultiplier += 0.10f;
                break;
            case 27: //gravity well — +5% junk multiplier per level (additive with case 11)
                if (init)
                    junkMultiplier += 0.05f * researchCount[index];
                else
                    junkMultiplier += 0.05f;
                break;
            case 28: //void lens — +5% turret range per level (additive with case 5)
                if (init)
                    turretRangeMultiplier += 0.05f * researchCount[index];
                else
                    turretRangeMultiplier += 0.05f;
                break;
            case 29: //stellar core — immediately grant 5 cores per purchase
                // Effect is immediate: cores are persisted in the 'cores' field.
                // On reload (init=true) we skip because granted cores are already in the save.
                if (!init)
                    addCores(5);
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

    private void EnsureResearchCountSize()
    {
        int needed = researchInfo != null ? researchInfo.researchItemsSO.Length : 0;
        while (researchCount.Count < needed)
            researchCount.Add(0);
    }

    // Recomputes productionRateMultiplier from all research indices that contribute to it.
    // Call this from any case that modifies production rate so ordering never matters.
    private void RecalculateProductionRate()
    {
        productionRateMultiplier = 1f
            + 0.01f * GetResearchCount(13)   // Core Extraction
            + 0.05f * GetResearchCount(18)   // Wind Power
            + 0.02f * GetResearchCount(19)   // Drill Automation
            + 0.02f * GetResearchCount(23);  // Neutron Drill
    }

    private int GetResearchCount(int index)
    {
        return (index < researchCount.Count) ? researchCount[index] : 0;
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

        EnemySpawner.OnWaveCompleted += OnWaveCompleted;
    }

    private void OnDestroy()
    {
        EnemySpawner.OnWaveCompleted -= OnWaveCompleted;
    }

    private void OnWaveCompleted(int wave)
    {
        recordWaveCompleted(wave);
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
        dollars += (1 + spinBonus) * power;
        addXpPoints(xpPerClick + xpPerClickBonus);
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
            EnsureResearchCountSize();
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

            // Restore lifetime stats
            lifetimeTotalEnemiesKilled   = data.lifetimeTotalEnemiesKilled;
            lifetimeTotalWavesCompleted  = data.lifetimeTotalWavesCompleted;
            lifetimeTotalMoneyEarned     = data.lifetimeTotalMoneyEarned;
            lifetimeHighestWave          = data.lifetimeHighestWave;
            lifetimePrestigeCount        = data.lifetimePrestigeCount;
            lifetimeRecordPeakPassive    = data.lifetimeRecordPeakPassive;
            lifetimeRecordHighestXPLevel = data.lifetimeRecordHighestXPLevel;
            completedMissionIndices      = data.completedMissionIndices ?? new List<int>();
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
        float baseIncome = passive * productionRateMultiplier;
        float earned = baseIncome * (1 + (getDMEarningsBonus()/100));
        dollars += earned;
        recordMoneyEarned(earned);
        updateRecordPassive(baseIncome);
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
        int earnedDarkMatter = Mathf.RoundToInt(getDMAvailable() * prestigeDMMultiplier);

        // Track prestige stats before resetting
        lifetimePrestigeCount++;
        if (currentXPLevel > lifetimeRecordHighestXPLevel)
            lifetimeRecordHighestXPLevel = currentXPLevel;

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
