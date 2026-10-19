using System;
using StrategyDemo.Gameplay.Board;

namespace StrategyDemo.Gameplay.Placement
{
    /// <summary>
    /// Turns a frame of <see cref="PointerInput"/> into placement intents: follow the
    /// pointer, place on Select (keep placing while Modifier is held), cancel on Command or Cancel.
    /// Clicks over the UI are left to the UI and never reach the board.
    /// </summary>
    public sealed class PlacementInputHandler
    {
        private readonly BuildingPlacer _placer;
        private readonly GridView _gridView;

        /// <param name="placer">Placement state and rules the input drives.</param>
        /// <param name="gridView">Converts the pointer to cells.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public PlacementInputHandler(BuildingPlacer placer, GridView gridView)
        {
            _placer = placer ?? throw new ArgumentNullException(nameof(placer));
            _gridView = gridView != null ? gridView : throw new ArgumentNullException(nameof(gridView));
        }

        /// <summary>Applies one frame of input; does nothing while the player is not placing.</summary>
        /// <returns>
        /// True when placement owned this frame's input (the player was placing), so no other handler may act on
        /// the same click; e.g. the click that places a building must not also select it.
        /// </returns>
        public bool Handle(in PointerInput input)
        {
            if (!_placer.IsPlacing)
            {
                return false;
            }

            // Esc is a keyboard action, so it works wherever the pointer is.
            if (input.CancelPressed)
            {
                _placer.Cancel();
                return true;
            }

            if (input.IsOverUi)
            {
                // Hide the ghost behind the panels; a menu click arrives separately as a new Begin.
                _placer.ClearCandidate();
                return true;
            }

            if (input.CommandPressed)
            {
                _placer.Cancel();
                return true;
            }

            // Move before placing so a click lands where the player sees the ghost in this very frame.
            _placer.MoveTo(_gridView.FootprintOriginAt(input.PointerWorld, _placer.Definition.Footprint));
            if (input.SelectPressed)
            {
                _placer.TryPlace(input.ModifierHeld);
            }

            return true;
        }
    }
}
