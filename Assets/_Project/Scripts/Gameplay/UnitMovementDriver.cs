using System;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Runs <see cref="UnitMovement"/> once per frame. The only Unity-facing part of movement: the rules stay in pure
    /// C#, and one central loop moves every soldier instead of one <c>Update</c> per soldier.
    /// </summary>
    /// <remarks>
    /// Default execution order (0) runs before <c>InputController</c> (100), so placement validates against the grid
    /// as it is after this frame's steps.
    /// </remarks>
    public sealed class UnitMovementDriver : MonoBehaviour
    {
        private UnitMovement _movement;

        /// <summary>Starts ticking <paramref name="movement"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="movement"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Already initialised.</exception>
        public void Init(UnitMovement movement)
        {
            if (_movement != null)
            {
                throw new InvalidOperationException($"{nameof(UnitMovementDriver)} on '{name}' is already initialised.");
            }

            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
        }

        private void Update()
        {
            // The editor can report a negative frame time (seen while stepping a paused game with the Frame Debugger
            // open). Tick rejects it as a bug in its caller; here it is skipped so one odd frame does not throw.
            float deltaTime = Time.deltaTime;
            if (_movement != null && deltaTime >= 0f)
            {
                _movement.Tick(deltaTime);
            }
        }
    }
}
