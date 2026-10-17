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
        private const string PanelSpritePath = "Assets/_Project/Art/Generated/UI/Panel.png";
        private const string GroundSpritePath = "Assets/_Project/Art/Generated/Board/Ground.png";
        private const string BoardEdgeSpritePath = "Assets/_Project/Art/Generated/Board/BoardEdge.png";
        // One Canvas scaled from a 1920x1080 reference, width and height weighted equally.
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
            // No 2D lights are used, so sprites render unlit (one pass, batches with the atlas).
            Assert.That(ground.sharedMaterial, Is.Not.Null);
            Assert.That(ground.sharedMaterial.name, Is.EqualTo(UnlitSpriteMaterial));
        }

        [Test]
        public void Ground_IsOneCellRepeatingTileCentredOnBoard()
        {
            SpriteRenderer ground = Root(BoardGroup).GetComponentInChildren<GridView>(true).GetComponentInChildren<SpriteRenderer>(true);
            Sprite sprite = ground.sprite;
            Assert.That(sprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(GroundSpritePath));

            // GridView places the ground at the board centre, which is only right for a centred pivot.
            Assert.That(sprite.pivot, Is.EqualTo(sprite.rect.size * 0.5f));
            // One tile per cell, so the tile's edge line draws the grid exactly on the cell borders.
            Assert.That((Vector2)sprite.bounds.size, Is.EqualTo(Vector2.one));
            Assert.That(sprite.border, Is.EqualTo(Vector4.zero));
            var importer = (TextureImporter)AssetImporter.GetAtPath(GroundSpritePath);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect), "Tiled draw mode needs a Full Rect mesh.");
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat), "Bilinear sampling at a tile edge must blend with the opposite edge.");
        }

        [Test]
        public void Grid_HasSlicedBoardEdgeAboveGround()
        {
            GridView grid = Root(BoardGroup).GetComponentInChildren<GridView>(true);
            var edge = new SerializedObject(grid).FindProperty("_edge").objectReferenceValue as SpriteRenderer;
            SpriteRenderer ground = grid.GetComponentInChildren<SpriteRenderer>(true);

            Assert.That(edge, Is.Not.Null);
            Assert.That(edge.transform.parent, Is.SameAs(grid.transform));
            Assert.That(AssetDatabase.GetAssetPath(edge.sprite), Is.EqualTo(BoardEdgeSpritePath));
            Assert.That(edge.drawMode, Is.EqualTo(SpriteDrawMode.Sliced), "Sliced keeps the 1 px line 1 px at any board size.");
            Assert.That(edge.sortingLayerName, Is.EqualTo(GroundSortingLayer));
            Assert.That(edge.sortingOrder, Is.GreaterThan(ground.sortingOrder), "Drawn over the ground tiles.");
            Assert.That(edge.sharedMaterial, Is.SameAs(ground.sharedMaterial));
        }

        [Test]
        public void SystemsGroup_HasUnitMovementDriver()
        {
            Assert.That(Root(SystemsGroup).GetComponentInChildren<UnitMovementDriver>(true), Is.Not.Null);
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
        public void SystemsGroup_HasMinimumWindowSizeAtLeastHalfHd()
        {
            // Found by type name, like the bootstrap: App is not referenced by the tests.
            MonoBehaviour guard = Root(SystemsGroup).GetComponentsInChildren<MonoBehaviour>(true)
                .SingleOrDefault(component => component.GetType().Name == "MinimumWindowSize");
            Assert.That(guard, Is.Not.Null, "T2: the resizable window must not shrink until text is unreadable.");

            Vector2Int minimum = new SerializedObject(guard).FindProperty("_minimum").vector2IntValue;
            Assert.That(minimum.x, Is.GreaterThanOrEqualTo(960));
            Assert.That(minimum.y, Is.GreaterThanOrEqualTo(540));
        }

        [Test]
        public void SystemsGroup_HasGameBootstrapWithAllReferencesAssigned()
        {
            // Found by type name: the test assembly may not reference App.
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

            // Same GameObject: drag events reach only the dragged object, and the menu needs them.
            var scrollRect = menu.GetComponent<ScrollRect>();
            Assert.That(scrollRect, Is.Not.Null);
            Assert.That(new SerializedObject(menu).FindProperty("_itemPrefab").objectReferenceValue, Is.Not.Null);
            Assert.That(scrollRect.vertical, Is.True);
            Assert.That(scrollRect.horizontal, Is.False);
            // Unrestricted: no end to clamp against, the list loops. An infinite list has no scrollbar.
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
            // RectMask2D clips without the stencil pass a Mask adds.
            Assert.That(viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(viewport.GetComponent<Mask>(), Is.Null);
            // Nested canvas: scrolling rebuilds only the list, not the static panels.
            Assert.That(viewport.GetComponent<Canvas>(), Is.Not.Null);
            Assert.That(viewport.GetComponent<GraphicRaycaster>(), Is.Not.Null, "A nested canvas needs its own raycaster for clicks.");
        }

        [TestCase(ProductionMenuPanel)]
        [TestCase(InformationPanel)]
        public void SidePanel_UsesSlicedGeneratedPanelSprite(string panelName)
        {
            Image panel = UiElement(panelName).GetComponent<Image>();

            Assert.That(panel.sprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(panel.sprite), Is.EqualTo(PanelSpritePath));
            // Sliced: corners keep their size, the flat centre stretches to any resolution (T2).
            Assert.That(panel.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(panel.pixelsPerUnitMultiplier, Is.EqualTo(1f));
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
