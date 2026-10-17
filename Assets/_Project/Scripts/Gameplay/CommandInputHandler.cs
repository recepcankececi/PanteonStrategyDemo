using System;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Turns a right click on the board into an order for the selected soldiers (R6): walk to the clicked cell, or up
    /// to the building or soldier standing there. Attacking that target arrives in M6. A selected building ignores
    /// the click; clicks over the UI never reach the board.
    /// </summary>
    public sealed class CommandInputHandler
    {
        private readonly SelectionService _selection;
        private readonly UnitMovement _movement;
        private readonly GridView _gridView;

        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public CommandInputHandler(SelectionService selection, UnitMovement movement, GridView gridView)
        {
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _gridView = gridView != null ? gridView : throw new ArgumentNullException(nameof(gridView));
        }

        /// <summary>Applies one frame of input. Call only when placement did not consume the frame.</summary>
        public void Handle(in PointerInput input)
        {
            if (!input.CommandPressed || input.IsOverUi)
            {
                return;
            }

            // Buildings in the selection are skipped by the movement itself; an off-board cell is ignored there too.
            _movement.MoveGroup(_selection.All, _gridView.WorldToCell(input.PointerWorld));
        }
    }
}
