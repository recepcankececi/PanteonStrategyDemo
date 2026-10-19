using System;
using StrategyDemo.Core;
using StrategyDemo.Gameplay.Board;
using StrategyDemo.Gameplay.Combat;
using StrategyDemo.Gameplay.Movement;
using UnityEngine;

namespace StrategyDemo.Gameplay.Selection
{
    /// <summary>
    /// Turns a right click on the board into an order for the selected soldiers: attack the building or soldier on the
    /// clicked cell (R9), or walk to the cell when it is empty (R6). A selected building ignores the click; clicks over
    /// the UI never reach the board.
    /// </summary>
    public sealed class CommandInputHandler
    {
        private readonly SelectionService _selection;
        private readonly UnitMovement _movement;
        private readonly UnitCombat _combat;
        private readonly GridMap _grid;
        private readonly OccupantRegistry _occupants;
        private readonly GridView _gridView;

        /// <param name="selection">The soldiers that receive the order.</param>
        /// <param name="movement">Carries out move orders.</param>
        /// <param name="combat">Carries out attack orders.</param>
        /// <param name="grid">Board occupancy: what stands on the clicked cell.</param>
        /// <param name="occupants">Resolves that cell's id to the target.</param>
        /// <param name="gridView">Converts the pointer to a cell.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public CommandInputHandler(SelectionService selection, UnitMovement movement, UnitCombat combat, GridMap grid,
            OccupantRegistry occupants, GridView gridView)
        {
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _occupants = occupants ?? throw new ArgumentNullException(nameof(occupants));
            _gridView = gridView != null ? gridView : throw new ArgumentNullException(nameof(gridView));
        }

        /// <summary>Applies one frame of input. Call only when placement did not consume the frame.</summary>
        public void Handle(in PointerInput input)
        {
            if (!input.CommandPressed || input.IsOverUi)
            {
                return;
            }

            Vector2Int cell = _gridView.WorldToCell(input.PointerWorld);
            // Ignore the click entirely: stopping the attack here while the move is refused would leave soldiers
            // walking to their old target without hitting it.
            if (!_grid.IsInside(cell))
            {
                return;
            }

            // The attack rule leaves the clicked soldier itself out of its group's attack. When nobody may attack (the
            // only selected soldier clicked itself), the click is a move to that cell instead.
            if (_occupants.TryGetAt(_grid, cell, out IBoardOccupant occupant) && occupant is IDamageable target
                && _combat.Attack(_selection.All, target))
            {
                return;
            }

            // A move cancels any attack. Buildings in the selection are skipped by both.
            _combat.Stop(_selection.All);
            _movement.MoveGroup(_selection.All, cell);
        }
    }
}
