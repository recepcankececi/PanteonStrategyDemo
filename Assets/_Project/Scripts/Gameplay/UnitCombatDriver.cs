using System;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Runs <see cref="UnitCombat"/> once per frame. The only Unity-facing part of combat: the rules stay in pure C#.
    /// </summary>
    /// <remarks>
    /// Execution order 10: after <see cref="UnitMovementDriver"/> (0), so range is judged on this frame's positions,
    /// and before <c>InputController</c> (100), so cells freed by a death are free when placement validates.
    /// </remarks>
    [DefaultExecutionOrder(10)]
    public sealed class UnitCombatDriver : MonoBehaviour
    {
        private UnitCombat _combat;

        /// <summary>Starts ticking <paramref name="combat"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="combat"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Already initialised.</exception>
        public void Init(UnitCombat combat)
        {
            if (_combat != null)
            {
                throw new InvalidOperationException($"{nameof(UnitCombatDriver)} on '{name}' is already initialised.");
            }

            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        private void Update()
        {
            _combat?.Tick(Time.deltaTime);
        }
    }
}
