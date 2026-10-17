using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using StrategyDemo.Core;
using StrategyDemo.Data;
using StrategyDemo.Gameplay;
using StrategyDemo.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>
    /// Information panel (I3, R4, R5): what it shows per selection, the Barracks production list and clicking
    /// a soldier (R3). Built in a preview scene with the real row prefab.
    /// </summary>
    public class InfoPanelTests
    {
        private const string RowPrefabPath = "Assets/_Project/Prefabs/UI/UnitProductionItem.prefab";

        private Scene _scene;
        private TestBoard _board;
        private GridMap _grid;
        private OccupantRegistry _registry;
        private SelectionService _selection;
        private UnitProducer _producer;
        private InfoPanel _panel;
        private GameObject _emptyState;
        private GameObject _details;
        private Image _image;
        private TMP_Text _nameLabel;
        private TMP_Text _healthLabel;
        private TMP_Text _footprintLabel;
        private TMP_Text _damageLabel;
        private GameObject _productionSection;
        private Transform _unitList;
        private TMP_Text _statusLabel;
        private UnitDefinition[] _soldiers;
        private BuildingDefinition _barracks;
        private BuildingDefinition _powerPlant;

        [SetUp]
        public void CreatePanel()
        {
            _scene = EditorSceneManager.NewPreviewScene();
            _board = new TestBoard(Vector3.zero, 10, 8);
            _soldiers = new[] { _board.Unit("Soldier1", 10), _board.Unit("Soldier2", 5), _board.Unit("Soldier3", 2) };
            _barracks = _board.Producer("Barracks", new Vector2Int(3, 2), new Vector2Int(1, -1), _soldiers);
            _powerPlant = _board.Definition("PowerPlant", new Vector2Int(2, 2));

            _grid = new GridMap(10, 8);
            _registry = new OccupantRegistry();
            _selection = new SelectionService(_grid, _registry);
            _producer = new UnitProducer(_grid, _registry, new CellReservations(10, 8), new FakeUnitFactory());

            var root = new GameObject("InformationPanel", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(root, _scene);
            _emptyState = Child("EmptyState", root.transform).gameObject;
            _details = Child("Details", root.transform).gameObject;
            _image = Child("SelectedImage", _details.transform).gameObject.AddComponent<Image>();
            _nameLabel = Label("NameLabel", _details.transform);
            _healthLabel = Label("HealthLabel", _details.transform);
            _footprintLabel = Label("FootprintLabel", _details.transform);
            _damageLabel = Label("DamageLabel", _details.transform);
            _productionSection = Child("ProductionSection", _details.transform).gameObject;
            _unitList = Child("UnitList", _productionSection.transform);
            _statusLabel = Label("StatusLabel", _details.transform);

            _panel = root.AddComponent<InfoPanel>();
            var serialized = new SerializedObject(_panel);
            serialized.FindProperty("_emptyState").objectReferenceValue = _emptyState;
            serialized.FindProperty("_details").objectReferenceValue = _details;
            serialized.FindProperty("_image").objectReferenceValue = _image;
            serialized.FindProperty("_nameLabel").objectReferenceValue = _nameLabel;
            serialized.FindProperty("_healthLabel").objectReferenceValue = _healthLabel;
            serialized.FindProperty("_footprintLabel").objectReferenceValue = _footprintLabel;
            serialized.FindProperty("_damageLabel").objectReferenceValue = _damageLabel;
            serialized.FindProperty("_productionSection").objectReferenceValue = _productionSection;
            serialized.FindProperty("_unitList").objectReferenceValue = _unitList;
            serialized.FindProperty("_unitItemPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnitProductionItemView>(RowPrefabPath);
            serialized.FindProperty("_statusLabel").objectReferenceValue = _statusLabel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _panel.Init(_selection, _producer);
        }

        [TearDown]
        public void DestroyPanel()
        {
            EditorSceneManager.ClosePreviewScene(_scene);
            _board.Dispose();
        }

        [Test]
        public void Init_NothingSelected_ShowsEmptyStateOnly()
        {
            Assert.That(_emptyState.activeSelf, Is.True);
            Assert.That(_details.activeSelf, Is.False);
        }

        [Test]
        public void Init_Twice_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _panel.Init(_selection, _producer));
        }

        [Test]
        public void Refresh_BarracksSelected_ShowsImageNameHealthAndFootprint()
        {
            SelectBuilding(_barracks, new Vector2Int(3, 3));

            Assert.That(_details.activeSelf, Is.True);
            Assert.That(_emptyState.activeSelf, Is.False);
            Assert.That(_image.sprite, Is.SameAs(_barracks.Sprite));
            Assert.That(_image.enabled, Is.True);
            Assert.That(_nameLabel.text, Is.EqualTo(_barracks.DisplayName));
            Assert.That(Text(_healthLabel), Is.EqualTo($"HP {_barracks.MaxHealth}/{_barracks.MaxHealth}"));
            Assert.That(Text(_footprintLabel), Is.EqualTo("Size 3×2"));
            Assert.That(_damageLabel.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Refresh_BarracksSelected_ListsItsThreeSoldiersInOrderWithDamage()
        {
            SelectBuilding(_barracks, new Vector2Int(3, 3));

            Assert.That(_productionSection.activeSelf, Is.True);
            Assert.That(_panel.Rows.Select(row => row.Unit), Is.EqualTo(_soldiers));
            // Sibling order is what the layout group draws, top to bottom.
            Assert.That(ActiveRowsInHierarchyOrder().Select(row => row.Unit), Is.EqualTo(_soldiers));
        }

        [Test]
        public void Refresh_PowerPlantSelected_HasNoProductionSection()
        {
            SelectBuilding(_powerPlant, new Vector2Int(0, 0));

            Assert.That(_productionSection.activeSelf, Is.False);
            Assert.That(_panel.Rows, Is.Empty);
            Assert.That(ActiveRowsInHierarchyOrder(), Is.Empty);
        }

        [Test]
        public void Refresh_SoldierSelected_ShowsDamageAndNoProduction()
        {
            SelectUnit(_soldiers[1], new Vector2Int(6, 6));

            Assert.That(_nameLabel.text, Is.EqualTo("Soldier2"));
            Assert.That(Text(_healthLabel), Is.EqualTo("HP 10/10"));
            Assert.That(Text(_footprintLabel), Is.EqualTo("Size 1×1"));
            Assert.That(_damageLabel.gameObject.activeSelf, Is.True);
            Assert.That(Text(_damageLabel), Is.EqualTo("Damage 5"));
            Assert.That(_productionSection.activeSelf, Is.False);
            Assert.That(_image.sprite, Is.SameAs(_soldiers[1].Sprite));
        }

        [Test]
        public void Refresh_SelectionCleared_ReturnsToEmptyStateAndReleasesRows()
        {
            SelectBuilding(_barracks, new Vector2Int(3, 3));

            _selection.Clear();

            Assert.That(_emptyState.activeSelf, Is.True);
            Assert.That(_details.activeSelf, Is.False);
            Assert.That(_panel.Rows, Is.Empty);
        }

        [Test]
        public void Refresh_ReselectingBarracks_ReusesPooledRows()
        {
            SelectBuilding(_barracks, new Vector2Int(3, 3));
            _selection.Clear();
            _selection.SelectAt(new Vector2Int(3, 3));

            Assert.That(_unitList.childCount, Is.EqualTo(3), "Rows are rebound, not instantiated again.");
        }

        [Test]
        public void ClickingSoldierRow_ProducesThatSoldierAtTheSpawnCell()
        {
            SelectBuilding(_barracks, new Vector2Int(3, 3));
            int before = _registry.Count;

            _panel.Rows[2].HandleClick();

            Assert.That(_registry.Count, Is.EqualTo(before + 1));
            int id = _grid.GetOccupant(new Vector2Int(4, 2));
            Assert.That(_registry.TryGet(id, out IBoardOccupant produced), Is.True);
            Assert.That(((FakeUnit)produced).Definition, Is.SameAs(_soldiers[2]));
            Assert.That(_statusLabel.text, Is.Empty);
            Assert.That(_selection.Selected, Is.InstanceOf<Building>(), "Producing keeps the Barracks selected.");
        }

        [Test]
        public void ClickingSoldierRow_BoardFull_ShowsMessageAndProducesNothing()
        {
            SelectBuilding(_barracks, new Vector2Int(3, 3));
            FillEveryFreeCell();
            int before = _registry.Count;

            _panel.Rows[0].HandleClick();

            Assert.That(_registry.Count, Is.EqualTo(before));
            Assert.That(_statusLabel.text, Is.Not.Empty);
        }

        [Test]
        public void SelectionChange_ClearsStatusMessage()
        {
            SelectBuilding(_barracks, new Vector2Int(3, 3));
            FillEveryFreeCell();
            _panel.Rows[0].HandleClick();

            _selection.Clear();

            Assert.That(_statusLabel.text, Is.Empty);
        }

        [Test]
        public void Health_SelectedBuildingTakesDamage_LabelFollowsLive()
        {
            Building building = SelectBuilding(_barracks, new Vector2Int(3, 3));

            building.Health.TakeDamage(30);

            Assert.That(Text(_healthLabel), Is.EqualTo($"HP {_barracks.MaxHealth - 30}/{_barracks.MaxHealth}"));
        }

        [Test]
        public void Health_AfterSelectionChanged_OldObjectNoLongerUpdatesLabel()
        {
            Building building = SelectBuilding(_barracks, new Vector2Int(3, 3));
            SelectUnit(_soldiers[0], new Vector2Int(0, 0));

            building.Health.TakeDamage(30);

            Assert.That(Text(_healthLabel), Is.EqualTo("HP 10/10"));
        }

        [Test]
        public void Refresh_SeveralSoldiersSelected_ShowsGroupHeaderWithFirstImageAndNoStats()
        {
            SelectUnit(_soldiers[1], new Vector2Int(0, 0));
            SelectUnit(_soldiers[2], new Vector2Int(1, 0));
            SelectUnit(_soldiers[0], new Vector2Int(2, 0));

            _selection.SelectSoldiersIn(new RectInt(0, 0, 3, 1));
            _panel.Refresh();

            Assert.That(Text(_nameLabel), Is.EqualTo("3 soldiers selected"));
            Assert.That(_image.sprite, Is.SameAs(_soldiers[1].Sprite), "Box order: bottom row, left to right.");
            Assert.That(_healthLabel.gameObject.activeSelf, Is.False);
            Assert.That(_footprintLabel.gameObject.activeSelf, Is.False);
            Assert.That(_damageLabel.gameObject.activeSelf, Is.False);
            Assert.That(_productionSection.activeSelf, Is.False);
        }

        [Test]
        public void Refresh_GroupShrinksToOne_ShowsThatSoldiersStatsAgain()
        {
            SelectUnit(_soldiers[0], new Vector2Int(0, 0));
            Unit second = SelectUnit(_soldiers[1], new Vector2Int(1, 0));
            _selection.SelectSoldiersIn(new RectInt(0, 0, 2, 1));

            // One of the two was destroyed (the despawner removes it from the selection).
            _selection.Remove(second);
            _panel.Refresh();

            Assert.That(Text(_nameLabel), Is.EqualTo(_soldiers[0].DisplayName));
            Assert.That(_healthLabel.gameObject.activeSelf, Is.True);
            Assert.That(Text(_healthLabel), Is.EqualTo("HP 10/10"));
        }

        [Test]
        public void Refresh_SelectedObjectRemoved_ShowsEmptyState()
        {
            Building building = SelectBuilding(_barracks, new Vector2Int(3, 3));

            _selection.Remove(building);
            _panel.Refresh();

            Assert.That(_emptyState.activeSelf, Is.True);
            Assert.That(_details.activeSelf, Is.False);
            Assert.That(_panel.Rows, Is.Empty);
        }

        private Building SelectBuilding(BuildingDefinition definition, Vector2Int origin)
        {
            Building building = TestBoard.CreateBuildingObject(definition.name);
            SceneManager.MoveGameObjectToScene(building.gameObject, _scene);
            int id = _registry.NextId();
            building.Init(definition, origin, id, Vector3.zero);
            _grid.Occupy(origin, definition.Footprint, id);
            _registry.Add(building);
            _selection.SelectAt(origin);
            _panel.Refresh();
            return building;
        }

        private Unit SelectUnit(UnitDefinition definition, Vector2Int cell)
        {
            Unit unit = TestBoard.CreateUnitObject(definition.name);
            SceneManager.MoveGameObjectToScene(unit.gameObject, _scene);
            int id = _registry.NextId();
            unit.Init(definition, cell, id, Vector3.zero);
            _grid.Occupy(cell, Vector2Int.one, id);
            _registry.Add(unit);
            _selection.SelectAt(cell);
            _panel.Refresh();
            return unit;
        }

        private void FillEveryFreeCell()
        {
            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (_grid.GetOccupant(cell) == GridMap.EmptyId)
                    {
                        _grid.Occupy(cell, Vector2Int.one, _registry.NextId());
                    }
                }
            }
        }

        private IEnumerable<UnitProductionItemView> ActiveRowsInHierarchyOrder()
        {
            return _unitList.Cast<Transform>()
                .Where(child => child.gameObject.activeSelf)
                .Select(child => child.GetComponent<UnitProductionItemView>());
        }

        // SetText fills TMP's char buffer; the text property turns that buffer back into a string.
        private static string Text(TMP_Text label)
        {
            return label.text;
        }

        private static Transform Child(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform)).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static TMP_Text Label(string name, Transform parent)
        {
            return Child(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        }

        /// <summary>A soldier without a GameObject that remembers its type.</summary>
        private sealed class FakeUnit : IBoardOccupant
        {
            public FakeUnit(UnitDefinition definition, int occupantId, Vector2Int cell)
            {
                Definition = definition;
                OccupantId = occupantId;
                Origin = cell;
            }

            public UnitDefinition Definition { get; }

            public int OccupantId { get; }

            public Vector2Int Origin { get; }

            public Vector2Int Footprint => Vector2Int.one;

            public bool IsMobile => true;

            public Vector3 WorldCenter => Vector3.zero;
        }

        private sealed class FakeUnitFactory : IUnitFactory
        {
            public IBoardOccupant Create(UnitDefinition definition, Vector2Int cell, int occupantId)
            {
                return new FakeUnit(definition, occupantId, cell);
            }

            public void Release(Unit unit)
            {
            }
        }
    }
}
