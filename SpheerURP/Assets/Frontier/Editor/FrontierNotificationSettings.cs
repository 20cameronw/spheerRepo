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
            ValidateScripts(BuildTarget.Android, BuildTargetGroup.Android);
        }

        [MenuItem("Frontier/Notifications/Validate iOS Scripts")]
        public static void ValidateIosScripts()
        {
            ValidateScripts(BuildTarget.iOS, BuildTargetGroup.iOS);
        }

        static void ValidateScripts(BuildTarget target, BuildTargetGroup group)
        {
            string output = Path.Combine(Path.GetTempPath(), "FrontierNotification" + target + "Scripts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output);
            try
            {
                var compiled = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings
                {
                    target = target,
                    group = group,
                    options = ScriptCompilationOptions.None
                }, output);
                if (compiled.assemblies == null || compiled.assemblies.Count == 0)
                    throw new InvalidOperationException(target + " player script compilation returned no assemblies.");
                Debug.Log("Frontier: " + target + " player scripts compiled, including native notification permission and scheduling APIs.");
            }
            finally { Directory.Delete(output, true); }
        }
    }
}
