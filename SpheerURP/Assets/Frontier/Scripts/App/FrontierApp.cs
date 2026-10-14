using UnityEngine;

namespace Frontier
{
    /// <summary>
    /// The app lifecycle of a running session: ticks the game state every frame (debounced save, play time) and writes the save
    /// immediately when the app goes to the background, loses focus, quits or is torn down. Created by <see cref="FrontierBootstrap"/>.
    /// </summary>
    public sealed class FrontierApp : MonoBehaviour
    {
        GameState state;

        public void Init(GameState gs) { state = gs; }

        void Update() { if (state != null) state.Tick(); }

        void OnApplicationPause(bool paused) { if (paused && state != null) state.Flush(); }

        void OnApplicationFocus(bool focused) { if (!focused && state != null) state.Flush(); }

        void OnApplicationQuit() { if (state != null) state.Flush(); }

        void OnDestroy() { if (state != null) state.Flush(); }
    }
}
