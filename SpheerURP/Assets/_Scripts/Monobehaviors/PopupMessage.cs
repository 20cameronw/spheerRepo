using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A single notification icon that lives on the right side of the screen.
/// Behaviour:
///   1. Spawned off-screen to the right → slides in to its stacked position.
///   2. After <wiggleDelay> seconds without interaction → wiggle loop to grab attention.
///   3. Tapping the icon → stops wiggle, expands to show the full message in the centre.
///   4. Closing the expanded message → icon fades out and destroys itself.
/// </summary>
public class PopupMessage : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text    messageText;
    [SerializeField] private GameObject  messagePanel;
    [SerializeField] private Button      openButton;
    [SerializeField] private Image       icon;

    [Header("Animation Settings")]
    [SerializeField] private float slideInDuration  = 0.4f;
    [SerializeField] private float wiggleDelay      = 5f;
    [SerializeField] private float wiggleAngle      = 12f;
    [SerializeField] private float wiggleDuration   = 0.12f;
    [SerializeField] private int   wiggleCycles     = 4;

    [Header("Expand Settings")]
    [SerializeField] private float expandedScale    = 8f;

    private Transform centerParent;
    private bool      isExpanded     = false;
    private bool      isWiggling     = false;
    private int       wiggleTweenId  = -1;
    private Vector3   originalPos;
    private Transform originalParent;

    public void Setup(string message, Transform centerContainer)
    {
        messageText.text = message;
        centerParent     = centerContainer;
        messagePanel.SetActive(false);

        openButton.onClick.AddListener(ToggleExpand);

        // Start off-screen to the right, then slide in after PopupManager positions us
        Invoke(nameof(SlideIn), 0.05f);

        // Schedule the wiggle reminder
        Invoke(nameof(StartWiggle), wiggleDelay);
    }

    /// <summary>Called by PopupManager after the icon has been placed in the container.</summary>
    private void SlideIn()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) return;

        // Animate from 80 units to the right back to final position
        Vector2 dest = rt.anchoredPosition;
        rt.anchoredPosition = dest + new Vector2(200f, 0f);
        LeanTween.moveLocalX(gameObject, rt.anchoredPosition.x - 200f, slideInDuration)
            .setEaseOutBack();
    }

    private void StartWiggle()
    {
        if (isExpanded) return;
        isWiggling  = true;
        RunWiggleCycle();
    }

    private void RunWiggleCycle()
    {
        if (!isWiggling || isExpanded) return;

        wiggleTweenId = LeanTween.rotateZ(gameObject, wiggleAngle, wiggleDuration)
            .setEaseInOutSine()
            .setLoopPingPong(wiggleCycles)
            .setOnComplete(() =>
            {
                LeanTween.rotateZ(gameObject, 0f, wiggleDuration * 0.5f);
                if (isWiggling && !isExpanded)
                    Invoke(nameof(RunWiggleCycle), 3f); // pause between bursts
            }).id;
    }

    private void StopWiggle()
    {
        isWiggling = false;
        CancelInvoke(nameof(RunWiggleCycle));
        if (wiggleTweenId >= 0)
        {
            LeanTween.cancel(wiggleTweenId);
            wiggleTweenId = -1;
        }
        LeanTween.rotateZ(gameObject, 0f, wiggleDuration * 0.5f);
    }

    private void ToggleExpand()
    {
        if (isExpanded) return;

        StopWiggle();
        CancelInvoke(nameof(StartWiggle));
        isExpanded = true;

        originalParent = transform.parent;
        originalPos    = transform.position;
        transform.SetParent(centerParent, true);

        LeanTween.move(gameObject, centerParent.position, 0.4f).setEaseOutBack();
        LeanTween.scale(gameObject, Vector3.one * expandedScale, 0.4f).setEaseOutBack()
            .setOnComplete(() =>
            {
                if (icon != null) icon.gameObject.SetActive(false);
                messagePanel.SetActive(true);
            });
    }

    public void Close()
    {
        messagePanel.SetActive(false);
        if (icon != null) icon.gameObject.SetActive(true);

        LeanTween.move(gameObject, originalPos, 0.35f).setEaseInBack();
        LeanTween.scale(gameObject, Vector3.zero, 0.35f).setEaseInBack()
            .setOnComplete(() =>
            {
                transform.SetParent(originalParent, true);
                Destroy(gameObject);
            });
    }
}
