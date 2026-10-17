using System;
using System.Collections.Generic;
using UnityEngine;

namespace StrategyDemo.Core
{
    /// <summary>
    /// Finds the free cell closest to a wanted cell with a breadth-first search (BFS), e.g. where a produced
    /// soldier appears when its building's spawn cell is off the board or taken, or where each soldier of a group
    /// stops around a move target. The caller decides what "free" means.
    /// </summary>
    /// <remarks>
    /// BFS visits cells ring by ring (every cell 1 step away, then every cell 2 steps away, ...), so the first
    /// free cell it meets is one of the nearest in 4-neighbour steps. The search only spreads through occupied
    /// cells the caller allows (e.g. the producing building and soldiers, who can walk away), never through other
    /// buildings: a soldier is never placed in a pocket walled off from its building. Buffers are reused between
    /// searches, so a search allocates nothing once the instance exists.
    /// </remarks>
    public sealed class FreeCellSearch
    {
        // Down first: the spawn cell sits below its building, so a tie prefers staying below it.
        private static readonly Vector2Int[] NeighbourSteps =
        {
            Vector2Int.down, Vector2Int.left, Vector2Int.right, Vector2Int.up
        };

        private readonly GridMap _grid;
        private readonly Queue<Vector2Int> _frontier;
        // One flag per cell, row-major like GridMap; cleared at the start of every search.
        private readonly bool[] _visited;

        /// <param name="grid">The board searched; its size fixes the visited buffer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="grid"/> is null.</exception>
        public FreeCellSearch(GridMap grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _visited = new bool[grid.Width * grid.Height];
            _frontier = new Queue<Vector2Int>(_visited.Length);
        }

        /// <summary>
        /// Finds the free cell nearest to <paramref name="wanted"/>. A wanted cell off the board is first clamped
        /// to the nearest board cell, so a building on the bottom edge still spawns right next to itself.
        /// </summary>
        /// <param name="wanted">Preferred cell; returned as is when it is on the board and free.</param>
        /// <param name="canPassThrough">
        /// Which occupant ids the search may spread through; empty cells are always passable and the start cell is
        /// always expanded. Pass a cached delegate: creating one per call would allocate.
        /// </param>
        /// <param name="isFree">
        /// Whether a cell is an acceptable result, e.g. "empty and not reserved". Cached delegate, as above.
        /// </param>
        /// <param name="found">Nearest free cell; meaningful only when the method returns true.</param>
        /// <returns>False when no free cell can be reached through passable cells (e.g. the board is full).</returns>
        /// <exception cref="ArgumentNullException"><paramref name="canPassThrough"/> or <paramref name="isFree"/> is null.</exception>
        public bool TryFindNearest(Vector2Int wanted, Predicate<int> canPassThrough, Predicate<Vector2Int> isFree, out Vector2Int found)
        {
            if (canPassThrough == null)
            {
                throw new ArgumentNullException(nameof(canPassThrough));
            }

            if (isFree == null)
            {
                throw new ArgumentNullException(nameof(isFree));
            }

            var start = new Vector2Int(
                Mathf.Clamp(wanted.x, 0, _grid.Width - 1),
                Mathf.Clamp(wanted.y, 0, _grid.Height - 1));

            Array.Clear(_visited, 0, _visited.Length);
            _frontier.Clear();
            Visit(start);

            while (_frontier.Count > 0)
            {
                Vector2Int cell = _frontier.Dequeue();
                if (isFree(cell))
                {
                    found = cell;
                    return true;
                }

                int occupant = _grid.GetOccupant(cell);

                // A wall (another building) ends this branch; the start is exempt so a taken spawn cell still spreads.
                // An empty cell that is not free (e.g. reserved) is no wall: the search walks on past it.
                if (cell != start && occupant != GridMap.EmptyId && !canPassThrough(occupant))
                {
                    continue;
                }

                for (int i = 0; i < NeighbourSteps.Length; i++)
                {
                    Vector2Int next = cell + NeighbourSteps[i];
                    if (_grid.IsInside(next) && !_visited[ToIndex(next)])
                    {
                        Visit(next);
                    }
                }
            }

            found = default;
            return false;
        }

        private void Visit(Vector2Int cell)
        {
            // Marked when queued, not when dequeued: otherwise a cell reachable from two neighbours is queued twice.
            _visited[ToIndex(cell)] = true;
            _frontier.Enqueue(cell);
        }

        private int ToIndex(Vector2Int cell)
        {
            return cell.y * _grid.Width + cell.x;
        }
    }
}
