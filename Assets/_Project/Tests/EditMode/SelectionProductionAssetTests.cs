using System.Linq;
using NUnit.Framework;
using StrategyDemo.Data;
using StrategyDemo.EditorTools;
using StrategyDemo.Gameplay;
using StrategyDemo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Assets and scene objects selection and production rely on (M4): soldier art, prefabs, highlight, panel.</summary>
    public class SelectionProductionAssetTests
    {
        private const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string UnitPrefabPath = "Assets/_Project/Prefabs/Board/Unit.prefab";
        private const string RowPrefabPath = "Assets/_Project/Prefabs/UI/UnitProductionItem.prefab";
        private const string WorldAtlasPath = "Assets/_Project/Art/Atlases/WorldAtlas.spriteatlasv2";
        private const string CellHighlightPath = "Assets/_Project/Art/Sprites/CellHighlight.png";
        private const string BarracksPath = "Assets/_Project/ScriptableObjects/Buildings/Barracks.asset";
        private const string UnitsFolder = "Assets/_Project/ScriptableObjects/Units/";
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

        [TestCase("Soldier1")]
        [TestCase("Soldier2")]
        [TestCase("Soldier3")]
        public void Soldier_IdleFramesShareOneSizeAndPivotAtBoardDensity(string asset)
        {
            UnitDefinition unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>(UnitsFolder + asset + ".asset");
            Assert.That(unit.IdleFrames, Is.Not.Empty);
            Sprite first = unit.IdleFrames[0];

            // Equal rects and pivots are what keep the idle animation from jittering.
            foreach (Sprite frame in unit.IdleFrames)
            {
                Assert.That(frame, Is.Not.Null);
                Assert.That(frame.rect.size, Is.EqualTo(first.rect.size), frame.name);
                Assert.That(frame.pivot, Is.EqualTo(first.pivot), frame.name);
                Assert.That(frame.pixelsPerUnit, Is.EqualTo(UnitSheetSlicer.PixelsPerUnit), frame.name);
            }

            // The pivot lies below the feet, on the cell's bottom edge like a building's.
            Assert.That(first.pivot.y, Is.EqualTo(-UnitSheetSlicer.FeetAboveCellBottom));
            Assert.That(unit.Icon, Is.SameAs(first), "The information panel icon is the first idle frame.");
            Assert.That(unit.IdleFramesPerSecond, Is.GreaterThan(0f));
        }

        [TestCase("Soldier1")]
        [TestCase("Soldier2")]
        [TestCase("Soldier3")]
        public void WorldAtlas_PacksSoldierSheet(string asset)
        {
            UnitDefinition unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>(UnitsFolder + asset + ".asset");
            string sheet = AssetDatabase.GetAssetPath(unit.IdleFrames[0]);
            // Read from disk; GetPackables() would also show unsaved in-memory edits.
            SpriteAtlasAsset atlas = SpriteAtlasAsset.Load(WorldAtlasPath);
            SerializedProperty packables = new SerializedObject(atlas).FindProperty("m_ImporterData.packables");
            string[] packed = Enumerable.Range(0, packables.arraySize)
                .Select(i => AssetDatabase.GetAssetPath(packables.GetArrayElementAtIndex(i).objectReferenceValue))
                .ToArray();

            Assert.That(packed, Has.Member(sheet));
        }

        [Test]
        public void Barracks_SpawnsBelowMiddleOfBottomRow()
        {
            var barracks = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(BarracksPath);

            Assert.That(barracks.SpawnOffset, Is.EqualTo(new Vector2Int(barracks.Footprint.x / 2, -1)));
        }

        [Test]
        public void UnitPrefab_DrawsUnlitOnObjectsLayerSortedByPivotWithFlipbook()
        {
            var unit = AssetDatabase.LoadAssetAtPath<Unit>(UnitPrefabPath);
            Assert.That(unit, Is.Not.Null, UnitPrefabPath);

            var renderer = unit.GetComponent<SpriteRenderer>();
            Assert.That(renderer.sortingLayerName, Is.EqualTo(ObjectsLayer), "Same layer as buildings so they Y-sort together.");
            Assert.That(renderer.spriteSortPoint, Is.EqualTo(SpriteSortPoint.Pivot));
            Assert.That(renderer.sharedMaterial.name, Is.EqualTo(UnlitSpriteMaterial));
            Assert.That(unit.GetComponent<SpriteFlipbook>(), Is.Not.Null);
            Assert.That(unit.GetComponent<Animator>(), Is.Null, "Idle animation uses the flipbook, not an Animator.");
        }

        [Test]
        public void UnitProductionRowPrefab_HasAllReferences()
        {
            var row = AssetDatabase.LoadAssetAtPath<UnitProductionItemView>(RowPrefabPath);
            Assert.That(row, Is.Not.Null, RowPrefabPath);

            AssertAllReferencesAssigned(row);
        }

        [Test]
        public void Board_HasUnitContainersAndSelectionHighlightOnFx()
        {
            Transform board = Root(BoardGroup).transform;
            Assert.That(board.Find("Units"), Is.Not.Null);
            Assert.That(board.Find("Pool_Unit"), Is.Not.Null, "Pool parents are named Pool_<Type> (CLAUDE.md).");

            var highlight = board.GetComponentInChildren<SelectionHighlight>(true);
            Assert.That(highlight, Is.Not.Null);
            var renderer = highlight.GetComponent<SpriteRenderer>();
            Assert.That(renderer.sortingLayerName, Is.EqualTo(FxLayer));
            Assert.That(renderer.drawMode, Is.EqualTo(SpriteDrawMode.Tiled));
            Assert.That(AssetDatabase.GetAssetPath(renderer.sprite), Is.EqualTo(CellHighlightPath));
            Assert.That(renderer.sharedMaterial.name, Is.EqualTo(UnlitSpriteMaterial));

            var ghostOverlay = board.Find("PlacementGhost/FootprintOverlay").GetComponent<SpriteRenderer>();
            Assert.That(renderer.sortingOrder, Is.LessThan(ghostOverlay.sortingOrder), "The placement preview draws over the selection.");
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
