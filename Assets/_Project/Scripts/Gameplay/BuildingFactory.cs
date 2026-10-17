using System;
using StrategyDemo.Core;
using StrategyDemo.Data;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Takes buildings from an <see cref="ObjectPool{T}"/>: a building is instantiated only when no destroyed one is
    /// waiting. Active buildings live under the buildings container, released ones under the pool container.
    /// </summary>
    /// <remarks>
    /// Buildings are placed without limit (R3) and destroyed in combat (R10): the create/destroy churn pooling exists
    /// for. One prefab serves every type; <see cref="Building.Init"/> applies the type.
    /// </remarks>
    public sealed class BuildingFactory : IBuildingFactory
    {
        private readonly Building _prefab;
        private readonly Transform _activeContainer;
        private readonly Transform _poolContainer;
        private readonly GridView _gridView;
        private readonly ObjectPool<Building> _pool;

        /// <param name="prefab">Building prefab.</param>
        /// <param name="activeContainer">Parent of buildings on the board.</param>
        /// <param name="poolContainer">Parent of destroyed buildings waiting in the pool.</param>
        /// <param name="gridView">Converts cells to world positions.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public BuildingFactory(Building prefab, Transform activeContainer, Transform poolContainer, GridView gridView)
        {
            _prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            _activeContainer = activeContainer != null ? activeContainer : throw new ArgumentNullException(nameof(activeContainer));
            _poolContainer = poolContainer != null ? poolContainer : throw new ArgumentNullException(nameof(poolContainer));
            _gridView = gridView != null ? gridView : throw new ArgumentNullException(nameof(gridView));
            _pool = new ObjectPool<Building>(CreateBuilding, TakeFromPool, ReturnToPool);
        }

        /// <summary>Buildings ever instantiated; stays flat while destroyed buildings are reused.</summary>
        public int CreatedCount => _pool.CountAll;

        /// <inheritdoc/>
        public IBoardOccupant Create(BuildingDefinition definition, Vector2Int origin, int occupantId)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            Building building = _pool.Get();
            try
            {
                building.Init(definition, origin, occupantId, _gridView.FootprintCenter(origin, definition.Footprint));
            }
            catch
            {
                // A building that failed to initialise must not stay on the board half-configured.
                _pool.Release(building);
                throw;
            }

            return building;
        }

        /// <inheritdoc/>
        public void Release(Building building)
        {
            _pool.Release(building);
        }

        private Building CreateBuilding()
        {
            // Created inactive under the pool, like a released building; TakeFromPool moves it to the board.
            Building building = Object.Instantiate(_prefab, _poolContainer);
            building.name = _prefab.name;
            building.gameObject.SetActive(false);
            return building;
        }

        private void TakeFromPool(Building building)
        {
            building.transform.SetParent(_activeContainer, false);
            building.gameObject.SetActive(true);
        }

        private void ReturnToPool(Building building)
        {
            building.ResetState();
            building.gameObject.SetActive(false);
            building.transform.SetParent(_poolContainer, false);
            building.name = _prefab.name;
        }
    }
}
