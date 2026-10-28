namespace Frontier.Hyperspace.Tests
{
    /// <summary>
    /// Golden digests of SelfCheck.Run (plan, events, final state for each of the three canonical scenarios). They were produced on the .NET 8 CoreCLR and verified identical on
    /// Unity's Mono; any change to the simulation, the library or the default tuning changes them on purpose: regenerate with `run.ps1 digest` and update this table.
    /// </summary>
    public static class Golden
    {
        public static readonly ulong[] Digests =
        {
            0xEF8CE0688767BECEUL, 0x70AEECA16E204073UL, 0xAE61F53B4950E6C3UL,
            0x552FE83D957EBFF6UL, 0xF6C2864F84672986UL, 0x1CDD824B6AE5441CUL,
            0xAF6249FCE21DA302UL, 0x665088AB298DE860UL, 0xB6DBA49EF911AB6BUL,
        };
    }
}
