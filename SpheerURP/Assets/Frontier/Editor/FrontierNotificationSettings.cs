using System;
using System.IO;
using Unity.Notifications;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Build.Player;
using UnityEngine;

namespace Frontier.EditorTools
{
    public sealed class FrontierNotificationSettings : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }

        [MenuItem("Frontier/Notifications/Configure Safe Defaults")]
        public static void Configure()
        {
            NotificationSettings.iOSSettings.RequestAuthorizationOnAppLaunch = false;
            NotificationSettings.iOSSettings.NotificationRequestAuthorizationForRemoteNotificationsOnAppLaunch = false;
            NotificationSettings.iOSSettings.AddRemoteNotificationCapability = false;
            NotificationSettings.AndroidSettings.ExactSchedulingOption = 0;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (NotificationSettings.iOSSettings.RequestAuthorizationOnAppLaunch
                || NotificationSettings.iOSSettings.NotificationRequestAuthorizationForRemoteNotificationsOnAppLaunch
                || NotificationSettings.iOSSettings.AddRemoteNotificationCapability
                || NotificationSettings.AndroidSettings.ExactSchedulingOption != 0)
                throw new BuildFailedException("Frontier notifications must not request launch permission, enable push or use exact alarms. Run Frontier/Notifications/Configure Safe Defaults.");
        }

        [MenuItem("Frontier/Notifications/Validate Android Scripts")]
        public static void ValidateAndroidScripts()
        {
            string output = Path.Combine(Path.GetTempPath(), "FrontierNotificationAndroidScripts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output);
            try
            {
                var compiled = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings
                {
                    target = BuildTarget.Android,
                    group = BuildTargetGroup.Android,
                    options = ScriptCompilationOptions.None
                }, output);
                if (compiled.assemblies == null || compiled.assemblies.Count == 0)
                    throw new InvalidOperationException("Android player script compilation returned no assemblies.");
                Debug.Log("Frontier: Android player scripts compiled, including native notification permission and scheduling APIs.");
            }
            finally { Directory.Delete(output, true); }
        }
    }
}
