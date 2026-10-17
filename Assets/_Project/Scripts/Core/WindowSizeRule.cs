using UnityEngine;

namespace StrategyDemo.Core
{
    /// <summary>
    /// Smallest window the game is played in (T2). Below it the Canvas scales the panels so far down that text is
    /// unreadable and board cells shrink to a few pixels, so a windowed build is grown back to it.
    /// </summary>
    public static class WindowSizeRule
    {
        /// <summary>
        /// The size <paramref name="current"/> must grow to so neither side is below <paramref name="minimum"/>.
        /// </summary>
        /// <returns>True when <paramref name="current"/> is too small on either side.</returns>
        public static bool TryGrowToMinimum(Vector2Int current, Vector2Int minimum, out Vector2Int grown)
        {
            // Each side on its own: a window that is wide enough but too short only grows taller.
            grown = Vector2Int.Max(current, minimum);
            return grown != current;
        }
    }
}
