using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class AudioAssetFlowTests
    {
        [UnityTest]
        public IEnumerator ClosingHyperspaceAudioCannotDestroySharedCombatClips()
        {
            var library = Resources.Load<AudioCueLibrary>("Frontier/AudioCueLibrary");
            var zap = library.Get(AudioCue.Zap).clip;
            var boom = library.Get(AudioCue.Boom).clip;
            var impact = library.Get(AudioCue.Impact).clip;
            var warp = library.Get(AudioCue.Warp).clip;
            var bad = library.Get(AudioCue.Bad).clip;
            var good = library.Get(AudioCue.Good).clip;
            var engine = Resources.Load<SoundscapeLibrary>("Frontier/SoundscapeLibrary").engine;
            int clipsBefore = Resources.FindObjectsOfTypeAll<AudioClip>().Length;
            var audio = new Hyperspace.View.HyperspaceAudio();
            Assert.AreEqual(clipsBefore, Resources.FindObjectsOfTypeAll<AudioClip>().Length,
                "Starting a hyperspace run should reuse preloaded audio cues instead of creating clips.");
            audio.Dispose();
            yield return null;
            Assert.IsNotNull(zap);
            Assert.IsNotNull(boom);
            Assert.IsNotNull(impact);
            Assert.IsNotNull(warp);
            Assert.IsNotNull(bad);
            Assert.IsNotNull(good);
            Assert.IsNotNull(engine);
            Assert.DoesNotThrow(library.Validate);
            audio = new Hyperspace.View.HyperspaceAudio();
            audio.Dispose();
            yield return null;
            Assert.IsNotNull(library.Get(AudioCue.Zap).clip);
            Assert.IsNotNull(library.Get(AudioCue.Boom).clip);
            Assert.IsNotNull(engine);
        }
    }
}
