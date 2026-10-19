using System.Collections.Generic;
using StrategyDemo.Core;
using StrategyDemo.Gameplay.Board;
using StrategyDemo.Gameplay.Units;
using UnityEngine;

namespace StrategyDemo.Gameplay.Movement
{
    /// <summary>
    /// Keeps the grid cell each walking soldier holds in step with where its centre is. Part of
    /// <see cref="UnitMovement"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Unit.Origin"/> follows the centre (it changes at the midpoint between two cells), and the grid cell
    /// follows when that cell is free. While a soldier walks through a cell another soldier holds, it holds no cell at
    /// all. So every grid cell a soldier holds is one it stands in, and after <see cref="Settle"/> every cell a soldier
    /// stands in is held by one of the soldiers in it: a building can never be placed on a soldier.
    /// </remarks>
    internal sealed class HeldCellTracker
    {
        private readonly GridMap _grid;
        private readonly GridView _gridView;

        /// <param name="grid">Board occupancy the held cells are written to.</param>
        /// <param name="gridView">Converts a soldier's position to its cell.</param>
        /// <remarks>Arguments are checked by <see cref="UnitMovement"/>, the only creator.</remarks>
        public HeldCellTracker(GridMap grid, GridView gridView)
        {
            _grid = grid;
            _gridView = gridView;
        }

        /// <summary>Makes every order's held cell follow its soldier; call after the soldiers moved.</summary>
        public void Settle(IReadOnlyList<MoveOrder> orders)
        {
            // Again until nothing changes: a cell one soldier lets go of may be the cell another one, earlier in the
            // list, is standing in. Each soldier changes at most twice (let go, take), so this ends after a few passes.
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < orders.Count; i++)
                {
                    changed |= UpdateGridCell(orders[i]);
                }
            }
        }

        /// <summary>Gives back the grid cell <paramref name="order"/>'s soldier holds. Returns true when it held one.</summary>
        public bool ReleaseHeldCell(MoveOrder order)
        {
            if (!order.HoldsCell)
            {
                return false;
            }

            _grid.Release(order.HeldCell, Vector2Int.one, order.Unit.OccupantId);
            order.HoldsCell = false;
            return true;
        }

        /// <summary>Makes the grid follow the soldier's centre. Returns true when the grid changed.</summary>
        private bool UpdateGridCell(MoveOrder order)
        {
            Unit unit = order.Unit;
            // The same cell as MovePlanner.BodyCell: where the soldier's centre is.
            Vector2Int body = _gridView.WorldToCell(unit.transform.position);
            // The centre crossed into another cell, i.e. passed the midpoint between two cell centres.
            if (body != unit.Origin)
            {
                unit.SetCell(body);
            }

            if (order.HoldsCell && order.HeldCell == body)
            {
                return false;
            }

            bool changed = ReleaseHeldCell(order);
            // Another soldier standing in this cell may hold it; then this one holds nothing until it is free.
            if (_grid.GetOccupant(body) == GridMap.EmptyId)
            {
                _grid.Occupy(body, Vector2Int.one, unit.OccupantId);
                order.HoldsCell = true;
                order.HeldCell = body;
                changed = true;
            }

            return changed;
        }
    }
}
