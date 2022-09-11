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
            currentShopCard.description.text = shopPanelInfo.shopItemsSO[i].description;
            currentShopCard.bonus.text = "Bonus: " + shopPanelInfo.shopItemsSO[i].bonus.ToString();
            currentShopCard.icon.sprite = shopPanelInfo.shopItemsSO[i].Icon;
            int index = shopPanelInfo.shopItemsSO[i].upgradeIndex;
            currentShopCard.upgradeIndex = index;
            currentShopCard.cost.text = "Cost: " + TransactionManager.Instance.getCostOfUpgradeStructure(index);
        }
    }
}
