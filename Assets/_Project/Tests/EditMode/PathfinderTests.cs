using System;
using System.Collections.Generic;
using NUnit.Framework;
using StrategyDemo.Core;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
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

            Assert.That(_path, Has.Count.EqualTo(10), "Soldiers never block a path (ADR-001 Karar 11).");
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
