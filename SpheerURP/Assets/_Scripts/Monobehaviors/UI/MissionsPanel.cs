using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class MissionsPanel : MenuPanel
{
    [Header("Data")]
    [SerializeField] private MissionsListSO missionsListSO;
    [SerializeField] private GameObject missionCardTemplate;
    [SerializeField] private Transform missionListContent;

    private List<GameObject> missionCards = new List<GameObject>();

    void Start()
    {
        BuildCards();
        gameObject.SetActive(false);
    }

    public override void OpenPanel()
    {
        RefreshCards();
        base.OpenPanel();
    }

    private void BuildCards()
    {
        if (missionsListSO == null || missionCardTemplate == null || missionListContent == null)
            return;

        foreach (var mission in missionsListSO.missions)
        {
            GameObject card = Instantiate(missionCardTemplate, missionListContent);
            missionCards.Add(card);
        }
        RefreshCards();
    }

    public void RefreshCards()
    {
        if (missionsListSO == null) return;
        Player p = Player.Instance;
        if (p == null) return;

        for (int i = 0; i < missionsListSO.missions.Length && i < missionCards.Count; i++)
        {
            MissionSO mission = missionsListSO.missions[i];
            GameObject card = missionCards[i];

            TMP_Text titleText    = card.transform.Find("TitleText")?.GetComponent<TMP_Text>();
            TMP_Text descText     = card.transform.Find("DescText")?.GetComponent<TMP_Text>();
            TMP_Text progressText = card.transform.Find("ProgressText")?.GetComponent<TMP_Text>();
            TMP_Text rewardText   = card.transform.Find("RewardText")?.GetComponent<TMP_Text>();
            Image checkmark       = card.transform.Find("Checkmark")?.GetComponent<Image>();
            Image iconImage       = card.transform.Find("Icon")?.GetComponent<Image>();
            Slider progressBar    = card.transform.Find("ProgressBar")?.GetComponent<Slider>();

            bool completed = p.isMissionComplete(i);

            float current = GetCurrentProgress(mission, p);
            float target  = mission.targetValue;

            if (titleText)    titleText.text    = mission.name;
            if (descText)     descText.text     = mission.description;
            if (rewardText)   rewardText.text   = "Reward: " + mission.rewardDescription;
            if (iconImage && mission.icon) iconImage.sprite = mission.icon;

            if (completed)
            {
                if (progressText) progressText.text = "COMPLETE!";
                if (progressBar)
                {
                    progressBar.minValue = 0;
                    progressBar.maxValue = 1;
                    progressBar.value    = 1;
                }
                if (checkmark) checkmark.gameObject.SetActive(true);
            }
            else
            {
                float clamped = Mathf.Min(current, target);
                if (progressText) progressText.text = FormatValue(clamped) + " / " + FormatValue(target);
                if (progressBar)
                {
                    progressBar.minValue = 0;
                    progressBar.maxValue = target;
                    progressBar.value    = clamped;
                }
                if (checkmark) checkmark.gameObject.SetActive(false);

                // Auto-complete if threshold reached
                if (current >= target)
                    p.completeMission(i);
            }
        }
    }

    private float GetCurrentProgress(MissionSO mission, Player p)
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

    private string FormatValue(float value)
    {
        if (value >= 1000000000f) return value.ToString("0.##E0");
        if (value >= 1000f)       return Mathf.Round(value).ToString("N0");
        return Mathf.Round(value).ToString();
    }
}
