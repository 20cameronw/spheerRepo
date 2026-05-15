using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;

public class LeaderboardPanel : MenuPanel
{
    [Header("Leaderboard Settings")]
    [SerializeField] private string leaderboardId = "lifetime-value";
    [SerializeField] private int topEntriesCount = 100;

    [Header("UI References")]
    [SerializeField] private Transform rowContainer;
    [SerializeField] private GameObject rowPrefab;
    [SerializeField] private TMP_Text statusText;

    public override void OpenPanel()
    {
        base.OpenPanel();
        _ = LoadLeaderboardAsync();
    }

    private async Task LoadLeaderboardAsync()
    {
        SetStatus("Loading...");
        ClearRows();

        try
        {
            if (UnityServicesManager.Instance != null)
                await UnityServicesManager.Instance.InitializeAsync();

            double score = Player.Instance != null ? (double)Player.Instance.getLifetimeTotalMoneyEarned() : 0.0;
            await LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboardId, score);

            var options = new GetScoresOptions { Limit = topEntriesCount };
            LeaderboardScoresPage page = await LeaderboardsService.Instance.GetScoresAsync(leaderboardId, options);

            ClearRows();
            SetStatus(null);

            if (page.Results == null || page.Results.Count == 0)
            {
                SetStatus("No entries yet.");
                return;
            }

            foreach (var entry in page.Results)
                SpawnRow(entry.Rank + 1, entry.PlayerName ?? entry.PlayerId, entry.Score);
        }
        catch (System.Exception e)
        {
            SetStatus("Failed to load leaderboard.");
            Debug.LogError("[LeaderboardPanel] " + e);
        }
    }

    private void SpawnRow(int rank, string playerName, double score)
    {
        if (rowPrefab == null || rowContainer == null) return;

        GameObject row = Instantiate(rowPrefab, rowContainer);
        TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>();
        if (texts.Length >= 1) texts[0].text = "#" + rank;
        if (texts.Length >= 2) texts[1].text = playerName;
        if (texts.Length >= 3) texts[2].text = score > 999999999.0 ? score.ToString("0.##E0") : ((long)score).ToString("N0");
    }

    private void ClearRows()
    {
        if (rowContainer == null) return;
        foreach (Transform child in rowContainer)
            Destroy(child.gameObject);
    }

    private void SetStatus(string message)
    {
        if (statusText == null) return;
        statusText.text = message ?? string.Empty;
        statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }
}

