using System;
using NUnit.Framework;
using StrategyDemo.Core;
using StrategyDemo.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>
    /// Right click orders the selected soldiers to attack what stands on the clicked cell (R9) or to walk to it when it is
    /// empty (R6); UI clicks do nothing.
    /// </summary>
    public class CommandInputHandlerTests
    {
        private TestBoard _board;
        private GridMap _grid;
        private OccupantRegistry _registry;
        private CellReservations _reservations;
        private SelectionService _selection;
        private UnitMovement _movement;
        private UnitCombat _combat;
        private CommandInputHandler _handler;
        private Unit _unit;
        private Building _tower;

        [SetUp]
        public void CreateHandler()
        {
            _board = new TestBoard(Vector3.zero, 10, 8);
            _grid = new GridMap(10, 8);
            _registry = new OccupantRegistry();
            _reservations = new CellReservations(10, 8);
            _selection = new SelectionService(_grid, _registry);
            _movement = new UnitMovement(_grid, _registry, _reservations, _board.View);
            var despawner = new OccupantDespawner(_grid, _registry, _movement, _selection, new NullUnitFactory(), new NullBuildingFactory());
            _combat = new UnitCombat(_movement, new FreeForAllAttackRule(), despawner);
            _handler = new CommandInputHandler(_selection, _movement, _combat, _grid, _registry, _board.View);

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
            if (_tower != null)
            {
                Object.DestroyImmediate(_tower.gameObject);
            }

            _board.Dispose();
        }

        [Test]
        public void Constructor_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new CommandInputHandler(null, _movement, _combat, _grid, _registry, _board.View));
            Assert.Throws<ArgumentNullException>(() => new CommandInputHandler(_selection, null, _combat, _grid, _registry, _board.View));
            Assert.Throws<ArgumentNullException>(() => new CommandInputHandler(_selection, _movement, null, _grid, _registry, _board.View));
            Assert.Throws<ArgumentNullException>(() => new CommandInputHandler(_selection, _movement, _combat, null, _registry, _board.View));
            Assert.Throws<ArgumentNullException>(() => new CommandInputHandler(_selection, _movement, _combat, _grid, null, _board.View));
            Assert.Throws<ArgumentNullException>(() => new CommandInputHandler(_selection, _movement, _combat, _grid, _registry, null));
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

        [Test]
        public void Command_OnBuilding_AttacksIt()
        {
            Building tower = PlaceTower(new Vector2Int(6, 4));
            _selection.SelectAt(new Vector2Int(1, 1));

            _handler.Handle(Frame(new Vector3(6.5f, 4.5f, 0f), command: true));

            Assert.That(_combat.StateOf(_unit), Is.EqualTo(CombatState.Chasing));
            Assert.That(_combat.TargetOf(_unit), Is.SameAs(tower));
        }

        [Test]
        public void Command_OnEmptyCellWhileAttacking_CancelsAttackAndMoves()
        {
            PlaceTower(new Vector2Int(6, 4));
            _selection.SelectAt(new Vector2Int(1, 1));
            _handler.Handle(Frame(new Vector3(6.5f, 4.5f, 0f), command: true));

            _handler.Handle(Frame(new Vector3(0.5f, 6.5f, 0f), command: true));

            Assert.That(_combat.StateOf(_unit), Is.EqualTo(CombatState.Idle));
            Assert.That(_reservations.OwnerOf(new Vector2Int(0, 6)), Is.EqualTo(_unit.OccupantId));
        }

        [Test]
        public void Command_OnTheOnlySelectedSoldierItself_IsAMoveThatCancelsItsAttack()
        {
            PlaceTower(new Vector2Int(6, 4));
            _selection.SelectAt(new Vector2Int(1, 1));
            _handler.Handle(Frame(new Vector3(6.5f, 4.5f, 0f), command: true));

            // It may not attack itself, so the click falls through to a move onto its own cell.
            _handler.Handle(Frame(new Vector3(1.5f, 1.5f, 0f), command: true));

            Assert.That(_combat.AttackerCount, Is.Zero);
            Assert.That(_reservations.OwnerOf(new Vector2Int(1, 1)), Is.EqualTo(_unit.OccupantId));
        }

        private Building PlaceTower(Vector2Int cell)
        {
            _tower = TestBoard.CreateBuildingObject("Tower");
            int id = _registry.NextId();
            _tower.Init(_board.Definition("Tower", Vector2Int.one), cell, id, _board.View.CellToWorld(cell));
            _grid.Occupy(cell, Vector2Int.one, id);
            _registry.Add(_tower);
            return _tower;
        }

        private static PointerInput Frame(Vector3 world, bool overUi = false, bool command = false)
        {
            return new PointerInput(world, overUi, false, command, false, false);
        }
    }
}
