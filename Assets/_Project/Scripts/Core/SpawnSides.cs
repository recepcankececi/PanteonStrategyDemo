using System;
using UnityEngine;

namespace StrategyDemo.Core
{
    /// <summary>
    /// The cells a producing building may take as its spawn point, in the order they are tried: the middle of the
    /// bottom side, then the right, the top and the left side, each one cell outside the footprint.
    /// </summary>
    /// <remarks>
    /// The building takes the first candidate that is usable when it is placed and keeps it for life, so the player
    /// always knows where its soldiers will appear. Bottom first because the board is seen from above and "in front of"
    /// a building reads as below it. Pure rule, no board state: the caller decides what "usable" means.
    /// </remarks>
    public static class SpawnSides
    {
        /// <summary>Number of candidate cells, one per side.</summary>
        public const int Count = 4;

        /// <summary>
        /// Candidate <paramref name="index"/> (0 = bottom, 1 = right, 2 = top, 3 = left) of a building whose bottom-left
        /// cell is <paramref name="origin"/>; may lie off the board.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not in [0, <see cref="Count"/>).</exception>
        public static Vector2Int Candidate(Vector2Int origin, Vector2Int footprint, int index)
        {
            // Integer halves: on an even side the "middle" is the cell right of (or above) the centre line.
            int middleX = origin.x + footprint.x / 2;
            int middleY = origin.y + footprint.y / 2;
            switch (index)
            {
                case 0: return new Vector2Int(middleX, origin.y - 1);
                case 1: return new Vector2Int(origin.x + footprint.x, middleY);
                case 2: return new Vector2Int(middleX, origin.y + footprint.y);
                case 3: return new Vector2Int(origin.x - 1, middleY);
                default: throw new ArgumentOutOfRangeException(nameof(index), index, $"Spawn side must be in [0, {Count}).");
            }
        }

        /// <summary>First candidate for which <paramref name="isUsable"/> holds.</summary>
        /// <param name="isUsable">Pass a cached delegate: creating one per call would allocate.</param>
        /// <param name="spawnCell">The chosen cell; meaningful only when the method returns true.</param>
        /// <returns>False when no side is usable; the building cannot be placed there.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="isUsable"/> is null.</exception>
        public static bool TryChoose(Vector2Int origin, Vector2Int footprint, Predicate<Vector2Int> isUsable, out Vector2Int spawnCell)
        {
            if (isUsable == null)
            {
                throw new ArgumentNullException(nameof(isUsable));
            }

            for (int i = 0; i < Count; i++)
            {
                spawnCell = Candidate(origin, footprint, i);
                if (isUsable(spawnCell))
                {
                    return true;
                }
            }

            spawnCell = default;
            return false;
        }
    }
}
