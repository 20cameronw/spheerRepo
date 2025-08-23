using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PrestigeData : MonoBehaviour
{
    [SerializeField] private TMP_Text amountAvailableText;
    [SerializeField] private TMP_Text currentDarkMatterText;
    [SerializeField] private TMP_Text currentEarningsBonusText;

    void Update()
    {
        currentDarkMatterText.text = Player.Instance.getDarkMatter().ToString();
        currentEarningsBonusText.text = Player.Instance.getDMEarningsBonus().ToString() + "%";
        amountAvailableText.text = Player.Instance.getDMAvailable().ToString();
    }
}
