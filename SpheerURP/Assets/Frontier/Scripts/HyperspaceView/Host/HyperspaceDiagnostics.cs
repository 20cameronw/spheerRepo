using System.Text;
using UnityEngine;

namespace Frontier.Hyperspace.View
{
    public static class HyperspaceDiagnostics
    {
        // Verified x64 CoreCLR/Mono baselines. No replay sharing is enabled by this adapter.
        static readonly ulong[] expected =
        {
            0xEF8CE0688767BECEUL, 0x028F0E874BE49031UL, 0x67F9B6E19D5AAD44UL,
            0x552FE83D957EBFF6UL, 0xA1CF849613F13291UL, 0xE13DDE9D6F9F8CB4UL,
            0xAF6249FCE21DA302UL, 0x60F9F4B94E18EC86UL, 0x0A007E8631244178UL
        };
        public static string Check()
        {
            var actual = SelfCheck.Run(new HyperspaceTuning(), HyperspaceSetup.Library);
            bool match = actual.Length == expected.Length;
            var log = new StringBuilder("Hyperspace determinism self-check\n");
            for (int i = 0; i < actual.Length; i++)
            {
                if (i >= expected.Length || actual[i] != expected[i]) match = false;
                log.Append(i).Append(": 0x").Append(actual[i].ToString("X16")).AppendLine();
            }
            log.Append(match ? "PASS: all nine golden digests match" : "MISMATCH: do not enable cross-device replays");
            Debug.Log(log.ToString());
            return match ? "Hyperspace: 9/9 golden digests match" : "Hyperspace: runtime mismatch (see player log)";
        }
    }
}
