using System;
using System.Collections.Generic;
using UnityEngine;

namespace StrategyDemo.Core
{
    /// <summary>
    /// Cells promised to someone, so that no soldier ever stops where another one will stand. Each owner holds at most
    /// one cell and each cell at most one owner. A reservation is one of two kinds:
    /// <list type="bullet">
    /// <item><b>Temporary</b> (<see cref="ReserveStopCell"/>): the cell a soldier will stop on. A standing soldier
    /// reserves its own cell, a walking one its destination; every new order moves the reservation.</item>
    /// <item><b>Permanent</b> (<see cref="ReserveSpawnCell"/>): a producing building's spawn cell, held for the
    /// building's whole life. No soldier stops on it and no building may be placed on it; it never moves and is let go
    /// only when the building leaves the board.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="GridMap"/> on purpose: the grid says where things <i>are</i> (buildings block paths,
    /// a soldier blocks building placement), reservations say where things <i>will stand</i>. A reserved cell can
    /// still be walked through. Pure C#, tested without a scene.
    /// </remarks>
    public sealed class CellReservations
    {
        // Cell → owner id (row-major, like GridMap), plus the reverse map so an owner's cell is found in O(1).
        private readonly int[] _owners;
        // Cell → whether its reservation is permanent; false for unreserved cells.
        private readonly bool[] _isPermanent;
        private readonly Dictionary<int, Vector2Int> _cellOfOwner = new Dictionary<int, Vector2Int>();
        private readonly int _width;
        private readonly int _height;

        /// <param name="width">Board width in cells.</param>
        /// <param name="height">Board height in cells.</param>
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
            _isPermanent = new bool[width * height];
        }

        /// <summary>Number of owners holding a cell, of either kind.</summary>
        public int Count => _cellOfOwner.Count;

        /// <summary>Owner of <paramref name="cell"/> (either kind), or <see cref="GridMap.EmptyId"/> when nobody reserved it.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="cell"/> is off the board.</exception>
        public int OwnerOf(Vector2Int cell)
        {
            return _owners[ToIndex(cell)];
        }

        /// <summary>
        /// True when <paramref name="cell"/> holds a permanent reservation (a building's spawn cell); false when it is
        /// unreserved or only a soldier's temporary stop cell.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="cell"/> is off the board.</exception>
        public bool IsPermanent(Vector2Int cell)
        {
            return _isPermanent[ToIndex(cell)];
        }

        /// <summary>The cell <paramref name="ownerId"/> holds, of either kind, if any.</summary>
        public bool TryGetCell(int ownerId, out Vector2Int cell)
        {
            return _cellOfOwner.TryGetValue(ownerId, out cell);
        }

        /// <summary>
        /// Temporary reservation: <paramref name="cell"/> is where soldier <paramref name="ownerId"/> will stop. The
        /// stop cell it held before is let go, since an owner holds one cell at most. Reserving the cell it already
        /// holds changes nothing.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="ownerId"/> is not positive or <paramref name="cell"/> is off the board.</exception>
        /// <exception cref="InvalidOperationException">
        /// Another owner holds <paramref name="cell"/>, or <paramref name="ownerId"/> holds a permanent reservation
        /// (a spawn cell never moves).
        /// </exception>
        public void ReserveStopCell(Vector2Int cell, int ownerId)
        {
            int index = CheckReservable(cell, ownerId);
            if (_cellOfOwner.TryGetValue(ownerId, out Vector2Int held) && _isPermanent[ToIndex(held)])
            {
                throw new InvalidOperationException($"Owner {ownerId} holds the permanent reservation {held}; it cannot move.");
            }

            Release(ownerId);
            _owners[index] = ownerId;
            _cellOfOwner.Add(ownerId, cell);
        }

        /// <summary>
        /// Permanent reservation: <paramref name="cell"/> is the spawn cell of building <paramref name="ownerId"/> for
        /// its whole life, until <see cref="Release"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="ownerId"/> is not positive or <paramref name="cell"/> is off the board.</exception>
        /// <exception cref="InvalidOperationException">
        /// Another owner holds <paramref name="cell"/>, or <paramref name="ownerId"/> already holds a cell (a spawn cell
        /// is chosen once, when the building is placed).
        /// </exception>
        public void ReserveSpawnCell(Vector2Int cell, int ownerId)
        {
            int index = CheckReservable(cell, ownerId);
            if (_cellOfOwner.TryGetValue(ownerId, out Vector2Int held))
            {
                throw new InvalidOperationException($"Owner {ownerId} already holds {held}; a spawn cell is reserved once.");
            }

            _owners[index] = ownerId;
            _isPermanent[index] = true;
            _cellOfOwner.Add(ownerId, cell);
        }

        /// <summary>Lets go of the cell <paramref name="ownerId"/> holds, of either kind. Returns false when it held none.</summary>
        public bool Release(int ownerId)
        {
            if (!_cellOfOwner.TryGetValue(ownerId, out Vector2Int cell))
            {
                return false;
            }

            int index = ToIndex(cell);
            _owners[index] = GridMap.EmptyId;
            _isPermanent[index] = false;
            _cellOfOwner.Remove(ownerId);
            return true;
        }

        private int CheckReservable(Vector2Int cell, int ownerId)
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

            return index;
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
