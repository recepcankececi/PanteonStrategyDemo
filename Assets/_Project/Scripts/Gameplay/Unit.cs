using System;
using StrategyDemo.Data;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// A soldier standing on the board: its type and the one cell it holds. Pooled, so <see cref="Init"/> and
    /// <see cref="ResetState"/> fully define its state. Runtime state lives here, never on the shared
    /// <see cref="UnitDefinition"/> asset (ADR-001 Karar 4).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(SpriteFlipbook))]
    public sealed class Unit : MonoBehaviour, IBoardOccupant
    {
        private SpriteFlipbook _flipbook;
        private SpriteRenderer _renderer;

        /// <summary>Type of this soldier; null while waiting in the pool.</summary>
        public UnitDefinition Definition { get; private set; }

        /// <inheritdoc/>
        public int OccupantId { get; private set; }

        /// <inheritdoc/>
        public Vector2Int Origin { get; private set; }

        /// <inheritdoc/>
        public Vector2Int Footprint => Vector2Int.one;

        /// <inheritdoc/>
        public bool IsMobile => true;

        /// <summary>Shows <paramref name="definition"/> standing on <paramref name="cell"/> and starts its idle animation.</summary>
        /// <param name="worldPosition">Bottom-edge middle of the cell; the sprite's pivot goes here.</param>
        /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="definition"/> has no idle frames.</exception>
        public void Init(UnitDefinition definition, Vector2Int cell, int occupantId, Vector3 worldPosition)
        {
            Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            if (definition.IdleFrames.Count == 0)
            {
                throw new ArgumentException($"Unit '{definition.name}' has no idle frames.", nameof(definition));
            }

            OccupantId = occupantId;
            Origin = cell;
            // The id keeps names unique in the hierarchy, e.g. "Soldier1_7".
            name = $"{definition.name}_{occupantId}";
            transform.position = worldPosition;
            // Starting frame from the id: soldiers of one type produced in a row do not bob in lockstep.
            Flipbook.Play(definition.IdleFrames, definition.IdleFramesPerSecond, occupantId);
        }

        /// <summary>Clears all state before the soldier goes back to the pool, so reuse cannot leak the old soldier.</summary>
        public void ResetState()
        {
            Flipbook.Stop();
            Renderer.sprite = null;
            Definition = null;
            OccupantId = 0;
            Origin = default;
        }

        /// <inheritdoc/>
        public bool ContainsPoint(Vector2 worldPoint)
        {
            Bounds bounds = Renderer.bounds;
            return Renderer.sprite != null
                && worldPoint.x >= bounds.min.x && worldPoint.x <= bounds.max.x
                && worldPoint.y >= bounds.min.y && worldPoint.y <= bounds.max.y;
        }

        // Lazy: the pool configures soldiers before their first Awake could run (and EditMode tests never run it).
        private SpriteFlipbook Flipbook => _flipbook != null ? _flipbook : _flipbook = GetComponent<SpriteFlipbook>();

        private SpriteRenderer Renderer => _renderer != null ? _renderer : _renderer = GetComponent<SpriteRenderer>();
    }
}
