using System;
using StrategyDemo.Data;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Gameplay
{
    /// <summary>Instantiates placed buildings from one prefab under a named board container.</summary>
    /// <remarks>
    /// Not pooled yet: buildings are only created (at click rate), never destroyed, so there is nothing to
    /// reuse. Pooling arrives with destruction in M6 (R10), when create/destroy churn exists.
    /// </remarks>
    public sealed class BuildingFactory : IBuildingFactory
    {
        private readonly Building _prefab;
        private readonly Transform _container;
        private readonly GridView _gridView;

        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public BuildingFactory(Building prefab, Transform container, GridView gridView)
        {
            _prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            _container = container != null ? container : throw new ArgumentNullException(nameof(container));
            _gridView = gridView != null ? gridView : throw new ArgumentNullException(nameof(gridView));
        }

        /// <inheritdoc/>
        public IBoardOccupant Create(BuildingDefinition definition, Vector2Int origin, int occupantId)
        {
            Building building = Object.Instantiate(_prefab, _container);
            building.Init(definition, origin, occupantId, _gridView.FootprintCenter(origin, definition.Footprint));
            return building;
        }
    }
}
