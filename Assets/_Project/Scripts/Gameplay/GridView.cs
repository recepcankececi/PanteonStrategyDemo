using System;
using StrategyDemo.Core;
using StrategyDemo.Data;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Scene side of the grid: draws the ground and converts between cells and world positions.
    /// The board's bottom-left corner is this transform's position at <see cref="Init"/>; the board does
    /// not move afterwards. Holds no occupancy (see <see cref="GridMap"/>).
    /// </summary>
    public sealed class GridView : MonoBehaviour
    {
        [SerializeField, Tooltip("Tiled sprite covering the whole board; its sprite pivot must be centred.")]
        private SpriteRenderer _ground;

        // Cached so bounds, ground and conversions always agree, even if the transform is touched later.
        private Vector3 _origin;

        /// <summary>World rectangle covered by the board. Valid after <see cref="Init"/>.</summary>
        public Rect WorldBounds { get; private set; }

        /// <summary>Sizes the ground to the board described by <paramref name="config"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The ground renderer is not assigned.</exception>
        public void Init(GridConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (_ground == null)
            {
                throw new InvalidOperationException($"{nameof(GridView)} on '{name}' has no ground renderer assigned.");
            }

            _origin = transform.position;
            Vector2 boardSize = new Vector2(config.Width, config.Height) * GridConfig.CellWorldSize;
            WorldBounds = new Rect(_origin, boardSize);

            // Tiled draw mode repeats the grass centre and keeps the sliced border on the board edge.
            // Changing size rebuilds the mesh only; the shared material is untouched, so batching holds.
            _ground.size = boardSize;
            Vector2 center = WorldBounds.center;
            _ground.transform.position = new Vector3(center.x, center.y, _ground.transform.position.z);
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

        /// <summary>World position of the bottom-edge middle of the area at <paramref name="areaOrigin"/> (a building's pivot).</summary>
        public Vector3 FootprintBaseCenter(Vector2Int areaOrigin, Vector2Int footprint)
        {
            return GridCoordinates.FootprintBaseCenter(areaOrigin, footprint, _origin, GridConfig.CellWorldSize);
        }
    }
}
