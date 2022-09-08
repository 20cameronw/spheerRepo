using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public static Player Instance; //this class is a singleton
    private float dollars;
    private float dollarsGainedThisSecond;
    private float passive;

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

        InvokeRepeating("CalculateDollarsGained", 1f, 1f);
    }

    void Start()
    {

    }


    void Update()
    {

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

    private void LoadPlayerData()
    {
        if (SaveSystem.LoadPlayer() != null)
        {
            //load data from file to object
            PlayerData data = SaveSystem.LoadPlayer();
            //reload data from object to player
            dollars = data.dollars;
        }
    }

    private void CalculateDollarsGained()
    {
        SaveSystem.SavePlayer(this);
        float previous = dollars;

        dollars += passive;

        dollarsGainedThisSecond = dollars - previous;
    }
}
