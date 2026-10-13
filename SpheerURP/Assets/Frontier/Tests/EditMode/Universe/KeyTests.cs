using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Frontier.Universe.Tests
{
    [TestFixture]
    public class KeyTests
    {
        static PlanetKey RandomKey(ref Rng r)
        {
            int x = r.NextInt(SectorCoord.MinCoord, SectorCoord.MaxCoord + 1);
            int y = r.NextInt(SectorCoord.MinCoord, SectorCoord.MaxCoord + 1);
            int sys = r.Chance(0.3) ? SystemKey.AuthoredBase + r.NextInt(SystemKey.MaxIndex - SystemKey.AuthoredBase + 1) : r.NextInt(SystemKey.AuthoredBase);
            return new PlanetKey(x, y, sys, r.NextInt(PlanetKey.MaxIndex + 1));
        }

        [Test]
        public void PlanetKey_RoundTripsThroughStringAndId()
        {
            var r = new Rng(2024UL);
            for (int i = 0; i < 50000; i++)
            {
                PlanetKey k = i < 200 ? new PlanetKey((i % 9) - 4, (i % 7) - 3, i % 5, i % 15) : RandomKey(ref r);
                string s = k.ToString();
                PlanetKey back;
                Assert.IsTrue(PlanetKey.TryParse(s, out back), s);
                Assert.AreEqual(k, back, s);
                PlanetKey fromId;
                Assert.IsTrue(PlanetKey.TryFromId(k.Id, out fromId));
                Assert.AreEqual(k, fromId);

                SystemKey sk = k.System, skBack;
                Assert.IsTrue(SystemKey.TryParse(sk.ToString(), out skBack));
                Assert.AreEqual(sk, skBack);
                SectorCoord sc = sk.Sector, scBack;
                Assert.IsTrue(SectorCoord.TryParse(sc.ToString(), out scBack));
                Assert.AreEqual(sc, scBack);
            }
            Assert.AreEqual("p:12:-4:2:3", new PlanetKey(12, -4, 2, 3).ToString());
            Assert.AreEqual("s:12:-4:2", new SystemKey(12, -4, 2).ToString());
            Assert.AreEqual("c:-1:0", new SectorCoord(-1, 0).ToString());
        }

        [Test]
        public void Ids_AreUniqueAcrossFiftyThousandKeys()
        {
            var planetIds = new Dictionary<ulong, PlanetKey>();
            var planetStrings = new Dictionary<string, PlanetKey>();
            var systemIds = new Dictionary<ulong, SystemKey>();
            var all = new HashSet<ulong>();
            int collisions = 0, planets = 0, systems = 0;

            Action<PlanetKey> add = k =>
            {
                PlanetKey other;
                if (planetIds.TryGetValue(k.Id, out other)) { if (other != k) collisions++; return; }
                planetIds[k.Id] = k;
                planets++;
                string s = k.ToString();
                if (planetStrings.TryGetValue(s, out other) && other != k) collisions++;
                planetStrings[s] = k;
                if (!all.Add(k.Id)) collisions++;
                SystemKey sk = k.System;
                if (!systemIds.ContainsKey(sk.Id))
                {
                    systemIds[sk.Id] = sk;
                    systems++;
                    if (!all.Add(sk.Id)) collisions++;
                    if (!all.Add(sk.Sector.Id)) { /* a sector id may be shared by systems of the same sector; only kind-crossing matters */ }
                }
            };

            for (int y = -12; y < 13; y++)
                for (int x = -12; x < 13; x++)
                    for (int s = 0; s < 4; s++)
                        for (int p = 0; p < 8; p++)
                            add(new PlanetKey(x, y, s < 3 ? s : SystemKey.AuthoredBase + (x & 3), p));
            var rng = new Rng(77UL);
            while (planets < 50000) add(RandomKey(ref rng));

            TestLog.Info("sampled " + planets + " distinct planet keys in " + systems + " systems: id collisions = " + collisions);
            Assert.AreEqual(50000, planets);
            Assert.AreEqual(0, collisions);
            Assert.AreEqual(planets, planetStrings.Count);
        }

        [Test]
        public void SectorSystemAndPlanetIds_NeverCollideAcrossKinds()
        {
            var seen = new HashSet<ulong>();
            for (int y = -5; y <= 5; y++)
            {
                for (int x = -5; x <= 5; x++)
                {
                    var sc = new SectorCoord(x, y);
                    Assert.IsTrue(seen.Add(sc.Id));
                    for (int s = 0; s < 6; s++)
                    {
                        var sk = new SystemKey(sc, s);
                        Assert.IsTrue(seen.Add(sk.Id), sk.ToString());
                        for (int p = 0; p <= PlanetKey.MaxIndex; p++) Assert.IsTrue(seen.Add(sk.Planet(p).Id));
                    }
                }
            }
        }

        [Test]
        public void ExtremeCoordinates_PackWithoutLoss()
        {
            int[] xs = { SectorCoord.MinCoord, SectorCoord.MinCoord + 1, -1, 0, 1, SectorCoord.MaxCoord - 1, SectorCoord.MaxCoord };
            foreach (int x in xs)
            {
                foreach (int y in xs)
                {
                    var k = new PlanetKey(x, y, SystemKey.MaxIndex, PlanetKey.MaxIndex);
                    Assert.IsTrue(k.IsValid);
                    PlanetKey back;
                    Assert.IsTrue(PlanetKey.TryFromId(k.Id, out back));
                    Assert.AreEqual(k, back);
                    Assert.IsTrue(PlanetKey.TryParse(k.ToString(), out back));
                    Assert.AreEqual(k, back);
                }
            }
            Assert.IsFalse(new SectorCoord(SectorCoord.MaxCoord + 1, 0).IsValid);
            Assert.IsFalse(new PlanetKey(0, 0, 5, PlanetKey.MaxIndex + 1).IsValid);
            Assert.IsFalse(new PlanetKey(0, 0, KeyBitsSectorIndex, 0).IsValid);
        }

        const int KeyBitsSectorIndex = 0xFFF;

        [Test]
        public void TryParse_RejectsMalformedStrings()
        {
            string[] bad =
            {
                null, "", "p", "p:", "p:1:2:3", "p:1:2:3:", "p:1:2:3:4:5", "p:a:2:3:4", "p:1:2:3:x", "q:1:2:3:4", "P:1:2:3:4",
                "p:01:2:3:4", "p:1:2:3:04", "p:-0:2:3:4", "p:+1:2:3:4", "p:1:2:3:15", "p:1:2:4095:0", "p:1:2:-1:0", "p:1:2:3:-1",
                "p: 1:2:3:4", "p:1:2:3:4 ", "p:8388608:0:0:0", "p:-8388609:0:0:0", "p:1::3:4", "p:99999999999:0:0:0"
            };
            foreach (string s in bad)
            {
                PlanetKey k;
                Assert.IsFalse(PlanetKey.TryParse(s, out k), "should reject '" + s + "'");
            }
            SystemKey sk;
            Assert.IsFalse(SystemKey.TryParse("s:1:2", out sk));
            Assert.IsFalse(SystemKey.TryParse("s:1:2:4095", out sk));
            SectorCoord sc;
            Assert.IsFalse(SectorCoord.TryParse("c:1", out sc));
            Assert.IsFalse(SectorCoord.TryParse("c:1:2:3", out sc));
        }

        [Test]
        public void SectorOf_UsesFloorDivisionOnLightYears()
        {
            Assert.AreEqual(new SectorCoord(0, 0), SectorCoord.FromLy(0.0, 63.999, 64.0));
            Assert.AreEqual(new SectorCoord(1, 0), SectorCoord.FromLy(64.0, 0.0, 64.0));
            Assert.AreEqual(new SectorCoord(-1, -1), SectorCoord.FromLy(-0.0001, -64.0, 64.0));
            Assert.AreEqual(new SectorCoord(-2, 3), SectorCoord.FromLy(-64.0001, 192.0, 64.0));
            Assert.AreEqual(new SectorCoord(SectorCoord.MaxCoord, SectorCoord.MinCoord), SectorCoord.FromLy(1e30, -1e30, 64.0));
            var u = TestUniverses.Plain();
            Assert.AreEqual(new SectorCoord(-1, 0), u.SectorOf(-1.0, 1.0));
        }

        [Test]
        public void SystemKey_AuthoredFlagFollowsIndex()
        {
            Assert.IsFalse(new SystemKey(0, 0, 999).IsAuthored);
            Assert.IsTrue(new SystemKey(0, 0, 1000).IsAuthored);
            Assert.AreEqual(1000, SystemKey.AuthoredBase);
        }
    }
}
