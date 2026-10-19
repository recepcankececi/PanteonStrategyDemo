using StrategyDemo.Data;
using StrategyDemo.Gameplay.Board;
using StrategyDemo.Gameplay.Buildings;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode.Support
{
    /// <summary>
    /// Factory that creates no scene object, for tests about placement rules and visuals; released buildings are reset
    /// but stay in the scene for the test to destroy.
    /// </summary>
    internal sealed class NullBuildingFactory : IBuildingFactory
    {
        public int ReleasedCount { get; private set; }

        public IBoardOccupant Create(BuildingDefinition definition, Vector2Int origin, int occupantId, Vector2Int? spawnCell)
        {
            return new FakeOccupant(occupantId, origin, definition.Footprint);
        }

        public void Release(Building building)
        {
            // Reset like the real pool does, so a test holding a stale reference sees what the game would see.
            building.ResetState();
            ReleasedCount++;
        }
    }
}
