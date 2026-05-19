using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Procedurally built HUD overlay shown while the player is attacking an enemy world.
/// Displays the total remaining health of the enemy base as a red progress bar plus text.
///
/// Add this component to the Game Managers GameObject alongside
/// <see cref="AttackManager"/> — no scene setup required.
/// </summary>
public class AttackProgressUI : MonoBehaviour
{
    // ── Runtime UI refs ───────────────────────────────────────────────────────

    private GameObject    rootPanel;
    private RectTransform fillRect;
    private TMP_Text      healthLabel;

    private string currentBaseName = "Enemy Base";

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        BuildUI();
        SetVisible(false);

        AttackManager.OnAttackStarted      += OnAttackStarted;
        AttackManager.OnAttackEnded        += OnAttackEnded;
        AttackWorldView.OnBuildingDestroyedEvent += _ => RefreshBar();
    }

    private void OnDestroy()
    {
        AttackManager.OnAttackStarted      -= OnAttackStarted;
        AttackManager.OnAttackEnded        -= OnAttackEnded;
        AttackWorldView.OnBuildingDestroyedEvent -= _ => RefreshBar();
    }

    private void Update()
    {
        if (rootPanel != null && rootPanel.activeSelf)
            RefreshBar();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnAttackStarted(EnemyBaseData data)
    {
        currentBaseName = data.baseName;
        RefreshBar();
        SetVisible(true);
    }

    private void OnAttackEnded(bool _) => SetVisible(false);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RefreshBar()
    {
        AttackWorldView world = AttackManager.Instance != null
            ? AttackManager.Instance.GetCurrentAttackWorldView()
            : null;
        if (world == null) return;

        float ratio = world.MaxHealth > 0f ? world.CurrentHealth / world.MaxHealth : 1f;
        ratio = Mathf.Clamp01(ratio);

        // Scale the fill panel by moving its right anchor
        if (fillRect != null)
        {
            fillRect.anchorMax = new Vector2(ratio, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }

        if (healthLabel != null)
        {
            healthLabel.text =
                $"{currentBaseName}  "
                + $"{Mathf.CeilToInt(world.CurrentHealth):N0} / "
                + $"{Mathf.CeilToInt(world.MaxHealth):N0} HP";
        }
    }

    private void SetVisible(bool visible)
    {
        if (rootPanel != null) rootPanel.SetActive(visible);
    }

    // ── UI construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        // ── Canvas ────────────────────────────────────────────────────────────
        var canvasGO = new GameObject("AttackProgressCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Outer panel (top of screen) ───────────────────────────────────────
        rootPanel = MakePanel("AttackHUDPanel", canvasGO.transform,
            anchorMin: new Vector2(0.05f, 0.88f),
            anchorMax: new Vector2(0.95f, 0.96f),
            color: new Color(0f, 0f, 0f, 0.72f));

        // ── Label (top half of panel) ─────────────────────────────────────────
        var labelGO = new GameObject("HealthLabel");
        labelGO.transform.SetParent(rootPanel.transform, false);
        var labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.52f);
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(6f, 0f);
        labelRect.offsetMax = new Vector2(-6f, -2f);

        healthLabel = labelGO.AddComponent<TextMeshProUGUI>();
        healthLabel.text              = "Enemy Base";
        healthLabel.color             = Color.white;
        healthLabel.alignment         = TextAlignmentOptions.Center;
        healthLabel.enableAutoSizing  = true;
        healthLabel.fontSizeMin       = 6f;
        healthLabel.fontSizeMax       = 22f;

        // ── Bar track (bottom half of panel) ─────────────────────────────────
        var trackGO = MakePanel("BarTrack", rootPanel.transform,
            anchorMin: new Vector2(0.01f, 0.06f),
            anchorMax: new Vector2(0.99f, 0.50f),
            color: new Color(0.15f, 0.05f, 0.05f, 1f));

        // ── Red fill bar ──────────────────────────────────────────────────────
        var fillGO = MakePanel("BarFill", trackGO.transform,
            anchorMin: Vector2.zero,
            anchorMax: Vector2.one,
            color: new Color(0.85f, 0.12f, 0.12f, 1f));

        fillRect = fillGO.GetComponent<RectTransform>();
    }

    /// <summary>Creates a <see cref="GameObject"/> with an <see cref="Image"/> and <see cref="RectTransform"/>.</summary>
    private static GameObject MakePanel(string name, Transform parent,
                                        Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt         = go.AddComponent<RectTransform>();
        rt.anchorMin   = anchorMin;
        rt.anchorMax   = anchorMax;
        rt.offsetMin   = Vector2.zero;
        rt.offsetMax   = Vector2.zero;

        var img  = go.AddComponent<Image>();
        img.color = color;

        return go;
    }
}
