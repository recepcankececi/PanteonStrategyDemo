using System;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Turns a frame of <see cref="PointerInput"/> into selection intents: a left click on the board selects
    /// what is under the pointer (or clears the selection on empty ground), Esc clears it. Clicks over the UI
    /// belong to the UI, so clicking a button in the information panel keeps the building selected.
    /// </summary>
    public sealed class SelectionInputHandler
    {
        private readonly SelectionService _selection;
        private readonly GridView _gridView;

        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public SelectionInputHandler(SelectionService selection, GridView gridView)
        {
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _gridView = gridView != null ? gridView : throw new ArgumentNullException(nameof(gridView));
        }

        /// <summary>Applies one frame of input. Call only when placement did not consume the frame.</summary>
        public void Handle(in PointerInput input)
        {
            if (input.CancelPressed)
            {
                _selection.Clear();
                return;
            }

            if (input.SelectPressed && !input.IsOverUi)
            {
                _selection.SelectAt(_gridView.WorldToCell(input.PointerWorld));
            }
        }
    }
}
