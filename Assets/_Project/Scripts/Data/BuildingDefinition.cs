using System;
using System.Collections.Generic;
using UnityEngine;

namespace StrategyDemo.Data
{
    /// <summary>
    /// Static description of a building type: name, image, footprint, health and what it can produce.
    /// </summary>
    /// <remarks>
    /// The footprint covers only the building's base (the walls standing on the ground). The sprite's
    /// pivot sits at the bottom centre of that base, so the roof is drawn over the cells above the
    /// footprint without occupying them; Y-based sorting keeps the overlap correct (ADR-003, ADR-004).
    /// </remarks>
    [CreateAssetMenu(fileName = "BuildingDefinition", menuName = "Strategy Demo/Building Definition")]
    public sealed class BuildingDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField, Tooltip("Board image; also used as the icon. Pivot at the bottom centre of the base.")]
        private Sprite _sprite;
        [SerializeField, Tooltip("Cells covered by the base (not the roof). Width must match the sprite's width in cells.")]
        private Vector2Int _footprint = Vector2Int.one;
        [SerializeField, Min(1)] private int _maxHealth = 1;
        [SerializeField, Tooltip("Units this building can produce. Empty = no production menu (R5).")]
        private UnitDefinition[] _producibleUnits = Array.Empty<UnitDefinition>();
        [SerializeField, Tooltip("Cell, relative to the footprint's bottom-left cell, where produced units appear.")]
        private Vector2Int _spawnOffset;

        /// <summary>Name shown to the player.</summary>
        public string DisplayName => _displayName;

        /// <summary>Board image, also used as the menu and information-panel icon.</summary>
        public Sprite Sprite => _sprite;

        /// <summary>Cells covered on the board, in columns × rows. Always at least 1×1.</summary>
        public Vector2Int Footprint => _footprint;

        /// <summary>Health a newly placed building starts with.</summary>
        public int MaxHealth => _maxHealth;

        /// <summary>Units this building can produce, in menu order. Read-only by contract: never cast back to an array.</summary>
        public IReadOnlyList<UnitDefinition> ProducibleUnits => _producibleUnits;

        /// <summary>True when the building has a production menu.</summary>
        public bool CanProduceUnits => _producibleUnits.Length > 0;

        /// <summary>Spawn cell relative to the footprint's bottom-left cell.</summary>
        public Vector2Int SpawnOffset => _spawnOffset;

        private void OnValidate()
        {
            // [Min] does not apply to Vector2Int; a 0-cell building would be placeable anywhere, even on others.
            _footprint = Vector2Int.Max(_footprint, Vector2Int.one);
        }
    }
}
