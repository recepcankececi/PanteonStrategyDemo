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
    public sealed class Building : MonoBehaviour
    {
        /// <summary>Type of this building.</summary>
        public BuildingDefinition Definition { get; private set; }

        /// <summary>Bottom-left cell of the covered area.</summary>
        public Vector2Int Origin { get; private set; }

        /// <summary>Id written into the <see cref="StrategyDemo.Core.GridMap"/> cells this building covers.</summary>
        public int OccupantId { get; private set; }

        /// <summary>Cells covered on the board, in columns × rows.</summary>
        public Vector2Int Footprint => Definition.Footprint;

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
            GetComponent<SpriteRenderer>().sprite = definition.Sprite;
        }
    }
}
