using StrategyDemo.Data;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Puts a building's scene object on the board. Kept behind an interface so the placement rules
    /// (<see cref="BuildingPlacer"/>) are tested without creating GameObjects.
    /// </summary>
    public interface IBuildingFactory
    {
        /// <summary>Creates the scene object for a building that already holds its cells in the grid.</summary>
        /// <returns>The created building; never null.</returns>
        IBoardOccupant Create(BuildingDefinition definition, Vector2Int origin, int occupantId);
    }
}
