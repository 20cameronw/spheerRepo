using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject MainMenu;
    [SerializeField] private GameObject SettingsMenu;
    [SerializeField] private GameObject Shop;
    [SerializeField] private GameObject WorldsShop;

    private GameObject currentMenu;


    public void OpenMainMenu()
    {
        currentMenu = Instantiate(MainMenu);
    }

    public void CloseMenu()
    {
        Destroy(currentMenu);
    }

}
