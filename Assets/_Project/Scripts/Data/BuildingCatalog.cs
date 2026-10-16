using System;
using System.Collections.Generic;
using UnityEngine;

namespace StrategyDemo.Data
{
    /// <summary>The buildings the production menu offers, in display order.</summary>
    [CreateAssetMenu(fileName = "BuildingCatalog", menuName = "Strategy Demo/Building Catalog")]
    public sealed class BuildingCatalog : ScriptableObject
    {
        [SerializeField] private BuildingDefinition[] _buildings = Array.Empty<BuildingDefinition>();

        /// <summary>Buildings in menu order. Read-only by contract: never cast back to an array, the asset is shared.</summary>
        public IReadOnlyList<BuildingDefinition> Buildings => _buildings;
    }
}
