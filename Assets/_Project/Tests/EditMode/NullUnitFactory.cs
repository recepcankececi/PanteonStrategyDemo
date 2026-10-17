using StrategyDemo.Data;
using StrategyDemo.Gameplay;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>
    /// Factory that creates no scene object and keeps released soldiers where they are, for tests whose soldiers are
    /// built by hand (<see cref="TestBoard.CreateUnitObject"/>) and destroyed by the test.
    /// </summary>
    internal sealed class NullUnitFactory : IUnitFactory
    {
        public int ReleasedCount { get; private set; }

        public IBoardOccupant Create(UnitDefinition definition, Vector2Int cell, int occupantId)
        {
            return new FakeOccupant(occupantId, cell, Vector2Int.one, isMobile: true);
        }

        public void Release(Unit unit)
        {
            // Reset like the real pool does, so a test holding a stale reference sees what the game would see.
            unit.ResetState();
            ReleasedCount++;
        }
    }
}
