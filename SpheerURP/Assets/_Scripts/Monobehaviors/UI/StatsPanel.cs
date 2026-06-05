using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class StatsPanel : MenuPanel
{
    [Header("Stat Labels")]
    [SerializeField] private TMP_Text wavesCompletedText;
    [SerializeField] private TMP_Text highestWaveText;
    [SerializeField] private TMP_Text enemiesKilledText;
    [SerializeField] private TMP_Text moneyEarnedText;
    [SerializeField] private TMP_Text prestigeCountText;
    [SerializeField] private TMP_Text peakPassiveText;
    [SerializeField] private TMP_Text highestXPLevelText;
    [SerializeField] private TMP_Text currentWaveText;
    [SerializeField] private TMP_Text currentXPLevelText;
    [SerializeField] private TMP_Text darkMatterText;

    public override void OpenPanel()
    {
        RefreshStats();
        base.OpenPanel();
    }

    public void RefreshStats()
    {
        Player p = Player.Instance;
        if (p == null) return;

        if (wavesCompletedText)
            wavesCompletedText.text  = "Waves Completed: " + p.getLifetimeWavesCompleted().ToString("N0");
        if (highestWaveText)
            highestWaveText.text     = "Highest Wave: " + p.getLifetimeHighestWave().ToString("N0");
        if (enemiesKilledText)
            enemiesKilledText.text   = "Enemies Killed: " + p.getLifetimeEnemiesKilled().ToString("N0");
        if (moneyEarnedText)
        {
            float m = p.getLifetimeTotalMoneyEarned();
            moneyEarnedText.text     = "Total Earned: " + (m > 999999999 ? m.ToString("0.##E0") : Mathf.Round(m).ToString("N0"));
        }
        if (prestigeCountText)
            prestigeCountText.text   = "Prestiges: " + p.getLifetimePrestigeCount().ToString("N0");
        if (peakPassiveText)
        {
            float pp = p.getLifetimeRecordPeakPassive();
            peakPassiveText.text     = "Peak Income/s: " + (pp > 999999 ? pp.ToString("0.##E0") : Mathf.Round(pp).ToString("N0"));
        }
        if (highestXPLevelText)
            highestXPLevelText.text  = "Best XP Level: " + p.getLifetimeRecordHighestXPLevel().ToString("N0");
        if (currentWaveText)
            currentWaveText.text     = "Current Wave: —";
        if (currentXPLevelText)
            currentXPLevelText.text  = "Current XP Level: " + p.getCurrentXPLevel().ToString("N0");
        if (darkMatterText)
            darkMatterText.text      = "Dark Matter: " + p.getDarkMatter().ToString("N0");
    }
}
