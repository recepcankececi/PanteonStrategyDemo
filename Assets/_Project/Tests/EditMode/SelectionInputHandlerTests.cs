using NUnit.Framework;
using StrategyDemo.Core;
using StrategyDemo.Gameplay;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>
    /// How frames of input drive selection: click selects, ground click and Esc deselect, UI clicks are ignored, and a
    /// drag across cells box-selects soldiers on release.
    /// </summary>
    public class SelectionInputHandlerTests
    {
        private TestBoard _board;
        private GridMap _grid;
        private OccupantRegistry _registry;
        private SelectionService _selection;
        private SelectionInputHandler _handler;
        private FakeOccupant _powerPlant;

        [SetUp]
        public void CreateHandler()
        {
            // Board origin at the world origin: world (x, y) lies in cell (floor x, floor y).
            _board = new TestBoard(Vector3.zero, 10, 8);
            _grid = new GridMap(10, 8);
            _registry = new OccupantRegistry();
            _selection = new SelectionService(_grid, _registry);
            _handler = new SelectionInputHandler(_selection, _board.View);

            _powerPlant = new FakeOccupant(_registry.NextId(), new Vector2Int(4, 3), new Vector2Int(2, 2));
            _grid.Occupy(_powerPlant.Origin, _powerPlant.Footprint, _powerPlant.OccupantId);
            _registry.Add(_powerPlant);
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

            Assert.That(_selection.Selected, Is.SameAs(_powerPlant));
        }

        [Test]
        public void Select_JustAboveFootprint_SelectsNothing()
        {
            // Sprites never draw outside their cells, so the cell under the pointer is the whole hit test.
            var tower = new FakeOccupant(_registry.NextId(), new Vector2Int(1, 1), Vector2Int.one);
            _grid.Occupy(tower.Origin, tower.Footprint, tower.OccupantId);
            _registry.Add(tower);

            _handler.Handle(Frame(new Vector3(1.5f, 2.05f, 0f), select: true));

            Assert.That(_selection.Selected, Is.Null);
        }

        [Test]
        public void Select_OnGround_ClearsSelection()
        {
            _selection.SelectAt(_powerPlant.Origin);

            _handler.Handle(Frame(new Vector3(0.5f, 0.5f, 0f), select: true));

            Assert.That(_selection.Selected, Is.Null);
        }

        [Test]
        public void Select_OverUi_KeepsSelection()
        {
            // E.g. clicking a soldier button in the information panel must not deselect the Barracks behind it.
            _selection.SelectAt(_powerPlant.Origin);

            _handler.Handle(Frame(new Vector3(0.5f, 0.5f, 0f), overUi: true, select: true));

            Assert.That(_selection.Selected, Is.SameAs(_powerPlant));
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
            _selection.SelectAt(_powerPlant.Origin);

            _handler.Handle(Frame(new Vector3(5.5f, 4.5f, 0f), overUi: true, cancel: true));

            Assert.That(_selection.Selected, Is.Null);
        }

        [Test]
        public void Drag_AcrossCells_ShowsBoxAndSelectsSoldiersOnRelease()
        {
            FakeOccupant a = PlaceSoldier(new Vector2Int(0, 0));
            FakeOccupant b = PlaceSoldier(new Vector2Int(2, 1));
            PlaceSoldier(new Vector2Int(8, 7));

            _handler.Handle(Frame(new Vector3(0.5f, 0.5f, 0f), select: true, held: true));
            _handler.Handle(Frame(new Vector3(2.5f, 1.5f, 0f), held: true));

            Assert.That(_handler.IsDragging, Is.True);
            Assert.That(_handler.DragArea, Is.EqualTo(new RectInt(0, 0, 3, 2)));

            _handler.Handle(Frame(new Vector3(2.5f, 1.5f, 0f), released: true));

            Assert.That(_handler.IsDragging, Is.False);
            Assert.That(_selection.All, Is.EquivalentTo(new IBoardOccupant[] { a, b }));
        }

        [Test]
        public void Drag_DownAndLeft_BoxStillSpansBothCorners()
        {
            _handler.Handle(Frame(new Vector3(6.5f, 6.5f, 0f), select: true, held: true));
            _handler.Handle(Frame(new Vector3(2.5f, 1.5f, 0f), held: true));

            Assert.That(_handler.DragArea, Is.EqualTo(new RectInt(2, 1, 5, 6)));
        }

        [Test]
        public void Drag_PastBoardEdge_BoxIsClampedToBoard()
        {
            _handler.Handle(Frame(new Vector3(7.5f, 5.5f, 0f), select: true, held: true));
            _handler.Handle(Frame(new Vector3(30f, 30f, 0f), held: true));

            Assert.That(_handler.DragArea, Is.EqualTo(new RectInt(7, 5, 3, 3)));
        }

        [Test]
        public void PressAndReleaseInOneCell_IsAClickNotABox()
        {
            FakeOccupant soldier = PlaceSoldier(new Vector2Int(1, 1));

            _handler.Handle(Frame(new Vector3(1.2f, 1.2f, 0f), select: true, held: true));
            _handler.Handle(Frame(new Vector3(1.8f, 1.8f, 0f), held: true));
            Assert.That(_handler.IsDragging, Is.False);
            _handler.Handle(Frame(new Vector3(1.8f, 1.8f, 0f), released: true));

            Assert.That(_selection.All, Is.EqualTo(new IBoardOccupant[] { soldier }));
        }

        [Test]
        public void Cancel_WhileDragging_DropsTheBox()
        {
            PlaceSoldier(new Vector2Int(1, 1));
            _handler.Handle(Frame(new Vector3(0.5f, 0.5f, 0f), select: true, held: true));
            _handler.Handle(Frame(new Vector3(3.5f, 3.5f, 0f), held: true));

            _handler.Handle(Frame(new Vector3(3.5f, 3.5f, 0f), cancel: true, held: true));
            _handler.Handle(Frame(new Vector3(3.5f, 3.5f, 0f), released: true));

            Assert.That(_handler.IsDragging, Is.False);
            Assert.That(_selection.All, Is.Empty, "The release after Esc selects nothing.");
        }

        [Test]
        public void PressOverUi_NeverStartsABox()
        {
            _handler.Handle(Frame(new Vector3(0.5f, 0.5f, 0f), overUi: true, select: true, held: true));
            _handler.Handle(Frame(new Vector3(4.5f, 4.5f, 0f), held: true));

            Assert.That(_handler.IsDragging, Is.False);
        }

        private FakeOccupant PlaceSoldier(Vector2Int cell)
        {
            var soldier = new FakeOccupant(_registry.NextId(), cell, Vector2Int.one, isMobile: true);
            _grid.Occupy(cell, Vector2Int.one, soldier.OccupantId);
            _registry.Add(soldier);
            return soldier;
        }

        private static PointerInput Frame(
            Vector3 world, bool overUi = false, bool select = false, bool cancel = false, bool held = false, bool released = false)
        {
            return new PointerInput(world, overUi, select, false, cancel, false, held, released);
        }
    }
}
