using StrategyDemo.Gameplay;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>A board occupant without a GameObject, for tests about grid and selection rules.</summary>
    internal sealed class FakeOccupant : IBoardOccupant
    {
        private readonly Rect _image;

        /// <param name="image">World rectangle of its image for click tests; empty (never hit) by default.</param>
        public FakeOccupant(int occupantId, Vector2Int origin, Vector2Int footprint, bool isMobile = false, Rect image = default)
        {
            OccupantId = occupantId;
            Origin = origin;
            Footprint = footprint;
            IsMobile = isMobile;
            _image = image;
        }

        public int OccupantId { get; }

        public Vector2Int Origin { get; }

        public Vector2Int Footprint { get; }

        public bool IsMobile { get; }

        public bool ContainsPoint(Vector2 worldPoint)
        {
            return _image.width > 0f && _image.Contains(worldPoint);
        }
    }
}
