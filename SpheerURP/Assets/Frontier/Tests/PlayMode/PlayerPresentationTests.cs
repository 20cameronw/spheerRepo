using System.Collections;
using System.Linq;
using Frontier.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class PlayerPresentationTests
    {
        [Test]
        public void PooledWeaponsKeepLightningBeamsAndBulletsDistinct()
        {
            var from = new Vector3(100, 200, 300);
            var to = from + Vector3.right;
            for (int i = 0; i < 30; i++)
            {
                Fx.Bullet(from, to, Color.yellow);
                var line = Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None)
                    .Single(item => item.gameObject.activeInHierarchy && item.GetPosition(0) == from);
                Assert.AreEqual(2, line.positionCount);
                Assert.IsTrue(line.GetComponent<Fx.TracerShot>().enabled);
                Assert.IsTrue(line.GetComponent<FadeOut>() == null || !line.GetComponent<FadeOut>().enabled);
                FxTransientPool.Release(line.gameObject);

                Fx.Bolt(from, to, Color.cyan);
                line = Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None)
                    .Single(item => item.gameObject.activeInHierarchy && item.GetPosition(0) == from);
                Assert.AreEqual(5, line.positionCount);
                Assert.IsTrue(line.GetComponent<Fx.TracerShot>() == null || !line.GetComponent<Fx.TracerShot>().enabled);
                Assert.IsTrue(line.GetComponent<FadeOut>().enabled);
                FxTransientPool.Release(line.gameObject);

                Fx.Beam(from, to, Color.red);
                line = Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None)
                    .Single(item => item.gameObject.activeInHierarchy && item.GetPosition(0) == from);
                Assert.AreEqual(2, line.positionCount);
                Assert.AreEqual(to, line.GetPosition(1));
                FxTransientPool.Release(line.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator DevelopmentRewardPreviewWaitsAndRejectsUnknownPlacement()
        {
            var provider = new DevelopmentAdsPreview();
            Assert.IsFalse(provider.ShowRewardedAsync("missing").Result.succeeded);
            var result = provider.ShowRewardedAsync("double-yield");
            yield return new WaitForSecondsRealtime(1);
            Assert.IsFalse(result.IsCompleted, "A preview must not immediately pretend the ad finished.");
            float deadline = Time.realtimeSinceStartup + 8;
            while (!result.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(result.IsCompleted);
            Assert.IsTrue(result.Result.succeeded);
            Assert.IsTrue(result.Result.value.rewardEarned);
        }
    }
}
