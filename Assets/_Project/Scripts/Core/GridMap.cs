using System;
using UnityEngine;

namespace StrategyDemo.Core
{
    /// <summary>
    /// Occupancy of the board: which cell is covered by which object. Pure C#, so placement,
    /// pathfinding and combat share one source of truth that is testable without a scene.
    /// </summary>
    public sealed class GridMap
    {
        /// <summary>Occupant id stored in a free cell.</summary>
        public const int EmptyId = 0;

        // Row-major flat array (index = y * Width + x): one memory block, cheap to index and clear.
        private readonly int[] _occupantIds;

        /// <summary>Creates an empty grid.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Width or height is not positive.</exception>
        public GridMap(int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "Grid width must be positive.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "Grid height must be positive.");
            }

            Width = width;
            Height = height;
            _occupantIds = new int[width * height];
        }

        /// <summary>Raised after cells are occupied or released, with the affected area.</summary>
        public event Action<RectInt> AreaChanged;

        /// <summary>Number of columns.</summary>
        public int Width { get; }

        /// <summary>Number of rows.</summary>
        public int Height { get; }

        /// <summary>True when <paramref name="cell"/> lies on the board.</summary>
        public bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
        }

        /// <summary>True when the whole area lies on the board. Empty or negative sizes are never inside.</summary>
        public bool IsAreaInside(Vector2Int origin, Vector2Int size)
        {
            return size.x > 0 && size.y > 0 && IsInside(origin) && IsInside(origin + size - Vector2Int.one);
        }

        /// <summary>True when the whole area lies on the board and no cell in it is occupied.</summary>
        public bool IsAreaFree(Vector2Int origin, Vector2Int size)
        {
            if (!IsAreaInside(origin, size))
            {
                return false;
            }

            for (int y = origin.y; y < origin.y + size.y; y++)
            {
                for (int x = origin.x; x < origin.x + size.x; x++)
                {
                    if (_occupantIds[ToIndex(x, y)] != EmptyId)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Id of the object covering <paramref name="cell"/>, or <see cref="EmptyId"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="cell"/> is off the board.</exception>
        public int GetOccupant(Vector2Int cell)
        {
            if (!IsInside(cell))
            {
                throw new ArgumentOutOfRangeException(nameof(cell), cell, "Cell is outside the grid.");
            }

            return _occupantIds[ToIndex(cell.x, cell.y)];
        }

        /// <summary>Marks the area as covered by <paramref name="occupantId"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="occupantId"/> is not positive.</exception>
        /// <exception cref="InvalidOperationException">The area is off the board or already occupied.</exception>
        public void Occupy(Vector2Int origin, Vector2Int size, int occupantId)
        {
            if (occupantId <= EmptyId)
            {
                throw new ArgumentOutOfRangeException(nameof(occupantId), occupantId, "Occupant id must be positive.");
            }

            // Callers are expected to check IsAreaFree first; reaching here means a logic bug, so fail loudly.
            if (!IsAreaFree(origin, size))
            {
                throw new InvalidOperationException($"Area at {origin} of size {size} is off the grid or already occupied.");
            }

            Fill(origin, size, occupantId);
        }

        /// <summary>Frees the area occupied by <paramref name="occupantId"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The area is not fully on the board.</exception>
        /// <exception cref="InvalidOperationException">A cell in the area is not held by <paramref name="occupantId"/>.</exception>
        public void Release(Vector2Int origin, Vector2Int size, int occupantId)
        {
            if (!IsAreaInside(origin, size))
            {
                throw new ArgumentOutOfRangeException(nameof(origin), origin, $"Area of size {size} is not fully on the grid.");
            }

            // A wrong origin or size would silently free a neighbour's cells; check ownership of every cell first.
            for (int y = origin.y; y < origin.y + size.y; y++)
            {
                for (int x = origin.x; x < origin.x + size.x; x++)
                {
                    if (_occupantIds[ToIndex(x, y)] != occupantId)
                    {
                        throw new InvalidOperationException($"Cell ({x},{y}) is not held by occupant {occupantId}.");
                    }
                }
            }

            Fill(origin, size, EmptyId);
        }

        private void Fill(Vector2Int origin, Vector2Int size, int occupantId)
        {
            for (int y = origin.y; y < origin.y + size.y; y++)
            {
                for (int x = origin.x; x < origin.x + size.x; x++)
                {
                    _occupantIds[ToIndex(x, y)] = occupantId;
                }
            }

            AreaChanged?.Invoke(new RectInt(origin, size));
        }

        private int ToIndex(int x, int y)
        {
            return y * Width + x;
        }
    }
}
