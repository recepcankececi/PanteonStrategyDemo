using System;
using NUnit.Framework;
using StrategyDemo.Core;
using StrategyDemo.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Right click orders the selected soldiers to the clicked cell (R6); UI clicks and buildings do nothing.</summary>
    public class CommandInputHandlerTests
    {
        private TestBoard _board;
        private GridMap _grid;
        private OccupantRegistry _registry;
        private CellReservations _reservations;
        private SelectionService _selection;
        private UnitMovement _movement;
        private CommandInputHandler _handler;
        private Unit _unit;

        [SetUp]
        public void CreateHandler()
        {
            _board = new TestBoard(Vector3.zero, 10, 8);
            _grid = new GridMap(10, 8);
            _registry = new OccupantRegistry();
            _reservations = new CellReservations(10, 8);
            _selection = new SelectionService(_grid, _registry);
            _movement = new UnitMovement(_grid, _registry, _reservations, _board.View);
            _handler = new CommandInputHandler(_selection, _movement, _board.View);

            _unit = TestBoard.CreateUnitObject("Soldier");
            int id = _registry.NextId();
            _unit.Init(_board.Unit("Soldier"), new Vector2Int(1, 1), id, _board.View.CellToWorld(new Vector2Int(1, 1)));
            _grid.Occupy(new Vector2Int(1, 1), Vector2Int.one, id);
            _registry.Add(_unit);
            _reservations.Reserve(new Vector2Int(1, 1), id);
        }

        [TearDown]
        public void DestroyObjects()
        {
            Object.DestroyImmediate(_unit.gameObject);
            _board.Dispose();
        }

        [Test]
        public void Constructor_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new CommandInputHandler(null, _movement, _board.View));
            Assert.Throws<ArgumentNullException>(() => new CommandInputHandler(_selection, null, _board.View));
            Assert.Throws<ArgumentNullException>(() => new CommandInputHandler(_selection, _movement, null));
        }

        [Test]
        public void Command_SoldierSelected_OrdersItToClickedCell()
        {
            _selection.SelectAt(new Vector2Int(1, 1));

            _handler.Handle(Frame(new Vector3(6.5f, 4.5f, 0f), command: true));

            Assert.That(_movement.IsMoving(_unit), Is.True);
            Assert.That(_reservations.OwnerOf(new Vector2Int(6, 4)), Is.EqualTo(_unit.OccupantId));
        }

        [Test]
        public void Command_OverUi_DoesNothing()
        {
            _selection.SelectAt(new Vector2Int(1, 1));

            _handler.Handle(Frame(new Vector3(6.5f, 4.5f, 0f), overUi: true, command: true));

            Assert.That(_movement.IsMoving(_unit), Is.False);
        }

        [Test]
        public void Command_NothingSelected_DoesNothing()
        {
            _handler.Handle(Frame(new Vector3(6.5f, 4.5f, 0f), command: true));

            Assert.That(_movement.MovingCount, Is.Zero);
        }

        [Test]
        public void PointerMoveWithoutCommand_DoesNothing()
        {
            _selection.SelectAt(new Vector2Int(1, 1));

            _handler.Handle(Frame(new Vector3(6.5f, 4.5f, 0f)));

            Assert.That(_movement.MovingCount, Is.Zero);
        }

        private static PointerInput Frame(Vector3 world, bool overUi = false, bool command = false)
        {
            return new PointerInput(world, overUi, false, command, false, false);
        }
    }
}
