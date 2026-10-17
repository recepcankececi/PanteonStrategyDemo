using System;
using System.Collections.Generic;
using StrategyDemo.Core;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// What the player has selected (R4, R6, I3): one building or soldier by click, or several soldiers by box.
    /// Pure C#: cells go in, the objects on them come out through the grid (cell → id) and the
    /// registry (id → object).
    /// </summary>
    /// <remarks>
    /// Listeners (information panel, highlight) read <see cref="Selected"/> or <see cref="All"/> when
    /// <see cref="SelectionChanged"/> fires. The list is reused, so selecting allocates nothing once it has grown.
    /// </remarks>
    public sealed class SelectionService
    {
        private readonly GridMap _grid;
        private readonly OccupantRegistry _occupants;
        private readonly List<IBoardOccupant> _selected = new List<IBoardOccupant>();
        // Scratch list for box selection; kept to avoid allocating one per box.
        private readonly List<IBoardOccupant> _boxed = new List<IBoardOccupant>();

        /// <param name="grid">Board occupancy: which id stands on a clicked cell.</param>
        /// <param name="occupants">Resolves that id to the building or soldier.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public SelectionService(GridMap grid, OccupantRegistry occupants)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _occupants = occupants ?? throw new ArgumentNullException(nameof(occupants));
        }

        /// <summary>Raised after the selection changes; never for a click that keeps the same selection.</summary>
        public event Action SelectionChanged;

        /// <summary>
        /// First selected building or soldier, the one the information panel shows; null when nothing is selected.
        /// </summary>
        public IBoardOccupant Selected => _selected.Count > 0 ? _selected[0] : null;

        /// <summary>Everything selected: one building, or one or more soldiers. Empty when nothing is selected.</summary>
        public IReadOnlyList<IBoardOccupant> All => _selected;

        /// <summary>
        /// Selects whatever stands on <paramref name="cell"/>. An empty cell, or a cell off the board, clears the
        /// selection, the usual "click the ground to deselect" behaviour.
        /// </summary>
        public void SelectAt(Vector2Int cell)
        {
            IBoardOccupant occupant = OccupantOf(cell);
            if (occupant == null)
            {
                Clear();
                return;
            }

            if (_selected.Count == 1 && ReferenceEquals(_selected[0], occupant))
            {
                return;
            }

            _selected.Clear();
            _selected.Add(occupant);
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Box selection: selects every soldier standing inside <paramref name="area"/>, in no particular order. Buildings
        /// are ignored (soldiers are what a box is for); a box with no soldier clears the selection.
        /// </summary>
        /// <remarks>
        /// Soldiers are found by where they stand (<see cref="IBoardOccupant.Origin"/>), not through the grid: a soldier
        /// passing through another one's cell, or a second soldier produced while the first is still on the spawn cell,
        /// holds no grid cell for a moment, yet it is inside the box and must be selected.
        /// </remarks>
        public void SelectSoldiersIn(RectInt area)
        {
            // Collected apart first, so an unchanged selection can be detected and raise no event.
            _boxed.Clear();
            foreach (IBoardOccupant occupant in _occupants.All)
            {
                if (occupant.IsMobile && area.Contains(occupant.Origin))
                {
                    _boxed.Add(occupant);
                }
            }

            if (IsSameAsSelected(_boxed))
            {
                return;
            }

            _selected.Clear();
            _selected.AddRange(_boxed);
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Drops <paramref name="occupant"/> from the selection (it was destroyed); the rest stays selected. Ignored when
        /// it is not selected.
        /// </summary>
        public void Remove(IBoardOccupant occupant)
        {
            // List.Remove uses Equals, which for these objects means "the same object".
            if (occupant != null && _selected.Remove(occupant))
            {
                SelectionChanged?.Invoke();
            }
        }

        /// <summary>Clears the selection (e.g. Esc). Ignored when nothing is selected.</summary>
        public void Clear()
        {
            if (_selected.Count == 0)
            {
                return;
            }

            _selected.Clear();
            SelectionChanged?.Invoke();
        }

        private bool IsSameAsSelected(List<IBoardOccupant> candidates)
        {
            if (candidates.Count != _selected.Count)
            {
                return false;
            }

            // Compared as sets: the registry's order is not stable, and the same soldiers in another order are the
            // same selection. A box holds few soldiers, so the quadratic check is cheap.
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!_selected.Contains(candidates[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private IBoardOccupant OccupantOf(Vector2Int cell)
        {
            return _occupants.TryGetAt(_grid, cell, out IBoardOccupant occupant) ? occupant : null;
        }
    }
}
