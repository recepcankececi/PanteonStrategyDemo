using System;
using System.Collections.Generic;
using StrategyDemo.Core;
using StrategyDemo.Data;
using StrategyDemo.Gameplay.Board;
using StrategyDemo.Gameplay.Buildings;
using StrategyDemo.Gameplay.Movement;
using StrategyDemo.Gameplay.Placement;
using StrategyDemo.Gameplay.Units;
using UnityEngine;

namespace StrategyDemo.Gameplay.Production
{
    /// <summary>
    /// Produces soldiers from a building (R3, R5, R6, R7): instantly and without limit, always on the building's spawn
    /// cell, from where the new soldier walks to the nearest free cell so the spawn cell clears for the next one.
    /// Pure C# apart from the soldier object itself, tested without a scene.
    /// </summary>
    /// <remarks>
    /// The spawn cell is chosen when the building is placed (<see cref="BuildingPlacer"/>) and reserved by it for life,
    /// so no soldier ever stops there and no building is ever placed on it. Production is refused while no soldier on
    /// the spawn cell could reach a free cell (the board is full, or the Barracks is walled in): the soldier would have
    /// nowhere to stand. Soldiers hold their cell in the <see cref="GridMap"/> like buildings do, so a building can never
    /// be placed on a soldier and selection finds a soldier the same way it finds a building.
    /// </remarks>
    public sealed class UnitProducer
    {
        private readonly GridMap _grid;
        private readonly OccupantRegistry _occupants;
        private readonly CellReservations _reservations;
        private readonly IUnitFactory _factory;
        private readonly UnitMovement _movement;

        /// <param name="grid">Board occupancy; the new soldier occupies the spawn cell when it is free.</param>
        /// <param name="occupants">Registry that hands out ids and records the soldier.</param>
        /// <param name="reservations">Stop and spawn cells; read only for <see cref="AvailabilityVersion"/>.</param>
        /// <param name="factory">Creates (or reuses) the soldier object.</param>
        /// <param name="movement">Walks the new soldier off the spawn cell.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public UnitProducer(GridMap grid, OccupantRegistry occupants, CellReservations reservations, IUnitFactory factory,
            UnitMovement movement)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _occupants = occupants ?? throw new ArgumentNullException(nameof(occupants));
            _reservations = reservations ?? throw new ArgumentNullException(nameof(reservations));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
        }

        /// <summary>
        /// Changes whenever something <see cref="CanProduce"/> reads may have changed: grid occupancy or reservations.
        /// A view caches <see cref="CanProduce"/> and asks again only when this differs from the value it saw.
        /// </summary>
        /// <remarks>
        /// The sum of two counters that only go up: any change to either changes it. The registry needs no counter:
        /// <see cref="CanProduce"/> reads it only for ids found on the grid, so a registry change matters only together
        /// with a grid change.
        /// </remarks>
        public int AvailabilityVersion => unchecked(_grid.Version + _reservations.Version);

        /// <summary>
        /// True when <paramref name="producer"/> can produce a soldier now: it has a spawn cell and a soldier there could
        /// walk to a free cell. The information panel disables its production buttons while this is false.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="producer"/> is null.</exception>
        public bool CanProduce(Building producer)
        {
            if (producer == null)
            {
                throw new ArgumentNullException(nameof(producer));
            }

            return producer.HasSpawnCell && _movement.HasStopCellNear(producer.SpawnCell);
        }

        /// <summary>Produces one <paramref name="unit"/> on the spawn cell of <paramref name="producer"/>.</summary>
        /// <param name="produced">The new soldier; null when the method returns false.</param>
        /// <returns>False when <see cref="CanProduce"/> is false; nothing changes then.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="producer"/> or <paramref name="unit"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="producer"/> cannot produce <paramref name="unit"/> (R5).</exception>
        /// <remarks>If the factory throws, the cell is given back and the exception is passed on.</remarks>
        public bool TryProduce(Building producer, UnitDefinition unit, out Unit produced)
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
            if (!Contains(producer.Definition.ProducibleUnits, unit))
            {
                throw new ArgumentException($"'{producer.Definition.name}' cannot produce '{unit.name}'.", nameof(unit));
            }

            produced = null;
            if (!CanProduce(producer))
            {
                return false;
            }

            Vector2Int spawnCell = producer.SpawnCell;
            int occupantId = _occupants.NextId();
            // The previous soldier may still be walking out of the spawn cell. Then the new one holds no grid cell until
            // that one has left (movement takes the cell for it), exactly like two soldiers passing through one cell.
            bool holdsCell = _grid.GetOccupant(spawnCell) == GridMap.EmptyId;
            if (holdsCell)
            {
                // Grid first, as with buildings: if the soldier cannot be created, give the cell back. Occupy raises
                // AreaChanged before the soldier is registered; listeners see an unknown id on a cell that is already
                // the Barracks' reservation, so nothing can be placed there either way.
                _grid.Occupy(spawnCell, Vector2Int.one, occupantId);
            }

            try
            {
                produced = _factory.Create(unit, spawnCell, occupantId);
                _occupants.Add(produced);
            }
            catch
            {
                if (holdsCell)
                {
                    _grid.Release(spawnCell, Vector2Int.one, occupantId);
                }

                produced = null;
                throw;
            }

            // The spawn cell is the building's reservation, so the nearest cell the soldier may stop on is elsewhere.
            _movement.StepAside(produced);
            return true;
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
