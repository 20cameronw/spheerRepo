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

        // Build a sorted list: epics first (no level gate), then commons sorted by requiredXPLevel ascending.
        // We preserve the original array so upgradeIndex values remain correct — we only change spawn order.
        Research[] allResearch = researchPanelInfo.researchItemsSO;

        // Create a mapping: researchItemsSO index → instantiated card GameObject
        // Epics are spawned unsorted (no XP gate), commons are spawned sorted by requiredXPLevel.
        List<int> commonIndices = new List<int>();
        for (int i = 0; i < allResearch.Length; i++)
        {
            if (!allResearch[i].isEpic) commonIndices.Add(i);
        }
        commonIndices.Sort((a, b) => allResearch[a].requiredXPLevel.CompareTo(allResearch[b].requiredXPLevel));

        // Instantiate in original array order so researchCards[i] still maps to researchItemsSO[i].
        for (int i = 0; i < allResearch.Length; i++)
            researchCards.Add(null);

        // Epic cards
        for (int i = 0; i < allResearch.Length; i++)
        {
            if (allResearch[i].isEpic)
            {
                GameObject card = Instantiate(researchPanelInfo.epicCardTemplate, epicPanelContents);
                researchCards[i] = card;
            }
        }

        // Common cards in XP-level order
        foreach (int i in commonIndices)
        {
            GameObject card = Instantiate(researchPanelInfo.commonCardTemplate, commonPanelContents);
            researchCards[i] = card;
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
            // Epics never have an XP level gate.
            bool levelLocked = !researchInfo.isEpic && Player.Instance.getCurrentXPLevel() < researchInfo.requiredXPLevel;

            if (levelLocked)
            {
                // Grey out entire card — hide all content so nothing is revealed until unlocked.
                if (currentCard.lockGroup != null)
                {
                    currentCard.lockGroup.alpha = 0.25f;
                    currentCard.lockGroup.interactable = false;
                    currentCard.lockGroup.blocksRaycasts = false;
                }
                currentCard.button.interactable = false;
                currentCard.cost.text = "Lvl " + researchInfo.requiredXPLevel;
            }
            else if (maxedOut)
            {
                if (currentCard.lockGroup != null)
                {
                    currentCard.lockGroup.alpha = 1f;
                    currentCard.lockGroup.interactable = true;
                    currentCard.lockGroup.blocksRaycasts = true;
                }
                currentCard.button.interactable = false;
                currentCard.cost.text = "MAXED";
            }
            else
            {
                if (currentCard.lockGroup != null)
                {
                    currentCard.lockGroup.alpha = 1f;
                    currentCard.lockGroup.interactable = true;
                    currentCard.lockGroup.blocksRaycasts = true;
                }
                currentCard.button.interactable = true;
                currentCard.cost.text = "" + TransactionManager.Instance.getCostOfResearchUpgrade(index);
            }
        }
    }
}
