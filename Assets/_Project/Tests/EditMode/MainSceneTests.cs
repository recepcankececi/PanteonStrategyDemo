using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Guards the Main scene structure reviewers read (T4) and the UI layout and scaling (T2, I4).</summary>
    public class MainSceneTests
    {
        private const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string SystemsGroup = "--- Systems ---";
        private const string BoardGroup = "--- Board ---";
        private const string UiGroup = "--- UI ---";
        private const string CameraGroup = "--- Camera ---";
        private const string ProductionMenuPanel = "ProductionMenuPanel";
        private const string GameBoardArea = "GameBoardArea";
        private const string InformationPanel = "InformationPanel";

        // ADR-001 Karar 7: one Canvas scaled from a 1920x1080 reference, width and height weighted equally.
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        private const float MatchWidthOrHeight = 0.5f;

        // Names Unity gives new objects; a reviewer reads them as "nobody looked at this".
        // "Global Light 2D" is kept on purpose: CLAUDE.md names it as the scene's light.
        private static readonly Regex DefaultName = new Regex(
            @"^(GameObject|New Game Object|Image|RawImage|Panel|Canvas|Text|Button|Sprite|Square|Main Camera|" +
            @"Scroll View|Viewport|Content|Scrollbar Horizontal|Scrollbar Vertical|Toggle|Slider|Dropdown|InputField)" +
            @"( \(TMP\))?( \(\d+\))?$");

        private Scene _scene;

        // A preview scene reads the saved file, not the editor's in-memory copy: unsaved hierarchy
        // edits cannot pass the tests, and the developer's open scenes are never touched.
        [OneTimeSetUp]
        public void OpenMainScene()
        {
            _scene = EditorSceneManager.OpenPreviewScene(MainScenePath);
        }

        [OneTimeTearDown]
        public void CloseMainScene()
        {
            EditorSceneManager.ClosePreviewScene(_scene);
        }

        [Test]
        public void RootObjects_AreExactlyTheFourGroups()
        {
            string[] roots = _scene.GetRootGameObjects().Select(root => root.name).ToArray();

            Assert.That(roots, Is.EquivalentTo(new[] { SystemsGroup, BoardGroup, UiGroup, CameraGroup }));
        }

        [Test]
        public void Hierarchy_ContainsNoDefaultNames()
        {
            string[] defaultNamed = AllTransforms()
                .Select(transform => transform.name)
                .Where(name => DefaultName.IsMatch(name))
                .ToArray();

            Assert.That(defaultNamed, Is.Empty);
        }

        [Test]
        public void Camera_IsOrthographicUnderCameraGroup()
        {
            Camera camera = Root(CameraGroup).GetComponentInChildren<Camera>();

            Assert.That(camera, Is.Not.Null);
            Assert.That(camera.orthographic, Is.True);
        }

        [Test]
        public void Scene_HasSingleRootCanvasUnderUiGroup()
        {
            Canvas[] rootCanvases = AllTransforms()
                .SelectMany(transform => transform.GetComponents<Canvas>())
                .Where(canvas => canvas.GetComponentsInParent<Canvas>(true).Length == 1)
                .ToArray();

            Assert.That(rootCanvases, Has.Length.EqualTo(1));
            Assert.That(rootCanvases[0].transform.IsChildOf(Root(UiGroup).transform), Is.True);
        }

        [Test]
        public void Canvas_ScalesWithScreenSizeFromReferenceResolution()
        {
            CanvasScaler scaler = Root(UiGroup).GetComponentInChildren<CanvasScaler>(true);

            Assert.That(scaler, Is.Not.Null);
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(ReferenceResolution));
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(MatchWidthOrHeight));
        }

        [Test]
        public void Layout_ProductionMenuLeft_BoardCenter_InformationRight()
        {
            RectTransform production = UiElement(ProductionMenuPanel);
            RectTransform board = UiElement(GameBoardArea);
            RectTransform information = UiElement(InformationPanel);

            Assert.That(production.anchorMin.x, Is.EqualTo(0f), "Production menu must stick to the left edge.");
            Assert.That(production.anchorMax.x, Is.EqualTo(0f));
            Assert.That(information.anchorMin.x, Is.EqualTo(1f), "Information panel must stick to the right edge.");
            Assert.That(information.anchorMax.x, Is.EqualTo(1f));
            Assert.That(board.anchorMin, Is.EqualTo(Vector2.zero), "Board area must stretch with the window.");
            Assert.That(board.anchorMax, Is.EqualTo(Vector2.one));
            // The board starts where the left panel ends and stops where the right panel starts.
            Assert.That(board.offsetMin.x, Is.EqualTo(production.rect.width));
            Assert.That(board.offsetMax.x, Is.EqualTo(-information.rect.width));
        }

        [Test]
        public void UiGroup_HasEventSystemDrivenByInputSystem()
        {
            EventSystem eventSystem = Root(UiGroup).GetComponentInChildren<EventSystem>(true);

            Assert.That(eventSystem, Is.Not.Null);
            Assert.That(eventSystem.GetComponent<InputSystemUIInputModule>(), Is.Not.Null);
        }

        private GameObject Root(string name)
        {
            GameObject root = _scene.GetRootGameObjects().FirstOrDefault(candidate => candidate.name == name);
            Assert.That(root, Is.Not.Null, $"Root group '{name}' is missing.");
            return root;
        }

        private RectTransform UiElement(string name)
        {
            RectTransform element = Root(UiGroup).GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(candidate => candidate.name == name);
            Assert.That(element, Is.Not.Null, $"UI element '{name}' is missing.");
            return element;
        }

        private Transform[] AllTransforms()
        {
            return _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .ToArray();
        }
    }
}
