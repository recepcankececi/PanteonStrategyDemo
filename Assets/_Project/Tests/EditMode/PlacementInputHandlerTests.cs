using NUnit.Framework;
using StrategyDemo.Core;
using StrategyDemo.Data;
using StrategyDemo.Gameplay;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>
    /// How one frame of input drives placement (ADR-001 Karar 6): follow, place, keep placing with Shift,
    /// cancel with right click or Esc, and never act on clicks over the UI.
    /// </summary>
    public class PlacementInputHandlerTests
    {
        private TestBoard _board;
        private GridMap _grid;
        private BuildingPlacer _placer;
        private PlacementInputHandler _handler;
        private BuildingDefinition _powerPlant;

        [SetUp]
        public void CreateHandler()
        {
            // Board origin at the world origin: world (x, y) lies in cell (floor x, floor y).
            _board = new TestBoard(Vector3.zero, 10, 8);
            _powerPlant = _board.Definition("PowerPlant", new Vector2Int(2, 2));
            _grid = new GridMap(10, 8);
            _placer = new BuildingPlacer(_grid, new OccupantRegistry(), new NullBuildingFactory());
            _handler = new PlacementInputHandler(_placer, _board.View);
        }

        [TearDown]
        public void DestroyBoard()
        {
            _board.Dispose();
        }

        [Test]
        public void Constructor_NullArgument_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new PlacementInputHandler(null, _board.View));
            Assert.Throws<System.ArgumentNullException>(() => new PlacementInputHandler(_placer, null));
        }

        [Test]
        public void PointerOnBoard_MovesCandidateSoCursorIsAtFootprintCentre()
        {
            _placer.Begin(_powerPlant);

            // A 2×2 area centred near (5.1, 4.2) has its centre on the cell corner (5, 4), so it starts at (4, 3).
            _handler.Handle(Frame(new Vector3(5.1f, 4.2f, 0f)));

            Assert.That(_placer.CandidateOrigin, Is.EqualTo(new Vector2Int(4, 3)));
        }

        [Test]
        public void Select_OnValidSpot_PlacesAndEndsPlacement()
        {
            _placer.Begin(_powerPlant);

            _handler.Handle(Frame(new Vector3(5f, 4f, 0f), select: true));

            Assert.That(_grid.GetOccupant(new Vector2Int(4, 3)), Is.Not.EqualTo(GridMap.EmptyId));
            Assert.That(_placer.IsPlacing, Is.False);
        }

        [Test]
        public void Select_WithModifierHeld_PlacesAndKeepsPlacing()
        {
            _placer.Begin(_powerPlant);

            _handler.Handle(Frame(new Vector3(5f, 4f, 0f), select: true, modifier: true));

            Assert.That(_grid.GetOccupant(new Vector2Int(4, 3)), Is.Not.EqualTo(GridMap.EmptyId));
            Assert.That(_placer.IsPlacing, Is.True);
        }

        [Test]
        public void Select_OverUi_NeverPlacesAndHidesGhost()
        {
            _placer.Begin(_powerPlant);
            _handler.Handle(Frame(new Vector3(5f, 4f, 0f)));

            _handler.Handle(Frame(new Vector3(5f, 4f, 0f), overUi: true, select: true));

            Assert.That(_grid.IsAreaFree(new Vector2Int(4, 3), _powerPlant.Footprint), Is.True);
            Assert.That(_placer.IsPlacing, Is.True, "A click on the UI does not end placement.");
            Assert.That(_placer.HasCandidate, Is.False);
        }

        [Test]
        public void Select_OffBoard_PlacesNothing()
        {
            _placer.Begin(_powerPlant);

            _handler.Handle(Frame(new Vector3(-3f, 4f, 0f), select: true));

            Assert.That(_placer.IsPlacing, Is.True);
            Assert.That(_placer.IsCandidateValid, Is.False);
        }

        [Test]
        public void Command_OnBoard_CancelsPlacement()
        {
            _placer.Begin(_powerPlant);

            _handler.Handle(Frame(new Vector3(5f, 4f, 0f), command: true));

            Assert.That(_placer.IsPlacing, Is.False);
        }

        [Test]
        public void Command_OverUi_IsLeftToTheUi()
        {
            _placer.Begin(_powerPlant);

            _handler.Handle(Frame(new Vector3(5f, 4f, 0f), overUi: true, command: true));

            Assert.That(_placer.IsPlacing, Is.True);
        }

        [Test]
        public void Cancel_EvenOverUi_CancelsPlacement()
        {
            _placer.Begin(_powerPlant);

            _handler.Handle(Frame(new Vector3(5f, 4f, 0f), overUi: true, cancel: true));

            Assert.That(_placer.IsPlacing, Is.False);
        }

        [Test]
        public void Cancel_WithSelectInSameFrame_PlacesNothing()
        {
            _placer.Begin(_powerPlant);

            _handler.Handle(Frame(new Vector3(5f, 4f, 0f), select: true, cancel: true));

            Assert.That(_grid.IsAreaFree(new Vector2Int(4, 3), _powerPlant.Footprint), Is.True);
        }

        [Test]
        public void NotPlacing_InputIsIgnoredAndNotConsumed()
        {
            bool consumed = _handler.Handle(Frame(new Vector3(5f, 4f, 0f), select: true));

            Assert.That(consumed, Is.False, "Selection must get the click when nothing is being placed.");
            Assert.That(_placer.IsPlacing, Is.False);
            Assert.That(_grid.IsAreaFree(Vector2Int.zero, new Vector2Int(10, 8)), Is.True);
        }

        [Test]
        public void Placing_ClickThatPlaces_IsConsumed()
        {
            _placer.Begin(_powerPlant);

            bool consumed = _handler.Handle(Frame(new Vector3(5f, 4f, 0f), select: true));

            Assert.That(consumed, Is.True, "The placing click must not also select the new building.");
        }

        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, true)]
        [TestCase(false, false, false)]
        public void Placing_AnyFrame_IsConsumed(bool overUi, bool command, bool cancel)
        {
            _placer.Begin(_powerPlant);

            bool consumed = _handler.Handle(Frame(new Vector3(5f, 4f, 0f), overUi: overUi, command: command, cancel: cancel));

            Assert.That(consumed, Is.True);
        }

        private static PointerInput Frame(Vector3 world, bool overUi = false, bool select = false, bool command = false, bool cancel = false, bool modifier = false)
        {
            return new PointerInput(world, overUi, select, command, cancel, modifier);
        }
    }
}
