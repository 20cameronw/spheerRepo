#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Frontier.EditorTools
{
    /// <summary>
    /// Applies the mobile baseline: portrait only, IL2CPP + ARM64 on Android, managed stripping. Idempotent, so it can be
    /// re-run after a settings reset. Product name and bundle ids are intentionally left alone until the final title is chosen.
    /// </summary>
    public static class ProjectConfigurator
    {
        [MenuItem("Frontier/Apply Mobile Project Settings")]
        public static void Apply()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            var android = NamedBuildTarget.Android;
            var ios = NamedBuildTarget.iOS;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(ios, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Medium);
            PlayerSettings.SetManagedStrippingLevel(ios, ManagedStrippingLevel.Medium);

            AssetDatabase.SaveAssets();
            Debug.Log("Frontier: mobile project settings applied (portrait, IL2CPP, ARM64, medium stripping).");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
#endif
