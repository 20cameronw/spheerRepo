using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static LeanTween;
using TMPro;

public class PopupMessage : MonoBehaviour
{
    public GameObject messageBox;
    public Button closeButton;

    public TMP_Text messageText;

    private void Start()
    {
        // Set the initial scale of the message box to zero
        messageBox.transform.localScale = Vector3.zero;

        messageBox = this.gameObject;
        closeButton = messageBox.GetComponentInChildren<Button>();
        
        // Add a listener to the close button
        closeButton.onClick.AddListener(TweenOutMessageBox);

        // Tween in the message box
        LeanTween.scale(messageBox, Vector3.one, 0.5f).setEase(LeanTweenType.easeOutBack);
    }

    private void TweenOutMessageBox()
    {
        LeanTween.scale(messageBox, Vector3.zero, 0.5f).setEase(LeanTweenType.easeInBack).setOnComplete(DestroyMessageBox);
    }

    private void DestroyMessageBox()
    {
        Destroy(this.gameObject);
    }
}
