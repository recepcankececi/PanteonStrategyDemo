using StrategyDemo.Data;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Puts a building's scene object on the board and takes it off again. Kept behind an interface so the placement rules
    /// (<see cref="BuildingPlacer"/>) are tested without creating GameObjects.
    /// </summary>
    public interface IBuildingFactory
    {
        /// <summary>Creates the scene object for a building that already holds its cells in the grid.</summary>
        /// <returns>The created building; never null.</returns>
        IBoardOccupant Create(BuildingDefinition definition, Vector2Int origin, int occupantId);

        /// <summary>Takes a destroyed building's scene object off the board. Its grid cells are the caller's to free.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="building"/> is null.</exception>
        /// <exception cref="System.InvalidOperationException"><paramref name="building"/> is not active from this factory.</exception>
        void Release(Building building);
    }
}
