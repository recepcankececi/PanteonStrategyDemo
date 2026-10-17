namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// A board object that can be taken off the board for good (R10). Each type knows what it holds on the board and
    /// which pool it came from, so <see cref="OccupantDespawner"/> runs the same steps for every type and a new type
    /// (e.g. a wall) only implements this interface: the despawner never changes (Open/Closed).
    /// </summary>
    public interface IDespawnable : IBoardOccupant
    {
        /// <summary>
        /// Gives back everything this object holds on the board: grid cells, and for a soldier its stop-cell
        /// reservation and move order. The first step of <see cref="OccupantDespawner.Despawn"/>.
        /// </summary>
        void LeaveBoard(DespawnContext board);

        /// <summary>
        /// Hands the scene object back to the factory that made it, which resets it. The last step of
        /// <see cref="OccupantDespawner.Despawn"/>; nothing may touch the object afterwards.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">The object was not made by a factory.</exception>
        void ReturnToPool();
    }
}
