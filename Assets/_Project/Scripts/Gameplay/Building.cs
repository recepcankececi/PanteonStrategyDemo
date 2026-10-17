using System;
using StrategyDemo.Data;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// A building standing on the board: which type it is and which cells it covers.
    /// Runtime state lives here, never on the shared <see cref="BuildingDefinition"/> asset (ADR-001 Karar 4).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class Building : MonoBehaviour, IBoardOccupant
    {
        private SpriteRenderer _renderer;

        /// <summary>Type of this building.</summary>
        public BuildingDefinition Definition { get; private set; }

        /// <inheritdoc/>
        public Vector2Int Origin { get; private set; }

        /// <inheritdoc/>
        public int OccupantId { get; private set; }

        /// <inheritdoc/>
        public Vector2Int Footprint => Definition.Footprint;

        /// <inheritdoc/>
        public bool IsMobile => false;

        /// <summary>Shows <paramref name="definition"/> standing on the area at <paramref name="origin"/>.</summary>
        /// <param name="worldPosition">Bottom-edge middle of the area; the sprite's pivot goes here.</param>
        /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
        public void Init(BuildingDefinition definition, Vector2Int origin, int occupantId, Vector3 worldPosition)
        {
            Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            Origin = origin;
            OccupantId = occupantId;
            // The id keeps names unique in the hierarchy, e.g. "Barracks_3".
            name = $"{definition.name}_{occupantId}";
            transform.position = worldPosition;
            Renderer.sprite = definition.Sprite;
        }

        /// <inheritdoc/>
        public bool ContainsPoint(Vector2 worldPoint)
        {
            // The sprite's rectangle, roof included; transparent corners count too, which is fine for a click target.
            Bounds bounds = Renderer.bounds;
            return worldPoint.x >= bounds.min.x && worldPoint.x <= bounds.max.x
                && worldPoint.y >= bounds.min.y && worldPoint.y <= bounds.max.y;
        }

        // Lazy so it also works on objects whose Awake has not run (EditMode tests).
        private SpriteRenderer Renderer => _renderer != null ? _renderer : _renderer = GetComponent<SpriteRenderer>();
    }
}
