using System;
using System.Collections.Generic;
using StrategyDemo.Core;
using UnityEngine;

namespace StrategyDemo.Gameplay.Board
{
    /// <summary>
    /// Hands out occupant ids and maps an id back to its building or soldier. The grid stores only ids (cheap,
    /// engine-free); this is the "id → object" half, so a clicked cell can be turned into the object on it.
    /// </summary>
    /// <remarks>
    /// Ids are handed out before the object exists because the grid is occupied first (the grid is the source of
    /// truth; see <see cref="StrategyDemo.Gameplay.Placement.BuildingPlacer.TryPlace"/>). Ids are never reused, so a stale id can never point at a
    /// newer object that took over the same number.
    /// </remarks>
    public sealed class OccupantRegistry
    {
        private readonly Dictionary<int, IBoardOccupant> _occupants = new Dictionary<int, IBoardOccupant>();
        // Last id handed out; 0 is GridMap.EmptyId, so the first id is 1.
        private int _lastId = GridMap.EmptyId;

        /// <summary>Number of registered occupants.</summary>
        public int Count => _occupants.Count;

        /// <summary>Returns a new, never used id for an occupant about to be created.</summary>
        public int NextId()
        {
            return ++_lastId;
        }

        /// <summary>Registers <paramref name="occupant"/> under its <see cref="IBoardOccupant.OccupantId"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="occupant"/> is null.</exception>
        /// <exception cref="ArgumentException">The id was not handed out by <see cref="NextId"/> or is already registered.</exception>
        public void Add(IBoardOccupant occupant)
        {
            if (occupant == null)
            {
                throw new ArgumentNullException(nameof(occupant));
            }

            int id = occupant.OccupantId;
            if (id <= GridMap.EmptyId || id > _lastId)
            {
                throw new ArgumentException($"Occupant id {id} was not handed out by this registry.", nameof(occupant));
            }

            if (_occupants.ContainsKey(id))
            {
                throw new ArgumentException($"Occupant id {id} is already registered.", nameof(occupant));
            }

            _occupants.Add(id, occupant);
        }

        /// <summary>Forgets the occupant with <paramref name="occupantId"/>. Returns false when none was registered.</summary>
        public bool Remove(int occupantId)
        {
            return _occupants.Remove(occupantId);
        }

        /// <summary>
        /// Every registered occupant. Typed as the dictionary's own value collection so a <c>foreach</c> over it uses a
        /// struct enumerator and allocates nothing; it is a read-only view.
        /// </summary>
        public Dictionary<int, IBoardOccupant>.ValueCollection All => _occupants.Values;

        /// <summary>Looks up the occupant registered under <paramref name="occupantId"/>.</summary>
        public bool TryGet(int occupantId, out IBoardOccupant occupant)
        {
            return _occupants.TryGetValue(occupantId, out occupant);
        }

        /// <summary>
        /// Looks up whatever holds <paramref name="cell"/> in <paramref name="grid"/>: cell → id (grid), id → object
        /// (here). False for a cell off the board or an empty one.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="grid"/> is null.</exception>
        public bool TryGetAt(GridMap grid, Vector2Int cell, out IBoardOccupant occupant)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            occupant = null;
            if (!grid.IsInside(cell))
            {
                return false;
            }

            int id = grid.GetOccupant(cell);
            // An id without a registered object would be a bookkeeping bug; treat it as empty ground.
            return id != GridMap.EmptyId && _occupants.TryGetValue(id, out occupant);
        }
    }
}
