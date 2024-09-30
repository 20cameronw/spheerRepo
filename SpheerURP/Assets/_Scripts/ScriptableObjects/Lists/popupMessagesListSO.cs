using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Popup Message List", menuName = "List of Popup Messages")]
public class popupMessagesListSO : ScriptableObject
{
    public List<popupMessageSO> popupMessages;
}
