using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventManager : MonoBehaviour
{
    public delegate void MainClickAction();
    public static event MainClickAction OnClicked;

    public void mineResource()
    {
        if (OnClicked != null)
        {
            OnClicked();
        }
    }
}
