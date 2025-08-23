using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class MenuPanel : MonoBehaviour
{
    protected RectTransform rectTransform;
    public bool isOpen = false; // Tracks whether the panel is open

    protected virtual void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public virtual void OpenPanel()
    {
        if (isOpen) return; // Prevent opening if already open

        isOpen = true;
        gameObject.SetActive(true);
        rectTransform.localScale = Vector3.zero;

        LeanTween.scale(rectTransform, Vector3.one, 0.3f)
            .setEase(LeanTweenType.easeOutBack)
            .setOnComplete(() => isOpen = true); // Confirm panel is fully open
    }

    public virtual void ClosePanel()
    {
        if (!isOpen) return; // Prevent closing if already closed

        isOpen = false;
        LeanTween.scale(rectTransform, Vector3.zero, 0.2f)
            .setEase(LeanTweenType.easeInBack)
            .setOnComplete(() =>
            {
                gameObject.SetActive(false);
                isOpen = false; // Confirm panel is fully closed
            });
    }

}
