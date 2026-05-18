using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages the stack of notification icons on the right side of the screen.
/// New icons slide in from the right and stack vertically. Existing icons
/// shift upward to make room — Egg Inc–style.
/// </summary>
public class PopupManager : MonoBehaviour
{
    public static PopupManager Instance;

    [Tooltip("Vertical layout container anchored to the right edge of the screen.")]
    [SerializeField] private Transform popupIconContainer;

    [Tooltip("Centre-screen container used when a notification is expanded.")]
    [SerializeField] private Transform popupCenterContainer;

    [SerializeField] private GameObject popupPrefab;

    [Tooltip("Vertical gap between stacked icons (in canvas units).")]
    [SerializeField] private float iconSpacing = 90f;

    private List<PopupMessage> activeIcons = new List<PopupMessage>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void ShowPopup(string message)
    {
        // Nudge all existing icons upward to make room for the new one
        for (int i = 0; i < activeIcons.Count; i++)
        {
            if (activeIcons[i] == null) continue;
            RectTransform rt = activeIcons[i].GetComponent<RectTransform>();
            if (rt != null)
            {
                Vector2 target = rt.anchoredPosition + new Vector2(0f, iconSpacing);
                LeanTween.value(activeIcons[i].gameObject,
                    rt.anchoredPosition, target, 0.3f)
                    .setEaseOutCubic()
                    .setOnUpdateVector2(v => { if (rt != null) rt.anchoredPosition = v; });
            }
        }

        // Spawn the new icon at the bottom of the stack
        GameObject newPopupGO = Instantiate(popupPrefab, popupIconContainer);
        PopupMessage popup = newPopupGO.GetComponent<PopupMessage>();
        activeIcons.Add(popup);
        popup.Setup(message, popupCenterContainer);

        // Clean up destroyed entries periodically
        activeIcons.RemoveAll(p => p == null);
    }
}

