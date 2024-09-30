using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class HealthBar : MonoBehaviour
{
    private Camera cam;
    [SerializeField] private Image healthBarSprite;

    void Start()
    {
        cam = Camera.main;
    }
  
    public void UpdateHealthBar(float maxHealth, float currentHealth)
    {
        healthBarSprite.fillAmount = currentHealth / maxHealth;
    }

    void Update() 
    {
        Vector3 upDirection = cam.transform.up;
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, upDirection);
    }
}
