using UnityEngine;

namespace Frontier
{
    public sealed class FrontierSoundscape : MonoBehaviour
    {
        SoundscapePlayer player;
        UIRoot ui;
        DefenseController defense;
        GameState state;
        bool suspended;

        public void Init(AudioService audio, UIRoot ui, DefenseController defense, GameState state)
        {
            this.ui = ui;
            this.defense = defense;
            this.state = state;
            var library = Resources.Load<SoundscapeLibrary>("Frontier/SoundscapeLibrary");
            if (library == null) throw new System.InvalidOperationException("Frontier soundscape library is missing.");
            player = new SoundscapePlayer(audio, library);
        }

        void Update()
        {
            if (player == null) return;
            var scene = ui.IsTravelling ? MusicScene.Travel : defense.Active ? MusicScene.Combat : MusicScene.Exploration;
            player.Tick(scene, state.planet.Weather, suspended, Time.unscaledDeltaTime);
        }

        void OnApplicationPause(bool value)
        {
            suspended = value;
            if (value) player?.Stop();
        }

        void OnDestroy() { player?.Stop(); }
    }
}
