using System;
using StrategyDemo.Core;
using StrategyDemo.Gameplay.Movement;

namespace StrategyDemo.Gameplay.Despawn
{
    /// <summary>
    /// The board services a despawned object gives its cells back to (<see cref="IDespawnable.LeaveBoard"/>). A class,
    /// not a parameter list: a future occupant type that needs another service adds a property here, and the existing
    /// types and the interface stay as they are.
    /// </summary>
    public sealed class DespawnContext
    {
        /// <param name="grid">Board occupancy the leaving object frees its cells in.</param>
        /// <param name="reservations">Reserved cells; a leaving building gives back its spawn cell here.</param>
        /// <param name="movement">Movement system a leaving soldier is removed from.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public DespawnContext(GridMap grid, CellReservations reservations, UnitMovement movement)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Reservations = reservations ?? throw new ArgumentNullException(nameof(reservations));
            Movement = movement ?? throw new ArgumentNullException(nameof(movement));
        }

        /// <summary>Board occupancy; a building frees its footprint here.</summary>
        public GridMap Grid { get; }

        /// <summary>Reserved cells; a building gives back its spawn cell here.</summary>
        public CellReservations Reservations { get; }

        /// <summary>Soldier movement; a soldier gives back its cell, reservation and move order here.</summary>
        public UnitMovement Movement { get; }
    }
}
