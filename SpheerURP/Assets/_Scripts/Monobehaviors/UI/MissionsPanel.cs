using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Missions panel — simplified card layout.
/// Each card shows:
///   • GoalText  — mission objective label
///   • ProgressBar — progress towards goal
///   • CheckboxUnchecked / CheckboxChecked — toggled based on completion
///
/// Missions are defined in MissionDefinitions.cs and never reset.
/// </summary>
public class MissionsPanel : MenuPanel
{
    [Header("Card Template")]
    [SerializeField] private GameObject missionCardTemplate;
    [SerializeField] private Transform  missionListContent;

    private List<GameObject> missionCards = new List<GameObject>();

    void Start()
    {
        BuildCards();
        gameObject.SetActive(false);
    }

    public override void OpenPanel()
    {
        base.OpenPanel();
        RefreshCards();
    }

    private void BuildCards()
    {
        if (missionCardTemplate == null || missionListContent == null) return;

        foreach (var mission in MissionDefinitions.All)
        {
            GameObject card = Instantiate(missionCardTemplate, missionListContent);
            missionCards.Add(card);
        }
        RefreshCards();
    }

    public void RefreshCards()
    {
        Player p = Player.Instance;
        if (p == null) return;

        for (int i = 0; i < MissionDefinitions.All.Length && i < missionCards.Count; i++)
        {
            MissionEntry mission = MissionDefinitions.All[i];
            GameObject   card    = missionCards[i];

            TMP_Text  goalText          = card.transform.Find("GoalText")?.GetComponent<TMP_Text>();
            Slider    progressBar       = card.transform.Find("ProgressBar")?.GetComponent<Slider>();
            GameObject checkboxChecked  = card.transform.Find("CheckboxChecked")?.gameObject;
            GameObject checkboxEmpty    = card.transform.Find("CheckboxUnchecked")?.gameObject;

            bool  completed = p.isMissionComplete(i);
            float current   = GetCurrentProgress(mission, p);
            float target    = mission.targetValue;
            float clamped   = Mathf.Min(current, target);

            if (goalText) goalText.text = mission.goalText;

            if (progressBar != null)
            {
                progressBar.minValue = 0f;
                progressBar.maxValue = target;
                progressBar.value    = clamped;
            }

            if (checkboxChecked)  checkboxChecked.SetActive(completed);
            if (checkboxEmpty)    checkboxEmpty.SetActive(!completed);

            // Auto-complete if threshold reached for the first time
            if (!completed && current >= target)
                p.completeMission(i);
        }
    }

    private float GetCurrentProgress(MissionEntry mission, Player p)
    {
        switch (mission.type)
        {
            case MissionType.ReachWave:
                return EnemySpawner.Instance != null ? EnemySpawner.Instance.currentWave : 0;
            case MissionType.EarnTotalMoney:
                return p.getLifetimeTotalMoneyEarned();
            case MissionType.KillTotalEnemies:
                return p.getLifetimeEnemiesKilled();
            case MissionType.CompleteWaves:
                return p.getLifetimeWavesCompleted();
            case MissionType.ReachXPLevel:
                return p.getCurrentXPLevel();
            case MissionType.PurchaseBuildings:
                int total = 0;
                foreach (int c in p.getBuildingCountList()) total += c;
                return total;
            case MissionType.CompleteResearch:
                int rTotal = 0;
                foreach (int c in p.getResearchCount()) rTotal += c;
                return rTotal;
            case MissionType.ReachPassiveIncome:
                return p.getPassive();
            case MissionType.Prestige:
                return p.getLifetimePrestigeCount();
            default:
                return 0;
        }
    }
}
