using System;
using Frontier.Hyperspace;
using Frontier.Hyperspace.Tests;

static class HyperspaceCheck
{
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "test") return Frontier.Universe.Runner.TestRunner.Run(args);
        var values = SelfCheck.Run(new HyperspaceTuning(), EncounterLibrary.CreateDefault());
        bool match = values.Length == Golden.Digests.Length;
        for (int i = 0; i < values.Length; i++)
        {
            Console.WriteLine(i + ": 0x" + values[i].ToString("X16"));
            if (i >= Golden.Digests.Length || values[i] != Golden.Digests[i]) match = false;
        }
        return match ? 0 : 1;
    }
}
