using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shop : MonoBehaviour
{
    [SerializeField] private GameObject StructuresPanel;
    [SerializeField] private GameObject ResearchPanel;
    [SerializeField] private GameObject BoostsPanel;

    private void DisableAll()
    {
        StructuresPanel.SetActive(false);
        ResearchPanel.SetActive(false);
        BoostsPanel.SetActive(false);
    }

    public void OpenPanel(string id)
    {
        DisableAll();
        switch (id)
        {
            case "Structures":
                StructuresPanel.SetActive(true);
                StructuresPanel.GetComponent<ShopPanel>().LoadCards();
                break;
            case "Research":
                ResearchPanel.SetActive(true);
                break;
            case "Boosts":
                BoostsPanel.SetActive(true);
                break;
            default:
                Debug.Log("No matching Panel ID");
                break;
        }
    }



}
