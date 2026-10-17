using StrategyDemo.Gameplay;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>A board occupant without a GameObject, for tests about grid and selection rules.</summary>
    internal sealed class FakeOccupant : IBoardOccupant
    {
        public FakeOccupant(int occupantId, Vector2Int origin, Vector2Int footprint, bool isMobile = false, Vector3 worldCenter = default)
        {
            WorldCenter = worldCenter;
            OccupantId = occupantId;
            Origin = origin;
            Footprint = footprint;
            IsMobile = isMobile;
        }

        public int OccupantId { get; }

        public Vector2Int Origin { get; }

        public Vector2Int Footprint { get; }

        public bool IsMobile { get; }

        public Vector3 WorldCenter { get; }
    }
}
