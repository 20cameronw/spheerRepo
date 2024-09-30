using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class EnemyHealth : MonoBehaviour
{

    [SerializeField]
    private float maxHealthPoints;

    [SerializeField]
    private float healthScalarPerXPLevel;

    [SerializeField]
    private Slider healthBar;

    [SerializeField]
    private int xpWorth;

    private float currentHealth;

    public void SetCurrentHealth(float value)
    {
        currentHealth = value;
        healthBar.value = currentHealth;
    }

    void Start()
    {
        int level = Player.Instance.getCurrentXPLevel();
        for (int i = 0; i < level; i++)
        {
            maxHealthPoints *= healthScalarPerXPLevel;
        }
        currentHealth = maxHealthPoints;
        healthBar.maxValue = currentHealth;
        healthBar.value = currentHealth;
    }

    public void TakeDamage(float damage)
    {
        float healthGoal = currentHealth - damage;
        SetCurrentHealth(healthGoal);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Player.Instance.addXpPoints(xpWorth);
        Destroy(gameObject);
    }
}