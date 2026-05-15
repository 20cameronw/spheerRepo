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

    public bool isEpic;

    public TMP_Text counter;

    public Button button;

    // Attach the root CanvasGroup here; the panel sets alpha/interactable to grey out locked cards.
    public CanvasGroup lockGroup;

    public void purchase()
    {
        TransactionManager.Instance.PurchaseResearch(upgradeIndex, isEpic);
    }

}
