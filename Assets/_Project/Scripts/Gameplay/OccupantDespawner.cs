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
    /// <item>Board: the object gives back what it holds (<see cref="IDespawnable.LeaveBoard"/>): a building its cells and
    /// spawn cell, a soldier its cell, stop-cell reservation and move order.</item>
    /// <item>Movement: soldiers approaching it carry on to their stop cells as a plain move.</item>
    /// <item>Registry: its id leads nowhere any more (ids are never reused).</item>
    /// <item>Selection: dropped if selected, so the information panel and the highlight update.</item>
    /// <item><see cref="Despawned"/>: combat drops the dead object's own attack and every attack aimed at it.</item>
    /// <item>Pool: the scene object goes back to its factory's pool and is reset there
    /// (<see cref="IDespawnable.ReturnToPool"/>).</item>
    /// </list>
    /// The type-specific steps (1 and 6) live on the objects themselves, so a new occupant type needs no change here.
    /// </remarks>
    public sealed class OccupantDespawner
    {
        private readonly OccupantRegistry _occupants;
        private readonly UnitMovement _movement;
        private readonly SelectionService _selection;
        private readonly DespawnContext _board;

        /// <param name="grid">Board occupancy; the dead object's cells are freed.</param>
        /// <param name="occupants">Registry the dead object is removed from.</param>
        /// <param name="reservations">A dead building's spawn cell is given back here.</param>
        /// <param name="movement">Drops the dead soldier's orders and anyone approaching the dead object.</param>
        /// <param name="selection">Loses the dead object if it was selected.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public OccupantDespawner(GridMap grid, OccupantRegistry occupants, CellReservations reservations, UnitMovement movement,
            SelectionService selection)
        {
            _occupants = occupants ?? throw new ArgumentNullException(nameof(occupants));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            // Checks grid, reservations and movement for null; built once, so a despawn allocates nothing.
            _board = new DespawnContext(grid, reservations, movement);
        }

        /// <summary>
        /// Raised while an object is being removed: it is off the grid, the registry and the selection, but not yet
        /// back in its pool, so listeners can still compare it with what they hold and read where it stood.
        /// </summary>
        public event Action<IBoardOccupant> Despawned;

        /// <summary>Takes <paramref name="occupant"/> off the board for good.</summary>
        /// <returns>False when it is not on the board (e.g. already removed); nothing changes then.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="occupant"/> is null.</exception>
        public bool Despawn(IDespawnable occupant)
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

            occupant.LeaveBoard(_board);
            _movement.ForgetTarget(occupant);
            _occupants.Remove(occupant.OccupantId);
            _selection.Remove(occupant);
            Despawned?.Invoke(occupant);
            occupant.ReturnToPool();
            return true;
        }
    }
}
