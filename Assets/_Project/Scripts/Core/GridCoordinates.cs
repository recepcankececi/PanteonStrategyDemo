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
    }
}
