using System;
using StrategyDemo.Core;
using StrategyDemo.Data;
using StrategyDemo.Gameplay.Board;
using StrategyDemo.Gameplay.Despawn;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Gameplay.Units
{
    /// <summary>
    /// Takes soldiers from an <see cref="ObjectPool{T}"/>: a soldier is instantiated only when no released one is
    /// waiting. Active soldiers live under the units container, released ones under the pool container.
    /// </summary>
    /// <remarks>
    /// Soldiers are produced without limit (R3) and destroyed in combat (R10), the create/destroy churn pooling
    /// exists for. <see cref="Release"/> is the return path a dead soldier takes (<see cref="IDespawnable.ReturnToPool"/>).
    /// </remarks>
    public sealed class UnitFactory : IUnitFactory
    {
        private readonly Unit _prefab;
        private readonly Transform _activeContainer;
        private readonly Transform _poolContainer;
        private readonly GridView _gridView;
        private readonly ObjectPool<Unit> _pool;
        // Read once: Object.name allocates a new string on every read, and a death must not allocate.
        private readonly string _pooledName;

        /// <param name="prefab">Soldier prefab.</param>
        /// <param name="activeContainer">Parent of soldiers on the board.</param>
        /// <param name="poolContainer">Parent of soldiers waiting in the pool.</param>
        /// <param name="gridView">Converts cells to world positions.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public UnitFactory(Unit prefab, Transform activeContainer, Transform poolContainer, GridView gridView)
        {
            _prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            _activeContainer = activeContainer != null ? activeContainer : throw new ArgumentNullException(nameof(activeContainer));
            _poolContainer = poolContainer != null ? poolContainer : throw new ArgumentNullException(nameof(poolContainer));
            _gridView = gridView != null ? gridView : throw new ArgumentNullException(nameof(gridView));
            _pooledName = prefab.name;
            _pool = new ObjectPool<Unit>(CreateUnit, TakeFromPool, ReturnToPool);
        }

        /// <summary>Soldiers ever instantiated; stays flat while released soldiers are reused.</summary>
        public int CreatedCount => _pool.CountAll;

        /// <inheritdoc/>
        public Unit Create(UnitDefinition definition, Vector2Int cell, int occupantId)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            Unit unit = _pool.Get();
            try
            {
                unit.Init(definition, cell, occupantId, _gridView.FootprintCenter(cell, Vector2Int.one), this);
            }
            catch
            {
                // A soldier that failed to initialise must not stay on the board half-configured.
                _pool.Release(unit);
                throw;
            }

            return unit;
        }

        /// <inheritdoc/>
        public void Release(Unit unit)
        {
            _pool.Release(unit);
        }

        private Unit CreateUnit()
        {
            // Created inactive under the pool, like a released soldier; TakeFromPool moves it to the board.
            Unit unit = Object.Instantiate(_prefab, _poolContainer);
            unit.name = _pooledName;
            unit.gameObject.SetActive(false);
            return unit;
        }

        private void TakeFromPool(Unit unit)
        {
            unit.transform.SetParent(_activeContainer, false);
            unit.gameObject.SetActive(true);
        }

        private void ReturnToPool(Unit unit)
        {
            unit.ResetState();
            unit.gameObject.SetActive(false);
            unit.transform.SetParent(_poolContainer, false);
            unit.name = _pooledName;
        }
    }
}
