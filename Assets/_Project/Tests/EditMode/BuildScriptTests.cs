using System;
using NUnit.Framework;
using StrategyDemo.EditorTools;
using UnityEditor;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Covers the pure option-building half of <see cref="BuildScript"/>; the build itself is checked by running the player (P2).</summary>
    public class BuildScriptTests
    {
        private const string ProductName = "StrategyDemo";
        private const string MainScene = "Assets/_Project/Scenes/Main.unity";
        private const string DisabledScene = "Assets/_Project/Scenes/Disabled.unity";

        [Test]
        public void CreateWindowsOptions_MixedScenes_IncludesOnlyEnabledScenes()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(MainScene, true),
                new EditorBuildSettingsScene(DisabledScene, false)
            };

            BuildPlayerOptions options = BuildScript.CreateWindowsOptions(scenes, ProductName);

            Assert.That(options.scenes, Is.EqualTo(new[] { MainScene }));
        }

        [Test]
        public void CreateWindowsOptions_ValidInput_TargetsWindows64()
        {
            BuildPlayerOptions options = BuildScript.CreateWindowsOptions(OneEnabledScene(), ProductName);

            Assert.That(options.target, Is.EqualTo(BuildTarget.StandaloneWindows64));
            Assert.That(options.targetGroup, Is.EqualTo(BuildTargetGroup.Standalone));
        }

        [Test]
        public void CreateWindowsOptions_ValidInput_WritesProductExeIntoOutputDirectory()
        {
            BuildPlayerOptions options = BuildScript.CreateWindowsOptions(OneEnabledScene(), ProductName);

            Assert.That(options.locationPathName, Is.EqualTo($"{BuildScript.OutputDirectory}/{ProductName}.exe"));
        }

        [Test]
        public void CreateWindowsOptions_NoEnabledScene_Throws()
        {
            var scenes = new[] { new EditorBuildSettingsScene(DisabledScene, false) };

            Assert.Throws<InvalidOperationException>(() => BuildScript.CreateWindowsOptions(scenes, ProductName));
        }

        [Test]
        public void CreateWindowsOptions_EmptySceneList_Throws()
        {
            Assert.Throws<InvalidOperationException>(
                () => BuildScript.CreateWindowsOptions(Array.Empty<EditorBuildSettingsScene>(), ProductName));
        }

        [Test]
        public void CreateWindowsOptions_NullSceneList_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => BuildScript.CreateWindowsOptions(null, ProductName));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        [TestCase("Strategy:Demo")]
        [TestCase("Strategy/Demo")]
        public void CreateWindowsOptions_InvalidProductName_Throws(string productName)
        {
            Assert.Throws<ArgumentException>(() => BuildScript.CreateWindowsOptions(OneEnabledScene(), productName));
        }

        private static EditorBuildSettingsScene[] OneEnabledScene()
        {
            return new[] { new EditorBuildSettingsScene(MainScene, true) };
        }
    }
}
