using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attached to the root GameObject of the procedurally generated enemy world.
/// Manages all <see cref="AttackBuildingView"/> children, tracks overall base
/// progress, and fires events that <see cref="AttackManager"/> listens to.
/// </summary>
public class AttackWorldView : MonoBehaviour, IAttackable
{
    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when every building on the base has been destroyed.</summary>
    public static event Action OnBaseDestroyed;

    /// <summary>Fired when a single building is destroyed. Passes the destroyed view.</summary>
    public static event Action<AttackBuildingView> OnBuildingDestroyedEvent;

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly List<AttackBuildingView> buildings = new List<AttackBuildingView>();

    private float baseHealth;
    private float currentBaseHealth;
    private bool  isDestroyed;

    // ── IAttackable ───────────────────────────────────────────────────────────

    public float CurrentHealth => currentBaseHealth;
    public float MaxHealth     => baseHealth;
    public bool  IsDestroyed   => isDestroyed;

    public void TakeDamage(float damage, AttackWeaponType weaponType)
    {
        if (isDestroyed) return;
        currentBaseHealth = Mathf.Max(0f, currentBaseHealth - damage);
        if (currentBaseHealth <= 0f) DestroyBase();
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="AttackManager"/> after all building prefabs have been
    /// placed.  Registers every <see cref="AttackBuildingView"/> found on children
    /// and calculates the aggregate base health.
    /// </summary>
    public void Initialise(EnemyBaseData data)
    {
        buildings.Clear();
        buildings.AddRange(GetComponentsInChildren<AttackBuildingView>(includeInactive: true));

        // Aggregate base health from all buildings
        baseHealth = 0f;
        foreach (AttackBuildingView b in buildings)
            baseHealth += b.MaxHealth;

        // Fallback so IAttackable.MaxHealth is never 0
        if (baseHealth <= 0f) baseHealth = 1f;
        currentBaseHealth = baseHealth;
    }

    // ── Building callbacks ────────────────────────────────────────────────────

    /// <summary>Called by <see cref="AttackBuildingView"/> when it is destroyed.</summary>
    public void OnBuildingDestroyed(AttackBuildingView building)
    {
        OnBuildingDestroyedEvent?.Invoke(building);

        // Subtract that building's max health from the base total
        currentBaseHealth = Mathf.Max(0f, currentBaseHealth - building.MaxHealth);

        bool allGone = true;
        foreach (AttackBuildingView b in buildings)
            if (!b.IsDestroyed) { allGone = false; break; }

        if (allGone) DestroyBase();
    }

    public List<AttackBuildingView> GetBuildings() => buildings;

    // ── Internal ──────────────────────────────────────────────────────────────

    private void DestroyBase()
    {
        if (isDestroyed) return;
        isDestroyed = true;
        OnBaseDestroyed?.Invoke();
    }
}
