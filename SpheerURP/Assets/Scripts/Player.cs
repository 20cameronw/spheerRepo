using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public static Player Instance; //this class is a singleton

    private float dollars;

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
}
