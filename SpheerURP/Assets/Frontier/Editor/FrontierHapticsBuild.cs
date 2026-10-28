using System;
using System.IO;
using System.Xml;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif
#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
#endif

namespace Frontier.EditorTools
{
    public static class FrontierHapticsBuild
    {
        public static void EnsureVibrationPermission(string manifestPath)
        {
            if (string.IsNullOrEmpty(manifestPath)) throw new ArgumentException("A manifest path is required.", nameof(manifestPath));
            var manifest = new XmlDocument();
            manifest.Load(manifestPath);
            var root = manifest.DocumentElement;
            if (root == null || root.Name != "manifest")
                throw new InvalidDataException("Expected an Android manifest document.");
            const string android = "http://schemas.android.com/apk/res/android";
            foreach (XmlNode node in root.ChildNodes)
                if (node is XmlElement element && element.Name == "uses-permission" &&
                    element.GetAttribute("name", android) == "android.permission.VIBRATE") return;
            var permission = manifest.CreateElement("uses-permission");
            var name = manifest.CreateAttribute("android", "name", android);
            name.Value = "android.permission.VIBRATE";
            permission.Attributes.Append(name);
            root.AppendChild(permission);
            manifest.Save(manifestPath);
        }

#if UNITY_IOS
        [PostProcessBuild(20)]
        public static void LinkAppleHaptics(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS) return;
            string path = PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(path);
            string frameworkTarget = project.GetUnityFrameworkTargetGuid();
            project.AddFrameworkToProject(frameworkTarget, "CoreHaptics.framework", false);
            project.AddFrameworkToProject(frameworkTarget, "UIKit.framework", false);
            project.WriteToFile(path);
        }
#endif
    }

#if UNITY_ANDROID
    public sealed class FrontierAndroidHapticsBuild : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder { get { return 20; } }
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            FrontierHapticsBuild.EnsureVibrationPermission(Path.Combine(path, "src", "main", "AndroidManifest.xml"));
        }
    }
#endif
}
