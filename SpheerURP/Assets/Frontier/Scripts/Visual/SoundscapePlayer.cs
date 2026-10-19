using UnityEngine;

namespace Frontier
{
    public sealed class SoundscapePlayer
    {
        readonly AudioService service;
        readonly SoundscapeLibrary library;
        AudioClip playing, requested, ambience;
        float level;
        bool previouslyTravel;

        public SoundscapePlayer(AudioService service, SoundscapeLibrary library)
        {
            this.service = service != null ? service : throw new System.ArgumentNullException(nameof(service));
            this.library = library != null ? library : throw new System.ArgumentNullException(nameof(library));
            library.Validate();
        }

        public void Tick(MusicScene scene, WeatherKind weather, bool suspended, float deltaSeconds)
        {
            if (service == null) return;
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0)
                throw new System.ArgumentOutOfRangeException(nameof(deltaSeconds));
            requested = suspended ? null : library.Music(scene);
            if (playing == null && requested != null) playing = requested;
            float target = playing == requested ? library.musicVolume : 0;
            level = Mathf.MoveTowards(level, target,
                deltaSeconds * Mathf.Max(0.001f, library.musicVolume) / library.transitionSeconds);
            if (level == 0 && playing != requested) playing = requested;
            service.SetMusic(playing, level);

            bool travel = scene == MusicScene.Travel;
            // Hyperspace owns the shared SFX bed while travelling.
            if (travel)
            {
                if (!previouslyTravel) service.ClearBedIfOwnedBy(ambience);
                ambience = null;
            }
            else
            {
                ambience = suspended ? null : library.Ambience(weather);
                service.SetBed(ambience, library.ambienceVolume * (scene == MusicScene.Combat ? 0.5f : 1f));
            }
            previouslyTravel = travel;
        }

        public void Stop()
        {
            if (service == null) return;
            service.SetMusic(null, 0);
            if (!previouslyTravel) service.SetBed(null, 0);
            playing = null;
            level = 0;
        }
    }
}
