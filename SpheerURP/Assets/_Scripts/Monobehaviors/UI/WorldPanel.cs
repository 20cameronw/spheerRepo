using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldPanel : MonoBehaviour
{
    [SerializeField] private WorldsListSO worldPanelInfo;
    private List<GameObject> worldCards;
    [SerializeField] private Transform contents;

    void Start()
    {
        worldCards = new List<GameObject>();
        for (int i = 0; i < worldPanelInfo.worldsList.Length; i++)
        {
            GameObject card = Instantiate(worldPanelInfo.cardTemplate, contents);
            worldCards.Add(card);
        }
        LoadCards();
    }

    public void LoadCards()
    {
        for (int i = 0; i < worldPanelInfo.worldsList.Length; i++)
        {
            WorldCard currentCard = worldCards[i].GetComponent<WorldCard>();
            World worldInfo = worldPanelInfo.worldsList[i];
            currentCard.TitleText.text = worldInfo.name;
            currentCard.Icon.sprite = worldInfo.Icon;
            currentCard.cost.text = "Cost: " + worldInfo.cost;
            currentCard.worldIndex = i;
        }
    }
}
