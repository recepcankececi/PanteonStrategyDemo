using StrategyDemo.Data;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Puts a soldier's scene object on the board and takes it off again. Kept behind an interface so the production rules
    /// (<see cref="UnitProducer"/>) are tested without creating GameObjects.
    /// </summary>
    public interface IUnitFactory
    {
        /// <summary>Creates (or reuses) the scene object for a soldier that already holds its cell in the grid.</summary>
        /// <returns>The soldier; never null.</returns>
        IBoardOccupant Create(UnitDefinition definition, Vector2Int cell, int occupantId);

        /// <summary>Takes a dead soldier's scene object off the board. Its grid cell is the caller's to free.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="unit"/> is null.</exception>
        /// <exception cref="System.InvalidOperationException"><paramref name="unit"/> is not active from this factory.</exception>
        void Release(Unit unit);
    }
}
