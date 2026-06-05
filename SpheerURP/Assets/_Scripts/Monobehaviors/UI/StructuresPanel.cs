using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StructuresPanel : MenuPanel
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
        gameObject.SetActive(false);
    }
    public override void OpenPanel()
    {
        base.OpenPanel();
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
            int count = Player.Instance.getNumberBuildings(i);
            currentShopCard.count.text = "x" + count;

            if (count <= 0) {
                currentShopCard.sellButton.interactable = false;
            } else {
                currentShopCard.sellButton.interactable = true;
            }


            int requiredLevel = shopPanelInfo.shopItemsSO[i].requiredXPLevel;
            int requiredTH    = shopPanelInfo.shopItemsSO[i].requiredTownHallLevel;
            float elecReq     = shopPanelInfo.shopItemsSO[i].electricityRequired;

            bool xpLocked  = requiredLevel > Player.Instance.getCurrentXPLevel();
            bool thLocked  = requiredTH > Player.Instance.getTownHallLevel();
            bool elecLocked = elecReq > 0f && Player.Instance.getElectricityFree() < elecReq;

            if (!xpLocked && !thLocked && !elecLocked)
            {
                currentShopCard.lockMask.active = false;
            }
            else
            {
                var reasons = new System.Collections.Generic.List<string>();
                if (xpLocked)   reasons.Add("XP Level " + requiredLevel);
                if (thLocked)   reasons.Add("Town Hall " + requiredTH);
                if (elecLocked) reasons.Add(elecReq + " ⚡");
                currentShopCard.requiredXPText.text = "Requires: " + string.Join(", ", reasons);
                currentShopCard.lockMask.active = true;
            }

            currentShopCard.panel = this;
        }
    }
}
