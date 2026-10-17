using System;
using System.Collections.Generic;
using StrategyDemo.Core;
using StrategyDemo.Data;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Moves soldiers along A* paths (R6, A1). Pure C#: <see cref="Tick"/> is driven once per frame by
    /// <see cref="UnitMovementDriver"/> and stepped by hand in EditMode tests.
    /// </summary>
    /// <remarks>
    /// <para>Rules, in the order a move order meets them:</para>
    /// <list type="number">
    /// <item>Only buildings block paths; soldiers walk through each other.</item>
    /// <item>An unreachable goal (e.g. a building's cell, or a walled-off pocket) leads to the reachable cell closest
    /// to it, so right-clicking a building walks the soldier up to it.</item>
    /// <item>Every soldier reserves the cell it will stop on (<see cref="CellReservations"/>); a cell already
    /// reserved is replaced by the nearest free one (BFS), so a group spreads around its target. A Barracks reserves its
    /// spawn cell for good, so soldiers walk through it but never stop on it.</item>
    /// <item>A soldier holds a grid cell only where its centre is: <see cref="Unit.Origin"/> follows the centre (it
    /// changes at the midpoint between two cells), and the grid cell follows when that cell is free. While it walks
    /// through a cell another soldier holds, it holds no cell at all. So every grid cell a soldier holds is one it
    /// stands in, and after each <see cref="Tick"/> and <see cref="Remove"/> every cell a soldier stands in is held
    /// by one of the soldiers in it: a building can never be placed on a soldier.</item>
    /// <item>A building placed on the remaining path or the stop cell triggers a new plan from where the soldier is.</item>
    /// <item>When every reachable cell is reserved (a pocket packed with soldiers), the soldier waits where it is and
    /// plans again every frame until one frees up. Rare, and cheap on this board, so no "something changed" flag.</item>
    /// <item>An approach (<see cref="Approach(Unit, IBoardOccupant)"/>) stops on the first cell of the path within
    /// attack range of the target; when that cell is reserved, on the nearest free cell within range; when no cell in
    /// range is free, on the nearest free cell at all, where combat makes it wait and try again.</item>
    /// </list>
    /// </remarks>
    public sealed class UnitMovement
    {
        private readonly GridMap _grid;
        private readonly OccupantRegistry _occupants;
        private readonly CellReservations _reservations;
        private readonly GridView _gridView;
        private readonly Pathfinder _pathfinder;
        private readonly FreeCellSearch _stopCellSearch;
        // Cached once: converting a method group to a delegate on every call would allocate.
        private readonly Predicate<int> _isSoldier;
        private readonly Predicate<Vector2Int> _canStopAt;
        private readonly Predicate<Vector2Int> _canStopInRange;
        private readonly ObjectPool<MoveOrder> _orderPool;
        private readonly List<MoveOrder> _orders = new List<MoveOrder>();
        // The order being planned, read by _canStopInRange: a cached delegate cannot take it as an argument.
        private MoveOrder _planning;

        /// <param name="grid">Board occupancy; soldiers' cells are moved in it as they walk.</param>
        /// <param name="occupants">Tells soldiers (walkable) from buildings (walls).</param>
        /// <param name="reservations">Stop cells of all soldiers and spawn cells of all Barracks: nobody else stops there.</param>
        /// <param name="gridView">Converts cells to world positions.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public UnitMovement(GridMap grid, OccupantRegistry occupants, CellReservations reservations, GridView gridView)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _occupants = occupants ?? throw new ArgumentNullException(nameof(occupants));
            _reservations = reservations ?? throw new ArgumentNullException(nameof(reservations));
            _gridView = gridView != null ? gridView : throw new ArgumentNullException(nameof(gridView));
            _isSoldier = IsSoldier;
            _canStopAt = CanStopAt;
            _canStopInRange = CanStopInRange;
            _pathfinder = new Pathfinder(grid, _isSoldier);
            _stopCellSearch = new FreeCellSearch(grid);
            int cellCount = grid.Width * grid.Height;
            _orderPool = new ObjectPool<MoveOrder>(() => new MoveOrder(cellCount), null, order => order.Clear());
        }

        /// <summary>Soldiers with an unfinished move order.</summary>
        public int MovingCount => _orders.Count;

        /// <summary>True while <paramref name="unit"/> has an unfinished move order.</summary>
        public bool IsMoving(Unit unit)
        {
            return FindOrder(unit) != null;
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
                    Plan(order);
                }
            }
        }

        /// <summary>
        /// True when a soldier standing on <paramref name="cell"/> could reach a cell it may stop on, by the same search
        /// <see cref="StepAside"/> uses: production asks this before putting a soldier on a spawn cell.
        /// </summary>
        public bool HasStopCellNear(Vector2Int cell)
        {
            return _grid.IsInside(cell) && _stopCellSearch.TryFindNearest(cell, _isSoldier, _canStopAt, out _);
        }

        /// <summary>
        /// Sends <paramref name="unit"/> to the nearest cell it may stop on, e.g. off the spawn cell it was just produced
        /// on: a spawn cell is reserved by its building, so no soldier ever stops there. A soldier already free to stop
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
            // A goal of its own cell: Plan keeps it when the soldier may stop there, else finds the nearest free cell.
            order.Goal = BodyCell(unit);
            Plan(order);
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
                ReleaseHeldCell(order);
                _orders.Remove(order);
                _orderPool.Release(order);
            }
            else if (_grid.IsInside(unit.Origin) && _grid.GetOccupant(unit.Origin) == unit.OccupantId)
            {
                // A standing soldier always holds the cell it stands on.
                _grid.Release(unit.Origin, Vector2Int.one, unit.OccupantId);
            }

            // A soldier walking through the freed cell takes it now, not next frame: no building may land on it between.
            SettleGridCells();
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
                Advance(_orders[i], deltaTime);
            }

            // Grid cells are updated after everyone moved.
            SettleGridCells();

            // Backwards, so removing an order (the last one moves into its slot) skips nobody.
            for (int i = _orders.Count - 1; i >= 0; i--)
            {
                MoveOrder order = _orders[i];
                if (IsComplete(order))
                {
                    _orders[i] = _orders[_orders.Count - 1];
                    _orders.RemoveAt(_orders.Count - 1);
                    _orderPool.Release(order);
                }
            }
        }

        private void SettleGridCells()
        {
            // Again until nothing changes: a cell one soldier lets go of may be the cell another one, earlier in the
            // list, is standing in. Each soldier changes at most twice (let go, take), so this ends after a few passes.
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < _orders.Count; i++)
                {
                    changed |= UpdateGridCell(_orders[i]);
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
            return order;
        }

        private void PlanApproach(Unit unit, IBoardOccupant target)
        {
            MoveOrder order = FindOrder(unit) ?? StartOrder(unit);
            order.Target = target;
            order.Range = unit.Definition.AttackRange;
            Plan(order);
        }

        private MoveOrder FindOrder(Unit unit)
        {
            // A loop over the few walking soldiers; no dictionary to keep in sync with the list.
            for (int i = 0; i < _orders.Count; i++)
            {
                if (ReferenceEquals(_orders[i].Unit, unit))
                {
                    return _orders[i];
                }
            }

            return null;
        }

        /// <summary>Plans the path and stop cell of <paramref name="order"/>. The soldier must hold no reservation.</summary>
        private void Plan(MoveOrder order)
        {
            Unit unit = order.Unit;
            Vector2Int start = BodyCell(unit);
            List<Vector2Int> path = order.Path;
            IBoardOccupant target = order.Target;
            if (target != null)
            {
                // The near side of the target: for a building, the footprint cell closest to the soldier.
                order.Goal = GridDistance.ClosestCellInArea(target.Origin, target.Footprint, start);
            }

            // Ends at the goal, or at the reachable cell closest to it when the goal cannot be reached.
            _pathfinder.TryFindPath(start, order.Goal, path);
            _planning = order;
            bool stopsOnPath = target == null ? CanStopAt(path[path.Count - 1]) : TrimAtFirstStopInRange(path);
            if (!stopsOnPath)
            {
                // The BFS spreads only through walkable cells, so the cell it finds is connected to the start: the
                // second search always reaches it. An approach takes the nearest free cell in range, else the nearest
                // free cell at all; combat lets the soldier wait there until a cell in range frees up.
                Vector2Int end = path[path.Count - 1];
                Predicate<Vector2Int> acceptable = target == null ? _canStopAt : _canStopInRange;
                if (_stopCellSearch.TryFindNearest(end, _isSoldier, acceptable, out Vector2Int free)
                    || (target != null && _stopCellSearch.TryFindNearest(end, _isSoldier, _canStopAt, out free)))
                {
                    _pathfinder.TryFindPath(start, free, path);
                }
                else
                {
                    // Every reachable cell is reserved (a pocket packed with soldiers). Wait here; the order stays
                    // open and plans again every frame until a cell frees up.
                    path.Clear();
                    path.Add(start);
                }
            }

            order.Destination = path[path.Count - 1];
            order.HasStopCell = CanStopAt(order.Destination);
            if (order.HasStopCell)
            {
                _reservations.Reserve(order.Destination, unit.OccupantId);
            }

            order.NextIndex = FirstWaypoint(path);
            _planning = null;
        }

        /// <summary>
        /// Cuts <paramref name="path"/> after its first cell that is within range of the target being planned and free
        /// to stop on, so a ranged soldier stops as soon as it can shoot. False (path unchanged) when there is none.
        /// </summary>
        private bool TrimAtFirstStopInRange(List<Vector2Int> path)
        {
            for (int i = 0; i < path.Count; i++)
            {
                if (CanStopInRange(path[i]))
                {
                    // Removing the tail shifts nothing and allocates nothing.
                    path.RemoveRange(i + 1, path.Count - i - 1);
                    return true;
                }
            }

            return false;
        }

        private static int FirstWaypoint(List<Vector2Int> path)
        {
            // Path[0] is the cell the soldier is in, maybe off its centre (redirected mid-walk), so it heads straight
            // for path[1]. That line stays inside the cells of the step: a side step's two cells, or for a diagonal
            // step the 2×2 block whose side cells the pathfinder keeps free (no corner cutting). Only a one-cell path
            // (stopping where it is) walks back to its own centre.
            return path.Count > 1 ? 1 : 0;
        }

        private void Advance(MoveOrder order, float deltaTime)
        {
            Unit unit = order.Unit;
            Vector3 position = unit.transform.position;

            // A building placed on the way or on the stop cell, or no stop cell yet: plan again from here.
            if (!order.HasStopCell || !_pathfinder.IsWalkable(order.Destination) || !IsNextCellWalkable(order))
            {
                _reservations.Release(unit.OccupantId);
                Plan(order);
            }

            float remaining = unit.Definition.MoveSpeed * GridConfig.CellWorldSize * deltaTime;
            // A loop, not one step: at a low frame rate one frame may pass several cell centres.
            while (!order.HasArrived && remaining > 0f)
            {
                // Checked again at every centre: a long frame must not carry the soldier into a new building.
                if (!IsNextCellWalkable(order))
                {
                    break;
                }

                Vector3 waypoint = _gridView.CellToWorld(order.Path[order.NextIndex]);
                Vector3 toWaypoint = waypoint - position;
                float distance = toWaypoint.magnitude;
                if (distance <= remaining)
                {
                    position = waypoint;
                    remaining -= distance;
                    order.NextIndex++;
                }
                else
                {
                    position += toWaypoint * (remaining / distance);
                    remaining = 0f;
                }

                unit.FaceTowards(toWaypoint);
            }

            unit.transform.position = position;
            unit.SetWalking(!order.HasArrived);
        }

        private bool IsNextCellWalkable(MoveOrder order)
        {
            if (order.HasArrived)
            {
                return true;
            }

            Vector2Int next = order.Path[order.NextIndex];
            if (!_pathfinder.IsWalkable(next))
            {
                return false;
            }

            // Walking back to its own centre never leaves its own cell.
            if (order.NextIndex == 0)
            {
                return true;
            }

            // A diagonal step needs its two side cells open, as when it was planned (no corner cutting): from an
            // off-centre start the line really crosses one of them, so a building placed there since means a new plan.
            Vector2Int from = order.Path[order.NextIndex - 1];
            return from.x == next.x || from.y == next.y
                || (_pathfinder.IsWalkable(new Vector2Int(next.x, from.y)) && _pathfinder.IsWalkable(new Vector2Int(from.x, next.y)));
        }

        /// <summary>Makes the grid follow the soldier's centre. Returns true when the grid changed.</summary>
        private bool UpdateGridCell(MoveOrder order)
        {
            Unit unit = order.Unit;
            Vector2Int body = BodyCell(unit);
            // The centre crossed into another cell, i.e. passed the midpoint between two cell centres.
            if (body != unit.Origin)
            {
                unit.SetCell(body);
            }

            if (order.HoldsCell && order.HeldCell == body)
            {
                return false;
            }

            bool changed = ReleaseHeldCell(order);
            // Another soldier standing in this cell may hold it; then this one holds nothing until it is free.
            if (_grid.GetOccupant(body) == GridMap.EmptyId)
            {
                _grid.Occupy(body, Vector2Int.one, unit.OccupantId);
                order.HoldsCell = true;
                order.HeldCell = body;
                changed = true;
            }

            return changed;
        }

        private bool ReleaseHeldCell(MoveOrder order)
        {
            if (!order.HoldsCell)
            {
                return false;
            }

            _grid.Release(order.HeldCell, Vector2Int.one, order.Unit.OccupantId);
            order.HoldsCell = false;
            return true;
        }

        private static bool IsComplete(MoveOrder order)
        {
            // Standing on its own reserved stop cell and holding it in the grid: from here it is a standing soldier.
            return order.HasArrived && order.HasStopCell && order.HoldsCell && order.HeldCell == order.Destination;
        }

        private Vector2Int BodyCell(Unit unit)
        {
            return _gridView.WorldToCell(unit.transform.position);
        }

        private bool CanStopAt(Vector2Int cell)
        {
            return _pathfinder.IsWalkable(cell) && _reservations.OwnerOf(cell) == GridMap.EmptyId;
        }

        private bool CanStopInRange(Vector2Int cell)
        {
            IBoardOccupant target = _planning.Target;
            return CanStopAt(cell) && GridDistance.ToArea(cell, target.Origin, target.Footprint) <= _planning.Range;
        }

        private bool IsSoldier(int occupantId)
        {
            return _occupants.TryGet(occupantId, out IBoardOccupant occupant) && occupant.IsMobile;
        }
    }
}
