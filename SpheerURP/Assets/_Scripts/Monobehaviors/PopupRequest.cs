using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum PopupType { RewardAd, Gift, Notification }

[System.Serializable]
public class PopupRequest
{
    public PopupType Type;
    public GameObject PopupPrefab;
    public string Message;
}
