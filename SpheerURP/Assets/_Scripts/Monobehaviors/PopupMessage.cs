using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PopupMessage : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private GameObject messagePanel; // Contains text + close button
    [SerializeField] private Button openButton;
    [SerializeField] private Image icon;

    [SerializeField] private float minimizedScale = 1f; // scale for circle
    [SerializeField] private float expandedScale = 10f;

    private Transform centerParent;
    private bool isExpanded = false;
    private Vector3 originalPos;
    private Transform originalParent;

    public void Setup(string message, Transform centerContainer)
    {
        messageText.text = message;
        centerParent = centerContainer;

        messagePanel.SetActive(false);

        transform.localScale = Vector3.one * minimizedScale;

        openButton.onClick.AddListener(ToggleExpand);
    }

    private void ToggleExpand()
    {
        Destroy(openButton.gameObject);
        Destroy(icon.gameObject);
        if (isExpanded) return;

        isExpanded = true;
        originalParent = transform.parent;
        originalPos = transform.position;

        transform.SetParent(centerParent, true);

        LeanTween.move(gameObject, centerParent.position, 0.4f).setEaseOutBack();
        LeanTween.scale(gameObject, Vector3.one * expandedScale, 0.4f).setEaseOutBack()
            .setOnComplete(() =>
            {
                messagePanel.SetActive(true);
            });
    }

    public void Close()
    {
        messagePanel.SetActive(false);

        LeanTween.move(gameObject, originalPos, 0.4f).setEaseInBack();
        LeanTween.scale(gameObject, Vector3.one * minimizedScale, 0.4f).setEaseInBack()
            .setOnComplete(() =>
            {
                transform.SetParent(originalParent, true);
                Destroy(gameObject);
            });
    }
}
