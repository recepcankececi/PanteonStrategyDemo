using System;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Turns a frame of <see cref="PointerInput"/> into selection intents: a left click on the board selects what is
    /// under the pointer (or clears the selection on empty ground); dragging with the left button held draws a box
    /// that selects the soldiers inside it on release; Esc clears the selection and drops a box.
    /// Clicks over the UI belong to the UI, so clicking a button in the information panel keeps the building selected.
    /// </summary>
    /// <remarks>
    /// The box is measured in cells, the unit selection works in: a press and release in one cell is a click, and
    /// the box drawn (<see cref="SelectionBox"/>) shows exactly the cells that will be searched.
    /// </remarks>
    public sealed class SelectionInputHandler
    {
        private readonly SelectionService _selection;
        private readonly GridView _gridView;
        private bool _isPressing;
        private Vector2Int _pressCell;

        /// <param name="selection">Receives the click and box selections.</param>
        /// <param name="gridView">Converts the pointer to cells.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public SelectionInputHandler(SelectionService selection, GridView gridView)
        {
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _gridView = gridView != null ? gridView : throw new ArgumentNullException(nameof(gridView));
        }

        /// <summary>True while the player drags a selection box.</summary>
        public bool IsDragging { get; private set; }

        /// <summary>Board cells inside the box; meaningful while <see cref="IsDragging"/>. Empty when the box is off the board.</summary>
        public RectInt DragArea { get; private set; }

        /// <summary>Applies one frame of input. Call only when placement did not consume the frame.</summary>
        public void Handle(in PointerInput input)
        {
            if (input.CancelPressed)
            {
                EndPress();
                _selection.Clear();
                return;
            }

            Vector2Int cell = _gridView.WorldToCell(input.PointerWorld);
            if (input.SelectPressed && !input.IsOverUi)
            {
                // Select on press for immediate feedback; a drag that follows replaces it on release.
                _selection.SelectAt(cell);
                _isPressing = true;
                _pressCell = cell;
            }

            if (!_isPressing)
            {
                return;
            }

            // Once the pointer leaves the pressed cell it is a drag, even if it comes back.
            if (cell != _pressCell)
            {
                IsDragging = true;
            }

            if (IsDragging)
            {
                DragArea = ClampToBoard(_pressCell, cell);
            }

            // Not held also ends the press: the release may have happened in a frame placement consumed.
            if (input.SelectReleased || !input.SelectHeld)
            {
                if (IsDragging)
                {
                    _selection.SelectSoldiersIn(DragArea);
                }

                EndPress();
            }
        }

        private void EndPress()
        {
            _isPressing = false;
            IsDragging = false;
            DragArea = default;
        }

        private RectInt ClampToBoard(Vector2Int a, Vector2Int b)
        {
            // Both corners are inclusive cells; the rectangle's max is exclusive, hence the +1.
            Vector2Int board = _gridView.BoardSize;
            int xMin = Mathf.Max(Mathf.Min(a.x, b.x), 0);
            int yMin = Mathf.Max(Mathf.Min(a.y, b.y), 0);
            int xMax = Mathf.Min(Mathf.Max(a.x, b.x) + 1, board.x);
            int yMax = Mathf.Min(Mathf.Max(a.y, b.y) + 1, board.y);
            // A box wholly off one side clamps to a negative size; report it as empty.
            return new RectInt(xMin, yMin, Mathf.Max(xMax - xMin, 0), Mathf.Max(yMax - yMin, 0));
        }
    }
}
