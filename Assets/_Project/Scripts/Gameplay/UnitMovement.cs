using System;
using System.Collections.Generic;
using StrategyDemo.Core;
using StrategyDemo.Data;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Moves soldiers along A* paths (R6, A1; ADR-008). Pure C#: <see cref="Tick"/> is driven once per frame by
    /// <see cref="UnitMovementDriver"/> and stepped by hand in EditMode tests.
    /// </summary>
    /// <remarks>
    /// <para>Rules, in the order a move order meets them:</para>
    /// <list type="number">
    /// <item>Only buildings block paths; soldiers walk through each other (ADR-001 Karar 11).</item>
    /// <item>An unreachable goal (e.g. a building's cell, or a walled-off pocket) leads to the reachable cell closest
    /// to it, so right-clicking a building walks the soldier up to it.</item>
    /// <item>Every soldier reserves the cell it will stop on (<see cref="CellReservations"/>); a cell already
    /// reserved is replaced by the nearest free one (BFS), so a group spreads around its target.</item>
    /// <item>A soldier holds a grid cell only where its centre is: <see cref="Unit.Origin"/> follows the centre (it
    /// changes at the midpoint between two cells), and the grid cell follows when that cell is free. While it walks
    /// through a cell another soldier holds, it holds no cell at all. So every grid cell a soldier holds is one it
    /// stands in, and after each <see cref="Tick"/> and <see cref="Remove"/> every cell a soldier stands in is held
    /// by one of the soldiers in it: a building can never be placed on a soldier.</item>
    /// <item>A building placed on the remaining path or the stop cell triggers a new plan from where the soldier is.</item>
    /// <item>When every reachable cell is reserved (a pocket packed with soldiers), the soldier waits where it is and
    /// plans again every frame until one frees up. Rare, and cheap on this board, so no "something changed" flag.</item>
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
        private readonly ObjectPool<MoveOrder> _orderPool;
        private readonly List<MoveOrder> _orders = new List<MoveOrder>();

        /// <param name="grid">Board occupancy; soldiers' cells are moved in it as they walk.</param>
        /// <param name="occupants">Tells soldiers (walkable) from buildings (walls).</param>
        /// <param name="reservations">Stop cells of all soldiers.</param>
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
                    order.Goal = target;
                    Plan(order);
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
            // A soldier without an order stands still and holds the cell it stands on.
            order.HoldsCell = true;
            order.HeldCell = unit.Origin;
            _orders.Add(order);
            return order;
        }

        private MoveOrder FindOrder(Unit unit)
        {
            // A loop over the few walking soldiers; no dictionary to keep in sync with the list.
            for (int i = 0; i < _orders.Count; i++)
            {
                if (_orders[i].Unit == unit)
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

            // Ends at the goal, or at the reachable cell closest to it when the goal cannot be reached.
            _pathfinder.TryFindPath(start, order.Goal, path);
            Vector2Int end = path[path.Count - 1];
            if (!CanStopAt(end))
            {
                // The BFS spreads only through walkable cells, so the cell it finds is connected to the start: the
                // second search always reaches it.
                if (_stopCellSearch.TryFindNearest(end, _isSoldier, _canStopAt, out Vector2Int free))
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
        }

        private static int FirstWaypoint(List<Vector2Int> path)
        {
            // Path[0] is the cell the soldier is in, maybe off its centre (redirected mid-walk). A straight line from
            // anywhere in a cell to a side neighbour's centre stays inside those two cells, so it heads there directly;
            // a line to a diagonal neighbour could clip a building's corner, so it first returns to its own centre.
            if (path.Count > 1)
            {
                Vector2Int step = path[1] - path[0];
                if (step.x == 0 || step.y == 0)
                {
                    return 1;
                }
            }

            return 0;
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
            return order.HasArrived || _pathfinder.IsWalkable(order.Path[order.NextIndex]);
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

        private bool IsSoldier(int occupantId)
        {
            return _occupants.TryGet(occupantId, out IBoardOccupant occupant) && occupant.IsMobile;
        }
    }
}
