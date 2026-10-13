using NUnit.Framework;
using Uni = Frontier.Universe.Universe;

namespace Frontier.Tests
{
    /// <summary>
    /// The universe golden digest under the PlayMode runtime. EditMode proves it on the editor's Mono; this fixture exists so the
    /// same check can run on a device build (Test Runner > PlayMode > Run on a connected IL2CPP/ARM64 player), where the C++
    /// compiler could in principle evaluate double arithmetic differently. A mismatch there means the generator is not
    /// device-deterministic and saved discoveries would drift.
    /// </summary>
    public class UniverseDeterminismTests
    {
        // Same value and region as Frontier.Universe.Tests.DeterminismTests.GoldenPlain / PlainX0..PlainY1 (the EditMode assembly
        // cannot be referenced from here). Generator V1 is frozen: both constants change only together with a new GeneratorVersion.
        const ulong GoldenPlain = 0x8133861D42016686UL;
        const int X0 = 55, Y0 = -190, X1 = 105, Y1 = -150;

        [Test]
        public void PureGenerator_Over2000Sectors_MatchesTheGoldenDigest()
        {
            var universe = new Uni(new Frontier.Universe.UniverseConfig());
            ulong digest = Frontier.Universe.UniverseDigest.OfRegion(universe, X0, Y0, X1, Y1);
            Assert.AreEqual(GoldenPlain, digest, "universe digest 0x" + digest.ToString("X16") + " differs on this runtime");
        }
    }
}
