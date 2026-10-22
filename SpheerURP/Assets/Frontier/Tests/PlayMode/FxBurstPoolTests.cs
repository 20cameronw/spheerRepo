using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class FxBurstPoolTests
    {
        [Test]
        public void AuthoredTemplatesContainSharedMaterialsAndNoPickingColliders()
        {
            foreach (string name in new[] { "Burst", "FxBolt", "FxRipple", "FxGlow" })
            {
                var prefab = FxTemplates.Load<GameObject>(name);
                Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>(true), name);
                var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                Assert.IsNotEmpty(renderers, name);
                foreach (var renderer in renderers) Assert.IsNotNull(renderer.sharedMaterial, name);
            }
            var burst = FxTemplates.Load<GameObject>("Burst").GetComponent<ParticleSystem>();
            Assert.IsFalse(burst.main.playOnAwake);
            Assert.IsFalse(burst.main.loop);
            Assert.IsFalse(burst.emission.enabled);
        }

        [UnityTest]
        public IEnumerator GlowSaturationDoesNotStealActiveInstancesAndOverflowIsDestroyed()
        {
            var owner = new GameObject("Saturated glow owner");
            var glows = new GameObject[129];
            try
            {
                for (int i = 0; i < glows.Length; i++)
                    glows[i] = Fx.GlowQuad(owner.transform, Color.cyan, 1f);
                Assert.AreEqual(128, FxTransientPool.GlowQuadPoolCount);
                for (int i = 0; i < glows.Length; i++)
                    for (int j = 0; j < i; j++) Assert.AreNotSame(glows[i], glows[j]);
                owner.SetActive(false);
                yield return null;
                foreach (var glow in glows) Assert.IsFalse(glow.GetComponent<MeshRenderer>().enabled);
                owner.SetActive(true);
                yield return null;
                foreach (var glow in glows) Assert.IsTrue(glow.GetComponent<MeshRenderer>().enabled);
                Object.Destroy(owner);
                yield return null;
                yield return null;
                yield return null;
                Assert.IsTrue(glows[128] == null, "overflow must be destroyed rather than retained");
                for (int i = 0; i < 128; i++) Assert.IsFalse(glows[i].activeSelf);
            }
            finally { if (owner != null) Object.Destroy(owner); }
        }

        [UnityTest]
        public IEnumerator ReusesParticleSystemAfterBurstCompletes()
        {
            Fx.Burst(Vector3.zero, Color.cyan, 4, 1f, 0.1f);
            int warmedCount = FxBurstPool.PoolCount;
            Assert.Greater(warmedCount, 0);

            yield return new WaitForSeconds(1.2f);

            Fx.Burst(Vector3.one, Color.magenta, 4, 1f, 0.1f);
            Assert.AreEqual(warmedCount, FxBurstPool.PoolCount);
        }

        [UnityTest]
        public IEnumerator ReusesBoltAndRippleObjectsAfterTheirAnimationsComplete()
        {
            Fx.Bolt(Vector3.zero, Vector3.right, Color.cyan, life: 0.05f);
            Fx.Ripple(Vector3.zero, Color.magenta, life: 0.05f);
            int boltCount = FxTransientPool.BoltPoolCount;
            int rippleCount = FxTransientPool.RipplePoolCount;
            Assert.That(boltCount, Is.InRange(1, 24));
            Assert.That(rippleCount, Is.InRange(1, 24));

            yield return new WaitForSeconds(0.15f);

            Fx.Bolt(Vector3.one, Vector3.up, Color.green, life: 0.05f);
            Fx.Ripple(Vector3.one, Color.yellow, life: 0.05f);
            Assert.AreEqual(boltCount, FxTransientPool.BoltPoolCount);
            Assert.AreEqual(rippleCount, FxTransientPool.RipplePoolCount);
        }

        [UnityTest]
        public IEnumerator GlowQuadReturnsToTheBoundedPoolWhenItsOwnerIsDestroyed()
        {
            var owner = new GameObject("Glow owner");
            owner.transform.position = new Vector3(3f, 2f, 1f);
            var glow = Fx.GlowQuad(owner.transform, Color.cyan, 1f, Vector3.up);
            int count = FxTransientPool.GlowQuadPoolCount;
            Assert.Greater(count, 0);
            Assert.LessOrEqual(count, 128);
            Assert.AreEqual(owner.transform.TransformPoint(Vector3.up), glow.transform.position);

            Object.Destroy(owner);
            yield return null;
            yield return null;
            Assert.IsFalse(glow.activeSelf, "a glow is reclaimed after its owner is destroyed");

            var nextOwner = new GameObject("Next glow owner");
            var reused = Fx.GlowQuad(nextOwner.transform, Color.magenta, 2f);
            Assert.AreSame(glow, reused);
            Assert.AreEqual(count, FxTransientPool.GlowQuadPoolCount);
            Assert.AreEqual(nextOwner.transform.position, reused.transform.position);

            Object.Destroy(nextOwner);
            yield return null;
            yield return null;
        }

        [Test]
        public void GlowQuadsShareMaterialWithoutSharingPerInstanceTint()
        {
            var first = Fx.GlowQuad(null, Color.cyan, 1f);
            var second = Fx.GlowQuad(null, Color.magenta, 2f);
            try
            {
                var firstRenderer = first.GetComponent<MeshRenderer>();
                var secondRenderer = second.GetComponent<MeshRenderer>();
                Assert.AreSame(firstRenderer.sharedMaterial, secondRenderer.sharedMaterial);
                var firstProperties = new MaterialPropertyBlock();
                var secondProperties = new MaterialPropertyBlock();
                firstRenderer.GetPropertyBlock(firstProperties);
                secondRenderer.GetPropertyBlock(secondProperties);
                Assert.AreEqual(Color.cyan, firstProperties.GetColor("_Color"));
                Assert.AreEqual(Color.magenta, secondProperties.GetColor("_Color"));
            }
            finally
            {
                Object.Destroy(first);
                Object.Destroy(second);
            }
        }
    }
}
