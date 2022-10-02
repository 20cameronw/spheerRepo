using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ResearchCard : MonoBehaviour
{
    public TMP_Text TitleText;
    
    public int upgradeIndex;
    
    public Image Icon;

    public TMP_Text cost;

    public TMP_Text bonus;

    public void purchase()
    {
        TransactionManager.Instance.PurchaseResearch(upgradeIndex);
    }
}
