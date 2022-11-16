using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResearchPanel : MonoBehaviour
{
    [SerializeField] private ResearchItemsListSO researchPanelInfo;
    private List<GameObject> researchCards;
    [SerializeField] private Transform contents;

    void Start()
    {
        researchCards = new List<GameObject>();
        for (int i = 0; i < researchPanelInfo.researchItemsSO.Length; i++)
        {
            GameObject card = Instantiate(researchPanelInfo.cardTemplate, contents);
            researchCards.Add(card);
        }
        LoadCards();
    }

    public void LoadCards()
    {
        for (int i = 0; i < researchPanelInfo.researchItemsSO.Length; i++)
        {
            ResearchCard currentCard = researchCards[i].GetComponent<ResearchCard>();
            Research researchInfo = researchPanelInfo.researchItemsSO[i];
            currentCard.TitleText.text = researchInfo.name;
            currentCard.bonus.text = "Bonus: " + researchInfo.bonus;
            currentCard.Icon.sprite = researchInfo.Icon;
            int index = researchInfo.upgradeIndex;
            currentCard.cost.text = "Cost: " + TransactionManager.Instance.getCostOfResearchUpgrade(index);
            currentCard.upgradeIndex = index;
        }
    }
}
