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
    [SerializeField] private List<int> buildingCount;
    [SerializeField] private List<int> researchCount;
    [SerializeField] private List<int> popupShown;

    [SerializeField] private popupMessagesListSO popupMessages;
    [SerializeField] private int currentWorld;
    [SerializeField] private float maxHealth;
    [SerializeField] private int currentXPLevel;

    [SerializeField] private int currentXP;
    [SerializeField] private float power = 1;

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
        currentXP += amount;
    }

    public int getCurrentXP()
    {
        return currentXP;
    }

    public int getCurrentXPLevel()
    {
        return currentXPLevel;
    }

    public void resetPower()
    {
        power = 2 * researchCount[0];
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

    public void addResearchCount(int index)
    {
        researchCount[index]++;
    }

    public void resetBuildingCount()
    {
        for (int i = 0; i < buildingCount.Count; i++)
        {
            buildingCount[i] = 0;
        }
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
            buildingCount = data.buildingCount;
            researchCount = data.researchCount;
            popupShown = data.popupShown;
            for (int i = 0; i < buildingCount.Count; i++)
            {
                worldSpawner.LoadObjects(buildingCount[i], i);
            }
            currentWorld = data.currentWorld;
            worldSpawner.SetCurrentWorld(currentWorld);
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
