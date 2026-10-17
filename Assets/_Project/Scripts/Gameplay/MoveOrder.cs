using System.Collections.Generic;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// One soldier's walk in progress: where the player sent it, where it will stop and the path there.
    /// Pooled by <see cref="UnitMovement"/>; the path list keeps its capacity across reuse, so planning allocates nothing.
    /// </summary>
    internal sealed class MoveOrder
    {
        /// <param name="pathCapacity">Longest possible path (the number of cells on the board).</param>
        public MoveOrder(int pathCapacity)
        {
            Path = new List<Vector2Int>(pathCapacity);
        }

        /// <summary>The walking soldier; null while the order waits in the pool.</summary>
        public Unit Unit { get; set; }

        /// <summary>
        /// Cell the player clicked; kept so a blocked path can be planned again towards it. For an approach, the
        /// target's cell nearest the soldier, recomputed on every plan.
        /// </summary>
        public Vector2Int Goal { get; set; }

        /// <summary>
        /// Building or soldier being approached to attack; null for a plain move. The stop cell is then picked within
        /// <see cref="Range"/> of it.
        /// </summary>
        public IBoardOccupant Target { get; set; }

        /// <summary>Attack range in cells; meaningful only while <see cref="Target"/> is set.</summary>
        public int Range { get; set; }

        /// <summary>Cell the soldier will stop on: the goal, or the nearest cell it can reach and reserve.</summary>
        public Vector2Int Destination { get; set; }

        /// <summary>Cells from where the soldier was when planned to <see cref="Destination"/>, both included.</summary>
        public List<Vector2Int> Path { get; }

        /// <summary>Index in <see cref="Path"/> of the cell centre the soldier walks to next; Path.Count once there.</summary>
        public int NextIndex { get; set; }

        /// <summary>
        /// False while every reachable cell is reserved by others: the soldier then waits where it is and plans again
        /// every frame. Normally true, with <see cref="Destination"/> reserved for this soldier.
        /// </summary>
        public bool HasStopCell { get; set; }

        /// <summary>
        /// True while the soldier holds a grid cell (<see cref="HeldCell"/>). False while it walks through a cell
        /// another soldier holds.
        /// </summary>
        public bool HoldsCell { get; set; }

        /// <summary>Grid cell the soldier holds; meaningful only while <see cref="HoldsCell"/>.</summary>
        public Vector2Int HeldCell { get; set; }

        /// <summary>True once the soldier has reached the centre of its destination.</summary>
        public bool HasArrived => NextIndex >= Path.Count;

        /// <summary>Forgets the soldier and path before the order returns to the pool.</summary>
        public void Clear()
        {
            Unit = null;
            Path.Clear();
            NextIndex = 0;
            Goal = default;
            Target = null;
            Range = 0;
            Destination = default;
            HasStopCell = false;
            HoldsCell = false;
            HeldCell = default;
        }
    }
}
