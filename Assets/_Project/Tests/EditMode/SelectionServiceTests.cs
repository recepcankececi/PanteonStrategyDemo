using System;
using NUnit.Framework;
using StrategyDemo.Core;
using StrategyDemo.Gameplay;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Clicked cell → selected object through grid and registry (R4, R6, I3).</summary>
    public class SelectionServiceTests
    {
        private GridMap _grid;
        private OccupantRegistry _registry;
        private SelectionService _selection;
        private int _changedCount;

        [SetUp]
        public void CreateSelection()
        {
            _grid = new GridMap(10, 8);
            _registry = new OccupantRegistry();
            _selection = new SelectionService(_grid, _registry);
            _selection.SelectionChanged += () => _changedCount++;
            _changedCount = 0;
        }

        [Test]
        public void Constructor_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SelectionService(null, _registry));
            Assert.Throws<ArgumentNullException>(() => new SelectionService(_grid, null));
        }

        [Test]
        public void SelectAt_AnyCellOfBuilding_SelectsTheBuilding()
        {
            IBoardOccupant barracks = Place(new Vector2Int(2, 2), new Vector2Int(3, 2));

            _selection.SelectAt(new Vector2Int(4, 3));

            Assert.That(_selection.Selected, Is.SameAs(barracks));
            Assert.That(_changedCount, Is.EqualTo(1));
        }

        [Test]
        public void SelectAt_SoldierCell_SelectsTheSoldier()
        {
            Place(new Vector2Int(2, 2), new Vector2Int(3, 2));
            IBoardOccupant soldier = Place(new Vector2Int(3, 1), Vector2Int.one);

            _selection.SelectAt(new Vector2Int(3, 1));

            Assert.That(_selection.Selected, Is.SameAs(soldier));
        }

        [Test]
        public void SelectAt_EmptyCell_ClearsSelection()
        {
            Place(new Vector2Int(2, 2), Vector2Int.one);
            _selection.SelectAt(new Vector2Int(2, 2));

            _selection.SelectAt(new Vector2Int(7, 7));

            Assert.That(_selection.Selected, Is.Null);
            Assert.That(_changedCount, Is.EqualTo(2));
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(10, 0)]
        [TestCase(0, 8)]
        public void SelectAt_OffBoard_ClearsSelectionWithoutThrowing(int x, int y)
        {
            Place(Vector2Int.zero, Vector2Int.one);
            _selection.SelectAt(Vector2Int.zero);

            _selection.SelectAt(new Vector2Int(x, y));

            Assert.That(_selection.Selected, Is.Null);
        }

        [Test]
        public void SelectAt_SameObjectAgain_DoesNotNotify()
        {
            Place(new Vector2Int(2, 2), new Vector2Int(3, 2));
            _selection.SelectAt(new Vector2Int(2, 2));

            _selection.SelectAt(new Vector2Int(3, 3));

            Assert.That(_changedCount, Is.EqualTo(1));
        }

        [Test]
        public void SelectAt_EmptyWithNothingSelected_DoesNotNotify()
        {
            _selection.SelectAt(new Vector2Int(5, 5));

            Assert.That(_changedCount, Is.Zero);
        }

        [Test]
        public void SelectAt_CellWithUnregisteredId_TreatedAsEmpty()
        {
            _grid.Occupy(new Vector2Int(1, 1), Vector2Int.one, _registry.NextId());

            _selection.SelectAt(new Vector2Int(1, 1));

            Assert.That(_selection.Selected, Is.Null);
        }

        [Test]
        public void Clear_WithSelection_ClearsAndNotifies()
        {
            Place(Vector2Int.zero, Vector2Int.one);
            _selection.SelectAt(Vector2Int.zero);

            _selection.Clear();

            Assert.That(_selection.Selected, Is.Null);
            Assert.That(_changedCount, Is.EqualTo(2));
        }

        private IBoardOccupant Place(Vector2Int origin, Vector2Int footprint)
        {
            var occupant = new FakeOccupant(_registry.NextId(), origin, footprint);
            _grid.Occupy(origin, footprint, occupant.OccupantId);
            _registry.Add(occupant);
            return occupant;
        }
    }
}
