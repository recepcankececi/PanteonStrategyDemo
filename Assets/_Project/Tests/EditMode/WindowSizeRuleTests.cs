using NUnit.Framework;
using StrategyDemo.Core;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>The minimum window rule (T2): each side grows to the minimum on its own; a large enough window is kept.</summary>
    public class WindowSizeRuleTests
    {
        private static readonly Vector2Int Minimum = new Vector2Int(1024, 576);

        [Test]
        public void TryGrowToMinimum_LargeEnough_KeepsSize()
        {
            bool grows = WindowSizeRule.TryGrowToMinimum(new Vector2Int(1920, 1080), Minimum, out Vector2Int grown);

            Assert.That(grows, Is.False);
            Assert.That(grown, Is.EqualTo(new Vector2Int(1920, 1080)));
        }

        [Test]
        public void TryGrowToMinimum_ExactlyMinimum_KeepsSize()
        {
            Assert.That(WindowSizeRule.TryGrowToMinimum(Minimum, Minimum, out _), Is.False);
        }

        [Test]
        public void TryGrowToMinimum_TooNarrowButTall_GrowsWidthOnly()
        {
            bool grows = WindowSizeRule.TryGrowToMinimum(new Vector2Int(720, 1000), Minimum, out Vector2Int grown);

            Assert.That(grows, Is.True);
            Assert.That(grown, Is.EqualTo(new Vector2Int(1024, 1000)));
        }

        [Test]
        public void TryGrowToMinimum_TooSmallBothWays_GrowsToMinimum()
        {
            bool grows = WindowSizeRule.TryGrowToMinimum(new Vector2Int(640, 360), Minimum, out Vector2Int grown);

            Assert.That(grows, Is.True);
            Assert.That(grown, Is.EqualTo(Minimum));
        }
    }
}
