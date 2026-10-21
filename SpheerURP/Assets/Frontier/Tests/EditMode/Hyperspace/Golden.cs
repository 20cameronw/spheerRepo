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
            0xEF8CE0688767BECEUL, 0x3BDCF5892839ADB0UL, 0xD4FD5ECD0922B0E3UL,
            0x552FE83D957EBFF6UL, 0xB0E94E1CACA52569UL, 0x97F345A4A76DEC70UL,
            0xAF6249FCE21DA302UL, 0x455BB62538A230B7UL, 0xC2FDE4538AA019F1UL,
        };
    }
}
