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
    [SerializeField] private float maxHealth;
    [SerializeField] private float currentHealth;
    [SerializeField] private float power = 1;

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

    public float getCurrentHealth()
    {
        return currentHealth / maxHealth;
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

    public void addResearchCount(int index)
    {
        researchCount[index]++;
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

    private void Start()
    {
        worldSpawner.SetCurrentWorld(0);
    }

    private void MineResource()
    {
        dollars += 1 * power;
    }

    private void LoadPlayerData()
    {
        if (SaveSystem.LoadPlayer() != null && RWFileData)
        {
            //load data from file to object
            PlayerData data = SaveSystem.LoadPlayer();
            //reload data from object to player
            dollars = data.dollars;
            buildingCount = data.buildingCount;
            researchCount = data.researchCount;
            for (int i = 0; i < buildingCount.Count; i++)
            {
                worldSpawner.LoadObjects(buildingCount[i], i);
            }
            resetPower();
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



}
