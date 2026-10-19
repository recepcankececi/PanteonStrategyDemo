using System;
using StrategyDemo.Core;
using StrategyDemo.Data;
using UnityEngine;

namespace StrategyDemo.Gameplay.Board
{
    /// <summary>
    /// Scene side of the grid: draws the ground and converts between cells and world positions.
    /// The board's bottom-left corner is this transform's position at <see cref="Init"/>; the board does
    /// not move afterwards. Holds no occupancy (see <see cref="GridMap"/>).
    /// </summary>
    public sealed class GridView : MonoBehaviour
    {
        [SerializeField, Tooltip("Tiled one-cell ground sprite stretched over the whole board; its pivot must be centred.")]
        private SpriteRenderer _ground;

        [SerializeField, Tooltip("Sliced frame drawing the grid line on the board's right and bottom edges; centred pivot.")]
        private SpriteRenderer _edge;

        // Cached so bounds, ground and conversions always agree, even if the transform is touched later.
        private Vector3 _origin;

        /// <summary>World rectangle covered by the board. Valid after <see cref="Init"/>.</summary>
        public Rect WorldBounds { get; private set; }

        /// <summary>Board size in cells (columns, rows). Valid after <see cref="Init"/>.</summary>
        public Vector2Int BoardSize { get; private set; }

        /// <summary>Sizes the ground to the board described by <paramref name="config"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The ground or edge renderer is not assigned.</exception>
        public void Init(GridConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (_ground == null || _edge == null)
            {
                throw new InvalidOperationException($"{nameof(GridView)} on '{name}' has no ground or edge renderer assigned.");
            }

            _origin = transform.position;
            BoardSize = new Vector2Int(config.Width, config.Height);
            Vector2 boardSize = new Vector2(config.Width, config.Height) * GridConfig.CellWorldSize;
            WorldBounds = new Rect(_origin, boardSize);

            // Tiled draw mode repeats the one-cell ground tile; the line on its edge draws the grid.
            // Changing size rebuilds the mesh only; the shared material is untouched, so batching holds.
            _ground.size = boardSize;
            Vector2 center = WorldBounds.center;
            _ground.transform.position = new Vector3(center.x, center.y, _ground.transform.position.z);
            // Each tile draws its line on its top and left edges only, so the board's right and bottom edges would
            // have none. A sliced frame (1 px border, line on its right and bottom) closes them at any board size.
            _edge.size = boardSize;
            _edge.transform.position = new Vector3(center.x, center.y, _edge.transform.position.z);
        }

        /// <summary>World position of the centre of <paramref name="cell"/>.</summary>
        public Vector3 CellToWorld(Vector2Int cell)
        {
            return GridCoordinates.CellToWorld(cell, _origin, GridConfig.CellWorldSize);
        }

        /// <summary>Cell under <paramref name="worldPosition"/>; may be off the board.</summary>
        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            return GridCoordinates.WorldToCell(worldPosition, _origin, GridConfig.CellWorldSize);
        }

        /// <summary>Bottom-left cell of a <paramref name="footprint"/> centred under <paramref name="worldPosition"/>; may be off the board.</summary>
        public Vector2Int FootprintOriginAt(Vector3 worldPosition, Vector2Int footprint)
        {
            return GridCoordinates.FootprintOriginAt(worldPosition, _origin, GridConfig.CellWorldSize, footprint);
        }

        /// <summary>World position of the centre of the area at <paramref name="areaOrigin"/> (a building's or soldier's pivot).</summary>
        public Vector3 FootprintCenter(Vector2Int areaOrigin, Vector2Int footprint)
        {
            return GridCoordinates.FootprintCenter(areaOrigin, footprint, _origin, GridConfig.CellWorldSize);
        }
    }
}
