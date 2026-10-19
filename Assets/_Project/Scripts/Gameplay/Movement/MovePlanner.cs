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
    /// <item>An approach stops on the nearest free cell within range of the target, found by one multi-goal A*
    /// search; when none is reachable, on the nearest free cell at all.</item>
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
        private readonly Predicate<Vector2Int> _canStopNearTarget;
        // The target being approached, read by _canStopNearTarget: a cached delegate cannot take it as an argument.
        private IBoardOccupant _approached;

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
            _canStopNearTarget = CanStopNearTarget;
            _pathfinder = new Pathfinder(grid, _isSoldier);
            _stopCellSearch = new FreeCellSearch(grid);
        }

        /// <summary>Cells the last A* search examined; tests use it to check an approach does not flood the board.</summary>
        internal int LastSearchExpandedCount => _pathfinder.ExpandedCount;

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
            _approached = target;
            Predicate<Vector2Int> canStop = target != null ? _canStopNearTarget : _canStopAt;
            bool endsOnStopCell;
            if (target != null)
            {
                // Every cell within range is a goal, and the search ends at the first free one: no board flood
                // towards the target's own (blocked) cells.
                RectInt inRange = GridDistance.WithinRange(target.Origin, target.Footprint, order.Range);
                endsOnStopCell = _pathfinder.TryFindPath(start, inRange, canStop, path);
            }
            else
            {
                // Ends at the goal, or at the reachable cell closest to it when the goal cannot be reached.
                _pathfinder.TryFindPath(start, order.Goal, path);
                endsOnStopCell = CanStopAt(path[path.Count - 1]);
            }

            if (!endsOnStopCell)
            {
                // The BFS spreads only through walkable cells, so the cell it finds is connected to the start: the
                // second search always reaches it. An approach that ends here found no free cell in range at all;
                // combat lets the soldier wait on this cell until one frees up.
                if (_stopCellSearch.TryFindNearest(path[path.Count - 1], _isSoldier, canStop, out Vector2Int free))
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
            order.HasStopCell = canStop(order.Destination);
            _approached = null;
            if (order.HasStopCell)
            {
                _reservations.ReserveStopCell(order.Destination, unit.OccupantId);
            }

            order.NextIndex = FirstWaypoint(path);
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

        private bool CanStopNearTarget(Vector2Int cell)
        {
            // The range area includes the target itself. A building's cells are walls anyway, but a walking soldier
            // reserves its destination, not the cell it stands in: without this check that cell would look free.
            return CanStopAt(cell) && GridDistance.ToArea(cell, _approached.Origin, _approached.Footprint) > 0;
        }

        private bool IsSoldier(int occupantId)
        {
            return _occupants.TryGet(occupantId, out IBoardOccupant occupant) && occupant.IsMobile;
        }
    }
}
