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

    }
}
