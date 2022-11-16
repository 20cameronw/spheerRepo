using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ShopCard : MonoBehaviour
{
    public TMP_Text TitleText;

    public int upgradeIndex;

    public Image icon;

    public TMP_Text cost;

    public TMP_Text bonus;

    public TMP_Text description;

    public TMP_Text count;

    public void purchase()
    {
        TransactionManager.Instance.PurchaseSomething(upgradeIndex);
    }

    public void sellItem()
    {
        TransactionManager.Instance.SellStructure(upgradeIndex);
    }
    
}
