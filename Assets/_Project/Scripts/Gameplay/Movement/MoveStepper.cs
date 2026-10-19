using StrategyDemo.Data;
using StrategyDemo.Gameplay.Board;
using StrategyDemo.Gameplay.Units;
using UnityEngine;

namespace StrategyDemo.Gameplay.Movement
{
    /// <summary>
    /// Walks one soldier along its planned path for one frame, and asks for a new plan when a building appears on the
    /// way. Part of <see cref="UnitMovement"/>.
    /// </summary>
    internal sealed class MoveStepper
    {
        private readonly MovePlanner _planner;
        private readonly GridView _gridView;

        /// <param name="planner">Plans again when the path is blocked.</param>
        /// <param name="gridView">Converts path cells to world positions.</param>
        /// <remarks>Arguments are checked by <see cref="UnitMovement"/>, the only creator.</remarks>
        public MoveStepper(MovePlanner planner, GridView gridView)
        {
            _planner = planner;
            _gridView = gridView;
        }

        /// <summary>Moves <paramref name="order"/>'s soldier <paramref name="deltaTime"/> seconds along its path.</summary>
        public void Advance(MoveOrder order, float deltaTime)
        {
            Unit unit = order.Unit;
            Vector3 position = unit.transform.position;

            if (!order.HasStopCell || !_planner.IsWalkable(order.Destination) || !IsNextCellWalkable(order))
            {
                _planner.Replan(order);
            }

            float remaining = unit.Definition.MoveSpeed * GridConfig.CellWorldSize * deltaTime;
            // A loop, not one step: at a low frame rate one frame may pass several cell centres.
            while (!order.HasArrived && remaining > 0f)
            {
                // Checked again at every centre: a long frame must not carry the soldier into a new building.
                if (!IsNextCellWalkable(order))
                {
                    break;
                }

                Vector3 waypoint = _gridView.CellToWorld(order.Path[order.NextIndex]);
                Vector3 toWaypoint = waypoint - position;
                float distance = toWaypoint.magnitude;
                if (distance <= remaining)
                {
                    position = waypoint;
                    remaining -= distance;
                    order.NextIndex++;
                }
                else
                {
                    position += toWaypoint * (remaining / distance);
                    remaining = 0f;
                }

                unit.FaceTowards(toWaypoint);
            }

            unit.transform.position = position;
            unit.SetWalking(!order.HasArrived);
        }

        private bool IsNextCellWalkable(MoveOrder order)
        {
            if (order.HasArrived)
            {
                return true;
            }

            Vector2Int next = order.Path[order.NextIndex];
            if (!_planner.IsWalkable(next))
            {
                return false;
            }

            // Walking back to its own centre never leaves its own cell.
            if (order.NextIndex == 0)
            {
                return true;
            }

            // A diagonal step needs its two side cells open, as when it was planned (no corner cutting): from an
            // off-centre start the line really crosses one of them, so a building placed there since means a new plan.
            Vector2Int from = order.Path[order.NextIndex - 1];
            return from.x == next.x || from.y == next.y
                || (_planner.IsWalkable(new Vector2Int(next.x, from.y)) && _planner.IsWalkable(new Vector2Int(from.x, next.y)));
        }
    }
}
