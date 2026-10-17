using System;
using NUnit.Framework;
using StrategyDemo.Core;
using StrategyDemo.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>The selection marker covers exactly the selected footprint and hides with no selection.</summary>
    public class SelectionHighlightTests
    {
        private TestBoard _board;
        private GridMap _grid;
        private OccupantRegistry _registry;
        private SelectionService _selection;
        private GameObject _highlightObject;
        private SpriteRenderer _renderer;
        private SelectionHighlight _highlight;

        [SetUp]
        public void CreateHighlight()
        {
            _board = new TestBoard(new Vector3(-3f, 2f, 0f), 10, 8);
            _grid = new GridMap(10, 8);
            _registry = new OccupantRegistry();
            _selection = new SelectionService(_grid, _registry);
            _highlightObject = new GameObject("SelectionHighlight");
            _renderer = _highlightObject.AddComponent<SpriteRenderer>();
            _renderer.drawMode = SpriteDrawMode.Tiled;
            _highlight = _highlightObject.AddComponent<SelectionHighlight>();
            _highlight.Init(_selection, _board.View);
        }

        [TearDown]
        public void DestroyObjects()
        {
            Object.DestroyImmediate(_highlightObject);
            _board.Dispose();
        }

        [Test]
        public void Init_NothingSelected_IsHidden()
        {
            Assert.That(_renderer.enabled, Is.False);
        }

        [Test]
        public void Init_Twice_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _highlight.Init(_selection, _board.View));
        }

        [Test]
        public void Refresh_BuildingSelected_CoversItsFootprint()
        {
            Select(new Vector2Int(2, 1), new Vector2Int(3, 2));

            _highlight.Refresh();

            Assert.That(_renderer.enabled, Is.True);
            Assert.That(_renderer.size, Is.EqualTo(new Vector2(3f, 2f)));
            // Board origin (-3, 2): the area spans x -1..2 and y 3..5, so its centre is (0.5, 4).
            Assert.That(_highlightObject.transform.position, Is.EqualTo(new Vector3(0.5f, 4f, 0f)));
        }

        [Test]
        public void Refresh_SoldierSelected_CoversOneCell()
        {
            Select(new Vector2Int(0, 0), Vector2Int.one);

            _highlight.Refresh();

            Assert.That(_renderer.size, Is.EqualTo(Vector2.one));
            Assert.That(_highlightObject.transform.position, Is.EqualTo(new Vector3(-2.5f, 2.5f, 0f)));
        }

        [Test]
        public void Refresh_SelectionCleared_Hides()
        {
            Select(new Vector2Int(2, 1), Vector2Int.one);
            _highlight.Refresh();

            _selection.Clear();
            _highlight.Refresh();

            Assert.That(_renderer.enabled, Is.False);
        }

        private void Select(Vector2Int origin, Vector2Int footprint)
        {
            var occupant = new FakeOccupant(_registry.NextId(), origin, footprint);
            _grid.Occupy(origin, footprint, occupant.OccupantId);
            _registry.Add(occupant);
            _selection.SelectAt(origin);
        }
    }
}
