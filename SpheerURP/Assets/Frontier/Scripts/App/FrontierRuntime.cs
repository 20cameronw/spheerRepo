using UnityEngine;
using UnityEngine.UIElements;
using Frontier.Services;

namespace Frontier
{
    /// <summary>
    /// The live objects that make up a running Frontier session. Published by <see cref="FrontierBootstrap"/> so tools
    /// (screenshot harness, debug panels, play-mode tests) can reach them without scene searches.
    /// </summary>
    public sealed class FrontierRuntime
    {
        public static FrontierRuntime Current { get; private set; }

        public GameDatabase Db;
        public GameState State;
        public Camera Camera;
        public SpaceBackdrop Backdrop;
        public PlanetView Planet;
        public DefenseController Defense;
        public WorldSurface Surface;
        public UIRoot Ui;
        public UIDocument Document;
        public FrontierServices Services;

        public static void Publish(FrontierRuntime runtime) { Current = runtime; }
        public static void Clear() { Current = null; }
    }
}
