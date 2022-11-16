using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class WorldCard : MonoBehaviour
{
    public TMP_Text TitleText;
    
    public int worldIndex;
    
    public Image Icon;

    public TMP_Text cost;

    public TMP_Text bonus;

    public void purchase()
    {
        TransactionManager.Instance.PurchaseWorld(worldIndex);
    }
}
