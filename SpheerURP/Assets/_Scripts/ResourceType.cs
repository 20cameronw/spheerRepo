/// <summary>
/// The resource types produced and consumed in Spheer.
/// Each building declares its type via <see cref="Upgrade.resourceProduced"/>.
/// </summary>
/// <remarks>
/// Routing is handled by <c>Player.RoutePassiveIncome</c>.
/// Storage types (NebuliteStorage, PlasmaStorage) add the building's <c>bonus</c>
/// to the corresponding capacity cap instead of generating per-second income —
/// the same pattern Electricity uses for <c>electricityCapacity</c>.
/// </remarks>
public enum ResourceType
{
    Nebulite,         // Primary building/upgrade resource (was "dollars")
    Plasma,           // Used for research and troops
    Electricity,      // Powers advanced buildings — bonus routes to electricityCapacity
    VoidCrystal,      // Late-game elite resource
    NebuliteStorage,  // Phase 2: building increases Nebulite storage cap (NebuliteVault)
    PlasmaStorage,    // Phase 2: building increases Plasma storage cap (PlasmaTank)
}
