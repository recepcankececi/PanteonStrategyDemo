using StrategyDemo.Data;
using StrategyDemo.Gameplay;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Factory that creates no scene object, for tests about placement rules and visuals only.</summary>
    internal sealed class NullBuildingFactory : IBuildingFactory
    {
        public IBoardOccupant Create(BuildingDefinition definition, Vector2Int origin, int occupantId)
        {
            return new FakeOccupant(occupantId, origin, definition.Footprint);
        }
    }
}
