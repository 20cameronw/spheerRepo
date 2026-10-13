#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Frontier.EditorTools
{
    /// <summary>
    /// Boots the Frontier scene in play mode, lets <see cref="HarnessRunner"/> capture every screen on virtual devices,
    /// then returns to edit mode (and exits when running in batch mode).
    /// Batch: Unity -batchmode -projectPath SpheerURP -executeMethod Frontier.EditorTools.ScreenshotHarness.Run -frontierShotDir out
    /// Do not pass -nographics: rendering needs a GPU.
    /// </summary>
    [InitializeOnLoad]
    public static class ScreenshotHarness
    {
        const string ActiveKey = "Frontier.Harness.Active";
        const string ScenePath = "Assets/Frontier/Scenes/Frontier.unity";

        static ScreenshotHarness() { EditorApplication.playModeStateChanged += OnPlayMode; }

        public static void Run()
        {
            string dir = ReadArg(HarnessRunner.DirArg);
            if (string.IsNullOrEmpty(dir))
            {
                Debug.LogError("Frontier harness: pass " + HarnessRunner.DirArg + " <output dir>");
                EditorApplication.Exit(2);
                return;
            }
            Start(Path.GetFullPath(dir));
        }

        [MenuItem("Frontier/Capture Screens")]
        public static void CaptureFromMenu()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            Start(Path.Combine(project, "Screens", DateTime.Now.ToString("yyyyMMdd-HHmmss")));
        }

        static void Start(string dir)
        {
            Directory.CreateDirectory(dir);
            string done = Path.Combine(dir, "done.txt");
            if (File.Exists(done)) File.Delete(done);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(HarnessRunner.SessionDirKey, dir);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(ActiveKey, false)) return;
            SessionState.SetBool(ActiveKey, false);
            string dir = SessionState.GetString(HarnessRunner.SessionDirKey, "");
            SessionState.EraseString(HarnessRunner.SessionDirKey);
            bool ok = !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "done.txt"));
            Debug.Log("Frontier harness finished: " + (ok ? "ok" : "no done.txt") + " -> " + dir);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        static string ReadArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
#endif
