using NUnit.Framework;
using StrategyDemo.Core;
using StrategyDemo.Gameplay;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>How one frame of input drives selection: click selects, ground click and Esc deselect, UI clicks are ignored.</summary>
    public class SelectionInputHandlerTests
    {
        private TestBoard _board;
        private GridMap _grid;
        private OccupantRegistry _registry;
        private SelectionService _selection;
        private SelectionInputHandler _handler;
        private FakeOccupant _house;

        [SetUp]
        public void CreateHandler()
        {
            // Board origin at the world origin: world (x, y) lies in cell (floor x, floor y).
            _board = new TestBoard(Vector3.zero, 10, 8);
            _grid = new GridMap(10, 8);
            _registry = new OccupantRegistry();
            _selection = new SelectionService(_grid, _registry);
            _handler = new SelectionInputHandler(_selection, _board.View);

            _house = new FakeOccupant(_registry.NextId(), new Vector2Int(4, 3), new Vector2Int(2, 2));
            _grid.Occupy(_house.Origin, _house.Footprint, _house.OccupantId);
            _registry.Add(_house);
        }

        [TearDown]
        public void DestroyBoard()
        {
            _board.Dispose();
        }

        [Test]
        public void Constructor_NullArgument_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new SelectionInputHandler(null, _board.View));
            Assert.Throws<System.ArgumentNullException>(() => new SelectionInputHandler(_selection, null));
        }

        [Test]
        public void Select_OnBuilding_SelectsIt()
        {
            _handler.Handle(Frame(new Vector3(5.5f, 4.5f, 0f), select: true));

            Assert.That(_selection.Selected, Is.SameAs(_house));
        }

        [Test]
        public void Select_OnImageAboveFootprint_SelectsIt()
        {
            var tower = new FakeOccupant(_registry.NextId(), new Vector2Int(1, 1), Vector2Int.one, image: new Rect(1f, 1f, 1f, 3f));
            _grid.Occupy(tower.Origin, tower.Footprint, tower.OccupantId);
            _registry.Add(tower);

            _handler.Handle(Frame(new Vector3(1.5f, 3.5f, 0f), select: true));

            Assert.That(_selection.Selected, Is.SameAs(tower));
        }

        [Test]
        public void Select_OnGround_ClearsSelection()
        {
            _selection.SelectAt(_house.Origin);

            _handler.Handle(Frame(new Vector3(0.5f, 0.5f, 0f), select: true));

            Assert.That(_selection.Selected, Is.Null);
        }

        [Test]
        public void Select_OverUi_KeepsSelection()
        {
            // E.g. clicking a soldier button in the information panel must not deselect the Barracks behind it.
            _selection.SelectAt(_house.Origin);

            _handler.Handle(Frame(new Vector3(0.5f, 0.5f, 0f), overUi: true, select: true));

            Assert.That(_selection.Selected, Is.SameAs(_house));
        }

        [Test]
        public void PointerMoveWithoutClick_ChangesNothing()
        {
            _handler.Handle(Frame(new Vector3(5.5f, 4.5f, 0f)));

            Assert.That(_selection.Selected, Is.Null);
        }

        [Test]
        public void Cancel_ClearsSelectionWherever_ThePointerIs()
        {
            _selection.SelectAt(_house.Origin);

            _handler.Handle(Frame(new Vector3(5.5f, 4.5f, 0f), overUi: true, cancel: true));

            Assert.That(_selection.Selected, Is.Null);
        }

        private static PointerInput Frame(Vector3 world, bool overUi = false, bool select = false, bool cancel = false)
        {
            return new PointerInput(world, overUi, select, false, cancel, false);
        }
    }
}
