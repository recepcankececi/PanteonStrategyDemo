using System;
using UnityEngine;

namespace StrategyDemo.Data
{
    /// <summary>Derives how many grid cells a sprite occupies from its pixel size.</summary>
    public static class Footprint
    {
        /// <summary>
        /// Returns the cell footprint of an image of <paramref name="pixelSize"/> on a grid of
        /// <paramref name="cellPixelSize"/>-pixel cells. Partial cells round up; the minimum is 1×1.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="cellPixelSize"/> is not positive.</exception>
        public static Vector2Int FromPixelSize(Vector2 pixelSize, int cellPixelSize)
        {
            if (cellPixelSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cellPixelSize), cellPixelSize, "Cell pixel size must be positive.");
            }

            // Round up: a sprite that spills into a cell must own it, or another building could be placed under it.
            return new Vector2Int(
                Mathf.Max(1, Mathf.CeilToInt(pixelSize.x / cellPixelSize)),
                Mathf.Max(1, Mathf.CeilToInt(pixelSize.y / cellPixelSize)));
        }
    }
}
