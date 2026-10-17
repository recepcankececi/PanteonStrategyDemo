using StrategyDemo.Data;
using StrategyDemo.Gameplay;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Factory that creates nothing, for tests about placement rules and visuals only.</summary>
    internal sealed class NullBuildingFactory : IBuildingFactory
    {
        public void Create(BuildingDefinition definition, Vector2Int origin, int occupantId)
        {
        }
    }
}
