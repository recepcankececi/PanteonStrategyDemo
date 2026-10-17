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
    /// <summary>Placement rules on a 10×8 board (R2, R3): validity, placing, keep-placing, cancel and switching.</summary>
    public class BuildingPlacerTests
    {
        private const int Width = 10;
        private const int Height = 8;

        private GridMap _grid;
        private OccupantRegistry _occupants;
        private RecordingFactory _factory;
        private BuildingPlacer _placer;
        private BuildingDefinition _barracks;
        private BuildingDefinition _powerPlant;
        // A 3×3 building that produces a soldier, so it needs a spawn cell like the shipped Barracks.
        private BuildingDefinition _producer;
        private UnitDefinition _soldier;
        private CellReservations _reservations;
        private int _changedCount;

        [SetUp]
        public void CreatePlacer()
        {
            _grid = new GridMap(Width, Height);
            _occupants = new OccupantRegistry();
            _reservations = new CellReservations(Width, Height);
            _factory = new RecordingFactory();
            _placer = new BuildingPlacer(_grid, _occupants, _reservations, _factory);
            _placer.Changed += () => _changedCount++;
            _changedCount = 0;
            _barracks = Definition(new Vector2Int(3, 2));
            _powerPlant = Definition(new Vector2Int(2, 2));
            _soldier = ScriptableObject.CreateInstance<UnitDefinition>();
            _producer = Definition(new Vector2Int(3, 3), _soldier);
        }

        [TearDown]
        public void DestroyDefinitions()
        {
            Object.DestroyImmediate(_barracks);
            Object.DestroyImmediate(_powerPlant);
            Object.DestroyImmediate(_producer);
            Object.DestroyImmediate(_soldier);
        }

        [Test]
        public void Constructor_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new BuildingPlacer(null, _occupants, _reservations, _factory));
            Assert.Throws<ArgumentNullException>(() => new BuildingPlacer(_grid, null, _reservations, _factory));
            Assert.Throws<ArgumentNullException>(() => new BuildingPlacer(_grid, _occupants, _reservations, null));
            Assert.Throws<ArgumentNullException>(() => new BuildingPlacer(_grid, _occupants, null, _factory));
        }

        [Test]
        public void Begin_NullDefinition_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _placer.Begin(null));
        }

        [Test]
        public void Begin_StartsPlacingWithoutCandidate()
        {
            _placer.Begin(_barracks);

            Assert.That(_placer.IsPlacing, Is.True);
            Assert.That(_placer.Definition, Is.SameAs(_barracks));
            Assert.That(_placer.HasCandidate, Is.False, "The ghost stays hidden until the pointer reaches the board.");
            Assert.That(_changedCount, Is.EqualTo(1));
        }

        [Test]
        public void MoveTo_FreeAreaOnBoard_IsValid()
        {
            _placer.Begin(_barracks);

            _placer.MoveTo(new Vector2Int(2, 3));

            Assert.That(_placer.HasCandidate, Is.True);
            Assert.That(_placer.CandidateOrigin, Is.EqualTo(new Vector2Int(2, 3)));
            Assert.That(_placer.IsCandidateValid, Is.True);
        }

        [TestCase(-1, 0, TestName = "MoveTo_PartlyOffLeftEdge_IsInvalid")]
        [TestCase(8, 0, TestName = "MoveTo_PartlyOffRightEdge_IsInvalid")]
        [TestCase(0, 7, TestName = "MoveTo_PartlyOffTopEdge_IsInvalid")]
        [TestCase(-20, -20, TestName = "MoveTo_WhollyOffBoard_IsInvalid")]
        public void MoveTo_OffBoard_IsInvalid(int x, int y)
        {
            _placer.Begin(_barracks);

            _placer.MoveTo(new Vector2Int(x, y));

            Assert.That(_placer.HasCandidate, Is.True, "An invalid spot is still shown, in red.");
            Assert.That(_placer.IsCandidateValid, Is.False);
        }

        [Test]
        public void MoveTo_TouchingBoardCorners_IsValid()
        {
            _placer.Begin(_barracks);

            _placer.MoveTo(new Vector2Int(Width - 3, Height - 2));

            Assert.That(_placer.IsCandidateValid, Is.True);
        }

        [Test]
        public void MoveTo_ExactOverlapWithBuilding_IsInvalid()
        {
            PlaceAt(_barracks, new Vector2Int(2, 2));
            _placer.Begin(_barracks);

            _placer.MoveTo(new Vector2Int(2, 2));

            Assert.That(_placer.IsCandidateValid, Is.False);
        }

        [Test]
        public void MoveTo_PartialOverlapByOneCell_IsInvalid()
        {
            PlaceAt(_barracks, new Vector2Int(2, 2));
            _placer.Begin(_powerPlant);

            // Power Plant covers (4..5, 3..4); the barracks covers (2..4, 2..3): they share cell (4, 3).
            _placer.MoveTo(new Vector2Int(4, 3));

            Assert.That(_placer.IsCandidateValid, Is.False);
        }

        [Test]
        public void MoveTo_RightNextToBuilding_IsValid()
        {
            PlaceAt(_barracks, new Vector2Int(2, 2));
            _placer.Begin(_powerPlant);

            _placer.MoveTo(new Vector2Int(5, 2));

            Assert.That(_placer.IsCandidateValid, Is.True);
        }

        [Test]
        public void MoveTo_SameOrigin_DoesNotNotifyAgain()
        {
            _placer.Begin(_barracks);
            _placer.MoveTo(new Vector2Int(1, 1));
            int afterFirstMove = _changedCount;

            _placer.MoveTo(new Vector2Int(1, 1));

            Assert.That(_changedCount, Is.EqualTo(afterFirstMove));
        }

        [Test]
        public void MoveTo_WhenNotPlacing_IsIgnored()
        {
            _placer.MoveTo(new Vector2Int(1, 1));

            Assert.That(_placer.HasCandidate, Is.False);
            Assert.That(_changedCount, Is.Zero);
        }

        [Test]
        public void TryPlace_ValidCandidate_OccupiesGridCreatesBuildingAndEnds()
        {
            _placer.Begin(_barracks);
            _placer.MoveTo(new Vector2Int(2, 2));

            bool placed = _placer.TryPlace(keepPlacing: false);

            Assert.That(placed, Is.True);
            Assert.That(_grid.IsAreaFree(new Vector2Int(2, 2), _barracks.Footprint), Is.False);
            Assert.That(_grid.GetOccupant(new Vector2Int(4, 3)), Is.EqualTo(1));
            Assert.That(_factory.Created, Has.Count.EqualTo(1));
            Assert.That(_factory.Created[0], Is.EqualTo((_barracks, new Vector2Int(2, 2), 1)));
            Assert.That(_placer.IsPlacing, Is.False);
            Assert.That(_placer.HasCandidate, Is.False);
        }

        [Test]
        public void TryPlace_InvalidCandidate_PlacesNothingAndKeepsPlacing()
        {
            _placer.Begin(_barracks);
            _placer.MoveTo(new Vector2Int(-1, 0));

            bool placed = _placer.TryPlace(keepPlacing: false);

            Assert.That(placed, Is.False);
            Assert.That(_factory.Created, Is.Empty);
            Assert.That(_grid.GetOccupant(Vector2Int.zero), Is.EqualTo(GridMap.EmptyId));
            Assert.That(_placer.IsPlacing, Is.True, "A click on a red spot must not end placement.");
        }

        [Test]
        public void TryPlace_OnOverlap_LeavesExistingBuildingUntouched()
        {
            PlaceAt(_barracks, new Vector2Int(2, 2));
            _placer.Begin(_powerPlant);
            _placer.MoveTo(new Vector2Int(3, 3));

            bool placed = _placer.TryPlace(keepPlacing: false);

            Assert.That(placed, Is.False);
            Assert.That(_grid.GetOccupant(new Vector2Int(3, 3)), Is.EqualTo(1));
            Assert.That(_factory.Created, Has.Count.EqualTo(1));
        }

        [Test]
        public void TryPlace_AreaOccupiedAfterMove_ReturnsFalseAndTurnsInvalid()
        {
            _placer.Begin(_barracks);
            _placer.MoveTo(new Vector2Int(2, 2));
            // Something else takes a cell while the pointer stands still (e.g. another system in a later milestone).
            _grid.Occupy(new Vector2Int(3, 3), Vector2Int.one, 99);

            bool placed = _placer.TryPlace(keepPlacing: false);

            Assert.That(placed, Is.False);
            Assert.That(_placer.IsCandidateValid, Is.False);
            Assert.That(_factory.Created, Is.Empty);
        }

        [Test]
        public void TryPlace_FactoryThrows_GivesCellsBackAndRethrows()
        {
            var placer = new BuildingPlacer(_grid, _occupants, _reservations, new ThrowingFactory());
            placer.Begin(_barracks);
            placer.MoveTo(new Vector2Int(2, 2));

            Assert.Throws<InvalidOperationException>(() => placer.TryPlace(keepPlacing: false));
            Assert.That(_grid.IsAreaFree(new Vector2Int(2, 2), _barracks.Footprint), Is.True);
            Assert.That(_occupants.Count, Is.Zero);
        }

        [Test]
        public void TryPlace_Valid_RegistersBuildingUnderTheIdWrittenToItsCells()
        {
            _placer.Begin(_barracks);
            _placer.MoveTo(new Vector2Int(2, 2));

            _placer.TryPlace(keepPlacing: false);

            int id = _grid.GetOccupant(new Vector2Int(4, 3));
            Assert.That(_occupants.TryGet(id, out IBoardOccupant occupant), Is.True);
            Assert.That(occupant.Origin, Is.EqualTo(new Vector2Int(2, 2)));
            Assert.That(occupant.Footprint, Is.EqualTo(_barracks.Footprint));
        }

        [Test]
        public void TryPlace_WithoutCandidate_ReturnsFalse()
        {
            _placer.Begin(_barracks);

            Assert.That(_placer.TryPlace(keepPlacing: false), Is.False);
            Assert.That(_factory.Created, Is.Empty);
        }

        [Test]
        public void TryPlace_WhenNotPlacing_ReturnsFalse()
        {
            Assert.That(_placer.TryPlace(keepPlacing: false), Is.False);
        }

        [Test]
        public void TryPlace_KeepPlacing_StaysInModeAndSameSpotTurnsInvalid()
        {
            _placer.Begin(_barracks);
            _placer.MoveTo(new Vector2Int(2, 2));

            _placer.TryPlace(keepPlacing: true);

            Assert.That(_placer.IsPlacing, Is.True);
            Assert.That(_placer.Definition, Is.SameAs(_barracks));
            Assert.That(_placer.HasCandidate, Is.True);
            Assert.That(_placer.IsCandidateValid, Is.False, "The new building now stands under the ghost.");
        }

        [Test]
        public void TryPlace_KeepPlacing_UnlimitedBuildingsGetDistinctIds()
        {
            _placer.Begin(_powerPlant);
            for (int x = 0; x < Width; x += 2)
            {
                _placer.MoveTo(new Vector2Int(x, 0));
                Assert.That(_placer.TryPlace(keepPlacing: true), Is.True, $"Placing at x = {x}.");
            }

            Assert.That(_factory.Created, Has.Count.EqualTo(Width / 2));
            Assert.That(_grid.GetOccupant(new Vector2Int(0, 0)), Is.EqualTo(1));
            Assert.That(_grid.GetOccupant(new Vector2Int(Width - 1, 1)), Is.EqualTo(Width / 2));
        }

        [Test]
        public void Cancel_MidPlacement_EndsWithoutPlacing()
        {
            _placer.Begin(_barracks);
            _placer.MoveTo(new Vector2Int(2, 2));

            _placer.Cancel();

            Assert.That(_placer.IsPlacing, Is.False);
            Assert.That(_placer.HasCandidate, Is.False);
            Assert.That(_factory.Created, Is.Empty);
            Assert.That(_grid.IsAreaFree(new Vector2Int(2, 2), _barracks.Footprint), Is.True);
        }

        [Test]
        public void Cancel_WhenNotPlacing_DoesNotNotify()
        {
            _placer.Cancel();

            Assert.That(_changedCount, Is.Zero);
        }

        [Test]
        public void Begin_WhilePlacing_SwitchesBuildingAndDropsCandidate()
        {
            _placer.Begin(_barracks);
            _placer.MoveTo(new Vector2Int(2, 2));

            _placer.Begin(_powerPlant);

            Assert.That(_placer.Definition, Is.SameAs(_powerPlant));
            Assert.That(_placer.HasCandidate, Is.False, "The old candidate was computed for the barracks' footprint.");
            Assert.That(_factory.Created, Is.Empty);
        }

        [Test]
        public void ClearCandidate_KeepsPlacingButHidesGhost()
        {
            _placer.Begin(_barracks);
            _placer.MoveTo(new Vector2Int(2, 2));

            _placer.ClearCandidate();

            Assert.That(_placer.IsPlacing, Is.True);
            Assert.That(_placer.HasCandidate, Is.False);
            Assert.That(_placer.TryPlace(keepPlacing: false), Is.False);
        }

        private void PlaceAt(BuildingDefinition definition, Vector2Int origin)
        {
            _placer.Begin(definition);
            _placer.MoveTo(origin);
            Assert.That(_placer.TryPlace(keepPlacing: false), Is.True, "Test setup could not place a building.");
        }

        private static BuildingDefinition Definition(Vector2Int footprint, UnitDefinition producible = null)
        {
            var definition = ScriptableObject.CreateInstance<BuildingDefinition>();
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_footprint").vector2IntValue = footprint;
            if (producible != null)
            {
                SerializedProperty units = serialized.FindProperty("_producibleUnits");
                units.arraySize = 1;
                units.GetArrayElementAtIndex(0).objectReferenceValue = producible;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        /// <summary>A building that is not placed through the placer, e.g. one walling off a spawn side.</summary>
        private void Wall(Vector2Int origin, Vector2Int size)
        {
            int id = _occupants.NextId();
            _grid.Occupy(origin, size, id);
            _occupants.Add(new FakeOccupant(id, origin, size));
        }

        /// <summary>A soldier standing on <paramref name="cell"/>: it holds the grid cell and reserves it as its stop cell.</summary>
        private int StandingSoldier(Vector2Int cell)
        {
            int id = WalkingSoldier(cell);
            _reservations.ReserveStopCell(cell, id);
            return id;
        }

        /// <summary>A soldier walking through <paramref name="cell"/>: it holds the grid cell, its stop cell is elsewhere.</summary>
        private int WalkingSoldier(Vector2Int cell)
        {
            int id = _occupants.NextId();
            _grid.Occupy(cell, Vector2Int.one, id);
            _occupants.Add(new FakeOccupant(id, cell, Vector2Int.one, isMobile: true));
            return id;
        }

        [Test]
        public void MoveTo_ProducerInOpenField_TakesCellBelowBottomMiddle()
        {
            _placer.Begin(_producer);

            _placer.MoveTo(new Vector2Int(3, 3));

            Assert.That(_placer.IsCandidateValid, Is.True);
            Assert.That(_placer.CandidateHasSpawnCell, Is.True);
            Assert.That(_placer.CandidateSpawnCell, Is.EqualTo(new Vector2Int(4, 2)));
        }

        [Test]
        public void MoveTo_ProducerOnBottomEdge_TakesRightSide()
        {
            _placer.Begin(_producer);

            _placer.MoveTo(new Vector2Int(3, 0));

            Assert.That(_placer.CandidateSpawnCell, Is.EqualTo(new Vector2Int(6, 1)));
        }

        [Test]
        public void MoveTo_ProducerInBottomRightCorner_TakesTopSide()
        {
            _placer.Begin(_producer);

            _placer.MoveTo(new Vector2Int(Width - 3, 0));

            Assert.That(_placer.CandidateSpawnCell, Is.EqualTo(new Vector2Int(Width - 2, 3)));
        }

        [Test]
        public void MoveTo_ProducerWithOnlyLeftSideOpen_TakesLeftSide()
        {
            // Top-right corner: bottom side walled off, right and top off the board.
            Wall(new Vector2Int(Width - 2, Height - 4), Vector2Int.one);
            _placer.Begin(_producer);

            _placer.MoveTo(new Vector2Int(Width - 3, Height - 3));

            Assert.That(_placer.CandidateSpawnCell, Is.EqualTo(new Vector2Int(Width - 4, Height - 2)));
        }

        [Test]
        public void MoveTo_ProducerWithNoUsableSide_IsInvalid()
        {
            // Bottom-left corner: bottom and left off the board, right and top walled off.
            Wall(new Vector2Int(3, 1), Vector2Int.one);
            Wall(new Vector2Int(1, 3), Vector2Int.one);
            _placer.Begin(_producer);

            _placer.MoveTo(Vector2Int.zero);

            Assert.That(_placer.IsCandidateValid, Is.False, "A Barracks whose soldiers could never appear must not be placed.");
            Assert.That(_placer.CandidateHasSpawnCell, Is.False);
            Assert.That(_placer.TryPlace(keepPlacing: false), Is.False);
        }

        [Test]
        public void MoveTo_ProducerSpawnSideIsStandingSoldiersCell_SkipsThatSide()
        {
            StandingSoldier(new Vector2Int(4, 2));
            _placer.Begin(_producer);

            _placer.MoveTo(new Vector2Int(3, 3));

            Assert.That(_placer.CandidateSpawnCell, Is.EqualTo(new Vector2Int(6, 4)), "Reserved by a soldier: the right side is next.");
        }

        [Test]
        public void MoveTo_ProducerSpawnSideHasSoldierWalkingThrough_KeepsThatSide()
        {
            // The soldier holds the cell only while passing; it stops elsewhere, so the cell stays usable.
            WalkingSoldier(new Vector2Int(4, 2));
            _placer.Begin(_producer);

            _placer.MoveTo(new Vector2Int(3, 3));

            Assert.That(_placer.CandidateSpawnCell, Is.EqualTo(new Vector2Int(4, 2)));
        }

        [Test]
        public void MoveTo_NonProducer_HasNoSpawnCell()
        {
            _placer.Begin(_powerPlant);

            _placer.MoveTo(new Vector2Int(3, 3));

            Assert.That(_placer.IsCandidateValid, Is.True);
            Assert.That(_placer.CandidateHasSpawnCell, Is.False);
        }

        [Test]
        public void MoveTo_NonProducerAgainstWalls_NeedsNoSpawnSide()
        {
            // The same corner that makes a producer invalid is fine for a building that produces nothing.
            Wall(new Vector2Int(2, 0), Vector2Int.one);
            Wall(new Vector2Int(0, 2), Vector2Int.one);
            _placer.Begin(_powerPlant);

            _placer.MoveTo(Vector2Int.zero);

            Assert.That(_placer.IsCandidateValid, Is.True);
        }

        [Test]
        public void MoveTo_FootprintOverAnotherProducersSpawnCell_IsInvalid()
        {
            PlaceAt(_producer, new Vector2Int(3, 3));
            _placer.Begin(_powerPlant);

            // The power plant (4..5, 1..2) would cover the Barracks' spawn cell (4, 2).
            _placer.MoveTo(new Vector2Int(4, 1));

            Assert.That(_placer.IsCandidateValid, Is.False);
        }

        [Test]
        public void MoveTo_FootprintOverSoldiersStopCell_IsValid()
        {
            // A soldier still walking to an empty cell: building there is allowed, the soldier plans a new stop cell.
            int soldier = _occupants.NextId();
            _occupants.Add(new FakeOccupant(soldier, new Vector2Int(9, 9), Vector2Int.one, isMobile: true));
            _reservations.ReserveStopCell(new Vector2Int(4, 1), soldier);
            _placer.Begin(_powerPlant);

            _placer.MoveTo(new Vector2Int(4, 1));

            Assert.That(_placer.IsCandidateValid, Is.True);
        }

        [Test]
        public void MoveTo_ProducerWhoseSpawnSideIsAnotherProducersSpawnCell_SkipsThatSide()
        {
            // Another Barracks owns (4, 2) as its spawn cell: a permanent reservation, not a soldier's temporary one.
            int otherBarracks = _occupants.NextId();
            _occupants.Add(new FakeOccupant(otherBarracks, new Vector2Int(5, 0), new Vector2Int(3, 1)));
            _reservations.ReserveSpawnCell(new Vector2Int(4, 2), otherBarracks);
            _placer.Begin(_producer);

            _placer.MoveTo(new Vector2Int(3, 3));

            Assert.That(_placer.CandidateSpawnCell, Is.EqualTo(new Vector2Int(6, 4)), "Two buildings never share a spawn cell.");
        }

        [Test]
        public void TryPlace_Producer_ReservesSpawnCellForTheBuildingAndHandsItToTheFactory()
        {
            PlaceAt(_producer, new Vector2Int(3, 3));

            int id = _grid.GetOccupant(new Vector2Int(3, 3));
            Assert.That(_reservations.OwnerOf(new Vector2Int(4, 2)), Is.EqualTo(id));
            Assert.That(_reservations.IsPermanent(new Vector2Int(4, 2)), Is.True, "A spawn cell is held for the building's life.");
            Assert.That(_factory.SpawnCells[0], Is.EqualTo(new Vector2Int(4, 2)));
            Assert.That(_grid.GetOccupant(new Vector2Int(4, 2)), Is.EqualTo(GridMap.EmptyId), "Soldiers still walk through it.");
        }

        [Test]
        public void TryPlace_NonProducer_ReservesNothing()
        {
            PlaceAt(_powerPlant, new Vector2Int(3, 3));

            Assert.That(_reservations.Count, Is.Zero);
            Assert.That(_factory.SpawnCells[0], Is.Null);
        }

        [Test]
        public void TryPlace_SpawnCellReservedWhilePointerStill_TakesNextSide()
        {
            // No grid event: a soldier merely reserves the chosen spawn cell. The click evaluates the spot again.
            _placer.Begin(_producer);
            _placer.MoveTo(new Vector2Int(3, 3));
            int soldier = _occupants.NextId();
            _occupants.Add(new FakeOccupant(soldier, new Vector2Int(9, 7), Vector2Int.one, isMobile: true));
            _reservations.ReserveStopCell(new Vector2Int(4, 2), soldier);

            Assert.That(_placer.TryPlace(keepPlacing: false), Is.True);
            Assert.That(_factory.SpawnCells[0], Is.EqualTo(new Vector2Int(6, 4)));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TryPlace_Producer_NotifiesExactlyOnce(bool keepPlacing)
        {
            // Occupy raises a grid event mid-placement; the placer must not re-evaluate and notify halfway through.
            _placer.Begin(_producer);
            _placer.MoveTo(new Vector2Int(3, 3));
            _changedCount = 0;

            _placer.TryPlace(keepPlacing);

            Assert.That(_changedCount, Is.EqualTo(1));
        }

        [Test]
        public void TryPlace_ProducerFactoryThrows_ReservesNothing()
        {
            var placer = new BuildingPlacer(_grid, _occupants, _reservations, new ThrowingFactory());
            placer.Begin(_producer);
            placer.MoveTo(new Vector2Int(3, 3));

            // The spawn cell is reserved before the scene object is created, so this also proves the reservation rollback.
            Assert.Throws<InvalidOperationException>(() => placer.TryPlace(keepPlacing: false));
            Assert.That(_reservations.Count, Is.Zero);
            Assert.That(_grid.IsAreaFree(new Vector2Int(3, 3), _producer.Footprint), Is.True);
        }

        [Test]
        public void GridChangedOnSpawnSideUnderStillPreview_MovesSpawnCellAndNotifies()
        {
            _placer.Begin(_producer);
            _placer.MoveTo(new Vector2Int(3, 3));
            _changedCount = 0;

            Wall(new Vector2Int(4, 2), Vector2Int.one);

            Assert.That(_placer.CandidateSpawnCell, Is.EqualTo(new Vector2Int(6, 4)));
            Assert.That(_changedCount, Is.EqualTo(1));
        }

        [Test]
        public void GridFreedUnderStillPreview_PreviewTurnsValid()
        {
            int blockerId = _occupants.NextId();
            _grid.Occupy(new Vector2Int(4, 4), Vector2Int.one, blockerId);
            _placer.Begin(_barracks);
            _placer.MoveTo(new Vector2Int(3, 3));
            Assert.That(_placer.IsCandidateValid, Is.False);
            _changedCount = 0;

            // The building under the preview is destroyed while the pointer stands still (R10).
            _grid.Release(new Vector2Int(4, 4), Vector2Int.one, blockerId);

            Assert.That(_placer.IsCandidateValid, Is.True);
            Assert.That(_changedCount, Is.EqualTo(1), "The ghost hears of it and turns green.");
        }

        [Test]
        public void GridChangedElsewhere_DoesNotNotify()
        {
            _placer.Begin(_barracks);
            _placer.MoveTo(new Vector2Int(0, 0));
            _changedCount = 0;

            _grid.Occupy(new Vector2Int(8, 6), Vector2Int.one, _occupants.NextId());

            Assert.That(_placer.IsCandidateValid, Is.True);
            Assert.That(_changedCount, Is.Zero);
        }

        [Test]
        public void GridChangedWhileNotPlacing_IsIgnored()
        {
            Assert.DoesNotThrow(() => _grid.Occupy(new Vector2Int(1, 1), Vector2Int.one, _occupants.NextId()));
            Assert.That(_changedCount, Is.Zero);
        }

        private sealed class ThrowingFactory : IBuildingFactory
        {
            public IBoardOccupant Create(BuildingDefinition definition, Vector2Int origin, int occupantId, Vector2Int? spawnCell)
            {
                throw new InvalidOperationException("Simulated failure while creating the scene object.");
            }

            public void Release(Building building)
            {
            }
        }

        /// <summary>Records what the placer asked to create instead of creating scene objects.</summary>
        private sealed class RecordingFactory : IBuildingFactory
        {
            public List<(BuildingDefinition Definition, Vector2Int Origin, int OccupantId)> Created { get; } =
                new List<(BuildingDefinition, Vector2Int, int)>();

            public List<Vector2Int?> SpawnCells { get; } = new List<Vector2Int?>();

            public IBoardOccupant Create(BuildingDefinition definition, Vector2Int origin, int occupantId, Vector2Int? spawnCell)
            {
                Created.Add((definition, origin, occupantId));
                SpawnCells.Add(spawnCell);
                return new FakeOccupant(occupantId, origin, definition.Footprint);
            }

            public void Release(Building building)
            {
            }
        }
    }
}
