using System;
using NUnit.Framework;
using StrategyDemo.Core;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode.Core
{
    /// <summary>Nearest-free-cell BFS used for soldier spawning (R6 spawn point, T5 edge cases).</summary>
    public class FreeCellSearchTests
    {
        // Most tests are about distance, so every occupied cell may be crossed; the wall tests use NothingPasses.
        private static readonly Predicate<int> AllPass = id => true;
        private static readonly Predicate<int> NothingPasses = id => false;

        private GridMap _grid;
        private FreeCellSearch _search;
        // "Free" as the spawn search used it before callers could define it: no occupant.
        private Predicate<Vector2Int> _isEmpty;

        [SetUp]
        public void CreateSearch()
        {
            _grid = new GridMap(6, 5);
            _search = new FreeCellSearch(_grid);
            _isEmpty = cell => _grid.GetOccupant(cell) == GridMap.EmptyId;
        }

        [Test]
        public void Constructor_NullGrid_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new FreeCellSearch(null));
        }

        [Test]
        public void TryFindNearest_WantedCellFree_ReturnsIt()
        {
            Assert.That(_search.TryFindNearest(new Vector2Int(2, 2), AllPass, _isEmpty, out Vector2Int found), Is.True);
            Assert.That(found, Is.EqualTo(new Vector2Int(2, 2)));
        }

        [Test]
        public void TryFindNearest_WantedCellTaken_ReturnsAdjacentCellPreferringBelow()
        {
            _grid.Occupy(new Vector2Int(2, 2), Vector2Int.one, 1);

            _search.TryFindNearest(new Vector2Int(2, 2), AllPass, _isEmpty, out Vector2Int found);

            Assert.That(found, Is.EqualTo(new Vector2Int(2, 1)));
        }

        [Test]
        public void TryFindNearest_SurroundedByOccupiedRing_ReturnsCellTwoStepsAway()
        {
            // A 3×3 block centred on (2, 2): every cell one step away is taken too.
            _grid.Occupy(new Vector2Int(1, 1), new Vector2Int(3, 3), 1);

            _search.TryFindNearest(new Vector2Int(2, 2), AllPass, _isEmpty, out Vector2Int found);

            int steps = Mathf.Abs(found.x - 2) + Mathf.Abs(found.y - 2);
            Assert.That(steps, Is.EqualTo(2));
            Assert.That(_grid.GetOccupant(found), Is.EqualTo(GridMap.EmptyId));
        }

        [Test]
        public void TryFindNearest_WantedCellBelowBoard_IsClampedOntoBoard()
        {
            Assert.That(_search.TryFindNearest(new Vector2Int(3, -1), AllPass, _isEmpty, out Vector2Int found), Is.True);
            Assert.That(found, Is.EqualTo(new Vector2Int(3, 0)));
        }

        [Test]
        public void TryFindNearest_WantedCellFarOffBoardAndTaken_FindsFreeCellNearClampedCell()
        {
            _grid.Occupy(new Vector2Int(5, 0), Vector2Int.one, 1);

            _search.TryFindNearest(new Vector2Int(40, -7), AllPass, _isEmpty, out Vector2Int found);

            Assert.That(found, Is.EqualTo(new Vector2Int(4, 0)));
        }

        [Test]
        public void TryFindNearest_OnlyOneFreeCellLeft_FindsIt()
        {
            _grid.Occupy(Vector2Int.zero, new Vector2Int(6, 4), 1);
            _grid.Occupy(new Vector2Int(0, 4), new Vector2Int(5, 1), 2);

            Assert.That(_search.TryFindNearest(Vector2Int.zero, AllPass, _isEmpty, out Vector2Int found), Is.True);
            Assert.That(found, Is.EqualTo(new Vector2Int(5, 4)));
        }

        [Test]
        public void TryFindNearest_BoardFull_ReturnsFalse()
        {
            _grid.Occupy(Vector2Int.zero, new Vector2Int(6, 5), 1);

            Assert.That(_search.TryFindNearest(new Vector2Int(2, 2), AllPass, _isEmpty, out _), Is.False);
        }

        [Test]
        public void TryFindNearest_NullPredicate_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _search.TryFindNearest(Vector2Int.zero, null, _isEmpty, out _));
        }

        [Test]
        public void TryFindNearest_FreeCellOnlyBehindWall_IsNotReached()
        {
            // Column x = 2 is a wall (id 1); the start (0, 0) is taken by id 2; everything left of the wall is taken.
            _grid.Occupy(new Vector2Int(2, 0), new Vector2Int(1, 5), 1);
            _grid.Occupy(Vector2Int.zero, new Vector2Int(2, 5), 2);

            Assert.That(_search.TryFindNearest(Vector2Int.zero, id => id == 2, _isEmpty, out _), Is.False, "Cells right of the wall are walled off.");
        }

        [Test]
        public void TryFindNearest_PocketBehindWall_IsSkippedForReachableCell()
        {
            // Producer (id 2) holds the start (2, 2) and its left, right and upper neighbours. Below it, walls (id 1)
            // enclose a free pocket at (2, 0). Crossing walls, BFS meets the pocket first (it searches downwards first).
            foreach (Vector2Int cell in new[] { new Vector2Int(2, 2), new Vector2Int(1, 2), new Vector2Int(3, 2), new Vector2Int(2, 3) })
            {
                _grid.Occupy(cell, Vector2Int.one, 2);
            }

            foreach (Vector2Int cell in new[] { new Vector2Int(1, 0), new Vector2Int(3, 0), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(3, 1) })
            {
                _grid.Occupy(cell, Vector2Int.one, 1);
            }

            _search.TryFindNearest(new Vector2Int(2, 2), AllPass, _isEmpty, out Vector2Int crossingWalls);
            _search.TryFindNearest(new Vector2Int(2, 2), id => id == 2, _isEmpty, out Vector2Int found);

            Assert.That(crossingWalls, Is.EqualTo(new Vector2Int(2, 0)), "Precondition: crossing walls reaches the pocket.");
            Assert.That(found, Is.EqualTo(new Vector2Int(0, 2)));
        }

        [Test]
        public void TryFindNearest_StartTakenByWall_StillSpreadsFromStart()
        {
            _grid.Occupy(new Vector2Int(2, 2), Vector2Int.one, 1);

            Assert.That(_search.TryFindNearest(new Vector2Int(2, 2), NothingPasses, _isEmpty, out Vector2Int found), Is.True);
            Assert.That(found, Is.EqualTo(new Vector2Int(2, 1)));
        }

        [Test]
        public void TryFindNearest_RepeatedSearches_ReuseBuffersAndStayCorrect()
        {
            // The visited flags of the first search must not leak into the second.
            _search.TryFindNearest(new Vector2Int(0, 0), AllPass, _isEmpty, out _);
            _grid.Occupy(new Vector2Int(4, 4), Vector2Int.one, 1);

            _search.TryFindNearest(new Vector2Int(4, 4), AllPass, _isEmpty, out Vector2Int found);

            Assert.That(found, Is.EqualTo(new Vector2Int(4, 3)));
        }

        [Test]
        public void TryFindNearest_NullIsFree_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _search.TryFindNearest(Vector2Int.zero, AllPass, null, out _));
        }

        [Test]
        public void TryFindNearest_CallerRejectsEmptyCell_ReturnsNearestAcceptedCell()
        {
            // E.g. an empty cell another soldier will stop on: not free, but no wall either.
            var rejected = new Vector2Int(2, 2);
            Predicate<Vector2Int> isFree = cell => cell != rejected && _isEmpty(cell);

            Assert.That(_search.TryFindNearest(rejected, NothingPasses, isFree, out Vector2Int found), Is.True);
            Assert.That(found, Is.EqualTo(new Vector2Int(2, 1)));
        }

        [Test]
        public void TryFindNearest_RejectedEmptyCellInGap_IsWalkedThrough()
        {
            // Column 1 is a wall except (1,0), which is empty but rejected; the free cells beyond it must still be found.
            _grid.Occupy(new Vector2Int(1, 1), new Vector2Int(1, 4), 9);
            Predicate<Vector2Int> isFree = cell => cell.x >= 2 && _isEmpty(cell);

            Assert.That(_search.TryFindNearest(new Vector2Int(0, 2), NothingPasses, isFree, out Vector2Int found), Is.True);
            Assert.That(found, Is.EqualTo(new Vector2Int(2, 0)));
        }
    }
}
