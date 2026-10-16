using UnityEngine;

namespace StrategyDemo.Data
{
    /// <summary>Board dimensions and the pixel size of one cell.</summary>
    [CreateAssetMenu(fileName = "GridConfig", menuName = "Strategy Demo/Grid Config")]
    public sealed class GridConfig : ScriptableObject
    {
        /// <summary>
        /// World size of one cell. Board sprites are imported with Pixels Per Unit equal to
        /// <see cref="CellPixelSize"/>, so one cell is always one world unit.
        /// </summary>
        public const float CellWorldSize = 1f;

        [SerializeField, Min(1)] private int _width = 24;
        [SerializeField, Min(1)] private int _height = 16;
        [SerializeField, Min(1), Tooltip("Art pixels per cell; must equal the Pixels Per Unit of every board sprite. Changing it requires re-importing the art.")]
        private int _cellPixelSize = 64;

        /// <summary>Number of columns.</summary>
        public int Width => _width;

        /// <summary>Number of rows.</summary>
        public int Height => _height;

        /// <summary>Art pixels covered by one cell.</summary>
        public int CellPixelSize => _cellPixelSize;
    }
}
