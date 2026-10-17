using System;
using System.Collections.Generic;
using NUnit.Framework;
using StrategyDemo.Core;
using StrategyDemo.Data;
using StrategyDemo.Gameplay;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>
    /// Soldier movement (R6): walking A* paths around buildings, grid cell at the midpoint, one stop cell per
    /// soldier, unreachable goals, repath when a building appears, walking through other soldiers.
    /// </summary>
    public class UnitMovementTests
    {
        private const int Width = 10;
        private const int Height = 8;
        private const float FrameTime = 0.02f;
        // Generous: the longest walk here is ~15 cells at 3 cells/s ≈ 250 frames.
        private const int MaxFrames = 2000;
        private const float Tolerance = 1e-4f;

        private TestBoard _board;
        private GridMap _grid;
        private OccupantRegistry _registry;
        private CellReservations _reservations;
        private UnitMovement _movement;
        private UnitDefinition _soldier;
        private readonly List<Unit> _units = new List<Unit>();

        [SetUp]
        public void CreateMovement()
        {
            // Board origin at the world origin: cell (x, y) has its centre at (x + 0.5, y + 0.5).
            _board = new TestBoard(Vector3.zero, Width, Height);
            _grid = new GridMap(Width, Height);
            _registry = new OccupantRegistry();
            _reservations = new CellReservations(Width, Height);
            _movement = new UnitMovement(_grid, _registry, _reservations, _board.View);
            _soldier = _board.Unit("Soldier");
        }

        [TearDown]
        public void DestroyObjects()
        {
            foreach (Unit unit in _units)
            {
                Object.DestroyImmediate(unit.gameObject);
            }

            _units.Clear();
            _board.Dispose();
        }

        [Test]
        public void Constructor_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new UnitMovement(null, _registry, _reservations, _board.View));
            Assert.Throws<ArgumentNullException>(() => new UnitMovement(_grid, null, _reservations, _board.View));
            Assert.Throws<ArgumentNullException>(() => new UnitMovement(_grid, _registry, null, _board.View));
            Assert.Throws<ArgumentNullException>(() => new UnitMovement(_grid, _registry, _reservations, null));
        }

        [Test]
        public void MoveGroup_NullList_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _movement.MoveGroup(null, Vector2Int.zero));
        }

        [Test]
        public void Tick_NegativeTime_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _movement.Tick(-1f));
        }

        [Test]
        public void MoveGroup_TargetOffBoard_IsIgnored()
        {
            Unit unit = Spawn(new Vector2Int(2, 2));

            _movement.MoveGroup(new IBoardOccupant[] { unit }, new Vector2Int(-1, 3));

            Assert.That(_movement.MovingCount, Is.Zero);
            Assert.That(ReservedCell(unit), Is.EqualTo(new Vector2Int(2, 2)), "The standing soldier keeps its cell.");
        }

        [Test]
        public void MoveGroup_BuildingInSelection_IsSkipped()
        {
            IBoardOccupant building = PlaceBuilding(new Vector2Int(5, 5), new Vector2Int(2, 2));

            Assert.DoesNotThrow(() => _movement.MoveGroup(new[] { building }, new Vector2Int(1, 1)));
            Assert.That(_movement.MovingCount, Is.Zero);
        }

        [Test]
        public void MoveGroup_OpenBoard_ArrivesStandingOnTargetAndHoldsIt()
        {
            Unit unit = Spawn(new Vector2Int(1, 1));

            Move(new Vector2Int(6, 4), unit);
            RunUntilIdle();

            Assert.That(unit.Origin, Is.EqualTo(new Vector2Int(6, 4)));
            Assert.That(_grid.GetOccupant(new Vector2Int(6, 4)), Is.EqualTo(unit.OccupantId));
            Assert.That(_grid.GetOccupant(new Vector2Int(1, 1)), Is.EqualTo(GridMap.EmptyId), "The start cell is given back.");
            Assert.That(ReservedCell(unit), Is.EqualTo(new Vector2Int(6, 4)));
            AssertAt(unit, new Vector2Int(6, 4));
            Assert.That(unit.IsWalking, Is.False);
            Assert.That(_movement.MovingCount, Is.Zero);
        }

        [Test]
        public void Tick_GridCellChangesOnlyPastTheMidpoint()
        {
            // Default speed 3 cells/s: 0.1 s = 0.3 cells (before the midpoint), 0.2 s = 0.6 cells (past it).
            Unit unit = Spawn(new Vector2Int(0, 0));
            Move(new Vector2Int(3, 0), unit);

            _movement.Tick(0.1f);
            Assert.That(unit.Origin, Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(_grid.GetOccupant(new Vector2Int(1, 0)), Is.EqualTo(GridMap.EmptyId));

            _movement.Tick(0.1f);
            Assert.That(unit.Origin, Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(_grid.GetOccupant(new Vector2Int(1, 0)), Is.EqualTo(unit.OccupantId));
            Assert.That(_grid.GetOccupant(new Vector2Int(0, 0)), Is.EqualTo(GridMap.EmptyId));
        }

        [Test]
        public void Tick_SpeedComesFromUnitDefinition()
        {
            var serialized = new SerializedObject(_soldier);
            serialized.FindProperty("_moveSpeed").floatValue = 2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Unit unit = Spawn(new Vector2Int(0, 0));
            Move(new Vector2Int(9, 0), unit);

            _movement.Tick(1f);

            Assert.That(unit.transform.position.x, Is.EqualTo(2.5f).Within(Tolerance), "2 cells/s for 1 s from x = 0.5.");
        }

        [Test]
        public void Tick_WhileWalking_WalksAndFacesHeading()
        {
            Unit unit = Spawn(new Vector2Int(0, 3));
            Move(new Vector2Int(5, 3), unit);

            _movement.Tick(FrameTime);

            Assert.That(unit.IsWalking, Is.True);
            // Heading +X; the art faces +Y, so the visual turns a quarter clockwise (-90°, i.e. 270°).
            Assert.That(unit.transform.GetChild(0).localEulerAngles.z, Is.EqualTo(270f).Within(0.01f));
        }

        [Test]
        public void Tick_LongFrame_PassesSeveralCellsAndStopsExactlyOnTarget()
        {
            Unit unit = Spawn(new Vector2Int(0, 0));
            Move(new Vector2Int(4, 0), unit);

            _movement.Tick(10f);

            AssertAt(unit, new Vector2Int(4, 0));
            Assert.That(unit.Origin, Is.EqualTo(new Vector2Int(4, 0)));
            Assert.That(_movement.MovingCount, Is.Zero);
        }

        [Test]
        public void MoveGroup_BuildingInTheWay_NeverEntersItsCells()
        {
            PlaceBuilding(new Vector2Int(4, 0), new Vector2Int(2, 6));
            Unit unit = Spawn(new Vector2Int(1, 2));

            Move(new Vector2Int(8, 2), unit);
            RunUntilIdle(() => Assert.That(BuildingAt(BodyCell(unit)), Is.False, $"Walked into the building at {BodyCell(unit)}."));

            Assert.That(unit.Origin, Is.EqualTo(new Vector2Int(8, 2)));
        }

        [Test]
        public void MoveGroup_ThreeSoldiersToOneCell_EachStopsOnItsOwnCell()
        {
            Unit a = Spawn(new Vector2Int(0, 0));
            Unit b = Spawn(new Vector2Int(1, 0));
            Unit c = Spawn(new Vector2Int(2, 0));
            var target = new Vector2Int(6, 5);

            Move(target, a, b, c);
            RunUntilIdle();

            var cells = new HashSet<Vector2Int> { a.Origin, b.Origin, c.Origin };
            Assert.That(cells, Has.Count.EqualTo(3), "No two soldiers end on one cell.");
            Assert.That(cells, Has.Member(target));
            foreach (Unit unit in new[] { a, b, c })
            {
                Assert.That(Pathfinder.Heuristic(unit.Origin, target), Is.LessThanOrEqualTo(Pathfinder.DiagonalStepCost), "The group gathers round the target.");
                Assert.That(_grid.GetOccupant(unit.Origin), Is.EqualTo(unit.OccupantId));
                AssertAt(unit, unit.Origin);
            }
        }

        [Test]
        public void MoveGroup_GroupMovesOntoItsOwnCells_ReusesThem()
        {
            // Old stop cells are released first, so a soldier may take a cell another group member is leaving.
            Unit a = Spawn(new Vector2Int(3, 3));
            Unit b = Spawn(new Vector2Int(4, 3));

            Move(new Vector2Int(4, 3), a, b);
            RunUntilIdle();

            Assert.That(new[] { a.Origin, b.Origin }, Has.Member(new Vector2Int(4, 3)));
            Assert.That(a.Origin, Is.Not.EqualTo(b.Origin));
            Assert.That(_reservations.Count, Is.EqualTo(2));
        }

        [Test]
        public void MoveGroup_TargetEnclosedByBuildings_StopsAtClosestReachableCell()
        {
            PlaceBuilding(new Vector2Int(6, 3), new Vector2Int(3, 1));
            PlaceBuilding(new Vector2Int(6, 5), new Vector2Int(3, 1));
            PlaceBuilding(new Vector2Int(6, 4), Vector2Int.one);
            PlaceBuilding(new Vector2Int(8, 4), Vector2Int.one);
            Unit unit = Spawn(new Vector2Int(1, 4));

            Move(new Vector2Int(7, 4), unit);
            RunUntilIdle();

            Assert.That(unit.Origin, Is.EqualTo(new Vector2Int(5, 4)));
        }

        [Test]
        public void MoveGroup_TargetIsBuilding_StopsNextToIt()
        {
            PlaceBuilding(new Vector2Int(4, 3), new Vector2Int(3, 3));
            Unit unit = Spawn(new Vector2Int(0, 4));

            Move(new Vector2Int(5, 4), unit);
            RunUntilIdle();

            Assert.That(unit.Origin, Is.EqualTo(new Vector2Int(3, 4)));
        }

        [Test]
        public void MoveGroup_TargetIsStandingSoldier_StopsNextToItAndLeavesItThere()
        {
            Unit other = Spawn(new Vector2Int(5, 5));
            Unit unit = Spawn(new Vector2Int(0, 5));

            Move(new Vector2Int(5, 5), unit);
            RunUntilIdle();

            Assert.That(other.Origin, Is.EqualTo(new Vector2Int(5, 5)));
            Assert.That(Pathfinder.Heuristic(unit.Origin, other.Origin), Is.EqualTo(Pathfinder.StraightStepCost), "Stops beside it.");
        }

        [Test]
        public void Tick_BuildingPlacedOnPath_RepathsAroundIt()
        {
            Unit unit = Spawn(new Vector2Int(0, 3));
            Move(new Vector2Int(8, 3), unit);
            _movement.Tick(0.1f);

            // A wall right across the straight line, leaving a way round at the top.
            PlaceBuilding(new Vector2Int(4, 0), new Vector2Int(1, 7));
            RunUntilIdle(() => Assert.That(BuildingAt(BodyCell(unit)), Is.False));

            Assert.That(unit.Origin, Is.EqualTo(new Vector2Int(8, 3)));
        }

        [Test]
        public void Tick_BuildingPlacedOnStopCell_StopsNextToIt()
        {
            Unit unit = Spawn(new Vector2Int(0, 3));
            Move(new Vector2Int(8, 3), unit);
            _movement.Tick(0.1f);

            // Placement only checks the grid; the reserved but still empty stop cell can be built on.
            PlaceBuilding(new Vector2Int(8, 3), Vector2Int.one);
            RunUntilIdle();

            Assert.That(Pathfinder.Heuristic(unit.Origin, new Vector2Int(8, 3)), Is.EqualTo(Pathfinder.StraightStepCost));
            Assert.That(ReservedCell(unit), Is.EqualTo(unit.Origin));
        }

        [Test]
        public void Tick_WalkingThroughStandingSoldier_HoldsNoCellUntilPastIt()
        {
            Unit standing = Spawn(new Vector2Int(2, 0));
            Unit walker = Spawn(new Vector2Int(0, 0));
            Move(new Vector2Int(4, 0), walker);

            // Small frames until the centre is inside (2,0): the standing soldier's cell is never taken over.
            while (walker.transform.position.x < 2.2f)
            {
                _movement.Tick(FrameTime);
            }

            Assert.That(walker.Origin, Is.EqualTo(new Vector2Int(2, 0)), "Origin follows the centre.");
            Assert.That(_grid.GetOccupant(new Vector2Int(2, 0)), Is.EqualTo(standing.OccupantId));
            Assert.That(_grid.GetOccupant(new Vector2Int(1, 0)), Is.EqualTo(GridMap.EmptyId), "No cell it has left stays held.");

            RunUntilIdle();
            Assert.That(walker.Origin, Is.EqualTo(new Vector2Int(4, 0)));
            Assert.That(_grid.GetOccupant(new Vector2Int(1, 0)), Is.EqualTo(GridMap.EmptyId));
            Assert.That(standing.Origin, Is.EqualTo(new Vector2Int(2, 0)));
        }

        [Test]
        public void MoveGroup_TwoSoldiersSwapCells_BothFinish()
        {
            // Bottom row: the second soldier's nearest free cell is the one the first is leaving, so they walk head-on
            // through each other. Neither may wait forever for a cell the other holds.
            Unit a = Spawn(new Vector2Int(3, 0));
            Unit b = Spawn(new Vector2Int(4, 0));

            Move(new Vector2Int(4, 0), a, b);
            RunUntilIdle(AssertGridMatchesSoldiers);

            Assert.That(a.Origin, Is.EqualTo(new Vector2Int(4, 0)));
            Assert.That(b.Origin, Is.EqualTo(new Vector2Int(3, 0)));
            Assert.That(_grid.GetOccupant(a.Origin), Is.EqualTo(a.OccupantId));
            Assert.That(_grid.GetOccupant(b.Origin), Is.EqualTo(b.OccupantId));
        }

        [Test]
        public void MoveGroup_SoldiersTradePlacesInCorridor_AllFinish()
        {
            // A walled 3-cell corridor (0..2, 0): the outer two soldiers trade places through the middle one.
            PlaceBuilding(new Vector2Int(0, 1), new Vector2Int(5, 1));
            PlaceBuilding(new Vector2Int(3, 0), Vector2Int.one);
            Unit a = Spawn(new Vector2Int(0, 0));
            Unit b = Spawn(new Vector2Int(1, 0));
            Unit c = Spawn(new Vector2Int(2, 0));

            Move(new Vector2Int(2, 0), a, b, c);
            RunUntilIdle(AssertGridMatchesSoldiers);

            Assert.That(a.Origin, Is.EqualTo(new Vector2Int(2, 0)));
            Assert.That(new HashSet<Vector2Int> { a.Origin, b.Origin, c.Origin }, Has.Count.EqualTo(3));
            foreach (Unit unit in new[] { a, b, c })
            {
                Assert.That(_grid.GetOccupant(unit.Origin), Is.EqualTo(unit.OccupantId));
            }
        }

        [Test]
        public void MoveGroup_EveryReachableCellReserved_WaitsUntilACellFrees()
        {
            // A 2-cell pocket (0..1, 0) whose cells are both reserved by others (ids 900, 901 stand in for soldiers).
            PlaceBuilding(new Vector2Int(0, 1), new Vector2Int(3, 1));
            PlaceBuilding(new Vector2Int(2, 0), Vector2Int.one);
            Unit unit = Spawn(new Vector2Int(0, 0));
            _reservations.Release(unit.OccupantId);
            _reservations.Reserve(new Vector2Int(0, 0), 900);
            _reservations.Reserve(new Vector2Int(1, 0), 901);

            Move(new Vector2Int(1, 0), unit);
            for (int i = 0; i < 20; i++)
            {
                _movement.Tick(FrameTime);
            }

            Assert.That(_movement.IsMoving(unit), Is.True, "The order waits instead of ending without a stop cell.");
            AssertAt(unit, new Vector2Int(0, 0));

            _reservations.Release(901);
            RunUntilIdle();
            Assert.That(unit.Origin, Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(ReservedCell(unit), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        public void Remove_WalkingSoldier_DropsOrderReservationAndGridCell()
        {
            Unit unit = Spawn(new Vector2Int(0, 0));
            Move(new Vector2Int(5, 0), unit);
            _movement.Tick(0.3f);
            Vector2Int cell = unit.Origin;

            _movement.Remove(unit);

            Assert.That(_movement.MovingCount, Is.Zero);
            Assert.That(_reservations.Count, Is.Zero);
            Assert.That(_grid.GetOccupant(cell), Is.EqualTo(GridMap.EmptyId));
            Assert.DoesNotThrow(() => _movement.Tick(FrameTime), "No order is left pointing at the soldier.");
        }

        [Test]
        public void Remove_StandingSoldier_FreesItsCellAndReservation()
        {
            Unit unit = Spawn(new Vector2Int(2, 2));

            _movement.Remove(unit);

            Assert.That(_grid.GetOccupant(new Vector2Int(2, 2)), Is.EqualTo(GridMap.EmptyId));
            Assert.That(_reservations.OwnerOf(new Vector2Int(2, 2)), Is.EqualTo(GridMap.EmptyId));
        }

        [Test]
        public void Remove_StandingSoldierWhileAnotherWalksThroughIt_WalkerTakesTheCellAtOnce()
        {
            Unit standing = Spawn(new Vector2Int(2, 0));
            Unit walker = Spawn(new Vector2Int(0, 0));
            Move(new Vector2Int(4, 0), walker);
            while (walker.transform.position.x < 2.2f)
            {
                _movement.Tick(FrameTime);
            }

            _movement.Remove(standing);

            // Not one frame with the cell empty: a building could be placed on the walker in between.
            Assert.That(_grid.GetOccupant(new Vector2Int(2, 0)), Is.EqualTo(walker.OccupantId));
        }

        [Test]
        public void Remove_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _movement.Remove(null));
        }

        [Test]
        public void MoveGroup_WhileWalking_RedirectsAndMovesTheReservation()
        {
            Unit unit = Spawn(new Vector2Int(0, 0));
            Move(new Vector2Int(9, 0), unit);
            _movement.Tick(0.5f);

            Move(new Vector2Int(2, 6), unit);

            Assert.That(_reservations.OwnerOf(new Vector2Int(9, 0)), Is.EqualTo(GridMap.EmptyId));
            Assert.That(ReservedCell(unit), Is.EqualTo(new Vector2Int(2, 6)));
            Assert.That(_movement.MovingCount, Is.EqualTo(1), "One order per soldier, reused.");
            RunUntilIdle();
            Assert.That(unit.Origin, Is.EqualTo(new Vector2Int(2, 6)));
        }

        [Test]
        public void MoveGroup_RedirectedOffCentreWithDiagonalFirstStep_NeverStepsBack()
        {
            // Walking right from (0,0), then redirected while still in cell (0,0) but right of its centre. The new path
            // starts with the diagonal step (0,0) → (1,1): the soldier heads straight for (1,1), never back to its own
            // centre first (the line stays inside the 2×2 block that no-corner-cutting keeps free of buildings).
            Unit unit = Spawn(new Vector2Int(0, 0));
            Move(new Vector2Int(9, 0), unit);
            _movement.Tick(0.1f);
            Assert.That(BodyCell(unit), Is.EqualTo(new Vector2Int(0, 0)), "Precondition: still in the start cell.");
            Assert.That(unit.transform.position.x, Is.GreaterThan(0.5f + Tolerance), "Precondition: off its centre.");

            Move(new Vector2Int(5, 5), unit);
            Vector3 firstWaypoint = _board.View.CellToWorld(new Vector2Int(1, 1));
            Vector3 start = unit.transform.position;
            float distance = Vector3.Distance(unit.transform.position, firstWaypoint);
            // Stops one frame short: the frame that reaches (1,1) walks on past it towards (2,2).
            float stepPerFrame = _soldier.MoveSpeed * GridConfig.CellWorldSize * FrameTime;
            while (distance > stepPerFrame)
            {
                _movement.Tick(FrameTime);
                float next = Vector3.Distance(unit.transform.position, firstWaypoint);
                Assert.That(next, Is.LessThan(distance), "The soldier moved away from its next cell.");
                distance = next;
            }

            // The first step really was the diagonal one: a straight step to (1,0) would never have gained height.
            Assert.That(unit.transform.position.y, Is.GreaterThan(start.y + 0.5f));
        }

        [Test]
        public void Tick_BuildingPlacedOnSideCellOfDiagonalStep_RepathsWithoutEnteringIt()
        {
            // Redirected off-centre onto a diagonal path, then a building lands on a side cell of that first diagonal
            // step: the straight line would cross it, so the soldier must plan again instead of walking through.
            Unit unit = Spawn(new Vector2Int(0, 0));
            Move(new Vector2Int(9, 0), unit);
            _movement.Tick(0.1f);
            Move(new Vector2Int(5, 5), unit);
            PlaceBuilding(new Vector2Int(1, 0), Vector2Int.one);

            RunUntilIdle(() =>
            {
                Assert.That(BuildingAt(BodyCell(unit)), Is.False, "The soldier's centre entered a building.");
                AssertGridMatchesSoldiers();
            });

            Assert.That(unit.Origin, Is.EqualTo(new Vector2Int(5, 5)));
        }

        [Test]
        public void MoveGroup_OwnCell_FinishesWithoutMoving()
        {
            Unit unit = Spawn(new Vector2Int(3, 3));

            Move(new Vector2Int(3, 3), unit);
            _movement.Tick(FrameTime);

            Assert.That(_movement.MovingCount, Is.Zero);
            AssertAt(unit, new Vector2Int(3, 3));
            Assert.That(ReservedCell(unit), Is.EqualTo(new Vector2Int(3, 3)));
        }

        [Test]
        public void Tick_AfterWarmUp_AllocatesNothing()
        {
            Unit a = Spawn(new Vector2Int(0, 0));
            Unit b = Spawn(new Vector2Int(0, 1));
            Move(new Vector2Int(9, 7), a, b);
            _movement.Tick(FrameTime);

            AllocationAssert.None(() =>
            {
                for (int i = 0; i < 50; i++)
                {
                    _movement.Tick(FrameTime);
                }
            });
        }

        [Test]
        public void StepAside_OnCellReservedByABuilding_WalksToNearestFreeCell()
        {
            // A Barracks' spawn cell: reserved by the building, so the soldier standing on it must move on.
            IBoardOccupant barracks = PlaceBuilding(new Vector2Int(3, 3), new Vector2Int(3, 3));
            var spawnCell = new Vector2Int(4, 2);
            _reservations.Reserve(spawnCell, barracks.OccupantId);
            Unit unit = SpawnUnreserved(spawnCell);

            _movement.StepAside(unit);
            RunUntilIdle();

            AssertAt(unit, new Vector2Int(4, 1));
            Assert.That(_grid.GetOccupant(spawnCell), Is.EqualTo(GridMap.EmptyId));
            Assert.That(_reservations.OwnerOf(spawnCell), Is.EqualTo(barracks.OccupantId));
        }

        [Test]
        public void StepAside_OnAFreeCell_StaysThere()
        {
            Unit unit = SpawnUnreserved(new Vector2Int(2, 2));

            _movement.StepAside(unit);
            RunUntilIdle();

            AssertAt(unit, new Vector2Int(2, 2));
            Assert.That(_reservations.OwnerOf(new Vector2Int(2, 2)), Is.EqualTo(unit.OccupantId));
        }

        [Test]
        public void StepAside_SoldierWithoutGridCell_TakesOneOnceTheHolderLeaves()
        {
            // Two soldiers produced in one frame share the spawn cell; only the first holds it in the grid.
            IBoardOccupant barracks = PlaceBuilding(new Vector2Int(3, 3), new Vector2Int(3, 3));
            var spawnCell = new Vector2Int(4, 2);
            _reservations.Reserve(spawnCell, barracks.OccupantId);
            Unit first = SpawnUnreserved(spawnCell);
            Unit second = SpawnUnreserved(spawnCell, holdsCell: false);

            _movement.StepAside(first);
            _movement.StepAside(second);
            RunUntilIdle();

            Assert.That(first.Origin, Is.Not.EqualTo(second.Origin));
            Assert.That(_grid.GetOccupant(first.Origin), Is.EqualTo(first.OccupantId));
            Assert.That(_grid.GetOccupant(second.Origin), Is.EqualTo(second.OccupantId));
            AssertGridMatchesSoldiers();
        }

        [Test]
        public void StepAside_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _movement.StepAside(null));
        }

        [Test]
        public void HasStopCellNear_OpenBoard_IsTrue()
        {
            Assert.That(_movement.HasStopCellNear(new Vector2Int(4, 4)), Is.True);
        }

        [Test]
        public void HasStopCellNear_CellWalledInByBuildings_IsFalse()
        {
            // (0, 0) with buildings on (1, 0) and (0, 1): no step leads anywhere, and (0, 0) itself is reserved.
            PlaceBuilding(new Vector2Int(1, 0), Vector2Int.one);
            PlaceBuilding(new Vector2Int(0, 1), Vector2Int.one);
            _reservations.Reserve(Vector2Int.zero, _registry.NextId());

            Assert.That(_movement.HasStopCellNear(Vector2Int.zero), Is.False);
        }

        [Test]
        public void HasStopCellNear_OffBoard_IsFalse()
        {
            Assert.That(_movement.HasStopCellNear(new Vector2Int(-1, 0)), Is.False);
        }

        /// <summary>A soldier standing on <paramref name="cell"/> without a stop-cell reservation, as just produced.</summary>
        private Unit SpawnUnreserved(Vector2Int cell, bool holdsCell = true)
        {
            Unit unit = TestBoard.CreateUnitObject("Soldier");
            _units.Add(unit);
            int id = _registry.NextId();
            unit.Init(_soldier, cell, id, _board.View.CellToWorld(cell));
            if (holdsCell)
            {
                _grid.Occupy(cell, Vector2Int.one, id);
            }

            _registry.Add(unit);
            return unit;
        }

        private Unit Spawn(Vector2Int cell)
        {
            Unit unit = TestBoard.CreateUnitObject("Soldier");
            _units.Add(unit);
            int id = _registry.NextId();
            unit.Init(_soldier, cell, id, _board.View.CellToWorld(cell));
            _grid.Occupy(cell, Vector2Int.one, id);
            _registry.Add(unit);
            _reservations.Reserve(cell, id);
            return unit;
        }

        private IBoardOccupant PlaceBuilding(Vector2Int origin, Vector2Int size)
        {
            var building = new FakeOccupant(_registry.NextId(), origin, size);
            _grid.Occupy(origin, size, building.OccupantId);
            _registry.Add(building);
            return building;
        }

        private void Move(Vector2Int target, params Unit[] units)
        {
            _movement.MoveGroup(units, target);
        }

        private void RunUntilIdle(Action eachFrame = null)
        {
            for (int frame = 0; frame < MaxFrames && _movement.MovingCount > 0; frame++)
            {
                _movement.Tick(FrameTime);
                eachFrame?.Invoke();
            }

            Assert.That(_movement.MovingCount, Is.Zero, "Movement did not finish.");
        }

        /// <summary>
        /// The grid rule: a soldier's Origin is the cell its centre is in, and every such cell is held by a
        /// soldier standing in it.
        /// </summary>
        private void AssertGridMatchesSoldiers()
        {
            foreach (Unit unit in _units)
            {
                Assert.That(unit.Origin, Is.EqualTo(BodyCell(unit)));
                int holder = _grid.GetOccupant(unit.Origin);
                Assert.That(holder, Is.Not.EqualTo(GridMap.EmptyId), $"{unit.name}'s cell {unit.Origin} is held by nobody.");
                Assert.That(_registry.TryGet(holder, out IBoardOccupant occupant), Is.True);
                Assert.That(BodyCell((Unit)occupant), Is.EqualTo(unit.Origin), "The holder stands in the cell it holds.");
            }
        }

        private bool BuildingAt(Vector2Int cell)
        {
            int id = _grid.GetOccupant(cell);
            return id != GridMap.EmptyId && _registry.TryGet(id, out IBoardOccupant occupant) && !occupant.IsMobile;
        }

        private Vector2Int BodyCell(Unit unit)
        {
            return _board.View.WorldToCell(unit.transform.position);
        }

        private Vector2Int ReservedCell(Unit unit)
        {
            Assert.That(_reservations.TryGetCell(unit.OccupantId, out Vector2Int cell), Is.True, "Every soldier holds a stop cell.");
            return cell;
        }

        private void AssertAt(Unit unit, Vector2Int cell)
        {
            Vector3 centre = _board.View.CellToWorld(cell);
            Assert.That(Vector3.Distance(unit.transform.position, centre), Is.LessThan(Tolerance));
        }
    }
}
