using System;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Current and maximum hit points of one building or soldier (R7, R8, R10). Pure C#, owned by the object it belongs
    /// to; the maximum comes from its definition asset, the current value never goes back into that shared asset.
    /// </summary>
    /// <remarks>
    /// <see cref="TakeDamage"/> reports the fatal hit through its return value, not an event: the caller that dealt the
    /// damage (combat) is the one who must act on the death, and it does so through one remove path
    /// (<see cref="OccupantDespawner"/>). Events are for views: the health bar, the hit flash and the information panel.
    /// </remarks>
    public sealed class Health
    {
        /// <summary>Hit points at full health; 0 while the owner waits in a pool.</summary>
        public int Max { get; private set; }

        /// <summary>Hit points left, between 0 and <see cref="Max"/>.</summary>
        public int Current { get; private set; }

        /// <summary>True while hit points are left; a dead object takes no further damage.</summary>
        public bool IsAlive => Current > 0;

        /// <summary>Hit points left as a share of the maximum, from 0 to 1.</summary>
        public float Fraction => Max > 0 ? (float)Current / Max : 0f;

        /// <summary>Raised after <see cref="Current"/> or <see cref="Max"/> changed.</summary>
        public event Action Changed;

        /// <summary>Raised after a hit that took hit points, with the amount taken; never for a hit on the dead.</summary>
        public event Action<int> Damaged;

        /// <summary>Starts over at full health with <paramref name="max"/> hit points (a new or reused object).</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> is not positive.</exception>
        public void Reset(int max)
        {
            if (max <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, "Maximum health must be positive.");
            }

            Max = max;
            Current = max;
            Changed?.Invoke();
        }

        /// <summary>
        /// Takes <paramref name="amount"/> hit points, never below 0. Ignored once dead, so an object cannot die twice.
        /// </summary>
        /// <returns>True only for the hit that brought the hit points to 0.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is negative.</exception>
        public bool TakeDamage(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Damage cannot be negative; healing is not damage.");
            }

            if (!IsAlive || amount == 0)
            {
                return false;
            }

            int taken = Math.Min(amount, Current);
            Current -= taken;
            Damaged?.Invoke(taken);
            Changed?.Invoke();
            return !IsAlive;
        }

        /// <summary>Back to the pooled state: no hit points and no listeners, so a reused object cannot call old views.</summary>
        public void Clear()
        {
            Max = 0;
            Current = 0;
            Changed = null;
            Damaged = null;
        }
    }
}
