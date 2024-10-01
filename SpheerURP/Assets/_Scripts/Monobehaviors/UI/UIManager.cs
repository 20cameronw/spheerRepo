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

    [Space(10)]
    [Header("Pop up manager")]
    [SerializeField] private GameObject popupPrefab;
    [SerializeField] private Transform popupParent;
    [SerializeField] private popupMessagesListSO popupMessages;

    
    void Start()
    {
        baseButtonColor = shopButton.GetComponent<Image>().color;
        StartCoroutine(CheckPopUpMessages());
    }

    public IEnumerator CheckPopUpMessages()
    {
        while (true)
        {
            yield return new WaitForSeconds(5);

            for (int i = 0; i < popupMessages.popupMessages.Count; i++)
            {
                if (Player.Instance.shouldCardBeShown(i))
                {
                    GameObject popup = Instantiate(popupPrefab, popupParent);
                    popup.GetComponentInChildren<PopupMessage>().messageText.text = popupMessages.popupMessages[i].message;
                    popup.GetComponentInChildren<PopupMessage>().hasOptions = popupMessages.popupMessages[i].hasOptions;
                    
                    Player.Instance.setCardShown(i);
                }   
            }
        }
    }

    private void OpenShop()
    {
        if (settingsOpen)
        {
            CloseSettings();
        }
        lerpShop(false);
        lerpButtonColor();
        shopOpen = true;
    }

    private void CloseShop()
    {
        lerpShop(true);
        lerpButtonColor();
        shopOpen = false;
    }

    private void lerpShop(bool close)
    {
        LeanTween.moveLocalX(Shop, close ? -400 : 0, .6f).setEase(LeanTweenType.easeOutBack);
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
