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
    }

    private void UpdateDollars()
    {
        float dollars = Player.Instance.getDollars();
        if (dollars > 99999)
        {
            DollarsText.text = dollars.ToString("0.##E0");
        }
        else
        {
            DollarsText.text = Mathf.Round(dollars).ToString("N0");
        }

    }

    private IEnumerator UpdatePassive()
    {
        while (true)
        {
            previousDollars = Player.Instance.getDollars();
            yield return new WaitForSeconds(1f);
            currentDollars = Player.Instance.getDollars();
            float passive = (currentDollars - previousDollars);
            if (passive < 0) continue;
            if (passive > 99999)
            {
                PassiveText.text = passive.ToString("0.##E0") + "/s";
            }
            else
            {
                PassiveText.text = passive.ToString("N0") + "/s";
            }

        }
    }
}
