using System;
using System.Collections.Generic;
using StrategyDemo.Core;
using StrategyDemo.Data;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Produces soldiers from a building (R3, R5, R6, R7): instantly and without limit, at the building's spawn
    /// cell, or at the nearest free cell when that one is off the board or taken. Pure C#, tested without a scene.
    /// </summary>
    /// <remarks>
    /// The fallback search never crosses another building, so a soldier is never put in a pocket walled off from its
    /// producer (it would be stuck there once movement arrives in M5). Soldiers hold their cell in the <see cref="GridMap"/> like buildings do, so two soldiers never stand on one
    /// cell, a building can never be placed on a soldier, and selection finds a soldier the same way it finds a
    /// building.
    /// </remarks>
    public sealed class UnitProducer
    {
        private readonly GridMap _grid;
        private readonly OccupantRegistry _occupants;
        private readonly IUnitFactory _factory;
        private readonly FreeCellSearch _freeCellSearch;
        // Cached once: a method group converted to a delegate on every call would allocate.
        private readonly Predicate<int> _canSpawnSearchPassThrough;
        // Grid id of the building producing right now; the spawn search may spread through its own cells.
        private int _producerId;

        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public UnitProducer(GridMap grid, OccupantRegistry occupants, IUnitFactory factory)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _occupants = occupants ?? throw new ArgumentNullException(nameof(occupants));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _freeCellSearch = new FreeCellSearch(grid);
            _canSpawnSearchPassThrough = CanSpawnSearchPassThrough;
        }

        /// <summary>
        /// Wanted spawn cell of a building whose bottom-left cell is <paramref name="producerOrigin"/>; may be off the board.
        /// </summary>
        public static Vector2Int SpawnCellOf(BuildingDefinition producer, Vector2Int producerOrigin)
        {
            if (producer == null)
            {
                throw new ArgumentNullException(nameof(producer));
            }

            return producerOrigin + producer.SpawnOffset;
        }

        /// <summary>Produces one <paramref name="unit"/> from the building <paramref name="producer"/> standing at <paramref name="producerOrigin"/>.</summary>
        /// <param name="producedAt">Cell the soldier stands on; meaningful only when the method returns true.</param>
        /// <returns>False when the board has no free cell left; nothing changes then.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="producer"/> or <paramref name="unit"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="producer"/> cannot produce <paramref name="unit"/> (R5).</exception>
        /// <remarks>If the factory throws, the cell is given back and the exception is passed on.</remarks>
        public bool TryProduce(BuildingDefinition producer, Vector2Int producerOrigin, UnitDefinition unit, out Vector2Int producedAt)
        {
            if (producer == null)
            {
                throw new ArgumentNullException(nameof(producer));
            }

            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            // The UI only offers what the building lists, so this guards the rule itself (R5: Power Plant produces nothing).
            if (!Contains(producer.ProducibleUnits, unit))
            {
                throw new ArgumentException($"'{producer.name}' cannot produce '{unit.name}'.", nameof(unit));
            }

            // The producer's cells are passable so a taken or off-board spawn cell still leads around the building.
            _producerId = _grid.IsInside(producerOrigin) ? _grid.GetOccupant(producerOrigin) : GridMap.EmptyId;
            if (!_freeCellSearch.TryFindNearest(SpawnCellOf(producer, producerOrigin), _canSpawnSearchPassThrough, out producedAt))
            {
                return false;
            }

            int occupantId = _occupants.NextId();
            // Grid first, as with buildings: if the soldier cannot be created, give the cell back.
            _grid.Occupy(producedAt, Vector2Int.one, occupantId);
            try
            {
                _occupants.Add(_factory.Create(unit, producedAt, occupantId));
            }
            catch
            {
                _grid.Release(producedAt, Vector2Int.one, occupantId);
                throw;
            }

            return true;
        }

        private bool CanSpawnSearchPassThrough(int occupantId)
        {
            // The producer itself, or a soldier who can walk aside; any other building is a wall.
            return occupantId == _producerId
                || (_occupants.TryGet(occupantId, out IBoardOccupant occupant) && occupant.IsMobile);
        }

        private static bool Contains(IReadOnlyList<UnitDefinition> units, UnitDefinition unit)
        {
            // A loop, not LINQ: no enumerator or delegate is allocated per click.
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] == unit)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
