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

        [Test]
        public void SelectAtPoint_OnRoofAboveFootprint_SelectsBuilding()
        {
            // Barracks base covers rows 2..3; its image reaches up to y = 6 (the roof).
            IBoardOccupant barracks = Place(new Vector2Int(2, 2), new Vector2Int(3, 2), image: new Rect(2f, 2f, 3f, 4f));

            _selection.SelectAt(new Vector2Int(3, 5), new Vector2(3.5f, 5.5f));

            Assert.That(_selection.Selected, Is.SameAs(barracks));
        }

        [Test]
        public void SelectAtPoint_SoldierHeadOverBuildingBase_SelectsFrontMostSoldier()
        {
            // The soldier stands below the house; its head overlaps the house's bottom cell and is drawn in front (Y-sort).
            Place(new Vector2Int(2, 2), new Vector2Int(2, 2), image: new Rect(2f, 2f, 2f, 3f));
            IBoardOccupant soldier = Place(new Vector2Int(2, 1), Vector2Int.one, image: new Rect(2f, 1f, 1f, 1.5f));

            _selection.SelectAt(new Vector2Int(2, 2), new Vector2(2.5f, 2.3f));

            Assert.That(_selection.Selected, Is.SameAs(soldier));
        }

        [Test]
        public void SelectAtPoint_CellOccupantTiesWithImageHit_KeepsCellOccupant()
        {
            IBoardOccupant left = Place(new Vector2Int(2, 2), Vector2Int.one, image: new Rect(1.8f, 2f, 1.4f, 1.5f));
            Place(new Vector2Int(3, 2), Vector2Int.one, image: new Rect(2.8f, 2f, 1.4f, 1.5f));

            _selection.SelectAt(new Vector2Int(2, 2), new Vector2(2.9f, 2.5f));

            Assert.That(_selection.Selected, Is.SameAs(left));
        }

        [Test]
        public void SelectAtPoint_NothingHit_ClearsSelection()
        {
            Place(new Vector2Int(2, 2), Vector2Int.one, image: new Rect(2f, 2f, 1f, 1.5f));
            _selection.SelectAt(new Vector2Int(2, 2));

            _selection.SelectAt(new Vector2Int(7, 7), new Vector2(7.5f, 7.5f));

            Assert.That(_selection.Selected, Is.Null);
        }

        private IBoardOccupant Place(Vector2Int origin, Vector2Int footprint, Rect image = default)
        {
            var occupant = new FakeOccupant(_registry.NextId(), origin, footprint, image: image);
            _grid.Occupy(origin, footprint, occupant.OccupantId);
            _registry.Add(occupant);
            return occupant;
        }
    }
}
