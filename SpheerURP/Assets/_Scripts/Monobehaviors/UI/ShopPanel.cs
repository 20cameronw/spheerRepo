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

            float cost = TransactionManager.Instance.getCostOfUpgradeStructure(index);
            if (cost > 99999)
            {
                currentShopCard.cost.text = "Cost: " + cost.ToString("0.##E0");
            }
            else
            {
                currentShopCard.cost.text = "Cost: " + cost.ToString("N0");
            }
            currentShopCard.count.text = "x" + Player.Instance.getNumberBuildings(i);


            int requiredLevel = shopPanelInfo.shopItemsSO[i].requiredXPLevel;

            if (requiredLevel <= Player.Instance.getCurrentXPLevel())
            {
                currentShopCard.lockMask.active = false;
            }
            else
            {
                currentShopCard.requiredXPText.text = "Required XP Level: " + requiredLevel;
                currentShopCard.lockMask.active = true;
            }
        }
    }
}
