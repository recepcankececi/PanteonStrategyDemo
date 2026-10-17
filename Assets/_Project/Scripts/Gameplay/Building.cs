using System;
using StrategyDemo.Data;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// A building standing on the board: which type it is, which cells it covers and its health.
    /// Pooled, so <see cref="Init"/> and <see cref="ResetState"/> fully define its state. Runtime state lives here,
    /// never on the shared <see cref="BuildingDefinition"/> asset.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class Building : MonoBehaviour, IDamageable
    {
        [SerializeField, Tooltip("Bar drawn at the top of the footprint.")] private HealthBar _healthBar;
        [SerializeField, Tooltip("Flash on this renderer when the building is hit.")] private HitFlash _hitFlash;

        private readonly Health _health = new Health();
        private SpriteRenderer _renderer;
        // The factory this building goes back to when it is destroyed; null while in the pool.
        private IBuildingFactory _owner;

        /// <summary>Type of this building; null while waiting in the pool.</summary>
        public BuildingDefinition Definition { get; private set; }

        /// <inheritdoc/>
        public Vector2Int Origin { get; private set; }

        /// <inheritdoc/>
        public int OccupantId { get; private set; }

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">The building is not initialised (it is in the pool).</exception>
        public Vector2Int Footprint => Definition != null
            ? Definition.Footprint
            // A stale reference held after the building was destroyed must fail loudly and clearly.
            : throw new InvalidOperationException($"{nameof(Building)} '{name}' is not initialised.");

        /// <inheritdoc/>
        public bool IsMobile => false;

        /// <inheritdoc/>
        public Vector3 WorldCenter => transform.position;

        /// <inheritdoc/>
        public Health Health => _health;

        /// <summary>Shows <paramref name="definition"/> standing on the area at <paramref name="origin"/>, at full health.</summary>
        /// <param name="worldPosition">Centre of the area; the sprite's pivot goes here.</param>
        /// <param name="owner">Factory that <see cref="IDespawnable.ReturnToPool"/> hands the building back to; null only in tests
        /// that never despawn it.</param>
        /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The health bar or hit flash is not assigned.</exception>
        public void Init(BuildingDefinition definition, Vector2Int origin, int occupantId, Vector3 worldPosition, IBuildingFactory owner = null)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (_healthBar == null || _hitFlash == null)
            {
                throw new InvalidOperationException($"{nameof(Building)} '{name}' has no health bar or hit flash assigned.");
            }

            Definition = definition;
            _owner = owner;
            Origin = origin;
            OccupantId = occupantId;
            // The id keeps names unique in the hierarchy, e.g. "Barracks_3".
            name = $"{definition.name}_{occupantId}";
            transform.position = worldPosition;
            Renderer.sprite = definition.Sprite;
            _healthBar.Bind(_health, definition.Footprint);
            _hitFlash.Bind(_health);
            _health.Reset(definition.MaxHealth);
        }

        /// <inheritdoc/>
        public void PinHealthBar(bool pinned)
        {
            _healthBar.SetPinned(pinned);
        }

        // Explicit: only the despawner, which holds an IDespawnable, can take the object off the board; a caller
        // holding the concrete type cannot skip the registry, selection and event steps.
        /// <inheritdoc/>
        void IDespawnable.LeaveBoard(DespawnContext board)
        {
            // A building holds nothing but its footprint cells.
            board.Grid.Release(Origin, Footprint, OccupantId);
        }

        /// <inheritdoc/>
        void IDespawnable.ReturnToPool()
        {
            // Read first: the factory resets the building, which clears the owner.
            IBuildingFactory owner = _owner ?? throw new InvalidOperationException($"{nameof(Building)} '{name}' was not made by a factory.");
            owner.Release(this);
        }

        /// <summary>Clears all state before the building goes back to the pool, so reuse cannot leak the old building.</summary>
        public void ResetState()
        {
            // Clear first: it drops every listener at once, so the views' unsubscribes below find nothing to remove.
            // Removing one listener of several would allocate a new delegate on every destruction.
            _health.Clear();
            if (_healthBar != null)
            {
                _healthBar.Unbind();
            }

            if (_hitFlash != null)
            {
                _hitFlash.Unbind();
            }

            Renderer.sprite = null;
            Definition = null;
            _owner = null;
            OccupantId = 0;
            Origin = default;
        }

        // Lazy so it also works on objects whose Awake has not run (EditMode tests).
        private SpriteRenderer Renderer => _renderer != null ? _renderer : _renderer = GetComponent<SpriteRenderer>();
    }
}
