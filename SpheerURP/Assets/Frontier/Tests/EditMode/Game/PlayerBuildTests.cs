using System;
using System.IO;
using Frontier.EditorTools;
using NUnit.Framework;
using UnityEditor.Build;

namespace Frontier.Tests
{
    public sealed class PlayerBuildTests
    {
        [Test]
        public void AndroidBuildRequiresExplicitAbsoluteUnusedApkAndNeverOverwritesAFile()
        {
            Assert.Throws<BuildFailedException>(() => FrontierPlayerBuild.AndroidOutputPath(Array.Empty<string>()));
            Assert.Throws<BuildFailedException>(() => FrontierPlayerBuild.AndroidOutputPath(new[] { "-frontierBuildPath" }));
            Assert.Throws<BuildFailedException>(() => FrontierPlayerBuild.AndroidOutputPath(new[] { "-frontierBuildPath", "relative.apk" }));
            if (Path.DirectorySeparatorChar == '\\')
            {
                Assert.Throws<BuildFailedException>(() => FrontierPlayerBuild.AndroidOutputPath(new[] { "-frontierBuildPath", @"\relative.apk" }));
                Assert.Throws<BuildFailedException>(() => FrontierPlayerBuild.AndroidOutputPath(new[] { "-frontierBuildPath", @"C:relative.apk" }));
            }
            Assert.Throws<BuildFailedException>(() => FrontierPlayerBuild.AndroidOutputPath(new[] { "-frontierBuildPath", Path.Combine(Path.GetTempPath(), "not-an-apk.aab") }));
            string path = Path.Combine(Path.GetTempPath(), "frontier-output-" + Guid.NewGuid().ToString("N") + ".apk");
            Assert.AreEqual(Path.GetFullPath(path), FrontierPlayerBuild.AndroidOutputPath(new[] { "-frontierBuildPath", path }));
            try
            {
                File.WriteAllText(path, "existing artifact");
                Assert.Throws<BuildFailedException>(() => FrontierPlayerBuild.AndroidOutputPath(new[] { "-frontierBuildPath", path }));
                Assert.AreEqual("existing artifact", File.ReadAllText(path));
            }
            finally { File.Delete(path); }
        }
    }
}
