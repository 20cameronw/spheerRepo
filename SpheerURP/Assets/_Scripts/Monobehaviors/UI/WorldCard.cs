using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class WorldCard : MonoBehaviour
{
    public TMP_Text TitleText;
    
    public int worldIndex;
    
    public Image Icon;

    public TMP_Text cost;

    public TMP_Text cores;

    public Button purchaseButton;

    public GameObject lockedImage;
    public GameObject unlockedImage;

    public void purchase()
    {
        TransactionManager.Instance.PurchaseWorld(worldIndex);
        isUnlocked(false);
    }

    public void isUnlocked(bool unlocked)
    {
        purchaseButton.interactable = unlocked;

        if (unlocked)
        {
            unlockedImage.SetActive(true);
            lockedImage.SetActive(false);
        }
        else
        {
            unlockedImage.SetActive(false);
            lockedImage.SetActive(true);
        }
        
    }
}
