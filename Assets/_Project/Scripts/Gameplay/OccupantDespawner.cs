using System;
using StrategyDemo.Core;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// The one path that takes a destroyed building or soldier off the board (R10), so no system keeps a reference to
    /// something that is gone. Pure C#, tested without a scene.
    /// </summary>
    /// <remarks>
    /// <para>Order, each step needing the object still intact:</para>
    /// <list type="number">
    /// <item>Grid: a building frees its cells; a soldier gives back its cell, stop-cell reservation and move order
    /// (<see cref="UnitMovement.Remove"/>).</item>
    /// <item>Movement: soldiers approaching it carry on to their stop cells as a plain move.</item>
    /// <item>Registry: its id leads nowhere any more (ids are never reused, ADR-006).</item>
    /// <item>Selection: dropped if selected, so the information panel and the highlight update.</item>
    /// <item><see cref="Despawned"/>: combat drops the dead object's own attack and every attack aimed at it.</item>
    /// <item>Factory: the scene object goes back to its pool and is reset there.</item>
    /// </list>
    /// </remarks>
    public sealed class OccupantDespawner
    {
        private readonly GridMap _grid;
        private readonly OccupantRegistry _occupants;
        private readonly UnitMovement _movement;
        private readonly SelectionService _selection;
        private readonly IUnitFactory _unitFactory;
        private readonly IBuildingFactory _buildingFactory;

        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public OccupantDespawner(GridMap grid, OccupantRegistry occupants, UnitMovement movement, SelectionService selection,
            IUnitFactory unitFactory, IBuildingFactory buildingFactory)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _occupants = occupants ?? throw new ArgumentNullException(nameof(occupants));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _unitFactory = unitFactory ?? throw new ArgumentNullException(nameof(unitFactory));
            _buildingFactory = buildingFactory ?? throw new ArgumentNullException(nameof(buildingFactory));
        }

        /// <summary>
        /// Raised while an object is being removed: it is off the grid, the registry and the selection, but not yet
        /// back in its pool, so listeners can still compare it with what they hold.
        /// </summary>
        public event Action<IBoardOccupant> Despawned;

        /// <summary>Takes <paramref name="occupant"/> off the board for good.</summary>
        /// <returns>False when it is not on the board (e.g. already removed); nothing changes then.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="occupant"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="occupant"/> is neither a <see cref="Unit"/> nor a <see cref="Building"/>.</exception>
        public bool Despawn(IBoardOccupant occupant)
        {
            if (occupant == null)
            {
                throw new ArgumentNullException(nameof(occupant));
            }

            // Registered under its id, and as this very object: a stale reference to a reused object must not remove
            // the new one.
            if (!_occupants.TryGet(occupant.OccupantId, out IBoardOccupant registered) || !ReferenceEquals(registered, occupant))
            {
                return false;
            }

            switch (occupant)
            {
                case Unit unit:
                    _movement.Remove(unit);
                    break;
                case Building building:
                    _grid.Release(building.Origin, building.Footprint, building.OccupantId);
                    break;
                default:
                    throw new ArgumentException($"Cannot despawn a {occupant.GetType().Name}.", nameof(occupant));
            }

            _movement.ForgetTarget(occupant);
            _occupants.Remove(occupant.OccupantId);
            _selection.Remove(occupant);
            Despawned?.Invoke(occupant);

            if (occupant is Unit deadUnit)
            {
                _unitFactory.Release(deadUnit);
            }
            else
            {
                _buildingFactory.Release((Building)occupant);
            }

            return true;
        }
    }
}
