using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject Settings;
    [SerializeField] private GameObject Shop;

    [Space(10)]
    [Header("Buttons Setup")]
    [SerializeField] private GameObject shopButton;
    private bool shopOpen;
    private Color baseButtonColor;
    private bool settingsOpen;


    void Start()
    {
        baseButtonColor = shopButton.GetComponent<Image>().color;
    }

    private void OpenShop()
    {
        if (settingsOpen)
        {
            CloseSettings();
        }
        Shop.SetActive(true);
        lerpButtonColor();
        shopOpen = true;
    }

    private void CloseShop()
    {
        Shop.SetActive(false);
        lerpButtonColor();
        shopOpen = false;
    }

    private void OpenSettings()
    {
        if (shopOpen)
        {
            CloseShop();
        }
        Settings.SetActive(true);
        lerpButtonColor();
        settingsOpen = true;
    }

    private void CloseSettings()
    {
        Settings.SetActive(false);
        lerpButtonColor();
        settingsOpen = false;
    }

    public void toggleSettings()
    {
        if (settingsOpen)
        {
            CloseSettings();
        }
        else
        {
            OpenSettings();
        }
    }

    public void toggleShop()
    {
        if (shopOpen)
        {
            CloseShop();
        }
        else if (settingsOpen)
        {
            CloseSettings();
        }
        else
        {
            OpenShop();
        }
    }

    private void lerpButtonColor()
    {
        //TODO: Lerp this shit
        if (shopOpen || settingsOpen)
        {
            shopButton.GetComponent<Image>().color = baseButtonColor;
            LeanTween.rotateLocal(shopButton, new Vector3(0, 0, 0), .6f).setEase(LeanTweenType.easeOutBack);
        }
        else
        {
            shopButton.GetComponent<Image>().color = Color.red;
            LeanTween.rotateLocal(shopButton, new Vector3(0, 0, 45), .6f).setEase(LeanTweenType.easeOutBack);
        }
    }

}
