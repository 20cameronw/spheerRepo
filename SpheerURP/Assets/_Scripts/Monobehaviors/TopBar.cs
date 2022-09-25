using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TopBar : MonoBehaviour
{
    [SerializeField] private TMP_Text DollarsText;
    [SerializeField] private TMP_Text PassiveText;

    [SerializeField] private float dollarsUpdateTime;

    [SerializeField] private Image EnergyBar;
    [SerializeField] private float energyResetTime;

    private float previousDollars;
    private float currentDollars;
    private void Start()
    {
        InvokeRepeating("UpdateDollars", dollarsUpdateTime, dollarsUpdateTime);
        StartCoroutine(UpdatePassive());
        InvokeRepeating("UpdateEnergyBar", energyResetTime, energyResetTime);
    }

    private void UpdateDollars()
    {
        DollarsText.text = Player.Instance.getDollars().ToString();
    }

    private IEnumerator UpdatePassive()
    {
        while (true)
        {
            previousDollars = Player.Instance.getDollars();
            yield return new WaitForSeconds(1f);
            currentDollars = Player.Instance.getDollars();
            PassiveText.text = (currentDollars - previousDollars).ToString() + "/s";
        }
    }

    private void UpdateEnergyBar()
    {
        EnergyBar.fillAmount = Player.Instance.getCurrentEnergy();
    }
}
