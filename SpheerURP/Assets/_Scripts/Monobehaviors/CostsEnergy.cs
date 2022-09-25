using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CostsEnergy : MonoBehaviour
{
    [SerializeField] private int costPerSecond;
    [SerializeField] private bool producesEnergy;

    private void Start()
    {
        InvokeRepeating("ProduceOrConsume", 1f, 1f);
    }

    private void ProduceOrConsume()
    {
        if (producesEnergy)
        {
            Player.Instance.AddToEnergy(costPerSecond);
        }
        else
        {
            for (int i = 0; i < costPerSecond; i++)
                Player.Instance.tickDownEnergy();
        }
    }
}
