using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TopBar : MonoBehaviour
{
    [SerializeField] private TMP_Text DollarsText;
    // Start is called before the first frame update
    private void Start()
    {
        InvokeRepeating("UpdateDollars", 1f, 1f);
    }

    private void UpdateDollars()
    {
        DollarsText.text = Player.Instance.getDollars().ToString();
    }
}
