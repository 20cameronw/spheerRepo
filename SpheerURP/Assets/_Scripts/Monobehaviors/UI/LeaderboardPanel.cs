using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LeaderboardPanel : MenuPanel
{
    [Header("Record Labels")]
    [SerializeField] private TMP_Text highestWaveRecord;
    [SerializeField] private TMP_Text highestXPLevelRecord;
    [SerializeField] private TMP_Text peakPassiveRecord;
    [SerializeField] private TMP_Text mostEnemiesKilledRecord;
    [SerializeField] private TMP_Text mostWavesCompletedRecord;
    [SerializeField] private TMP_Text prestigeCountRecord;
    [SerializeField] private TMP_Text mostMoneyEarnedRecord;
    [SerializeField] private TMP_Text currentDarkMatterRecord;

    public override void OpenPanel()
    {
        RefreshRecords();
        base.OpenPanel();
    }

    public void RefreshRecords()
    {
        Player p = Player.Instance;
        if (p == null) return;

        if (highestWaveRecord)
            highestWaveRecord.text = "🏆 Highest Wave\n" + p.getLifetimeHighestWave().ToString("N0");
        if (highestXPLevelRecord)
            highestXPLevelRecord.text = "🏆 Best XP Level\n" + p.getLifetimeRecordHighestXPLevel().ToString("N0");
        if (peakPassiveRecord)
        {
            float pp = p.getLifetimeRecordPeakPassive();
            peakPassiveRecord.text = "🏆 Peak Income/s\n" + (pp > 999999 ? pp.ToString("0.##E0") : Mathf.Round(pp).ToString("N0"));
        }
        if (mostEnemiesKilledRecord)
            mostEnemiesKilledRecord.text = "🏆 Enemies Killed\n" + p.getLifetimeEnemiesKilled().ToString("N0");
        if (mostWavesCompletedRecord)
            mostWavesCompletedRecord.text = "🏆 Waves Completed\n" + p.getLifetimeWavesCompleted().ToString("N0");
        if (prestigeCountRecord)
            prestigeCountRecord.text = "🏆 Prestiges\n" + p.getLifetimePrestigeCount().ToString("N0");
        if (mostMoneyEarnedRecord)
        {
            float m = p.getLifetimeTotalMoneyEarned();
            mostMoneyEarnedRecord.text = "🏆 Total Earned\n" + (m > 999999999 ? m.ToString("0.##E0") : Mathf.Round(m).ToString("N0"));
        }
        if (currentDarkMatterRecord)
            currentDarkMatterRecord.text = "🏆 Dark Matter\n" + p.getDarkMatter().ToString("N0");
    }
}
