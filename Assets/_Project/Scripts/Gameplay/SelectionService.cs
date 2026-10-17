using System;
using System.Collections.Generic;
using StrategyDemo.Core;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// What the player has selected (R4, R6, I3): one building or soldier by click, or several soldiers by box
    /// (ADR-001 Karar 11). Pure C#: cells go in, the objects on them come out through the grid (cell → id) and the
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
        /// Box selection: selects every soldier holding a cell inside <paramref name="area"/>, in row order from the
        /// bottom. Buildings are ignored (soldiers are what a box is for); a box with no soldier clears the selection.
        /// The part of the area off the board is ignored.
        /// </summary>
        public void SelectSoldiersIn(RectInt area)
        {
            // Collected apart first, so an unchanged selection can be detected and raise no event.
            _boxed.Clear();
            int xMax = Mathf.Min(area.xMax, _grid.Width);
            int yMax = Mathf.Min(area.yMax, _grid.Height);
            for (int y = Mathf.Max(area.yMin, 0); y < yMax; y++)
            {
                for (int x = Mathf.Max(area.xMin, 0); x < xMax; x++)
                {
                    IBoardOccupant occupant = OccupantOf(new Vector2Int(x, y));
                    // A soldier holds one cell, so it is met once; buildings are skipped.
                    if (occupant != null && occupant.IsMobile)
                    {
                        _boxed.Add(occupant);
                    }
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

            for (int i = 0; i < candidates.Count; i++)
            {
                if (!ReferenceEquals(candidates[i], _selected[i]))
                {
                    return false;
                }
            }

            return true;
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
    }
}
