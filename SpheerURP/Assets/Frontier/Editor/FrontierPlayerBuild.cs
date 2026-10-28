using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Frontier.EditorTools
{
    public static class FrontierPlayerBuild
    {
        public static string AndroidOutputPath(string[] arguments)
        {
            const string flag = "-frontierBuildPath";
            int index = Array.IndexOf(arguments, flag);
            if (index < 0 || index + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index + 1]))
                throw new BuildFailedException("Pass -frontierBuildPath with an absolute, unused .apk output path.");
            string path = arguments[index + 1];
            if (!Path.IsPathFullyQualified(path) || !string.Equals(Path.GetExtension(path), ".apk", StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("Android development output must be an absolute .apk path.");
            path = Path.GetFullPath(path);
            if (File.Exists(path) || Directory.Exists(path))
                throw new BuildFailedException("Build output already exists. Choose a new output path: " + path);
            return path;
        }

        public static void AndroidDevelopment()
        {
            string output = AndroidOutputPath(Environment.GetCommandLineArgs());
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Install Android Build Support, SDK/NDK and OpenJDK for this Unity version.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Start Unity with -buildTarget Android for this build entry point.");
            if (PlayerSettings.Android.useCustomKeystore)
                throw new BuildFailedException("This development-only entry point requires debug signing. Use a separate reviewed release pipeline for custom signing.");

            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length != 1 || scenes[0] != "Assets/Frontier/Scenes/Frontier.unity")
                throw new BuildFailedException("The Frontier device build must contain only the enabled Frontier scene.");
            var cues = Resources.Load<AudioCueLibrary>("Frontier/AudioCueLibrary");
            var soundscape = Resources.Load<SoundscapeLibrary>("Frontier/SoundscapeLibrary");
            if (cues == null || soundscape == null)
                throw new BuildFailedException("Frontier authored audio libraries are missing.");
            cues.Validate();
            soundscape.Validate();
            FrontierLocalization.ValidateEnglishCatalog();
            new FrontierNotificationSettings().OnPreprocessBuild(null);

            bool appBundle = EditorUserBuildSettings.buildAppBundle;
            bool export = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            try
            {
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = BuildTarget.Android,
                    options = BuildOptions.Development | BuildOptions.CompressWithLz4
                });
                if (report == null || report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Frontier Android development build failed; inspect the Unity build log.");
                if (!File.Exists(output) || new FileInfo(output).Length == 0)
                    throw new BuildFailedException("Unity reported success but no nonempty APK was produced.");
                Debug.Log("Frontier: Android development APK created at " + output +
                    " (" + new FileInfo(output).Length + " APK bytes; " + report.summary.totalSize +
                    " aggregate build bytes). Debug-signed device-test artifact, not a store release.");
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = appBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = export;
            }
        }
    }
}
