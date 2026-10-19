using System;
using System.Reflection;
using NUnit.Framework;
using StrategyDemo.Core;
using StrategyDemo.Gameplay.Board;
using StrategyDemo.Gameplay.Effects;
using StrategyDemo.Gameplay.Selection;
using StrategyDemo.Gameplay.Units;
using StrategyDemo.Tests.EditMode.Support;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Tests.EditMode.Gameplay.Selection
{
    /// <summary>One marker per selected object, sized to its footprint, following it as it moves; pooled.</summary>
    public class SelectionHighlightTests
    {
        private GridMap _grid;
        private OccupantRegistry _registry;
        private SelectionService _selection;
        private GameObject _highlightObject;
        private SpriteRenderer _markerPrefab;
        private SelectionHighlight _highlight;
        private TestBoard _board;
        private Unit _unit;

        [SetUp]
        public void CreateHighlight()
        {
            _board = new TestBoard(Vector3.zero, 10, 8);
            _grid = new GridMap(10, 8);
            _registry = new OccupantRegistry();
            _selection = new SelectionService(_grid, _registry);
            // A scene object stands in for the prefab: Instantiate copies either the same way.
            _markerPrefab = new GameObject("SelectionMarker").AddComponent<SpriteRenderer>();
            _markerPrefab.drawMode = SpriteDrawMode.Tiled;
            _highlightObject = new GameObject("SelectionHighlight");
            _highlight = _highlightObject.AddComponent<SelectionHighlight>();
            var serialized = new SerializedObject(_highlight);
            serialized.FindProperty("_markerPrefab").objectReferenceValue = _markerPrefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _highlight.Init(_selection);
        }

        [TearDown]
        public void DestroyObjects()
        {
            Object.DestroyImmediate(_highlightObject);
            Object.DestroyImmediate(_markerPrefab.gameObject);
            if (_unit != null)
            {
                Object.DestroyImmediate(_unit.gameObject);
            }

            _board.Dispose();
        }

        [Test]
        public void Init_NothingSelected_ShowsNoMarker()
        {
            Assert.That(_highlight.Markers, Is.Empty);
        }

        [Test]
        public void Init_Twice_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _highlight.Init(_selection));
        }

        [Test]
        public void Init_MarkerPrefabMissing_Throws()
        {
            var other = new GameObject("Unwired").AddComponent<SelectionHighlight>();
            try
            {
                Assert.Throws<InvalidOperationException>(() => other.Init(_selection));
            }
            finally
            {
                Object.DestroyImmediate(other.gameObject);
            }
        }

        [Test]
        public void Refresh_BuildingSelected_OneMarkerCoversItsFootprintAtItsCentre()
        {
            Place(new Vector2Int(2, 1), new Vector2Int(3, 2), isMobile: false, new Vector3(0.5f, 4f, 0f));
            _selection.SelectAt(new Vector2Int(2, 1));

            _highlight.Refresh();

            Assert.That(_highlight.Markers, Has.Count.EqualTo(1));
            SpriteRenderer marker = _highlight.Markers[0];
            Assert.That(marker.gameObject.activeSelf, Is.True);
            Assert.That(marker.size, Is.EqualTo(new Vector2(3f, 2f)));
            Assert.That(marker.transform.position, Is.EqualTo(new Vector3(0.5f, 4f, 0f)));
            Assert.That(marker.transform.parent, Is.SameAs(_highlightObject.transform), "Markers live under the highlight, not the scene root.");
        }

        [Test]
        public void Refresh_BoxOfThreeSoldiers_OneOneCellMarkerEach()
        {
            for (int x = 0; x < 3; x++)
            {
                Place(new Vector2Int(x, 0), Vector2Int.one, isMobile: true, new Vector3(x, 0f, 0f));
            }

            _selection.SelectSoldiersIn(new RectInt(0, 0, 3, 1));
            _highlight.Refresh();

            Assert.That(_highlight.Markers, Has.Count.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(_highlight.Markers[i].size, Is.EqualTo(Vector2.one));
                Assert.That(_highlight.Markers[i].transform.position, Is.EqualTo(_selection.All[i].WorldCenter));
            }
        }

        [Test]
        public void FollowSelection_SoldierMoved_MarkerFollows()
        {
            _unit = TestBoard.CreateUnitObject("Walker");
            int id = _registry.NextId();
            _unit.Init(_board.Unit("Soldier"), new Vector2Int(1, 1), id, new Vector3(1.5f, 1.5f, 0f));
            _grid.Occupy(new Vector2Int(1, 1), Vector2Int.one, id);
            _registry.Add(_unit);
            _selection.SelectAt(new Vector2Int(1, 1));
            _highlight.Refresh();

            _unit.transform.position = new Vector3(2.2f, 1.5f, 0f);
            _highlight.FollowSelection();

            Assert.That(_highlight.Markers[0].transform.position, Is.EqualTo(new Vector3(2.2f, 1.5f, 0f)));
        }

        [Test]
        public void Refresh_SelectionCleared_HidesMarkersAndReusesThem()
        {
            Place(new Vector2Int(2, 1), Vector2Int.one, isMobile: true, Vector3.zero);
            _selection.SelectAt(new Vector2Int(2, 1));
            _highlight.Refresh();
            SpriteRenderer first = _highlight.Markers[0];

            _selection.Clear();
            _highlight.Refresh();
            Assert.That(_highlight.Markers, Is.Empty);
            Assert.That(first.gameObject.activeSelf, Is.False, "A released marker is hidden, not destroyed.");

            _selection.SelectAt(new Vector2Int(2, 1));
            _highlight.Refresh();
            Assert.That(_highlight.Markers[0], Is.SameAs(first), "The pooled marker is reused.");
            Assert.That(_highlightObject.transform.childCount, Is.EqualTo(1));
        }

        [Test]
        public void Refresh_SelectedSoldier_PinsItsHealthBarUntilDeselected()
        {
            _unit = TestBoard.CreateUnitObject("Pinned");
            int id = _registry.NextId();
            _unit.Init(_board.Unit("Soldier"), new Vector2Int(1, 1), id, new Vector3(1.5f, 1.5f, 0f));
            _grid.Occupy(new Vector2Int(1, 1), Vector2Int.one, id);
            _registry.Add(_unit);
            var bar = _unit.GetComponentInChildren<HealthBar>();

            _selection.SelectAt(new Vector2Int(1, 1));
            _highlight.Refresh();
            Assert.That(bar.IsVisible, Is.True, "Selected: shown even at full health.");

            _selection.Clear();
            _highlight.Refresh();
            Assert.That(bar.IsVisible, Is.False);
        }

        [Test]
        public void OnDisable_UnpinsHealthBars()
        {
            _unit = TestBoard.CreateUnitObject("Pinned");
            int id = _registry.NextId();
            _unit.Init(_board.Unit("Soldier"), new Vector2Int(1, 1), id, new Vector3(1.5f, 1.5f, 0f));
            _grid.Occupy(new Vector2Int(1, 1), Vector2Int.one, id);
            _registry.Add(_unit);
            _selection.SelectAt(new Vector2Int(1, 1));
            _highlight.Refresh();

            // EditMode never runs a plain MonoBehaviour's OnDisable, so call it as Unity would.
            typeof(SelectionHighlight).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_highlight, null);

            Assert.That(_unit.GetComponentInChildren<HealthBar>().IsVisible, Is.False);
        }

        private void Place(Vector2Int origin, Vector2Int footprint, bool isMobile, Vector3 worldCenter)
        {
            var occupant = new FakeOccupant(_registry.NextId(), origin, footprint, isMobile, worldCenter);
            _grid.Occupy(origin, footprint, occupant.OccupantId);
            _registry.Add(occupant);
        }
    }
}
