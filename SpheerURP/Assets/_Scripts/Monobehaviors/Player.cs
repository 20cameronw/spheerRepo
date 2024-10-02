using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public static Player Instance; //this class is a singleton

    private float dollarsGainedThisSecond;
    private float passiveSum;
    private float passive;


    [Header("Setup References")]
    [SerializeField] private WorldSpawner worldSpawner;

    [Space(10)]
    [Header("Modifiable Data")]
    [SerializeField] private bool RWFileData;
    [SerializeField] private float dollars;

    [SerializeField] private int cores;
    [SerializeField] private List<int> buildingCount;
    [SerializeField] private List<int> researchCount;
    [SerializeField] private List<int> popupShown;

    [SerializeField] private popupMessagesListSO popupMessages;
    [SerializeField] private int currentWorld;
    [SerializeField] private float maxHealth;
    [SerializeField] private int currentXPLevel;

    [SerializeField] private int currentXP;
    [SerializeField] private float power = 1;

    [SerializeField] private float xpModifier = 1;

    public void setCardShown(int index)
    {
        popupShown[index] = 1;
    }

    public bool shouldCardBeShown(int index)
    {
        return popupMessages.popupMessages[index].xpLevel <= currentXPLevel && popupShown[index] == 0;
    }

    public void levelUpXP()
    {
        currentXP = 0;
        currentXPLevel++;
        FindObjectOfType<ShopPanel>()?.LoadCards();
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

    public void addToPassive(float amount)
    {
        passiveSum += amount;
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

    public void addResearchCount(int index, bool init)
    {
        //if we are loading from file we don't want to increment the research count
        if (!init) researchCount[index]++;
        //but we still want to apply the effects of the research on startup 
        
        //hard coding the effects for each research item
        switch (index)
        {
            case 0:
                int ower = 1;
                for (int i = 0; i < researchCount[index]; i++)
                    ower *= 2;
                power = ower;
                break;
            case 1:
                AddDollars(getDollars());
                break;
            case 2:
                xpModifier = xpModifier + 0.1f;
                break;
            default:
                Debug.Log("No effect coded in for this research");
                break;
        }
    }

    public void resetBuildingCount()
    {
        for (int i = 0; i < buildingCount.Count; i++)
        {
            buildingCount[i] = 0;
        }
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

    private void OnEnable() => EventManager.OnClicked += MineResource;

    private void OnDisable() => EventManager.OnClicked -= MineResource;

    private void MineResource()
    {
        dollars += 1 * power;
    }

    public List<int> getPopupShown()
    {
        return popupShown;
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
            popupShown = data.popupShown;
            currentWorld = data.currentWorld;
            worldSpawner.SetCurrentWorld(currentWorld);
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
        }
    }

    private void SaveAndAddPassive()
    {
        if (RWFileData)
        {
            SaveSystem.SavePlayer(this);
        }
        passive = passiveSum;
        passiveSum = 0;
        dollars += passive;
    }

    public void removeUpgrade(float bonus, int index)
    {
        researchCount[index]--;
    }

}
