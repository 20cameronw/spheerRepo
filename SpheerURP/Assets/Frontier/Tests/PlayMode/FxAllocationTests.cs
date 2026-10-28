using System.Collections.Generic;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class FxAllocationTests
    {
        [Test]
        public void WarmedPoolAcquisitionAndGlowCallsDoNotAllocateManagedMemory()
        {
            var owner = new GameObject("FX allocation owner");
            GameObject bolt = null, ripple = null, glow = null;
            try
            {
                bolt = FxTransientPool.AcquireBolt();
                ripple = FxTransientPool.AcquireRipple();
                glow = Fx.GlowQuad(owner.transform, Color.cyan, 1f);
                for (int i = 0; i < 32; i++) Exercise(owner.transform, ref bolt, ref ripple, ref glow);

                var options = ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread;
                using (var probe = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 32, options))
                {
                    var knownAllocation = new byte[4096];
                    System.GC.KeepAlive(knownAllocation);
                    probe.Stop();
                    if (!probe.Valid || probe.Count == 0)
                        Assert.Ignore("Unity GC.Alloc recording is unavailable on this runtime");
                    var probeSamples = new List<ProfilerRecorderSample>();
                    probe.CopyTo(probeSamples);
                    long probeBytes = 0;
                    foreach (var sample in probeSamples) probeBytes += sample.Value;
                    if (probeBytes < knownAllocation.Length)
                        Assert.Ignore("Unity GC.Alloc recording did not detect a known allocation");
                }

                using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 1024, options))
                {
                    for (int i = 0; i < 128; i++) Exercise(owner.transform, ref bolt, ref ripple, ref glow);
                    recorder.Stop();
                    var samples = new List<ProfilerRecorderSample>();
                    recorder.CopyTo(samples);
                    long bytes = 0;
                    foreach (var sample in samples) bytes += sample.Value;
                    Assert.AreEqual(0, bytes, "warmed pooled FX calls allocated managed memory");
                }
            }
            finally
            {
                FxTransientPool.Release(bolt);
                FxTransientPool.Release(ripple);
                FxTransientPool.ReleaseGlowQuad(glow, true);
                Object.Destroy(owner);
            }
        }

        static void Exercise(Transform owner, ref GameObject bolt, ref GameObject ripple, ref GameObject glow)
        {
            FxTransientPool.Release(bolt);
            FxTransientPool.Release(ripple);
            FxTransientPool.ReleaseGlowQuad(glow, true);
            bolt = FxTransientPool.AcquireBolt();
            ripple = FxTransientPool.AcquireRipple();
            glow = Fx.GlowQuad(owner, Color.cyan, 1f);
        }
    }
}
