using System;
using StrategyDemo.Core;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// The board services a despawned object gives its cells back to (<see cref="IDespawnable.LeaveBoard"/>). A class,
    /// not a parameter list: a future occupant type that needs another service adds a property here, and the existing
    /// types and the interface stay as they are.
    /// </summary>
    public sealed class DespawnContext
    {
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public DespawnContext(GridMap grid, UnitMovement movement)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Movement = movement ?? throw new ArgumentNullException(nameof(movement));
        }

        /// <summary>Board occupancy; a building frees its footprint here.</summary>
        public GridMap Grid { get; }

        /// <summary>Soldier movement; a soldier gives back its cell, reservation and move order here.</summary>
        public UnitMovement Movement { get; }
    }
}
