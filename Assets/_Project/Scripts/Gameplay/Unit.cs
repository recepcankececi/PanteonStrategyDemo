using System;
using StrategyDemo.Data;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// A soldier standing on the board: its type, the cell it stands in, where it faces and whether it walks.
    /// Pooled, so <see cref="Init"/> and <see cref="ResetState"/> fully define its state. Runtime state lives here,
    /// never on the shared <see cref="UnitDefinition"/> asset (ADR-001 Karar 4).
    /// </summary>
    /// <remarks>
    /// The art is top-down and faces up, so facing a direction is a rotation around Z. Only the <c>Visual</c> child
    /// turns: the root keeps position only, so anything later attached to the soldier (health bar, effects) stays
    /// upright. Rotating a <see cref="SpriteRenderer"/> changes only its vertices: the shared material and atlas stay,
    /// so batching holds.
    /// </remarks>
    public sealed class Unit : MonoBehaviour, IBoardOccupant
    {
        // Below this squared length a direction has no reliable angle (e.g. a zero step); the heading is kept.
        private const float MinHeadingSqrMagnitude = 1e-6f;
        // The art faces +Y while angles are measured from +X, so a heading's angle is offset by a quarter turn.
        private const float ArtFacingDegrees = 90f;

        [SerializeField, Tooltip("Child that draws the soldier; the only part that rotates to the heading.")]
        private SpriteFlipbook _visual;

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

        /// <inheritdoc/>
        public Vector3 WorldCenter => transform.position;

        /// <summary>True while the walk cycle plays; false while the soldier stands.</summary>
        public bool IsWalking { get; private set; }

        /// <summary>Shows <paramref name="definition"/> standing on <paramref name="cell"/>, facing up.</summary>
        /// <param name="worldPosition">Centre of the cell; the sprite's pivot goes here.</param>
        /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="definition"/> has no standing sprite or no walk frames.</exception>
        /// <exception cref="InvalidOperationException">The visual child is not assigned.</exception>
        public void Init(UnitDefinition definition, Vector2Int cell, int occupantId, Vector3 worldPosition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            // Checked here, not when the soldier first walks: bad data should fail at production, next to its cause.
            if (definition.Sprite == null || definition.WalkFrames.Count == 0)
            {
                throw new ArgumentException($"Unit '{definition.name}' needs a sprite and walk frames.", nameof(definition));
            }

            if (_visual == null)
            {
                throw new InvalidOperationException($"{nameof(Unit)} '{name}' has no visual assigned.");
            }

            Definition = definition;
            OccupantId = occupantId;
            Origin = cell;
            // The id keeps names unique in the hierarchy, e.g. "Assault_7".
            name = $"{definition.name}_{occupantId}";
            transform.position = worldPosition;
            _visual.transform.localRotation = Quaternion.identity;
            IsWalking = false;
            ShowStanding();
        }

        /// <summary>
        /// Turns the soldier to face <paramref name="direction"/> (world space, any length). A zero-length direction
        /// keeps the current heading.
        /// </summary>
        /// <exception cref="InvalidOperationException">The soldier is not initialised (it is in the pool).</exception>
        public void FaceTowards(Vector2 direction)
        {
            ThrowIfPooled();
            if (direction.sqrMagnitude < MinHeadingSqrMagnitude)
            {
                return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - ArtFacingDegrees;
            _visual.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>
        /// Plays the walk cycle while <paramref name="walking"/>, shows the standing sprite otherwise. Repeating the
        /// current state does nothing, so movement can call this every step without restarting the cycle.
        /// </summary>
        /// <exception cref="InvalidOperationException">The soldier is not initialised (it is in the pool).</exception>
        public void SetWalking(bool walking)
        {
            ThrowIfPooled();
            if (walking == IsWalking)
            {
                return;
            }

            IsWalking = walking;
            if (walking)
            {
                // Starting frame from the id: soldiers ordered to move together do not step in lockstep.
                _visual.Play(Definition.WalkFrames, Definition.WalkFramesPerSecond, OccupantId);
            }
            else
            {
                ShowStanding();
            }
        }

        /// <summary>
        /// Records that the soldier's centre is now in <paramref name="cell"/>. Only <see cref="UnitMovement"/> calls
        /// this; it also keeps the grid in step (a walking soldier holds its cell there whenever no other soldier does).
        /// </summary>
        internal void SetCell(Vector2Int cell)
        {
            ThrowIfPooled();
            Origin = cell;
        }

        /// <summary>Clears all state before the soldier goes back to the pool, so reuse cannot leak the old soldier.</summary>
        public void ResetState()
        {
            if (_visual != null)
            {
                _visual.Stop();
                _visual.transform.localRotation = Quaternion.identity;
                Renderer.sprite = null;
            }

            IsWalking = false;
            Definition = null;
            OccupantId = 0;
            Origin = default;
        }

        private void ShowStanding()
        {
            _visual.Stop();
            Renderer.sprite = Definition.Sprite;
        }

        private void ThrowIfPooled()
        {
            // A stale reference held by movement or combat must fail loudly, not animate a soldier in the pool.
            if (Definition == null)
            {
                throw new InvalidOperationException($"{nameof(Unit)} '{name}' is not initialised.");
            }
        }

        // Lazy: the pool configures soldiers before their first Awake could run (and EditMode tests never run it).
        private SpriteRenderer Renderer => _renderer != null ? _renderer : _renderer = _visual.GetComponent<SpriteRenderer>();
    }
}
