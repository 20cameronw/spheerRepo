#if UNITY_EDITOR
using System;
using System.IO;

namespace Frontier.EditorTools
{
    /// <summary>Shared helpers for the editor check scripts.</summary>
    internal static class CheckSupport
    {
        /// <summary>
        /// Points game saves at a throw-away folder. Without this, any check that triggers GameState.Notify() would
        /// overwrite the developer's real save in Application.persistentDataPath.
        /// </summary>
        public static void IsolateSaves()
        {
            string dir = Path.Combine(Path.GetTempPath(), "frontier-checks-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(dir);
            GameState.SaveDirectoryOverride = dir;
        }
    }
}
#endif
