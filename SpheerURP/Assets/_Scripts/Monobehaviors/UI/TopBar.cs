using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TopBar : MonoBehaviour
{
    [SerializeField] private TMP_Text DollarsText;
    [SerializeField] private TMP_Text PassiveText;

    [SerializeField] private TMP_Text coresText;

    // Phase 2 — additional resource displays (assign in Inspector; safe to leave null)
    [SerializeField] private TMP_Text plasmaText;
    [SerializeField] private TMP_Text electricityText;
    [SerializeField] private TMP_Text nebuliteCapText;  // Optional: shows Nebulite cap

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
        if (dollars > 999999999)
            DollarsText.text = dollars.ToString("0.##E0");
        else
            DollarsText.text = Mathf.Round(dollars).ToString("N0");

        // Phase 2: update Plasma and Electricity displays if wired
        if (plasmaText != null)
        {
            float p = Player.Instance.getPlasma();
            plasmaText.text = p > 999999 ? p.ToString("0.##E0") : Mathf.Round(p).ToString("N0");
        }

        if (electricityText != null)
        {
            // Shows free / total electricity (Phase 3 gate).
            float free  = Player.Instance.getElectricityFree();
            float total = Player.Instance.getElectricityCapacity();
            electricityText.text = free.ToString("F0") + "/" + total.ToString("F0") + " ⚡";
        }

        if (nebuliteCapText != null)
        {
            float cap  = Player.Instance.getNebuliteCapacity();
            nebuliteCapText.text = "Cap: " + (cap > 999999 ? cap.ToString("0.##E0") : Mathf.Round(cap).ToString("N0"));
        }
    }

    private IEnumerator UpdatePassive()
    {
        while (true)
        {
            UpdateCores();
            previousDollars = Player.Instance.getDollars();
            yield return new WaitForSeconds(1f);
            currentDollars = Player.Instance.getDollars();
            float passive = (currentDollars - previousDollars);
            PassiveText.text = passive.ToString("N0") + "/s";
        }
    }

    public void UpdateCores()
    {
        coresText.text = Player.Instance.getCores().ToString();
    }
}
