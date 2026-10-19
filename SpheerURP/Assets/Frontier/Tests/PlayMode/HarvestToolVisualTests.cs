using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class HarvestToolVisualTests
    {
        [UnityTest]
        public IEnumerator ExpandedRigsPreserveMountsAndShowEveryAuthoredToolTier()
        {
            var stage = MinigameStage.Create(Color.black);
            try
            {
                foreach (var kind in new[] { MinigameKind.Siphon, MinigameKind.Excavate, MinigameKind.Salvage })
                    for (int tier = 0; tier <= 6; tier++)
                    {
                        Vector3 mount = new Vector3(2f, 1f, -0.5f);
                        var rig = HarvestToolVisual.Build(stage, kind, tier, Color.cyan, mount);
                        Assert.AreEqual(stage.root, rig.parent);
                        Assert.AreEqual(mount, rig.localPosition);
                        Assert.AreEqual(Quaternion.identity, rig.localRotation);
                        int hardware = 0;
                        foreach (Transform child in rig)
                            if (child.name.StartsWith("TierHardware", System.StringComparison.Ordinal)) hardware++;
                        Assert.AreEqual(tier, hardware, kind + " tier " + tier);
                        Assert.Greater(rig.GetComponentsInChildren<Renderer>().Length, 0);
                        rig.gameObject.SetActive(false);
                    }
                yield return null;
                Assert.IsEmpty(stage.GetComponentsInChildren<Collider>(true), "tools must not intercept stage input");
            }
            finally { Object.Destroy(stage.gameObject); }
            yield return null;
        }

        [Test]
        public void SharedPalettePreservesStarterAndPoweredToolIdentity()
        {
            HarvestToolVisual.Palette(0, Color.cyan, out var starter, out var starterRim);
            HarvestToolVisual.Palette(6, Color.cyan, out var powered, out var poweredRim);
            Assert.Greater(starter.r, starter.b);
            Assert.Greater(powered.b, powered.r);
            Assert.Greater(poweredRim.maxColorComponent, starterRim.maxColorComponent);
        }
    }
}
