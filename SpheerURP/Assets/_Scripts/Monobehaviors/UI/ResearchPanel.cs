using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResearchPanel : MenuPanel
{
    [SerializeField] private ResearchItemsListSO researchPanelInfo;
    private List<GameObject> researchCards;
    [SerializeField] private GameObject commonPanel;
    [SerializeField] private GameObject epicPanel;

    [SerializeField] private Transform commonPanelContents;
    [SerializeField] private Transform epicPanelContents;

    public void OpenCommon() {
        commonPanel.SetActive(true);
        epicPanel.SetActive(false);
    }

    public void OpenEpic() {
        commonPanel.SetActive(false);
        epicPanel.SetActive(true);
    }

    void Start()
    {
        researchCards = new List<GameObject>();
        for (int i = 0; i < researchPanelInfo.researchItemsSO.Length; i++)
        {
            Research researchInfo = researchPanelInfo.researchItemsSO[i];
            if (researchInfo.isEpic) {
                GameObject card = Instantiate(researchPanelInfo.epicCardTemplate, epicPanelContents);
                researchCards.Add(card);
            } else {
                GameObject card = Instantiate(researchPanelInfo.commonCardTemplate, commonPanelContents);
                researchCards.Add(card);
            }
        }
        LoadCards();
        gameObject.SetActive(false);
    }

    public void LoadCards()
    { 
        for (int i = 0; i < researchPanelInfo.researchItemsSO.Length; i++)
        {
            ResearchCard currentCard = researchCards[i].GetComponent<ResearchCard>();
            Research researchInfo = researchPanelInfo.researchItemsSO[i];
            currentCard.TitleText.text = researchInfo.name;
            currentCard.bonus.text = researchInfo.bonus;
            currentCard.Icon.sprite = researchInfo.Icon;
            int index = researchInfo.upgradeIndex;
            currentCard.upgradeIndex = index;
            currentCard.isEpic = researchInfo.isEpic;
            int timesPurchased = Player.Instance.getResearchCount(index);
            currentCard.counter.text = timesPurchased + "/" + researchInfo.maxPurchases;

            bool maxedOut = timesPurchased >= researchInfo.maxPurchases;
            bool levelLocked = !researchInfo.isEpic && Player.Instance.getCurrentXPLevel() < researchInfo.requiredXPLevel;

            if (maxedOut)
            {
                currentCard.button.interactable = false;
                currentCard.cost.text = "MAXED";
            }
            else if (levelLocked)
            {
                currentCard.button.interactable = false;
                currentCard.cost.text = "Level " + researchInfo.requiredXPLevel + " required";
            }
            else
            {
                currentCard.button.interactable = true;
                currentCard.cost.text = "" + TransactionManager.Instance.getCostOfResearchUpgrade(index);
            }
        }
    }
}
