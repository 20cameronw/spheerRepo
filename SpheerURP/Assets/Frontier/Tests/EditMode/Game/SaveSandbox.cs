using System;
using System.IO;
using UnityEngine;

namespace Frontier.Tests
{
    /// <summary>
    /// Points the game's save file at a throw-away folder for the lifetime of a test and restores the static state afterwards, so
    /// tests never read or overwrite the developer's real save in Application.persistentDataPath.
    /// </summary>
    public sealed class SaveSandbox : IDisposable
    {
        readonly string dir;
        readonly string previousOverride;
        readonly GameState previousState;

        public SaveSandbox()
        {
            previousOverride = GameState.SaveDirectoryOverride;
            previousState = GameState.I;
            dir = Path.Combine(Application.temporaryCachePath, "frontier-tests-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            System.IO.Directory.CreateDirectory(dir);
            GameState.SaveDirectoryOverride = dir;
        }

        public string Directory { get { return dir; } }

        public string SaveFile { get { return Path.Combine(dir, "frontier_save.json"); } }

        public void Dispose()
        {
            GameState.SaveDirectoryOverride = previousOverride;
            GameState.I = previousState;
            try { System.IO.Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
