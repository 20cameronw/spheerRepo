using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Frontier.Tests
{
    public class ShipRigTests
    {
        [UnityTest]
        public IEnumerator CameraOnlyRigCannotBeRenderedByOtherCameras()
        {
            var host = new GameObject("Camera-only ship test");
            var target = new RenderTexture(64, 64, 24);
            var shipTarget = new RenderTexture(64, 64, 24);
            var pixels = new Texture2D(64, 64, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                var rig = ShipRig.Create(host.transform, 31, cameraOnly: true);
                Assert.AreEqual(0, rig.GetComponentsInChildren<ParticleSystem>(true).Length,
                    "Offscreen rendering uses camera-scoped geometry, not globally visible particle renderers.");
                rig.SetFlight(1f, 1f, 0f);
                foreach (var renderer in rig.GetComponentsInChildren<MeshRenderer>(true))
                    Assert.IsFalse(renderer.enabled, "Offscreen ships must use camera-scoped draws only.");
                Assert.Throws<ArgumentNullException>(() => rig.DrawForCamera(null));
                var observer = new GameObject("Unrelated camera");
                observer.transform.SetParent(host.transform, false);
                observer.transform.position = Vector3.back * 4f;
                var camera = observer.AddComponent<Camera>();
                camera.enabled = false;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                target.Create();
                camera.targetTexture = target;
                var destination = Object.Instantiate(observer, host.transform);
                var shipCamera = destination.GetComponent<Camera>();
                shipTarget.Create();
                shipCamera.targetTexture = shipTarget;
                for (int frame = 0; frame < 3; frame++)
                {
                    rig.DrawForCamera(shipCamera);
                    RenderPipeline.SubmitRenderRequest(shipCamera, new RenderPipeline.StandardRequest { destination = shipTarget });
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    yield return null;
                }
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                pixels.Apply();
                foreach (var pixel in pixels.GetPixels32())
                    Assert.IsTrue(pixel.r < 5 && pixel.g < 5 && pixel.b < 5,
                        "Even a camera including layer 31 must not see the camera-only ship.");
                RenderTexture.active = shipTarget;
                pixels.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                pixels.Apply();
                int bright = 0;
                foreach (var pixel in pixels.GetPixels32())
                    if (pixel.g > 50) bright++;
                Assert.Greater(bright, 10, "Camera-scoped draws must remain visible to their intended camera.");
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(host);
                target.Release();
                shipTarget.Release();
                Object.Destroy(target);
                Object.Destroy(shipTarget);
                Object.Destroy(pixels);
            }
        }

        [UnityTest]
        public IEnumerator SharedRigRetractsGearDrivesEnginesAndOwnsItsResources()
        {
            var host = new GameObject("Ship rig test");
            var rig = ShipRig.Create(host.transform, 31);
            var mesh = rig.GetComponentInChildren<MeshFilter>().sharedMesh;
            var material = rig.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            var previousQuality = PresentationQuality.Preset;
            try
            {
                Assert.AreEqual(1f, rig.Gear);
                Assert.IsTrue(rig.transform.Find("Hull/LandingLights").gameObject.activeSelf);
                Assert.AreEqual(3, Array.FindAll(rig.GetComponentsInChildren<Transform>(true),
                    t => t.name.StartsWith("LandingLeg_", StringComparison.Ordinal)).Length);
                Assert.AreEqual(0, rig.GetComponentsInChildren<Collider>().Length);
                var exhaust = rig.GetComponentsInChildren<ParticleSystem>(true);
                Assert.AreEqual(2, exhaust.Length);
                foreach (var particles in exhaust)
                {
                    Assert.IsFalse(particles.isPlaying);
                    Assert.LessOrEqual(particles.main.maxParticles, 24);
                    Assert.IsTrue(particles.main.useUnscaledTime);
                    Assert.AreEqual(0, particles.emission.burstCount);
                }
                Assert.LessOrEqual(rig.TriangleCount, 100);
                Assert.AreEqual(8, rig.GetComponentsInChildren<MeshRenderer>(true).Length);
                rig.SetFlight(1f, 0f, 100f);
                Assert.AreEqual(0f, rig.Gear);
                Assert.AreEqual(1f, rig.Throttle);
                Assert.IsFalse(rig.transform.Find("Hull/LandingLights").gameObject.activeSelf);
                Assert.AreEqual(90f, rig.transform.Find("Hull/LandingLeg_0").localEulerAngles.x, 0.01f);
                Assert.AreEqual(35f, rig.transform.Find("Hull").localEulerAngles.z, 0.01f);
                Assert.IsTrue(rig.transform.Find("Hull/Engine_1").gameObject.activeSelf);
                foreach (var particles in exhaust)
                {
                    Assert.IsTrue(particles.isPlaying);
                    Assert.AreEqual(60f * PresentationQuality.Budget.ParticleMultiplier,
                        particles.emission.rateOverTime.constant, 0.01f);
                }
                PresentationQuality.Preset = QualityPreset.Low;
                foreach (var particles in exhaust)
                {
                    Assert.AreEqual(11, particles.main.maxParticles);
                    Assert.AreEqual(27f, particles.emission.rateOverTime.constant, 0.01f);
                }
                foreach (var renderer in rig.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Assert.AreEqual(31, renderer.gameObject.layer);
                    Assert.AreSame(material, renderer.sharedMaterial);
                }
                Assert.Throws<ArgumentOutOfRangeException>(() => rig.SetFlight(float.NaN, 0f, 0f));
                rig.SetFlight(-1f, 2f, 0f);
                Assert.AreEqual(0f, rig.Throttle);
                Assert.AreEqual(1f, rig.Gear);
                Assert.IsFalse(rig.transform.Find("Hull/Engine_1").gameObject.activeSelf);
                foreach (var particles in exhaust) Assert.IsFalse(particles.isPlaying);
            }
            finally
            {
                PresentationQuality.Preset = previousQuality;
                Object.Destroy(host);
            }
            yield return null;
            yield return null;
            Assert.IsTrue(mesh == null);
            Assert.IsTrue(material == null);
        }
    }
}
