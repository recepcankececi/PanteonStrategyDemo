using StrategyDemo.Gameplay.Board;
using StrategyDemo.Gameplay.Despawn;

namespace StrategyDemo.Gameplay.Combat
{
    /// <summary>
    /// A board object that can be attacked and destroyed: buildings and soldiers (R9, R10). An attacker
    /// only needs this, never the concrete type; range is measured to the nearest cell of
    /// <see cref="IBoardOccupant.Footprint"/>, so a soldier at a large building's edge can hit it. Whatever can be
    /// destroyed can also be taken off the board, hence <see cref="IDespawnable"/>.
    /// </summary>
    public interface IDamageable : IDespawnable
    {
        /// <summary>Hit points of this object. Damage goes through <see cref="Gameplay.Health.TakeDamage"/>.</summary>
        Health Health { get; }

        /// <summary>
        /// Keeps the health bar visible at full health while <paramref name="pinned"/> (the object is selected). A
        /// damaged object shows its bar either way.
        /// </summary>
        void PinHealthBar(bool pinned);
    }
}
