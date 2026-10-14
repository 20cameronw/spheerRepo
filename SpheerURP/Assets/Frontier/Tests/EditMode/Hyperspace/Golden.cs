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
            0xEF8CE0688767BECEUL, 0x028F0E874BE49031UL, 0x67F9B6E19D5AAD44UL,
            0x552FE83D957EBFF6UL, 0xA1CF849613F13291UL, 0xE13DDE9D6F9F8CB4UL,
            0xAF6249FCE21DA302UL, 0x60F9F4B94E18EC86UL, 0x0A007E8631244178UL,
        };
    }
}
