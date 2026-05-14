using UnityEngine;
using UnityEngine.UI;

public class EnemyHealth : MonoBehaviour
{
    [SerializeField] private float maxHealthPoints;

    [SerializeField] private float healthScalarPerXPLevel;

    [SerializeField] private float waveHealthScalar = 1.08f;

    [SerializeField] private Slider healthBar;

    [SerializeField] private int xpWorth;

    private float currentHealth;

    private bool isDying = false;

    public void SetCurrentHealth(float value)
    {
        currentHealth = value;
        healthBar.value = currentHealth;
    }

    void Start()
    {
        int   level = Player.Instance.getCurrentXPLevel();
        int   wave  = EnemySpawner.Instance.currentWave;

        // Scale health by XP level
        for (int i = 0; i < level; i++)
            maxHealthPoints *= healthScalarPerXPLevel;

        // Scale health by wave number (compounding per wave)
        maxHealthPoints *= Mathf.Pow(waveHealthScalar, wave);

        currentHealth        = maxHealthPoints;
        healthBar.maxValue   = currentHealth;
        healthBar.value      = currentHealth;
    }

    public void TakeDamage(float damage)
    {
        float healthGoal = currentHealth - damage;
        SetCurrentHealth(healthGoal);

        if (currentHealth <= 0 && !isDying)
        {
            isDying = true;
            Die();
        }
    }

    private void Die()
    {
        // Clear the player's target if this enemy was being targeted
        if (Player.Instance.GetTarget() == transform)
            Player.Instance.ClearTarget();

        Player.Instance.addXpPoints(xpWorth);
        EnemySpawner.Instance.handleAlienDeath();
        Destroy(gameObject);
    }
}