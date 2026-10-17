using System;
using System.Collections.Generic;
using UnityEngine;

namespace StrategyDemo.Data
{
    /// <summary>
    /// Static description of a building type: name, image, footprint, health and what it can produce.
    /// </summary>
    /// <remarks>
    /// The sprite is exactly the footprint (footprint × cell pixels) with its pivot at the centre, so a building
    /// draws on its own cells and nowhere else. EditMode tests enforce this for every catalog entry.
    /// </remarks>
    [CreateAssetMenu(fileName = "BuildingDefinition", menuName = "Strategy Demo/Building Definition")]
    public sealed class BuildingDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField, Tooltip("Board image; also used as the icon. Exactly footprint × cell pixels, pivot centred.")]
        private Sprite _sprite;
        [SerializeField, Tooltip("Cells covered on the board. Must match the sprite's size in cells.")]
        private Vector2Int _footprint = Vector2Int.one;
        [SerializeField, Min(1)] private int _maxHealth = 1;
        [SerializeField, Tooltip("Units this building can produce. Empty = no production menu (R5).")]
        private UnitDefinition[] _producibleUnits = Array.Empty<UnitDefinition>();

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

        /// <summary>True when the building has a production menu, and so a spawn cell chosen when it is placed.</summary>
        public bool CanProduceUnits => _producibleUnits.Length > 0;

        private void OnValidate()
        {
            // [Min] does not apply to Vector2Int; a 0-cell building would be placeable anywhere, even on others.
            _footprint = Vector2Int.Max(_footprint, Vector2Int.one);
        }
    }
}
