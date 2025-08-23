using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class WorldsPanel : MenuPanel
{
    [SerializeField] private WorldsListSO worldPanelInfo;
    private List<GameObject> worldCards;
    [SerializeField] private TMP_Text WorldName;
    [SerializeField] private TMP_Text ClickValue;
    [SerializeField] private TMP_Text Description;
    [SerializeField] private Transform iconsParent;
    [SerializeField] private Image WorldImage;
    [SerializeField] private TMP_Text CurrentWorldValue;

    [SerializeField] private GameObject CurrentWorldStaticText;

    [SerializeField] private TMP_Text PreviousCoresText;
    [SerializeField] private GameObject BuyWorldButton;

    [SerializeField] private GameObject tinyIconPrefab;

    [SerializeField] private GameObject undiscoveredIconPrefab;



    private int currentPageIndex = 0;

    void Start() {
        for (int i = 0; i < worldPanelInfo.worldsList.Length; i++) {
            GameObject icon = Instantiate(tinyIconPrefab, iconsParent);
            icon.GetComponent<Image>().sprite = worldPanelInfo.worldsList[i].Icon;
        }
        loadCurrentPage();
    }

    public void cycleRight() {
        if (currentPageIndex + 1 < worldPanelInfo.worldsList.Length) {
            currentPageIndex++;
            loadCurrentPage();
        }
    }

    public void cycleLeft() {
        if (currentPageIndex > 0) {
            currentPageIndex--;
            loadCurrentPage();
        }
    }


    public void loadCurrentPage() {
        World worldInfo = worldPanelInfo.worldsList[currentPageIndex];
        WorldName.text = worldInfo.name;
        Description.text = worldInfo.description;
        WorldImage.sprite = worldInfo.Icon;
        ClickValue.text = worldInfo.cores.ToString();
        CurrentWorldValue.text = "Current World Value: " + Mathf.Round(Player.Instance.getDollars());
        if (Player.Instance.getCurrentWorld() == currentPageIndex) {
            CurrentWorldStaticText.GetComponent<TMP_Text>().text = "CURRENT WORLD";
            CurrentWorldStaticText.SetActive(true);
            BuyWorldButton.SetActive(false);
        } else if (currentPageIndex < Player.Instance.getCurrentWorld()) {
            CurrentWorldStaticText.GetComponent<TMP_Text>().text = "PREVIOUS WORLD";
            CurrentWorldStaticText.SetActive(true);
            BuyWorldButton.SetActive(false);
        } else if (Player.Instance.getCurrentWorld() + 1 == currentPageIndex){
            if (Player.Instance.getDollars() >= worldInfo.cost)
            {
                CurrentWorldStaticText.SetActive(false);
                PreviousCoresText.text = "+" + worldPanelInfo.worldsList[currentPageIndex - 1].cores.ToString() + " cores";
                BuyWorldButton.SetActive(true);
            }
            else
            {
                CurrentWorldStaticText.GetComponent<TMP_Text>().text = "Cost: " + worldInfo.cost;
                CurrentWorldStaticText.SetActive(true);
                BuyWorldButton.SetActive(false);
            }

        } else {
            CurrentWorldStaticText.GetComponent<TMP_Text>().text = "TOO FAR AWAY";
            CurrentWorldStaticText.SetActive(true);
            BuyWorldButton.SetActive(false);
        }
    }

    public void buyWorld() {
        TransactionManager.Instance.PurchaseWorld(currentPageIndex);
        loadCurrentPage();
    }
}
