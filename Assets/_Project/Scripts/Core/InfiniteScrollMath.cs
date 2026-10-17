using System;
using UnityEngine;

namespace StrategyDemo.Core
{
    /// <summary>
    /// Index arithmetic of a looping (wrap-around) list whose items have a fixed stride.
    /// "Virtual index" counts items from the list's start and may grow without
    /// bound in both directions; the data shown is the virtual index wrapped into the item count.
    /// </summary>
    public static class InfiniteScrollMath
    {
        /// <summary>Maps any integer, negative included, into [0, <paramref name="count"/>).</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not positive.</exception>
        public static int Wrap(int index, int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be positive.");
            }

            // C#'s % keeps the sign of the dividend (-1 % 3 == -1), so shift negatives back into range.
            int wrapped = index % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }

        /// <summary>
        /// Views needed so the viewport never shows a gap: every item that fits, plus one because a
        /// scrolled list shows a partial item at both the top and the bottom.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="stride"/> is not positive.</exception>
        public static int ViewCount(float viewportLength, float stride)
        {
            ValidateStride(stride);
            return Mathf.Max(0, Mathf.CeilToInt(viewportLength / stride)) + 1;
        }

        /// <summary>Virtual index of the item at the top edge for a list scrolled by <paramref name="scrollOffset"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="stride"/> is not positive.</exception>
        public static int FirstVisibleIndex(float scrollOffset, float stride)
        {
            ValidateStride(stride);
            // Floor, not truncation: at offset -0.5 stride the item above the start (-1) is at the top.
            return Mathf.FloorToInt(scrollOffset / stride);
        }

        /// <summary>
        /// Whole cycles, in items, to subtract from <paramref name="firstIndex"/> to bring it back into the
        /// first cycle [0, <paramref name="count"/>). A multiple of <paramref name="count"/>, so every view
        /// keeps showing the same data after the shift.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not positive.</exception>
        public static int RebaseShift(int firstIndex, int count)
        {
            return firstIndex - Wrap(firstIndex, count);
        }

        private static void ValidateStride(float stride)
        {
            if (!(stride > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(stride), stride, "Stride must be positive.");
            }
        }
    }
}
