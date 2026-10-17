using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>One soldier's attack in progress. Pooled by <see cref="UnitCombat"/>.</summary>
    internal sealed class AttackOrder
    {
        /// <summary>The attacking soldier; null while the order waits in the pool.</summary>
        public Unit Attacker { get; set; }

        /// <summary>Building or soldier being attacked.</summary>
        public IDamageable Target { get; set; }

        /// <summary>Chasing or Attacking; an order never exists in the Idle state.</summary>
        public CombatState State { get; set; }

        /// <summary>
        /// Target's cell when the chase was last planned; a soldier target that stepped off it is planned for again.
        /// </summary>
        public Vector2Int PlannedTargetCell { get; set; }

        /// <summary>Seconds until the chase plans its path again.</summary>
        public float RepathTimer { get; set; }

        /// <summary>Forgets the soldier and target before the order returns to the pool.</summary>
        public void Clear()
        {
            Attacker = null;
            Target = null;
            State = CombatState.Idle;
            PlannedTargetCell = default;
            RepathTimer = 0f;
        }
    }
}
