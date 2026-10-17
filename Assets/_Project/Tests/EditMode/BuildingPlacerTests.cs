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
        private int _changedCount;

        [SetUp]
        public void CreatePlacer()
        {
            _grid = new GridMap(Width, Height);
            _occupants = new OccupantRegistry();
            _factory = new RecordingFactory();
            _placer = new BuildingPlacer(_grid, _occupants, _factory);
            _placer.Changed += () => _changedCount++;
            _changedCount = 0;
            _barracks = Definition(new Vector2Int(3, 2));
            _powerPlant = Definition(new Vector2Int(2, 2));
        }

        [TearDown]
        public void DestroyDefinitions()
        {
            Object.DestroyImmediate(_barracks);
            Object.DestroyImmediate(_powerPlant);
        }

        [Test]
        public void Constructor_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new BuildingPlacer(null, _occupants, _factory));
            Assert.Throws<ArgumentNullException>(() => new BuildingPlacer(_grid, null, _factory));
            Assert.Throws<ArgumentNullException>(() => new BuildingPlacer(_grid, _occupants, null));
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
            var placer = new BuildingPlacer(_grid, _occupants, new ThrowingFactory());
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

        private static BuildingDefinition Definition(Vector2Int footprint)
        {
            var definition = ScriptableObject.CreateInstance<BuildingDefinition>();
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_footprint").vector2IntValue = footprint;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
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
            public IBoardOccupant Create(BuildingDefinition definition, Vector2Int origin, int occupantId)
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

            public IBoardOccupant Create(BuildingDefinition definition, Vector2Int origin, int occupantId)
            {
                Created.Add((definition, origin, occupantId));
                return new FakeOccupant(occupantId, origin, definition.Footprint);
            }

            public void Release(Building building)
            {
            }
        }
    }
}
