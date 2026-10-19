using System;
using System.Collections.Generic;
using StrategyDemo.Core;
using StrategyDemo.Gameplay.Board;
using StrategyDemo.Gameplay.Units;
using UnityEngine;

namespace StrategyDemo.Gameplay.Movement
{
    /// <summary>
    /// Moves soldiers along A* paths (R6, A1). Pure C#: <see cref="Tick"/> is driven once per frame by
    /// <see cref="UnitMovementDriver"/> and stepped by hand in EditMode tests.
    /// </summary>
    /// <remarks>
    /// <para>This class keeps the move orders and decides when to plan; three helpers do the work:
    /// <see cref="MovePlanner"/> (path and stop cell), <see cref="MoveStepper"/> (walking one frame) and
    /// <see cref="HeldCellTracker"/> (the grid cell a walking soldier holds).</para>
    /// <para>Rules, in the order a move order meets them:</para>
    /// <list type="number">
    /// <item>Only buildings block paths; soldiers walk through each other.</item>
    /// <item>An unreachable goal (e.g. a building's cell, or a walled-off pocket) leads to the reachable cell closest
    /// to it, so right-clicking a building walks the soldier up to it.</item>
    /// <item>Every soldier holds a temporary reservation on the cell it will stop on
    /// (<see cref="CellReservations.ReserveStopCell"/>); a cell already reserved is replaced by the nearest free one
    /// (BFS), so a group spreads around its target. A Barracks holds a permanent reservation on its spawn cell
    /// (<see cref="CellReservations.ReserveSpawnCell"/>), so soldiers walk through it but never stop on it.</item>
    /// <item>A soldier holds a grid cell only where its centre is (<see cref="HeldCellTracker"/> has the details), so
    /// after each <see cref="Tick"/> and <see cref="Remove"/> every cell a soldier stands in is held by one of the
    /// soldiers in it: a building can never be placed on a soldier.</item>
    /// <item>A building placed on the remaining path or the stop cell triggers a new plan from where the soldier is.</item>
    /// <item>When every reachable cell is reserved (a pocket packed with soldiers), the soldier waits where it is and
    /// plans again every frame until one frees up. Rare, and cheap on this board, so no "something changed" flag. Its
    /// order stays open, but it is not walking (<see cref="IsWalking"/>), so it may still fire at a target in range.</item>
    /// <item>An approach (<see cref="Approach(Unit, IBoardOccupant)"/>) stops on the nearest free cell within attack
    /// range of the target (one multi-goal A* search); when no cell in range is free, on the nearest free cell at all,
    /// where combat makes it wait and try again.</item>
    /// </list>
    /// </remarks>
    public sealed class UnitMovement
    {
        private readonly GridMap _grid;
        private readonly CellReservations _reservations;
        private readonly MovePlanner _planner;
        private readonly MoveStepper _stepper;
        private readonly HeldCellTracker _heldCells;
        private readonly ObjectPool<MoveOrder> _orderPool;
        // The list is the update order (a stable, allocation-free loop); the dictionary finds a soldier's order in O(1),
        // so per-frame questions like IsMoving do not scan the list. Both always hold the same orders.
        private readonly List<MoveOrder> _orders = new List<MoveOrder>();
        private readonly Dictionary<int, MoveOrder> _orderByUnitId;

        /// <param name="grid">Board occupancy; soldiers' cells are moved in it as they walk.</param>
        /// <param name="occupants">Tells soldiers (walkable) from buildings (walls).</param>
        /// <param name="reservations">Temporary stop cells of all soldiers and permanent spawn cells of all Barracks: nobody else stops there.</param>
        /// <param name="gridView">Converts cells to world positions.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public UnitMovement(GridMap grid, OccupantRegistry occupants, CellReservations reservations, GridView gridView)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (occupants == null)
            {
                throw new ArgumentNullException(nameof(occupants));
            }

            _reservations = reservations ?? throw new ArgumentNullException(nameof(reservations));
            if (gridView == null)
            {
                throw new ArgumentNullException(nameof(gridView));
            }

            _planner = new MovePlanner(grid, occupants, reservations, gridView);
            _stepper = new MoveStepper(_planner, gridView);
            _heldCells = new HeldCellTracker(grid, gridView);
            int cellCount = grid.Width * grid.Height;
            _orderPool = new ObjectPool<MoveOrder>(() => new MoveOrder(cellCount), null, order => order.Clear());
            // At most one soldier per cell: sized once, so adding an order never grows (allocates) the table.
            _orderByUnitId = new Dictionary<int, MoveOrder>(cellCount);
        }

        /// <summary>Soldiers with an unfinished move order.</summary>
        public int MovingCount => _orders.Count;

        /// <summary>Cells the last path search examined; lets tests check that an approach does not flood the board.</summary>
        internal int LastSearchExpandedCount => _planner.LastSearchExpandedCount;

        /// <summary>True while <paramref name="unit"/> has an unfinished move order.</summary>
        public bool IsMoving(Unit unit)
        {
            return FindOrder(unit) != null;
        }

        /// <summary>
        /// True while <paramref name="unit"/> is on its way to the end of its path. False once it stands there, even
        /// when its order stays open (it waits for a free stop cell, or for another soldier to leave its cell).
        /// </summary>
        public bool IsWalking(Unit unit)
        {
            MoveOrder order = FindOrder(unit);
            return order != null && !order.HasArrived;
        }

        /// <summary>
        /// Sends every soldier in <paramref name="occupants"/> towards <paramref name="target"/>; each gets its own
        /// stop cell. Buildings in the list are skipped, and a target off the board is ignored. A soldier already
        /// walking is redirected from where it is.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="occupants"/> is null.</exception>
        public void MoveGroup(IReadOnlyList<IBoardOccupant> occupants, Vector2Int target)
        {
            if (occupants == null)
            {
                throw new ArgumentNullException(nameof(occupants));
            }

            if (!_grid.IsInside(target))
            {
                return;
            }

            // Let go of every old stop cell first, so soldiers of the group may take each other's former cells.
            for (int i = 0; i < occupants.Count; i++)
            {
                if (occupants[i] is Unit unit)
                {
                    _reservations.Release(unit.OccupantId);
                }
            }

            for (int i = 0; i < occupants.Count; i++)
            {
                if (occupants[i] is Unit unit)
                {
                    MoveOrder order = FindOrder(unit) ?? StartOrder(unit);
                    order.Target = null;
                    order.Goal = target;
                    _planner.Plan(order);
                }
            }
        }

        /// <summary>
        /// True when a soldier standing on <paramref name="cell"/> could reach a cell it may stop on, by the same search
        /// <see cref="StepAside"/> uses: production asks this before putting a soldier on a spawn cell.
        /// </summary>
        public bool HasStopCellNear(Vector2Int cell)
        {
            return _planner.HasStopCellNear(cell);
        }

        /// <summary>
        /// Sends <paramref name="unit"/> to the nearest cell it may stop on, e.g. off the spawn cell it was just produced
        /// on: a spawn cell is its building's permanent reservation, so no soldier ever stops there. A soldier already free to stop
        /// where it stands stays there.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="unit"/> is null.</exception>
        public void StepAside(Unit unit)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            _reservations.Release(unit.OccupantId);
            MoveOrder order = FindOrder(unit) ?? StartOrder(unit);
            order.Target = null;
            // A goal of its own cell: the planner keeps it when the soldier may stop there, else finds the nearest free cell.
            order.Goal = _planner.BodyCell(unit);
            _planner.Plan(order);
        }

        /// <summary>
        /// Sends every soldier in <paramref name="units"/> to a cell within its attack range of
        /// <paramref name="target"/>, each to its own cell.
        /// </summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public void Approach(IReadOnlyList<Unit> units, IBoardOccupant target)
        {
            if (units == null)
            {
                throw new ArgumentNullException(nameof(units));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            // As in MoveGroup: let go of every old stop cell first, so the group may take each other's former cells.
            for (int i = 0; i < units.Count; i++)
            {
                _reservations.Release(units[i].OccupantId);
            }

            for (int i = 0; i < units.Count; i++)
            {
                PlanApproach(units[i], target);
            }
        }

        /// <summary>
        /// Sends <paramref name="unit"/> to a cell within its attack range of <paramref name="target"/>. Combat calls
        /// this again while a target walks away, or while the soldier waits for a free cell in range.
        /// </summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public void Approach(Unit unit, IBoardOccupant target)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            _reservations.Release(unit.OccupantId);
            PlanApproach(unit, target);
        }

        /// <summary>
        /// Forgets <paramref name="target"/> (it was destroyed): soldiers approaching it walk on to the stop cell they
        /// already reserved and stand there, as after a plain move.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null.</exception>
        public void ForgetTarget(IBoardOccupant target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            for (int i = 0; i < _orders.Count; i++)
            {
                MoveOrder order = _orders[i];
                if (ReferenceEquals(order.Target, target))
                {
                    order.Target = null;
                    // A later plan (a building placed on the way) heads back to the same cell.
                    order.Goal = order.Destination;
                }
            }
        }

        /// <summary>
        /// Takes <paramref name="unit"/> off the board for good (e.g. it died): drops its move order and gives back
        /// its stop-cell reservation and the grid cell it holds. Does nothing for a soldier that holds nothing.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="unit"/> is null.</exception>
        public void Remove(Unit unit)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            _reservations.Release(unit.OccupantId);
            MoveOrder order = FindOrder(unit);
            if (order != null)
            {
                _heldCells.ReleaseHeldCell(order);
                // A linear search, but once per death, not per frame.
                RemoveOrderAt(_orders.IndexOf(order));
            }
            else if (_grid.IsInside(unit.Origin) && _grid.GetOccupant(unit.Origin) == unit.OccupantId)
            {
                // A standing soldier always holds the cell it stands on.
                _grid.Release(unit.Origin, Vector2Int.one, unit.OccupantId);
            }

            // A soldier walking through the freed cell takes it now, not next frame: no building may land on it between.
            _heldCells.Settle(_orders);
        }

        /// <summary>Advances every walking soldier by <paramref name="deltaTime"/> seconds.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="deltaTime"/> is negative.</exception>
        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "Time cannot run backwards.");
            }

            for (int i = 0; i < _orders.Count; i++)
            {
                _stepper.Advance(_orders[i], deltaTime);
            }

            _heldCells.Settle(_orders);

            // Backwards, so removing an order (the last one moves into its slot) skips nobody.
            for (int i = _orders.Count - 1; i >= 0; i--)
            {
                if (IsComplete(_orders[i]))
                {
                    RemoveOrderAt(i);
                }
            }
        }

        private MoveOrder StartOrder(Unit unit)
        {
            MoveOrder order = _orderPool.Get();
            order.Unit = unit;
            // A soldier without an order holds the cell it stands on, unless it was just produced onto a spawn cell
            // another soldier is still walking out of; then it holds nothing until that one has left.
            order.HoldsCell = _grid.GetOccupant(unit.Origin) == unit.OccupantId;
            order.HeldCell = unit.Origin;
            _orders.Add(order);
            _orderByUnitId.Add(unit.OccupantId, order);
            return order;
        }

        private void RemoveOrderAt(int index)
        {
            // Swap-remove: the order of the list does not matter, and nothing behind the slot has to shift.
            MoveOrder order = _orders[index];
            _orders[index] = _orders[_orders.Count - 1];
            _orders.RemoveAt(_orders.Count - 1);
            _orderByUnitId.Remove(order.Unit.OccupantId);
            _orderPool.Release(order);
        }

        private void PlanApproach(Unit unit, IBoardOccupant target)
        {
            MoveOrder order = FindOrder(unit) ?? StartOrder(unit);
            order.Target = target;
            order.Range = unit.Definition.AttackRange;
            _planner.Plan(order);
        }

        private MoveOrder FindOrder(Unit unit)
        {
            // Ids are never reused, and an order is dropped before its soldier goes back to the pool (id 0): the id alone
            // identifies the soldier.
            return _orderByUnitId.TryGetValue(unit.OccupantId, out MoveOrder order) ? order : null;
        }

        private static bool IsComplete(MoveOrder order)
        {
            // Standing on its own reserved stop cell and holding it in the grid: from here it is a standing soldier.
            return order.HasArrived && order.HasStopCell && order.HoldsCell && order.HeldCell == order.Destination;
        }
    }
}
