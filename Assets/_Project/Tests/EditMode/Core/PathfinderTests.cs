using System;
using System.Collections.Generic;
using NUnit.Framework;
using StrategyDemo.Core;
using StrategyDemo.Tests.EditMode.Support;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode.Core
{
    /// <summary>A* (A1, R6): shortest 8-direction paths around buildings, no corner cutting, closest-cell fallback, no GC.</summary>
    public class PathfinderTests
    {
        private const int Width = 10;
        private const int Height = 8;
        // Ids from this value up are soldiers (walkable); below are buildings (walls).
        private const int FirstSoldierId = 100;

        private GridMap _grid;
        private Pathfinder _pathfinder;
        private List<Vector2Int> _path;
        private int _nextBuildingId;

        [SetUp]
        public void CreatePathfinder()
        {
            _grid = new GridMap(Width, Height);
            _pathfinder = new Pathfinder(_grid, id => id >= FirstSoldierId);
            _path = new List<Vector2Int>(Width * Height);
            _nextBuildingId = 1;
        }

        [Test]
        public void Constructor_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new Pathfinder(null, id => true));
            Assert.Throws<ArgumentNullException>(() => new Pathfinder(_grid, null));
        }

        [Test]
        public void TryFindPath_InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => _pathfinder.TryFindPath(Vector2Int.zero, Vector2Int.one, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _pathfinder.TryFindPath(new Vector2Int(-1, 0), Vector2Int.one, _path));
            Assert.Throws<ArgumentOutOfRangeException>(() => _pathfinder.TryFindPath(Vector2Int.zero, new Vector2Int(Width, 0), _path));
        }

        [TestCase(0, 0, 0, 0, 0)]
        [TestCase(0, 0, 5, 0, 50)]
        [TestCase(0, 0, 3, 3, 42)]
        [TestCase(1, 2, 7, 4, 68)] // 2 diagonal (28) + 4 straight (40)
        public void Heuristic_IsOctileDistance(int fx, int fy, int tx, int ty, int expected)
        {
            Assert.That(Pathfinder.Heuristic(new Vector2Int(fx, fy), new Vector2Int(tx, ty)), Is.EqualTo(expected));
        }

        [Test]
        public void TryFindPath_StartIsGoal_PathIsJustStart()
        {
            Assert.That(_pathfinder.TryFindPath(new Vector2Int(3, 3), new Vector2Int(3, 3), _path), Is.True);
            Assert.That(_path, Is.EqualTo(new[] { new Vector2Int(3, 3) }));
        }

        [Test]
        public void TryFindPath_OpenBoardDiagonal_TakesDiagonalSteps()
        {
            Assert.That(_pathfinder.TryFindPath(new Vector2Int(0, 0), new Vector2Int(3, 3), _path), Is.True);

            Assert.That(_path, Is.EqualTo(new[] { V(0, 0), V(1, 1), V(2, 2), V(3, 3) }));
        }

        [Test]
        public void TryFindPath_OpenBoard_CostEqualsHeuristic()
        {
            // On an empty board the octile estimate is exact, so the path costs exactly that.
            _pathfinder.TryFindPath(V(1, 6), V(8, 1), _path);

            Assert.That(Cost(_path), Is.EqualTo(Pathfinder.Heuristic(V(1, 6), V(8, 1))));
            AssertConnected(_path);
        }

        [Test]
        public void TryFindPath_WallInTheWay_GoesAroundItOnShortestRoute()
        {
            // A vertical wall at x = 4, y = 0..5; the only way past it is over the top (y = 6, 7).
            Wall(V(4, 0), V(1, 6));

            Assert.That(_pathfinder.TryFindPath(V(2, 2), V(6, 2), _path), Is.True);

            AssertConnected(_path);
            AssertAvoidsBuildings(_path);
            Assert.That(_path[_path.Count - 1], Is.EqualTo(V(6, 2)));
            // No corner cutting means the wall's top cell (4,6) is entered and left straight: (2,2)→(3,6) is 1 diagonal
            // + 3 straight, then (3,6)→(5,6) 2 straight, then (5,6)→(6,2) 1 diagonal + 3 straight.
            Assert.That(Cost(_path), Is.EqualTo(2 * Pathfinder.DiagonalStepCost + 8 * Pathfinder.StraightStepCost));
        }

        [Test]
        public void TryFindPath_OneCellCorridor_FindsIt()
        {
            // Two buildings leave a one-cell gap at y = 4 in column x = 5.
            Wall(V(5, 0), V(1, 4));
            Wall(V(5, 5), V(1, 3));

            Assert.That(_pathfinder.TryFindPath(V(1, 4), V(9, 4), _path), Is.True);

            Assert.That(_path, Has.Member(V(5, 4)));
            AssertAvoidsBuildings(_path);
        }

        [Test]
        public void TryFindPath_DiagonalPastBuildingCorner_IsNotCut()
        {
            // (1,0) is a building: the diagonal (0,0)->(1,1) would brush its corner, so the path goes up first.
            Wall(V(1, 0), V(1, 1));

            _pathfinder.TryFindPath(V(0, 0), V(1, 1), _path);

            Assert.That(_path, Is.EqualTo(new[] { V(0, 0), V(0, 1), V(1, 1) }));
        }

        [Test]
        public void TryFindPath_DiagonalGapBetweenTwoBuildings_IsNotSqueezedThrough()
        {
            // Buildings touch only at corners: (1,0) and (0,1). (0,0) is sealed in for a corner-cutting-free walker.
            Wall(V(1, 0), V(1, 1));
            Wall(V(0, 1), V(1, 1));

            Assert.That(_pathfinder.TryFindPath(V(0, 0), V(1, 1), _path), Is.False);
            Assert.That(_path, Is.EqualTo(new[] { V(0, 0) }));
        }

        [Test]
        public void TryFindPath_SoldiersOnTheWay_WalksThroughThem()
        {
            for (int x = 1; x < 9; x++)
            {
                _grid.Occupy(V(x, 3), Vector2Int.one, FirstSoldierId + x);
            }

            _pathfinder.TryFindPath(V(0, 3), V(9, 3), _path);

            Assert.That(_path, Has.Count.EqualTo(10), "Soldiers never block a path.");
        }

        [Test]
        public void TryFindPath_GoalEnclosed_ReturnsFalseAndEndsAtClosestReachableCell()
        {
            // A ring of buildings around (7,4): its inside cell is unreachable.
            Wall(V(6, 3), V(3, 1));
            Wall(V(6, 5), V(3, 1));
            Wall(V(6, 4), V(1, 1));
            Wall(V(8, 4), V(1, 1));

            Assert.That(_pathfinder.TryFindPath(V(1, 4), V(7, 4), _path), Is.False);

            // Nearest walkable cell to (7,4) by octile distance from the reachable side: (5,4) at cost 20.
            Assert.That(_path[_path.Count - 1], Is.EqualTo(V(5, 4)));
            AssertConnected(_path);
            AssertAvoidsBuildings(_path);
        }

        [Test]
        public void TryFindPath_GoalIsBuildingCell_EndsNextToBuilding()
        {
            // Right-clicking a building's centre: the walker stops beside it.
            Wall(V(4, 3), V(3, 3));

            Assert.That(_pathfinder.TryFindPath(V(0, 4), V(5, 4), _path), Is.False);

            Assert.That(_path[_path.Count - 1], Is.EqualTo(V(3, 4)));
        }

        [Test]
        public void TryFindPath_StartWalledIn_PathIsJustStart()
        {
            Wall(V(0, 1), V(2, 1));
            Wall(V(1, 0), V(1, 1));

            Assert.That(_pathfinder.TryFindPath(V(0, 0), V(9, 7), _path), Is.False);
            Assert.That(_path, Is.EqualTo(new[] { V(0, 0) }));
        }

        [Test]
        public void TryFindPath_ReusedBuffer_IsClearedAndRefilled()
        {
            _pathfinder.TryFindPath(V(0, 0), V(9, 7), _path);

            _pathfinder.TryFindPath(V(2, 2), V(3, 2), _path);

            Assert.That(_path, Is.EqualTo(new[] { V(2, 2), V(3, 2) }));
        }

        [Test]
        public void TryFindPath_RepeatedSearches_StampsKeepResultsIndependent()
        {
            // The second search must not see the first one's closed cells (searchId stamping).
            Wall(V(4, 0), V(1, 6));
            _pathfinder.TryFindPath(V(2, 2), V(6, 2), _path);
            int firstCost = Cost(_path);

            _pathfinder.TryFindPath(V(6, 2), V(2, 2), _path);

            Assert.That(Cost(_path), Is.EqualTo(firstCost));
        }

        [Test]
        public void IsWalkable_EmptySoldierBuildingAndOffBoard()
        {
            Wall(V(1, 1), V(1, 1));
            _grid.Occupy(V(2, 2), Vector2Int.one, FirstSoldierId);

            Assert.That(_pathfinder.IsWalkable(V(0, 0)), Is.True);
            Assert.That(_pathfinder.IsWalkable(V(2, 2)), Is.True);
            Assert.That(_pathfinder.IsWalkable(V(1, 1)), Is.False);
            Assert.That(_pathfinder.IsWalkable(V(-1, 0)), Is.False);
        }

        [Test]
        public void TryFindPath_AfterWarmUp_AllocatesNothing()
        {
            Wall(V(4, 0), V(1, 6));
            _pathfinder.TryFindPath(V(0, 0), V(9, 0), _path);

            AllocationAssert.None(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    _pathfinder.TryFindPath(V(0, 0), V(9, 0), _path);
                    _pathfinder.TryFindPath(V(9, 7), V(0, 7), _path);
                }
            });
        }

        [TestCase(7, 3, 0)]
        [TestCase(0, 3, 60)]
        [TestCase(0, 0, 68)]
        [TestCase(9, 7, 34)]
        public void HeuristicToArea_IsOctileDistanceToNearestCell(int x, int y, int expected)
        {
            // Area: x 6..8, y 2..4.
            Assert.That(Pathfinder.HeuristicToArea(V(x, y), new RectInt(6, 2, 3, 3)), Is.EqualTo(expected));
        }

        [Test]
        public void TryFindPath_GoalArea_EndsOnItsNearestCell()
        {
            Assert.That(_pathfinder.TryFindPath(V(0, 3), new RectInt(6, 2, 3, 3), null, _path), Is.True);

            Assert.That(_path[_path.Count - 1], Is.EqualTo(V(6, 3)));
            Assert.That(Cost(_path), Is.EqualTo(60));
        }

        [Test]
        public void TryFindPath_GoalAreaWithAcceptTest_EndsOnNearestAcceptedCell()
        {
            // The nearest cell is taken (e.g. another soldier's stop cell): the next cheapest one of the area is used.
            Predicate<Vector2Int> notTheNearest = cell => cell != V(6, 3);

            Assert.That(_pathfinder.TryFindPath(V(0, 3), new RectInt(6, 2, 3, 3), notTheNearest, _path), Is.True);

            Vector2Int end = _path[_path.Count - 1];
            Assert.That(end == V(6, 2) || end == V(6, 4), Is.True, $"Ended on {end}.");
            Assert.That(Cost(_path), Is.EqualTo(64));
        }

        [Test]
        public void TryFindPath_GoalAreaNothingAccepted_ReturnsFalseAndEndsNextToArea()
        {
            Assert.That(_pathfinder.TryFindPath(V(0, 3), new RectInt(6, 2, 3, 3), cell => false, _path), Is.False);

            Assert.That(Pathfinder.HeuristicToArea(_path[_path.Count - 1], new RectInt(6, 2, 3, 3)), Is.Zero,
                "The fallback is the closest cell, here one inside the area.");
        }

        [Test]
        public void TryFindPath_GoalAreaReachesPastTheBoard_StillWorks()
        {
            Assert.That(_pathfinder.TryFindPath(V(0, 0), new RectInt(8, 6, 5, 5), null, _path), Is.True);

            Assert.That(_path[_path.Count - 1], Is.EqualTo(V(8, 6)));
        }

        [Test]
        public void TryFindPath_EmptyGoalArea_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _pathfinder.TryFindPath(V(0, 0), new RectInt(3, 3, 0, 1), null, _path));
        }

        [Test]
        public void TryFindPath_ApproachByRangeArea_ExaminesFarFewerCellsThanBlockedSingleGoal()
        {
            // The real board (24×16) and a 2×2 building at the far side. Before: the goal was one of the building's own
            // (blocked) cells, which is never reached, so A* examined every reachable cell. After: every cell within
            // range 1 is a goal, and the search ends at the first one.
            var grid = new GridMap(24, 16);
            var pathfinder = new Pathfinder(grid, id => id >= FirstSoldierId);
            var footprintOrigin = V(20, 7);
            var footprint = V(2, 2);
            grid.Occupy(footprintOrigin, footprint, 1);
            Vector2Int start = V(1, 8);

            pathfinder.TryFindPath(start, GridDistance.ClosestCellInArea(footprintOrigin, footprint, start), _path);
            int singleGoalCount = pathfinder.ExpandedCount;
            bool reached = pathfinder.TryFindPath(start, GridDistance.WithinRange(footprintOrigin, footprint, 1), null, _path);
            int areaCount = pathfinder.ExpandedCount;

            Assert.That(singleGoalCount, Is.EqualTo(24 * 16 - 4), "Before: the whole board was flooded.");
            Assert.That(reached, Is.True);
            Assert.That(areaCount, Is.LessThan(singleGoalCount / 5), $"After: {areaCount} cells examined.");
            Assert.That(_path[_path.Count - 1], Is.EqualTo(V(19, 8)));
        }

        [Test]
        public void TryFindPath_GoalAreaAfterWarmUp_AllocatesNothing()
        {
            Predicate<Vector2Int> accept = cell => cell.y != 3;
            _pathfinder.TryFindPath(V(0, 3), new RectInt(6, 2, 3, 3), accept, _path);

            AllocationAssert.None(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    _pathfinder.TryFindPath(V(0, 3), new RectInt(6, 2, 3, 3), accept, _path);
                    _pathfinder.TryFindPath(V(9, 7), new RectInt(0, 0, 2, 2), null, _path);
                }
            });
        }

        private void Wall(Vector2Int origin, Vector2Int size)
        {
            _grid.Occupy(origin, size, _nextBuildingId++);
        }

        private void AssertAvoidsBuildings(List<Vector2Int> path)
        {
            foreach (Vector2Int cell in path)
            {
                Assert.That(_pathfinder.IsWalkable(cell), Is.True, $"Path crosses a building at {cell}.");
            }
        }

        private static void AssertConnected(List<Vector2Int> path)
        {
            for (int i = 1; i < path.Count; i++)
            {
                Vector2Int step = path[i] - path[i - 1];
                Assert.That(Mathf.Max(Mathf.Abs(step.x), Mathf.Abs(step.y)), Is.EqualTo(1), $"Gap between {path[i - 1]} and {path[i]}.");
            }
        }

        private static int Cost(List<Vector2Int> path)
        {
            int cost = 0;
            for (int i = 1; i < path.Count; i++)
            {
                Vector2Int step = path[i] - path[i - 1];
                cost += step.x != 0 && step.y != 0 ? Pathfinder.DiagonalStepCost : Pathfinder.StraightStepCost;
            }

            return cost;
        }

        private static Vector2Int V(int x, int y)
        {
            return new Vector2Int(x, y);
        }
    }
}
