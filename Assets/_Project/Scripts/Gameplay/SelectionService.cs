using System;
using StrategyDemo.Core;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Which building or soldier the player has selected (R4, R6, I3). Pure C#: a clicked cell goes in, the
    /// object on it comes out through the grid (cell → id) and the registry (id → object).
    /// </summary>
    /// <remarks>
    /// One object at a time for now; multi-select (box selection) arrives with movement in M5.
    /// Listeners (information panel, highlight) read <see cref="Selected"/> when <see cref="SelectionChanged"/> fires.
    /// </remarks>
    public sealed class SelectionService
    {
        private readonly GridMap _grid;
        private readonly OccupantRegistry _occupants;

        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public SelectionService(GridMap grid, OccupantRegistry occupants)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _occupants = occupants ?? throw new ArgumentNullException(nameof(occupants));
        }

        /// <summary>Raised after <see cref="Selected"/> changes; never for a click that keeps the same selection.</summary>
        public event Action SelectionChanged;

        /// <summary>Selected building or soldier; null when nothing is selected.</summary>
        public IBoardOccupant Selected { get; private set; }

        /// <summary>
        /// Selects whatever stands on <paramref name="cell"/>. An empty cell, or a cell off the board, clears the
        /// selection, the usual "click the ground to deselect" behaviour.
        /// </summary>
        public void SelectAt(Vector2Int cell)
        {
            SetSelected(OccupantOf(cell));
        }

        /// <summary>Clears the selection (e.g. Esc). Ignored when nothing is selected.</summary>
        public void Clear()
        {
            SetSelected(null);
        }

        private IBoardOccupant OccupantOf(Vector2Int cell)
        {
            if (!_grid.IsInside(cell))
            {
                return null;
            }

            int id = _grid.GetOccupant(cell);
            // An id without a registered object would be a bookkeeping bug; treat it as empty ground.
            return id != GridMap.EmptyId && _occupants.TryGet(id, out IBoardOccupant occupant) ? occupant : null;
        }

        private void SetSelected(IBoardOccupant occupant)
        {
            if (ReferenceEquals(Selected, occupant))
            {
                return;
            }

            Selected = occupant;
            SelectionChanged?.Invoke();
        }
    }
}
