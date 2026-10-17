using UnityEngine;

namespace StrategyDemo.Core
{
    /// <summary>Conversions between grid cells and world positions for a board whose bottom-left corner is <c>origin</c>.</summary>
    public static class GridCoordinates
    {
        /// <summary>World position of the centre of <paramref name="cell"/>.</summary>
        public static Vector3 CellToWorld(Vector2Int cell, Vector3 origin, float cellSize)
        {
            return new Vector3(
                origin.x + (cell.x + 0.5f) * cellSize,
                origin.y + (cell.y + 0.5f) * cellSize,
                origin.z);
        }

        /// <summary>Cell containing <paramref name="world"/>. Positions left of or below the board give negative cells.</summary>
        public static Vector2Int WorldToCell(Vector3 world, Vector3 origin, float cellSize)
        {
            // Floor, not a cast: a cast truncates toward zero and would map -0.5 to cell 0 instead of -1.
            return new Vector2Int(
                Mathf.FloorToInt((world.x - origin.x) / cellSize),
                Mathf.FloorToInt((world.y - origin.y) / cellSize));
        }

        /// <summary>
        /// Bottom-left cell of a <paramref name="footprint"/>-sized area whose centre is as close as
        /// possible to <paramref name="world"/>; used to keep the cursor in the middle of a building preview.
        /// The area may be partly or wholly off the board.
        /// </summary>
        public static Vector2Int FootprintOriginAt(Vector3 world, Vector3 origin, float cellSize, Vector2Int footprint)
        {
            // The area's centre lies footprint/2 cells right of and above its origin, so the best origin is the
            // cursor (in cells) minus half the footprint, rounded to the nearest whole cell. Floor(x + 0.5)
            // rounds .5 the same way on both sides of zero; Mathf.Round would round .5 to the even neighbour.
            return new Vector2Int(
                Mathf.FloorToInt((world.x - origin.x) / cellSize - footprint.x * 0.5f + 0.5f),
                Mathf.FloorToInt((world.y - origin.y) / cellSize - footprint.y * 0.5f + 0.5f));
        }

        /// <summary>
        /// World position of the middle of the bottom edge of the area starting at <paramref name="areaOrigin"/>:
        /// where a building's sprite pivot (bottom centre of its base) goes.
        /// </summary>
        public static Vector3 FootprintBaseCenter(Vector2Int areaOrigin, Vector2Int footprint, Vector3 origin, float cellSize)
        {
            return new Vector3(
                origin.x + (areaOrigin.x + footprint.x * 0.5f) * cellSize,
                origin.y + areaOrigin.y * cellSize,
                origin.z);
        }
    }
}
