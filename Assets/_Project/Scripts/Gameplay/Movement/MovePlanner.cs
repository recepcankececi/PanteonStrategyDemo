using System;
using System.Collections.Generic;
using StrategyDemo.Core;
using StrategyDemo.Gameplay.Board;
using StrategyDemo.Gameplay.Units;
using UnityEngine;

namespace StrategyDemo.Gameplay.Movement
{
    /// <summary>
    /// Plans a <see cref="MoveOrder"/>: its A* path, the cell the soldier will stop on and that cell's reservation.
    /// Part of <see cref="UnitMovement"/>, which decides when to plan; this class only knows how.
    /// </summary>
    /// <remarks>
    /// <para>The stop-cell rules (see <see cref="UnitMovement"/> for the whole list):</para>
    /// <list type="bullet">
    /// <item>A soldier may stop on a walkable cell nobody has reserved: not another soldier's stop cell, not a
    /// Barracks' spawn cell.</item>
    /// <item>A plain move stops at the end of its path when it may, else on the nearest cell it may stop on (BFS).</item>
    /// <item>An approach stops on the first cell of its path within range of the target; else on the nearest free cell
    /// in range; else on the nearest free cell at all.</item>
    /// </list>
    /// </remarks>
    internal sealed class MovePlanner
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
        // The order being planned, read by _canStopInRange: a cached delegate cannot take it as an argument.
        private MoveOrder _planning;

        /// <param name="grid">Board occupancy: buildings are walls.</param>
        /// <param name="occupants">Tells soldiers (walkable) from buildings (walls).</param>
        /// <param name="reservations">Stop and spawn cells; a planned stop cell is reserved here.</param>
        /// <param name="gridView">Converts a soldier's position to its cell.</param>
        /// <remarks>Arguments are checked by <see cref="UnitMovement"/>, the only creator.</remarks>
        public MovePlanner(GridMap grid, OccupantRegistry occupants, CellReservations reservations, GridView gridView)
        {
            _grid = grid;
            _occupants = occupants;
            _reservations = reservations;
            _gridView = gridView;
            _isSoldier = IsSoldier;
            _canStopAt = CanStopAt;
            _canStopInRange = CanStopInRange;
            _pathfinder = new Pathfinder(grid, _isSoldier);
            _stopCellSearch = new FreeCellSearch(grid);
        }

        /// <summary>True when a soldier may walk onto <paramref name="cell"/>: inside the board and no building there.</summary>
        public bool IsWalkable(Vector2Int cell)
        {
            return _pathfinder.IsWalkable(cell);
        }

        /// <summary>True when a soldier standing on <paramref name="cell"/> could reach a cell it may stop on.</summary>
        public bool HasStopCellNear(Vector2Int cell)
        {
            return _grid.IsInside(cell) && _stopCellSearch.TryFindNearest(cell, _isSoldier, _canStopAt, out _);
        }

        /// <summary>
        /// Gives back the stop cell <paramref name="order"/>'s soldier reserved and plans again from where it is, e.g.
        /// when a building was placed on its path.
        /// </summary>
        public void Replan(MoveOrder order)
        {
            _reservations.Release(order.Unit.OccupantId);
            Plan(order);
        }

        /// <summary>
        /// Plans the path and stop cell of <paramref name="order"/>. The soldier must hold no reservation: callers
        /// release it first (a group releases all of them before planning any, so its soldiers may swap cells).
        /// </summary>
        public void Plan(MoveOrder order)
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
                _reservations.ReserveStopCell(order.Destination, unit.OccupantId);
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

        /// <summary>The cell the soldier's centre is in; where a plan starts.</summary>
        public Vector2Int BodyCell(Unit unit)
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
