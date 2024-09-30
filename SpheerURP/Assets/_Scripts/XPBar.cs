using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class XPBar : MonoBehaviour
{
    private float xpGoal;
    private int xpLevel;

    private float currentXP;
    [SerializeField] private Slider xpBar;
    [SerializeField] private TMP_Text xpText;

    [SerializeField] private float xpIncreaseMultiplier;
    [SerializeField] private float xpBaseGoal;

    void Start()
    {
        refreshXPGoal();
        currentXP = Player.Instance.getCurrentXP();
        refreshXPLevel();
        StartCoroutine(refreshBar());
    }

    public void refreshXPLevel()
    {
        xpLevel = Player.Instance.getCurrentXPLevel();
        xpText.text = "XP lvl " + xpLevel;
    }

    public void refreshXPGoal()
    {
        float goal = xpBaseGoal;
        for (int i = 0; i < xpLevel; i++)
        {
            goal *= xpIncreaseMultiplier;
        }
        xpGoal = goal;
        xpBar.maxValue = xpGoal;
    }

    private IEnumerator refreshBar()
    {
        while (true)
        {
            yield return new WaitForSeconds(.2f);

            currentXP = Player.Instance.getCurrentXP();

            xpBar.value = currentXP;

            if (currentXP >= xpGoal)
            {
                levelUp();
            }
        }
    }

    public void levelUp()
    {
        Player.Instance.levelUpXP();
        refreshXPGoal();
        refreshXPLevel();
        currentXP = 0;
    }



}
