using System;
using System.Collections.Generic;
using UnityEngine;

namespace StrategyDemo.Data
{
    /// <summary>
    /// Static description of a building type: name, image, health and what it can produce.
    /// The footprint is not stored; it is derived from the sprite so art and grid can never disagree.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingDefinition", menuName = "Strategy Demo/Building Definition")]
    public sealed class BuildingDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField, Tooltip("Board image; also used as the icon. Its pixel size defines the footprint.")]
        private Sprite _sprite;
        [SerializeField, Min(1)] private int _maxHealth = 1;
        [SerializeField, Tooltip("Units this building can produce. Empty = no production menu (R5).")]
        private UnitDefinition[] _producibleUnits = Array.Empty<UnitDefinition>();
        [SerializeField, Tooltip("Cell, relative to the footprint's bottom-left cell, where produced units appear.")]
        private Vector2Int _spawnOffset;

        /// <summary>Name shown to the player.</summary>
        public string DisplayName => _displayName;

        /// <summary>Board image, also used as the menu and information-panel icon.</summary>
        public Sprite Sprite => _sprite;

        /// <summary>Health a newly placed building starts with.</summary>
        public int MaxHealth => _maxHealth;

        /// <summary>Units this building can produce, in menu order. Read-only by contract: never cast back to an array.</summary>
        public IReadOnlyList<UnitDefinition> ProducibleUnits => _producibleUnits;

        /// <summary>True when the building has a production menu.</summary>
        public bool CanProduceUnits => _producibleUnits.Length > 0;

        /// <summary>Spawn cell relative to the footprint's bottom-left cell.</summary>
        public Vector2Int SpawnOffset => _spawnOffset;

        /// <summary>Cells covered by this building on a grid of <paramref name="cellPixelSize"/>-pixel cells.</summary>
        /// <exception cref="InvalidOperationException">No sprite is assigned.</exception>
        public Vector2Int GetFootprint(int cellPixelSize)
        {
            if (_sprite == null)
            {
                throw new InvalidOperationException($"Building '{name}' has no sprite, so its footprint is unknown.");
            }

            return Footprint.FromPixelSize(_sprite.rect.size, cellPixelSize);
        }
    }
}
