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
    [SerializeField] private float HealthResetTime;

    private float previousDollars;
    private float currentDollars;
    private void Start()
    {
        InvokeRepeating("UpdateDollars", dollarsUpdateTime, dollarsUpdateTime);
        StartCoroutine(UpdatePassive());
        InvokeRepeating("UpdateHealthBar", HealthResetTime, HealthResetTime);
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

    private void UpdateHealthBar()
    {
        EnergyBar.fillAmount = Player.Instance.getCurrentHealth();
    }
}
