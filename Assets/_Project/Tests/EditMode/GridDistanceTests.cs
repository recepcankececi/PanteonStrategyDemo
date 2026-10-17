using NUnit.Framework;
using StrategyDemo.Core;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Attack distance: Chebyshev steps to the nearest cell of a footprint (ADR-001 Karar 5).</summary>
    public class GridDistanceTests
    {
        private static readonly Vector2Int BarracksOrigin = new Vector2Int(4, 4);
        private static readonly Vector2Int BarracksSize = new Vector2Int(3, 3);

        [Test]
        public void Chebyshev_DiagonalStep_CountsAsOne()
        {
            Assert.That(GridDistance.Chebyshev(new Vector2Int(2, 2), new Vector2Int(3, 3)), Is.EqualTo(1));
            Assert.That(GridDistance.Chebyshev(new Vector2Int(0, 0), new Vector2Int(3, 1)), Is.EqualTo(3));
        }

        [TestCase(0, 0, 4, 4)]
        [TestCase(5, 0, 5, 4)]
        [TestCase(9, 9, 6, 6)]
        [TestCase(5, 5, 5, 5)]
        public void ClosestCellInArea_ClampsEachAxisIntoTheFootprint(int fromX, int fromY, int expectedX, int expectedY)
        {
            Vector2Int closest = GridDistance.ClosestCellInArea(BarracksOrigin, BarracksSize, new Vector2Int(fromX, fromY));

            Assert.That(closest, Is.EqualTo(new Vector2Int(expectedX, expectedY)));
        }

        [Test]
        public void ToArea_NextToCornerOfLargeBuilding_IsOne()
        {
            // The building's centre is two cells further; range is measured to its nearest cell, not its centre.
            Assert.That(GridDistance.ToArea(new Vector2Int(3, 3), BarracksOrigin, BarracksSize), Is.EqualTo(1));
        }

        [Test]
        public void ToArea_ThreeCellsBesideEdge_IsThree()
        {
            Assert.That(GridDistance.ToArea(new Vector2Int(9, 5), BarracksOrigin, BarracksSize), Is.EqualTo(3));
        }

        [Test]
        public void ToArea_InsideFootprint_IsZero()
        {
            Assert.That(GridDistance.ToArea(new Vector2Int(5, 6), BarracksOrigin, BarracksSize), Is.Zero);
        }
    }
}
