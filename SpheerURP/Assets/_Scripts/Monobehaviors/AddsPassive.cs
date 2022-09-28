using UnityEngine;
using System.Collections;

public class AddsPassive : MonoBehaviour
{
    private float amount;

    public void setAmount(float amt)
    {
        amount = amt;
    }

    public void Start()
    {
        StartCoroutine("contributePassive");
        if (amount == 0)
        {
            Destroy(this); //removes this script instance from the game object
        }
    }

    private IEnumerator contributePassive()
    {
        while (true)
        {
            Player.Instance.addToPassive(amount);
            yield return new WaitForSeconds(1f);
        }
    }
}