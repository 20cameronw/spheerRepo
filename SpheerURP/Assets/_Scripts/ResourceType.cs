/// <summary>
/// The resource types produced and consumed in Spheer.
/// Each building declares which resource it produces via Upgrade.resourceProduced.
/// </summary>
public enum ResourceType
{
    Nebulite,    // Primary building/upgrade resource (was "dollars")
    Plasma,      // Used for research and troops
    Electricity, // Powers advanced buildings (capacity check, not consumed)
    VoidCrystal  // Late-game elite resource
}
