using System;
using System.Collections.Generic;
using UnityEngine;

namespace StrategyDemo.Core
{
    /// <summary>
    /// Shortest paths on the <see cref="GridMap"/> with A* (A1, R6): 8 directions, integer costs,
    /// octile heuristic, no corner cutting. When the goal cannot be reached, the path leads to the reachable cell
    /// closest to it instead.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A* expands cells in order of <c>f = g + h</c>: <c>g</c> is the known cost from the start, <c>h</c> an estimate of
    /// the cost still to go. The octile estimate never overestimates the true 8-direction cost, so the first time the
    /// goal is taken from the open list its path is a shortest one.
    /// </para>
    /// <para>
    /// No allocation per search: every per-cell array is created once. Instead of clearing them before each search,
    /// a cell's data counts only when its stamp equals the current search id; starting a search is one increment.
    /// </para>
    /// </remarks>
    public sealed class Pathfinder
    {
        /// <summary>Cost of a horizontal or vertical step.</summary>
        public const int StraightStepCost = 10;

        /// <summary>Cost of a diagonal step: √2 ≈ 1.4 times a straight one, kept integer for exact, fast comparisons.</summary>
        public const int DiagonalStepCost = 14;

        private const int NoParent = -1;

        // Straight steps first: on equal cost the search then prefers them, which keeps paths visually calm.
        private static readonly Vector2Int[] Steps =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left,
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 1),
        };

        private readonly GridMap _grid;
        private readonly Predicate<int> _canWalkThrough;
        private readonly PathNodeHeap _open;
        // Per cell (row-major, like GridMap): best known cost from the start and the cell it was reached from.
        private readonly int[] _costFromStart;
        private readonly int[] _parent;
        // Search id at which the cell was first reached / taken off the open list (closed).
        private readonly int[] _reachedStamp;
        private readonly int[] _closedStamp;
        private int _searchId;

        /// <param name="grid">Board whose occupancy decides where one can walk.</param>
        /// <param name="canWalkThrough">
        /// Which occupant ids do not block a path (soldiers walk through each other; buildings block). Empty cells are
        /// always walkable. Pass a cached delegate: it is called for every occupied neighbour.
        /// </param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public Pathfinder(GridMap grid, Predicate<int> canWalkThrough)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _canWalkThrough = canWalkThrough ?? throw new ArgumentNullException(nameof(canWalkThrough));
            int cellCount = grid.Width * grid.Height;
            _open = new PathNodeHeap(cellCount);
            _costFromStart = new int[cellCount];
            _parent = new int[cellCount];
            _reachedStamp = new int[cellCount];
            _closedStamp = new int[cellCount];
        }

        /// <summary>
        /// Octile distance in step costs: the cost of the shortest 8-direction path on an empty board.
        /// Take as many diagonal steps as the shorter axis needs, then go straight.
        /// </summary>
        public static int Heuristic(Vector2Int from, Vector2Int to)
        {
            int dx = Mathf.Abs(from.x - to.x);
            int dy = Mathf.Abs(from.y - to.y);
            return StraightStepCost * Mathf.Max(dx, dy) + (DiagonalStepCost - StraightStepCost) * Mathf.Min(dx, dy);
        }

        /// <summary>True when <paramref name="cell"/> is on the board and a soldier may walk on it.</summary>
        public bool IsWalkable(Vector2Int cell)
        {
            if (!_grid.IsInside(cell))
            {
                return false;
            }

            int occupant = _grid.GetOccupant(cell);
            return occupant == GridMap.EmptyId || _canWalkThrough(occupant);
        }

        /// <summary>
        /// Finds a shortest path from <paramref name="start"/> to <paramref name="goal"/> and writes it into
        /// <paramref name="path"/>, start and end cells included.
        /// </summary>
        /// <param name="start">Where the walker stands. Not checked for walkability: the walker is already there.</param>
        /// <param name="goal">Wanted end cell; may be blocked (e.g. a building's cell).</param>
        /// <param name="path">Caller-owned list, cleared and refilled, so repeated searches allocate nothing.</param>
        /// <returns>
        /// True when the path ends at <paramref name="goal"/>. False when the goal cannot be reached: the path then ends
        /// at the reachable cell closest to the goal (smallest heuristic, then cheapest), and is just
        /// <paramref name="start"/> when no cell is closer than the start itself.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="start"/> or <paramref name="goal"/> is off the board.</exception>
        public bool TryFindPath(Vector2Int start, Vector2Int goal, List<Vector2Int> path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (!_grid.IsInside(start))
            {
                throw new ArgumentOutOfRangeException(nameof(start), start, "Start is outside the grid.");
            }

            if (!_grid.IsInside(goal))
            {
                throw new ArgumentOutOfRangeException(nameof(goal), goal, "Goal is outside the grid.");
            }

            BeginSearch();
            int startNode = ToNode(start);
            int goalNode = ToNode(goal);
            int startH = Heuristic(start, goal);
            Reach(startNode, 0, NoParent);
            _open.Push(startNode, startH, startH);

            // Fallback for an unreachable goal: the closed cell nearest to it.
            int closestNode = startNode;
            int closestH = startH;

            while (_open.Count > 0)
            {
                int node = _open.Pop();
                _closedStamp[node] = _searchId;
                if (node == goalNode)
                {
                    WritePath(node, path);
                    return true;
                }

                Vector2Int cell = ToCell(node);
                int h = Heuristic(cell, goal);
                if (h < closestH || (h == closestH && _costFromStart[node] < _costFromStart[closestNode]))
                {
                    closestNode = node;
                    closestH = h;
                }

                ExpandNeighbours(node, cell, goal);
            }

            // Open list exhausted: every cell connected to the start was examined and none was the goal.
            WritePath(closestNode, path);
            return false;
        }

        private void ExpandNeighbours(int node, Vector2Int cell, Vector2Int goal)
        {
            for (int i = 0; i < Steps.Length; i++)
            {
                Vector2Int step = Steps[i];
                Vector2Int next = cell + step;
                if (!IsWalkable(next))
                {
                    continue;
                }

                bool isDiagonal = step.x != 0 && step.y != 0;
                // No corner cutting: a diagonal step needs both side cells open, or the soldier would brush through
                // a building's corner.
                if (isDiagonal && (!IsWalkable(new Vector2Int(next.x, cell.y)) || !IsWalkable(new Vector2Int(cell.x, next.y))))
                {
                    continue;
                }

                int nextNode = ToNode(next);
                // With a consistent heuristic a closed cell already has its cheapest cost; it is never reopened.
                if (_closedStamp[nextNode] == _searchId)
                {
                    continue;
                }

                int cost = _costFromStart[node] + (isDiagonal ? DiagonalStepCost : StraightStepCost);
                if (_reachedStamp[nextNode] != _searchId)
                {
                    int h = Heuristic(next, goal);
                    Reach(nextNode, cost, node);
                    _open.Push(nextNode, cost + h, h);
                }
                else if (cost < _costFromStart[nextNode])
                {
                    // Reached before, but this way is cheaper: keep it open with the better cost.
                    Reach(nextNode, cost, node);
                    _open.DecreaseF(nextNode, cost + Heuristic(next, goal));
                }
            }
        }

        private void BeginSearch()
        {
            // After ~2 billion searches the id would wrap and old stamps could look current: clear them once instead.
            if (_searchId == int.MaxValue)
            {
                Array.Clear(_reachedStamp, 0, _reachedStamp.Length);
                Array.Clear(_closedStamp, 0, _closedStamp.Length);
                _searchId = 0;
            }

            _searchId++;
            _open.Clear();
        }

        private void Reach(int node, int costFromStart, int parent)
        {
            _reachedStamp[node] = _searchId;
            _costFromStart[node] = costFromStart;
            _parent[node] = parent;
        }

        private void WritePath(int endNode, List<Vector2Int> path)
        {
            // Parents lead from the end back to the start; collect that way, then flip in place (no allocation).
            path.Clear();
            for (int node = endNode; node != NoParent; node = _parent[node])
            {
                path.Add(ToCell(node));
            }

            path.Reverse();
        }

        private int ToNode(Vector2Int cell)
        {
            return cell.y * _grid.Width + cell.x;
        }

        private Vector2Int ToCell(int node)
        {
            return new Vector2Int(node % _grid.Width, node / _grid.Width);
        }
    }
}
