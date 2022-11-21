using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class progressBar : MonoBehaviour
{
    [SerializeField] private Image healthBarSprite;

    public void UpdateProgressBar(float cost, float current)
    {
        healthBarSprite.fillAmount = current / cost;
    }

}
