using StrategyDemo.Data;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Puts a soldier's scene object on the board. Kept behind an interface so the production rules
    /// (<see cref="UnitProducer"/>) are tested without creating GameObjects.
    /// </summary>
    public interface IUnitFactory
    {
        /// <summary>Creates (or reuses) the scene object for a soldier that already holds its cell in the grid.</summary>
        /// <returns>The soldier; never null.</returns>
        IBoardOccupant Create(UnitDefinition definition, Vector2Int cell, int occupantId);
    }
}
