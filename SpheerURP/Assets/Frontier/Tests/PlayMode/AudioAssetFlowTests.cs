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
            var engine = Resources.Load<SoundscapeLibrary>("Frontier/SoundscapeLibrary").engine;
            var audio = new Hyperspace.View.HyperspaceAudio();
            audio.Dispose();
            yield return null;
            Assert.IsNotNull(zap);
            Assert.IsNotNull(boom);
            Assert.IsNotNull(impact);
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
