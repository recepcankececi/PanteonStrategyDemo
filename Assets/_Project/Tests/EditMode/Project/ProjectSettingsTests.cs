using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode.Project
{
    /// <summary>Guards project-level settings the case requires (T2 resizable window, single gameplay scene).</summary>
    public class ProjectSettingsTests
    {
        private const string MainScenePath = "Assets/_Project/Scenes/Main.unity";

        // 1600x900 leaves room for the title bar and taskbar on a 1080p desktop.
        private const int DefaultWindowWidth = 1600;
        private const int DefaultWindowHeight = 900;

        private const string Renderer2DPath = "Assets/_Project/Settings/Renderer2D.asset";

        // Buildings and soldiers share "Objects"; Ground draws below and FX above them.
        // Default stays first because Unity cannot remove it.
        private static readonly string[] ExpectedSortingLayers = { "Default", "Ground", "Objects", "FX" };

        [Test]
        public void BuildSettings_EnabledScenes_AreOnlyMainScene()
        {
            string[] enabled = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();

            Assert.That(enabled, Is.EqualTo(new[] { MainScenePath }));
        }

        [Test]
        public void PlayerSettings_Window_IsWindowedAndResizable()
        {
            Assert.That(PlayerSettings.fullScreenMode, Is.EqualTo(FullScreenMode.Windowed));
            Assert.That(PlayerSettings.resizableWindow, Is.True);
        }

        [Test]
        public void PlayerSettings_DefaultWindowSize_IsUsedInsteadOfNativeResolution()
        {
            // With native resolution on, Unity ignores the default size and opens a desktop-sized window.
            Assert.That(PlayerSettings.defaultIsNativeResolution, Is.False);
            Assert.That(PlayerSettings.defaultScreenWidth, Is.EqualTo(DefaultWindowWidth));
            Assert.That(PlayerSettings.defaultScreenHeight, Is.EqualTo(DefaultWindowHeight));
        }

        [Test]
        public void SortingLayers_Order_MatchesRenderingAdr()
        {
            string[] layers = SortingLayer.layers.Select(layer => layer.name).ToArray();

            Assert.That(layers, Is.EqualTo(ExpectedSortingLayers));
        }

        [Test]
        public void Renderer2D_UsesDefaultSorting()
        {
            // No Y-sorting: the art is top-down, so nothing is "behind" anything.
            // Layers and orders decide the draw order; only soldiers walking through each other overlap, briefly.
            var renderer = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(Renderer2DPath));

            Assert.That(renderer.FindProperty("m_TransparencySortMode").intValue, Is.EqualTo((int)TransparencySortMode.Default));
        }
    }
}
