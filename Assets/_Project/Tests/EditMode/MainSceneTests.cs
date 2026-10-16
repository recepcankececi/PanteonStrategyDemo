using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StrategyDemo.Gameplay;
using StrategyDemo.UI;
using UnityEditor;
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
        private const string GameBootstrapName = "GameBootstrap";
        private const string GroundSortingLayer = "Ground";
        private const string UnlitSpriteMaterial = "Sprite-Unlit-Default";
        private const string ScriptPropertyName = "m_Script";
        private const string PanelSpriteName = "WoodTable_Stitched";

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
        public void BoardGroup_HasGridWithTiledUnlitGroundOnGroundLayer()
        {
            GridView grid = Root(BoardGroup).GetComponentInChildren<GridView>(true);
            Assert.That(grid, Is.Not.Null);

            SpriteRenderer ground = grid.GetComponentInChildren<SpriteRenderer>(true);
            Assert.That(ground, Is.Not.Null);
            Assert.That(ground.drawMode, Is.EqualTo(SpriteDrawMode.Tiled));
            Assert.That(ground.sortingLayerName, Is.EqualTo(GroundSortingLayer));
            // ADR-001 Karar 9: no 2D lights are used, so sprites render unlit (one pass, batches with the atlas).
            Assert.That(ground.sharedMaterial, Is.Not.Null);
            Assert.That(ground.sharedMaterial.name, Is.EqualTo(UnlitSpriteMaterial));
        }

        [Test]
        public void Ground_SpriteSupportsTiledNineSliceCentredOnBoard()
        {
            SpriteRenderer ground = Root(BoardGroup).GetComponentInChildren<GridView>(true).GetComponentInChildren<SpriteRenderer>(true);
            Sprite sprite = ground.sprite;
            Assert.That(sprite, Is.Not.Null);

            // GridView places the ground at the board centre, which is only right for a centred pivot.
            Assert.That(sprite.pivot, Is.EqualTo(sprite.rect.size * 0.5f));
            // A border is what keeps the bush edge on the rim while the grass repeats.
            Assert.That(sprite.border, Is.Not.EqualTo(Vector4.zero));
            string assetPath = AssetDatabase.GetAssetPath(sprite);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect), "Tiled draw mode needs a Full Rect mesh.");
        }

        [Test]
        public void Camera_FitsBoardToBoardArea()
        {
            BoardCameraFitter fitter = Root(CameraGroup).GetComponentInChildren<BoardCameraFitter>(true);
            Assert.That(fitter, Is.Not.Null);

            var boardArea = new SerializedObject(fitter).FindProperty("_boardArea").objectReferenceValue as RectTransform;
            Assert.That(boardArea, Is.Not.Null);
            Assert.That(boardArea.name, Is.EqualTo(GameBoardArea));
        }

        [Test]
        public void SystemsGroup_HasGameBootstrapWithAllReferencesAssigned()
        {
            // Found by type name: the test assembly may not reference App (ADR-001 Karar 3).
            MonoBehaviour bootstrap = Root(SystemsGroup).GetComponentsInChildren<MonoBehaviour>(true)
                .SingleOrDefault(component => component.GetType().Name == GameBootstrapName);
            Assert.That(bootstrap, Is.Not.Null);

            var serialized = new SerializedObject(bootstrap);
            SerializedProperty property = serialized.GetIterator();
            int checkedReferences = 0;
            // Walk every serialized object reference so a newly added field is covered automatically.
            for (bool enterChildren = true; property.NextVisible(enterChildren); enterChildren = false)
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == ScriptPropertyName)
                {
                    continue;
                }

                Assert.That(property.objectReferenceValue, Is.Not.Null, $"{GameBootstrapName}.{property.name} is unassigned.");
                checkedReferences++;
            }

            Assert.That(checkedReferences, Is.GreaterThan(0));
        }

        [Test]
        public void UiGroup_HasEventSystemDrivenByInputSystem()
        {
            EventSystem eventSystem = Root(UiGroup).GetComponentInChildren<EventSystem>(true);

            Assert.That(eventSystem, Is.Not.Null);
            Assert.That(eventSystem.GetComponent<InputSystemUIInputModule>(), Is.Not.Null);
        }

        [Test]
        public void ProductionMenu_IsUnrestrictedVerticalScrollInProductionPanel()
        {
            ProductionMenu menu = UiElement(ProductionMenuPanel).GetComponentInChildren<ProductionMenu>(true);
            Assert.That(menu, Is.Not.Null);

            // Same GameObject: drag events reach only the dragged object, and the menu needs them (ADR-004).
            var scrollRect = menu.GetComponent<ScrollRect>();
            Assert.That(scrollRect, Is.Not.Null);
            Assert.That(new SerializedObject(menu).FindProperty("_itemPrefab").objectReferenceValue, Is.Not.Null);
            Assert.That(scrollRect.vertical, Is.True);
            Assert.That(scrollRect.horizontal, Is.False);
            // Unrestricted: no end to clamp against, the list loops (ADR-001 Karar 8). An infinite list has no scrollbar.
            Assert.That(scrollRect.movementType, Is.EqualTo(ScrollRect.MovementType.Unrestricted));
            Assert.That(scrollRect.verticalScrollbar, Is.Null);
            Assert.That(scrollRect.content, Is.Not.Null);
            Assert.That(scrollRect.content.anchorMin.y, Is.EqualTo(1f), "Rows hang from the content's top edge.");
            Assert.That(scrollRect.content.anchorMax.y, Is.EqualTo(1f));
        }

        [Test]
        public void ProductionViewport_ClipsWithRectMaskInsideNestedCanvas()
        {
            ScrollRect scrollRect = UiElement(ProductionMenuPanel).GetComponentInChildren<ScrollRect>(true);
            RectTransform viewport = scrollRect.viewport;

            Assert.That(viewport, Is.Not.Null);
            // RectMask2D clips without the stencil pass a Mask adds (ADR-001 Karar 9).
            Assert.That(viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(viewport.GetComponent<Mask>(), Is.Null);
            // Nested canvas: scrolling rebuilds only the list, not the static panels (ADR-001 Karar 7).
            Assert.That(viewport.GetComponent<Canvas>(), Is.Not.Null);
            Assert.That(viewport.GetComponent<GraphicRaycaster>(), Is.Not.Null, "A nested canvas needs its own raycaster for clicks.");
        }

        [TestCase(ProductionMenuPanel)]
        [TestCase(InformationPanel)]
        public void SidePanel_UsesStitchedWoodPanelSprite(string panelName)
        {
            Image panel = UiElement(panelName).GetComponent<Image>();

            Assert.That(panel.sprite, Is.Not.Null);
            Assert.That(panel.sprite.name, Is.EqualTo(PanelSpriteName));
            Assert.That(panel.type, Is.EqualTo(Image.Type.Tiled));
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
