using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopPanel : MonoBehaviour
{
    [SerializeField] private ShopItemsListSO shopPanelInfo;
    private List<GameObject> shopCards;
    [SerializeField] private Transform contents;
    void Start()
    {
        shopCards = new List<GameObject>();
        for (int i = 0; i < shopPanelInfo.shopItemsSO.Length; i++)
        {
            GameObject card = Instantiate(shopPanelInfo.cardTemplate, contents);
            shopCards.Add(card);
        }
        LoadCards();
    }


    public void LoadCards()
    {
        for (int i = 0; i < shopPanelInfo.shopItemsSO.Length; i++)
        {
            var currentShopCard = shopCards[i].GetComponent<ShopCard>();
            currentShopCard.TitleText.text = shopPanelInfo.shopItemsSO[i].name;
        }
    }
}
