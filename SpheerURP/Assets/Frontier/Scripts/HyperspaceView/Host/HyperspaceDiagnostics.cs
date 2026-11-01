using System.Text;
using UnityEngine;

namespace Frontier.Hyperspace.View
{
    public static class HyperspaceDiagnostics
    {
        // Verified x64 CoreCLR/Mono baselines. No replay sharing is enabled by this adapter.
        static readonly ulong[] expected =
        {
            0xEF8CE0688767BECEUL, 0x70AEECA16E204073UL, 0xAE61F53B4950E6C3UL,
            0x552FE83D957EBFF6UL, 0xF6C2864F84672986UL, 0x1CDD824B6AE5441CUL,
            0xAF6249FCE21DA302UL, 0x665088AB298DE860UL, 0xB6DBA49EF911AB6BUL
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
