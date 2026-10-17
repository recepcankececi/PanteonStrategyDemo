using System;
using System.Collections.Generic;
using UnityEngine;

namespace StrategyDemo.Core
{
    /// <summary>
    /// Which soldier will stop on which cell (ADR-008). Each owner holds at most one cell and each cell at most one
    /// owner, so two soldiers can never end up standing on the same cell, even while they still walk there.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="GridMap"/> on purpose: the grid says where things <i>are</i> (buildings block paths,
    /// a soldier blocks building placement), reservations say where soldiers <i>will stand</i>. A standing soldier
    /// reserves its own cell; a walking one reserves its destination. Pure C#, tested without a scene.
    /// </remarks>
    public sealed class CellReservations
    {
        // Cell → owner id (row-major, like GridMap), plus the reverse map so an owner's cell is found in O(1).
        private readonly int[] _owners;
        private readonly Dictionary<int, Vector2Int> _cellOfOwner = new Dictionary<int, Vector2Int>();
        private readonly int _width;
        private readonly int _height;

        /// <exception cref="ArgumentOutOfRangeException">Width or height is not positive.</exception>
        public CellReservations(int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");
            }

            _width = width;
            _height = height;
            _owners = new int[width * height];
        }

        /// <summary>Number of owners holding a cell.</summary>
        public int Count => _cellOfOwner.Count;

        /// <summary>Owner of <paramref name="cell"/>, or <see cref="GridMap.EmptyId"/> when nobody reserved it.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="cell"/> is off the board.</exception>
        public int OwnerOf(Vector2Int cell)
        {
            return _owners[ToIndex(cell)];
        }

        /// <summary>The cell <paramref name="ownerId"/> holds, if any.</summary>
        public bool TryGetCell(int ownerId, out Vector2Int cell)
        {
            return _cellOfOwner.TryGetValue(ownerId, out cell);
        }

        /// <summary>
        /// Gives <paramref name="cell"/> to <paramref name="ownerId"/>; a cell the owner held before is let go, since
        /// an owner holds one cell at most. Reserving the cell it already holds changes nothing.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="ownerId"/> is not positive or <paramref name="cell"/> is off the board.</exception>
        /// <exception cref="InvalidOperationException">Another owner holds <paramref name="cell"/>.</exception>
        public void Reserve(Vector2Int cell, int ownerId)
        {
            if (ownerId <= GridMap.EmptyId)
            {
                throw new ArgumentOutOfRangeException(nameof(ownerId), ownerId, "Owner id must be positive.");
            }

            int index = ToIndex(cell);
            int current = _owners[index];
            // Callers search for an unreserved cell first; a clash here is a logic bug, so fail loudly.
            if (current != GridMap.EmptyId && current != ownerId)
            {
                throw new InvalidOperationException($"Cell {cell} is already reserved by {current}.");
            }

            Release(ownerId);
            _owners[index] = ownerId;
            _cellOfOwner.Add(ownerId, cell);
        }

        /// <summary>Lets go of the cell <paramref name="ownerId"/> holds. Returns false when it held none.</summary>
        public bool Release(int ownerId)
        {
            if (!_cellOfOwner.TryGetValue(ownerId, out Vector2Int cell))
            {
                return false;
            }

            _owners[ToIndex(cell)] = GridMap.EmptyId;
            _cellOfOwner.Remove(ownerId);
            return true;
        }

        private int ToIndex(Vector2Int cell)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x >= _width || cell.y >= _height)
            {
                throw new ArgumentOutOfRangeException(nameof(cell), cell, "Cell is outside the board.");
            }

            return cell.y * _width + cell.x;
        }
    }
}
