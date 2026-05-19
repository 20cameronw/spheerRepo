using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Placed on each building GameObject in the procedurally generated enemy world.
/// Tracks that building's health during an attack and implements both
/// <see cref="IAttackable"/> and <see cref="IDefenseStructure"/> so weapons can
/// query resistances and deal damage without knowing concrete types.
/// </summary>
public class AttackBuildingView : MonoBehaviour, IDefenseStructure
{
    [Header("Combat Stats")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float defenseRating = 0f;

    [Header("Resistances")]
    [SerializeField] private AttackWeaponType[] vulnerableTo = new AttackWeaponType[0];
    [SerializeField] private AttackWeaponType[] resistantTo  = new AttackWeaponType[0];

    [Header("Optional UI")]
    [SerializeField] private Slider healthBarSlider;

    private float currentHealth;
    private bool  isDestroyed;

    // ── IAttackable ───────────────────────────────────────────────────────────
    public float CurrentHealth => currentHealth;
    public float MaxHealth     => maxHealth;
    public bool  IsDestroyed   => isDestroyed;

    // ── IDefenseStructure ─────────────────────────────────────────────────────
    public AttackWeaponType[] VulnerableTo => vulnerableTo;
    public AttackWeaponType[] ResistantTo  => resistantTo;
    public float DefenseRating             => defenseRating;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        currentHealth = maxHealth;
        UpdateHealthBar();
    }

    public void Initialise(float health, float defense)
    {
        maxHealth     = health;
        defenseRating = defense;
        currentHealth = maxHealth;
        if (healthBarSlider != null) healthBarSlider.maxValue = maxHealth;
        UpdateHealthBar();
    }

    // ── IAttackable ───────────────────────────────────────────────────────────

    public void TakeDamage(float damage, AttackWeaponType weaponType)
    {
        if (isDestroyed) return;

        float effective = CalculateEffectiveDamage(damage, weaponType);
        currentHealth = Mathf.Max(0f, currentHealth - effective);
        UpdateHealthBar();

        if (currentHealth <= 0f)
            DestroyBuilding();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private float CalculateEffectiveDamage(float raw, AttackWeaponType weaponType)
    {
        float dmg = Mathf.Max(0f, raw - defenseRating);

        foreach (AttackWeaponType t in vulnerableTo)
            if (t == weaponType) { dmg *= 2f; break; }

        foreach (AttackWeaponType t in resistantTo)
            if (t == weaponType) { dmg *= 0.5f; break; }

        return dmg;
    }

    private void DestroyBuilding()
    {
        isDestroyed = true;
        AttackWorldView worldView = GetComponentInParent<AttackWorldView>();
        worldView?.OnBuildingDestroyed(this);
        gameObject.SetActive(false);
    }

    private void UpdateHealthBar()
    {
        if (healthBarSlider == null) return;
        healthBarSlider.maxValue = maxHealth;
        healthBarSlider.value    = currentHealth;
    }
}
