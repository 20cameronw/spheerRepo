using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Popup Message", menuName = "Popup Message")]
public class popupMessageSO : ScriptableObject
{
    public string message;
    public int xpLevel;
}
