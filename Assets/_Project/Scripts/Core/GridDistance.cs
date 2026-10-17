using UnityEngine;

namespace StrategyDemo.Core
{
    /// <summary>
    /// Distances on the grid as attacks measure them: in whole cells, diagonal steps counting as one (Chebyshev), to
    /// the nearest cell of a target's footprint. A soldier next to the corner of a 4×4 building is at
    /// distance 1 from it, however far the building's centre is.
    /// </summary>
    public static class GridDistance
    {
        /// <summary>
        /// Cell of the area at <paramref name="areaOrigin"/> sized <paramref name="areaSize"/> closest to
        /// <paramref name="from"/>; <paramref name="from"/> itself when it lies inside the area.
        /// </summary>
        public static Vector2Int ClosestCellInArea(Vector2Int areaOrigin, Vector2Int areaSize, Vector2Int from)
        {
            // Clamping each axis on its own is enough: the area is an axis-aligned rectangle.
            return new Vector2Int(
                Mathf.Clamp(from.x, areaOrigin.x, areaOrigin.x + areaSize.x - 1),
                Mathf.Clamp(from.y, areaOrigin.y, areaOrigin.y + areaSize.y - 1));
        }

        /// <summary>Chebyshev distance: steps between two cells when a diagonal step costs the same as a straight one.</summary>
        public static int Chebyshev(Vector2Int a, Vector2Int b)
        {
            return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }

        /// <summary>Chebyshev distance from <paramref name="from"/> to the nearest cell of the area; 0 inside it.</summary>
        public static int ToArea(Vector2Int from, Vector2Int areaOrigin, Vector2Int areaSize)
        {
            return Chebyshev(from, ClosestCellInArea(areaOrigin, areaSize, from));
        }
    }
}
