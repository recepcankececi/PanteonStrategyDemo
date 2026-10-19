using System.Linq;
using NUnit.Framework;
using StrategyDemo.Gameplay.Buildings;
using StrategyDemo.Gameplay.Placement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace StrategyDemo.Tests.EditMode.Project
{
    /// <summary>Assets and scene objects placement relies on: input actions, the Building prefab, the ghost.</summary>
    public class PlacementAssetTests
    {
        private const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string InputActionsPath = "Assets/_Project/Settings/GameInputActions.inputactions";
        private const string BuildingPrefabPath = "Assets/_Project/Prefabs/Board/Building.prefab";
        private const string CellHighlightPath = "Assets/_Project/Art/Generated/Board/CellHighlight.png";
        private const string SpawnMarkerPath = "Assets/_Project/Art/Generated/Board/SpawnMarker.png";
        private const string BoardGroup = "--- Board ---";
        private const string SystemsGroup = "--- Systems ---";
        private const string BuildingsContainer = "Buildings";
        private const string ObjectsLayer = "Objects";
        private const string FxLayer = "FX";
        private const string GroundLayer = "Ground";
        private const string UnlitSpriteMaterial = "Sprite-Unlit-Default";
        private const string KeyboardMouseGroup = "Keyboard&Mouse";

        private Scene _scene;

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

        [TestCase("Point", "<Mouse>/position")]
        [TestCase("Select", "<Mouse>/leftButton")]
        [TestCase("Command", "<Mouse>/rightButton")]
        [TestCase("Cancel", "<Keyboard>/escape")]
        [TestCase("Modifier", "<Keyboard>/shift")]
        public void GameplayMap_HasActionBoundTo(string action, string path)
        {
            InputAction inputAction = InputActions().FindActionMap("Gameplay", throwIfNotFound: true).FindAction(action);

            Assert.That(inputAction, Is.Not.Null, $"Gameplay/{action} is missing.");
            Assert.That(inputAction.bindings.Select(binding => binding.path), Has.Member(path));
            Assert.That(inputAction.bindings.All(binding => binding.groups.Contains(KeyboardMouseGroup)), Is.True);
        }

        [Test]
        public void InputActions_KeepUiMapAndDropTemplatePlayerMap()
        {
            InputActionAsset actions = InputActions();

            // The EventSystem's UI input module reads the UI map from this asset.
            Assert.That(actions.FindActionMap("UI"), Is.Not.Null);
            Assert.That(actions.FindActionMap("Player"), Is.Null);
        }

        [Test]
        public void InputActions_GenerateWrapperClassIntoAppModule()
        {
            var importer = new SerializedObject(AssetImporter.GetAtPath(InputActionsPath));

            Assert.That(importer.FindProperty("m_GenerateWrapperCode").boolValue, Is.True);
            Assert.That(importer.FindProperty("m_WrapperCodePath").stringValue, Is.EqualTo("Assets/_Project/Scripts/App/GameInputActions.cs"));
            Assert.That(importer.FindProperty("m_WrapperCodeNamespace").stringValue, Is.EqualTo("StrategyDemo.App"));
        }

        [Test]
        public void BuildingPrefab_DrawsUnlitOnObjectsLayer()
        {
            var building = AssetDatabase.LoadAssetAtPath<Building>(BuildingPrefabPath);
            Assert.That(building, Is.Not.Null, BuildingPrefabPath);

            var renderer = building.GetComponent<SpriteRenderer>();
            Assert.That(renderer.sortingLayerName, Is.EqualTo(ObjectsLayer));
            Assert.That(renderer.sharedMaterial.name, Is.EqualTo(UnlitSpriteMaterial));
        }

        [Test]
        public void BuildingPrefab_HasHiddenSpawnMarkerOnTheGroundFromWorldAtlas()
        {
            var building = AssetDatabase.LoadAssetAtPath<Building>(BuildingPrefabPath);
            var marker = (SpriteRenderer)new SerializedObject(building).FindProperty("_spawnMarker").objectReferenceValue;

            Assert.That(marker, Is.Not.Null, "Building.Init needs a spawn marker.");
            Assert.That(marker.name, Is.EqualTo("SpawnMarker"));
            Assert.That(marker.transform.parent, Is.SameAs(building.transform));
            Assert.That(AssetDatabase.GetAssetPath(marker.sprite), Is.EqualTo(SpawnMarkerPath));
            Assert.That(marker.sortingLayerName, Is.EqualTo(GroundLayer), "Soldiers walk over it.");
            Assert.That(marker.sharedMaterial.name, Is.EqualTo(UnlitSpriteMaterial));
            Assert.That(marker.enabled, Is.False, "Shown only for a building that produces soldiers.");
        }

        [Test]
        public void Board_GhostHasSpawnMarkerAboveTheBuildingPreview()
        {
            PlacementGhost ghost = Root(BoardGroup).GetComponentInChildren<PlacementGhost>(true);
            var serialized = new SerializedObject(ghost);
            var buildingRenderer = (SpriteRenderer)serialized.FindProperty("_buildingRenderer").objectReferenceValue;
            var marker = (SpriteRenderer)serialized.FindProperty("_spawnMarkerRenderer").objectReferenceValue;

            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.name, Is.EqualTo("SpawnMarker"));
            Assert.That(marker.transform.parent, Is.SameAs(ghost.transform));
            Assert.That(AssetDatabase.GetAssetPath(marker.sprite), Is.EqualTo(SpawnMarkerPath));
            Assert.That(marker.sortingLayerName, Is.EqualTo(FxLayer));
            Assert.That(marker.sortingOrder, Is.GreaterThan(buildingRenderer.sortingOrder));
            Assert.That(marker.sharedMaterial.name, Is.EqualTo(UnlitSpriteMaterial));
        }

        [Test]
        public void CellHighlight_IsOneCellFullRectSpriteForTiling()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(CellHighlightPath);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CellHighlightPath);

            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect), "Tiled draw mode needs a Full Rect mesh.");
            Assert.That(sprite.bounds.size.x, Is.EqualTo(1f), "One tile must cover exactly one cell.");
            Assert.That(sprite.bounds.size.y, Is.EqualTo(1f));
        }

        [Test]
        public void Board_HasBuildingsContainerAndGhostWithFxRenderers()
        {
            GameObject board = Root(BoardGroup);
            Assert.That(board.transform.Find(BuildingsContainer), Is.Not.Null);

            PlacementGhost ghost = board.GetComponentInChildren<PlacementGhost>(true);
            Assert.That(ghost, Is.Not.Null);
            var serialized = new SerializedObject(ghost);
            var buildingRenderer = (SpriteRenderer)serialized.FindProperty("_buildingRenderer").objectReferenceValue;
            var footprintRenderer = (SpriteRenderer)serialized.FindProperty("_footprintRenderer").objectReferenceValue;

            Assert.That(buildingRenderer.sortingLayerName, Is.EqualTo(FxLayer));
            Assert.That(footprintRenderer.sortingLayerName, Is.EqualTo(FxLayer));
            Assert.That(footprintRenderer.sortingOrder, Is.LessThan(buildingRenderer.sortingOrder), "The overlay lies under the building preview.");
            Assert.That(footprintRenderer.drawMode, Is.EqualTo(SpriteDrawMode.Tiled));
            Assert.That(AssetDatabase.GetAssetPath(footprintRenderer.sprite), Is.EqualTo(CellHighlightPath));
            Assert.That(buildingRenderer.sharedMaterial.name, Is.EqualTo(UnlitSpriteMaterial));
            Assert.That(footprintRenderer.sharedMaterial.name, Is.EqualTo(UnlitSpriteMaterial));
        }

        [Test]
        public void Systems_HasInputControllerWithEventSystem()
        {
            // Found by type name: the test assembly does not reference App.
            MonoBehaviour controller = Root(SystemsGroup).GetComponentsInChildren<MonoBehaviour>(true)
                .SingleOrDefault(component => component.GetType().Name == "InputController");
            Assert.That(controller, Is.Not.Null);

            Assert.That(new SerializedObject(controller).FindProperty("_eventSystem").objectReferenceValue, Is.Not.Null);
            // Must run after the EventSystem (default order 0) so the "pointer over UI" answer is from this frame.
            var order = (DefaultExecutionOrder)System.Attribute.GetCustomAttribute(controller.GetType(), typeof(DefaultExecutionOrder));
            Assert.That(order, Is.Not.Null);
            Assert.That(order.order, Is.GreaterThan(0));
        }

        private static InputActionAsset InputActions()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            Assert.That(actions, Is.Not.Null, InputActionsPath);
            return actions;
        }

        private GameObject Root(string name)
        {
            GameObject root = _scene.GetRootGameObjects().FirstOrDefault(candidate => candidate.name == name);
            Assert.That(root, Is.Not.Null, $"Root group '{name}' is missing.");
            return root;
        }
    }
}
