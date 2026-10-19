using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier.Hyperspace.View
{
    public sealed class HyperspaceGame : ITravelMinigame, ITravelBackHandler
    {
        readonly GameState game;
        public HyperspaceSession Session { get; private set; }
        public HyperspaceGame(GameState game) { this.game = game; }
        public bool HandleBack() { return Session != null && Session.HandleBack(); }

        public void Run(VisualElement host, TravelRunRequest request, Action<TravelRunResult> done)
        {
            var go = new GameObject("Hyperspace Run");
            Session = go.AddComponent<HyperspaceSession>();
            Session.Begin(game, request.fullscreenHost ?? host, request, done);
        }
    }
}
