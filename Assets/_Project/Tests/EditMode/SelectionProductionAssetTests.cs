using System.Linq;
using NUnit.Framework;
using StrategyDemo.Data;
using StrategyDemo.Gameplay;
using StrategyDemo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Assets and scene objects selection and production rely on (M4): prefabs, highlight, panel.</summary>
    public class SelectionProductionAssetTests
    {
        private const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string UnitPrefabPath = "Assets/_Project/Prefabs/Board/Unit.prefab";
        private const string RowPrefabPath = "Assets/_Project/Prefabs/UI/UnitProductionItem.prefab";
        private const string SelectionMarkerPrefabPath = "Assets/_Project/Prefabs/Board/SelectionMarker.prefab";
        private const string CellHighlightPath = "Assets/_Project/Art/Generated/Board/CellHighlight.png";
        private const string BarracksPath = "Assets/_Project/ScriptableObjects/Buildings/Barracks.asset";
        private const string BoardGroup = "--- Board ---";
        private const string UiGroup = "--- UI ---";
        private const string ObjectsLayer = "Objects";
        private const string FxLayer = "FX";
        private const string UnlitSpriteMaterial = "Sprite-Unlit-Default";
        private const string ProjectWideActionsKey = "com.unity.input.settings.actions";

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

        [Test]
        public void Barracks_SpawnsBelowMiddleOfBottomRow()
        {
            var barracks = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(BarracksPath);

            Assert.That(barracks.SpawnOffset, Is.EqualTo(new Vector2Int(barracks.Footprint.x / 2, -1)));
        }

        [Test]
        public void UnitPrefab_DrawsUnlitOnObjectsLayerWithFlipbook()
        {
            var unit = AssetDatabase.LoadAssetAtPath<Unit>(UnitPrefabPath);
            Assert.That(unit, Is.Not.Null, UnitPrefabPath);

            var renderer = unit.GetComponentInChildren<SpriteRenderer>();
            Assert.That(renderer.sortingLayerName, Is.EqualTo(ObjectsLayer), "Same layer as buildings: one batch for both.");
            Assert.That(renderer.sharedMaterial.name, Is.EqualTo(UnlitSpriteMaterial));
            Assert.That(unit.GetComponent<SpriteRenderer>(), Is.Null, "The root never rotates, so it draws nothing.");
            Assert.That(renderer.transform.parent, Is.SameAs(unit.transform));
            Assert.That(renderer.GetComponent<SpriteFlipbook>(), Is.Not.Null);
            Assert.That(new SerializedObject(unit).FindProperty("_visual").objectReferenceValue, Is.SameAs(renderer.GetComponent<SpriteFlipbook>()));
            Assert.That(unit.GetComponent<Animator>(), Is.Null, "The walk cycle uses the flipbook, not an Animator.");
        }

        [Test]
        public void UnitProductionRowPrefab_HasAllReferences()
        {
            var row = AssetDatabase.LoadAssetAtPath<UnitProductionItemView>(RowPrefabPath);
            Assert.That(row, Is.Not.Null, RowPrefabPath);

            AssertAllReferencesAssigned(row);
        }

        [Test]
        public void Board_HasUnitContainersAndSelectionMarkersOnFx()
        {
            Transform board = Root(BoardGroup).transform;
            Assert.That(board.Find("Units"), Is.Not.Null);
            Assert.That(board.Find("Pool_Unit"), Is.Not.Null, "Pool parents are named Pool_<Type> (CLAUDE.md).");

            var highlight = board.GetComponentInChildren<SelectionHighlight>(true);
            Assert.That(highlight, Is.Not.Null);
            var marker = new SerializedObject(highlight).FindProperty("_markerPrefab").objectReferenceValue as SpriteRenderer;
            Assert.That(marker, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(marker), Is.EqualTo(SelectionMarkerPrefabPath), "Markers are copies of a prefab asset.");
            var box = board.GetComponentInChildren<SelectionBox>(true);
            Assert.That(box, Is.Not.Null);

            var ghostOverlay = board.Find("PlacementGhost/FootprintOverlay").GetComponent<SpriteRenderer>();
            foreach (SpriteRenderer renderer in new[] { marker, box.GetComponent<SpriteRenderer>() })
            {
                Assert.That(renderer.sortingLayerName, Is.EqualTo(FxLayer), renderer.name);
                Assert.That(renderer.drawMode, Is.EqualTo(SpriteDrawMode.Tiled), renderer.name);
                Assert.That(AssetDatabase.GetAssetPath(renderer.sprite), Is.EqualTo(CellHighlightPath), renderer.name);
                Assert.That(renderer.sharedMaterial.name, Is.EqualTo(UnlitSpriteMaterial), renderer.name);
                Assert.That(renderer.sortingOrder, Is.LessThan(ghostOverlay.sortingOrder), "The placement preview draws over the selection.");
            }
        }

        [Test]
        public void InformationPanel_HasInfoPanelWithAllReferences()
        {
            InfoPanel panel = Root(UiGroup).GetComponentsInChildren<InfoPanel>(true).SingleOrDefault();
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.name, Is.EqualTo("InformationPanel"));

            AssertAllReferencesAssigned(panel);
        }

        [Test]
        public void InputActions_AreNotRegisteredProjectWide()
        {
            // Registered project-wide, Input System enables its own copy of the asset next to GameInputActions,
            // so every action would run twice (ADR-005, closed in M4).
            Assert.That(EditorBuildSettings.TryGetConfigObject(ProjectWideActionsKey, out Object _), Is.False);
        }

        private static void AssertAllReferencesAssigned(Object component)
        {
            SerializedProperty property = new SerializedObject(component).GetIterator();
            int checkedReferences = 0;
            for (bool enterChildren = true; property.NextVisible(enterChildren); enterChildren = false)
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script")
                {
                    continue;
                }

                Assert.That(property.objectReferenceValue, Is.Not.Null, $"{component.name}.{property.name} is unassigned.");
                checkedReferences++;
            }

            Assert.That(checkedReferences, Is.GreaterThan(0));
        }

        private GameObject Root(string name)
        {
            GameObject root = _scene.GetRootGameObjects().FirstOrDefault(candidate => candidate.name == name);
            Assert.That(root, Is.Not.Null, $"Root group '{name}' is missing.");
            return root;
        }
    }
}
