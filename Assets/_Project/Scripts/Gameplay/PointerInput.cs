using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// One frame of player input, already reduced to what gameplay cares about. A struct read once per
    /// frame by the input layer: gameplay rules never touch the Input System, and tests build it by hand.
    /// </summary>
    public readonly struct PointerInput
    {
        /// <summary>Creates a frame snapshot.</summary>
        public PointerInput(Vector3 pointerWorld, bool isOverUi, bool selectPressed, bool commandPressed, bool cancelPressed, bool modifierHeld)
        {
            PointerWorld = pointerWorld;
            IsOverUi = isOverUi;
            SelectPressed = selectPressed;
            CommandPressed = commandPressed;
            CancelPressed = cancelPressed;
            ModifierHeld = modifierHeld;
        }

        /// <summary>Pointer position on the board plane, in world units.</summary>
        public Vector3 PointerWorld { get; }

        /// <summary>True when the pointer is over a UI element; clicks then belong to the UI.</summary>
        public bool IsOverUi { get; }

        /// <summary>Select (left click) went down this frame.</summary>
        public bool SelectPressed { get; }

        /// <summary>Command (right click) went down this frame.</summary>
        public bool CommandPressed { get; }

        /// <summary>Cancel (Esc) went down this frame.</summary>
        public bool CancelPressed { get; }

        /// <summary>Modifier (Shift) is held.</summary>
        public bool ModifierHeld { get; }
    }
}
