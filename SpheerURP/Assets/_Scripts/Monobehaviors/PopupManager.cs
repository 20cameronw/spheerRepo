using UnityEngine;
using System.Collections.Generic;

public class PopupManager : MonoBehaviour
{
    public static PopupManager Instance;

    [SerializeField] private Transform popupIconContainer;
    [SerializeField] private Transform popupCenterContainer;
    [SerializeField] private GameObject popupPrefab;

    private List<PopupMessage> activePopups = new List<PopupMessage>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void ShowPopup(string message)
    {
        GameObject newPopupGO = Instantiate(popupPrefab, popupIconContainer);
        PopupMessage popup = newPopupGO.GetComponent<PopupMessage>();
        popup.Setup(message, popupCenterContainer);
    }
}


