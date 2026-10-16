using System;
using NUnit.Framework;
using StrategyDemo.Core;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Index arithmetic behind the looping production menu (I1, U1).</summary>
    public class InfiniteScrollMathTests
    {
        [TestCase(0, 3, 0)]
        [TestCase(2, 3, 2)]
        [TestCase(3, 3, 0)]
        [TestCase(7, 3, 1)]
        [TestCase(-1, 3, 2)]
        [TestCase(-3, 3, 0)]
        [TestCase(-4, 3, 2)]
        [TestCase(5, 1, 0)]
        public void Wrap_AnyIndex_MapsIntoCount(int index, int count, int expected)
        {
            Assert.That(InfiniteScrollMath.Wrap(index, count), Is.EqualTo(expected));
        }

        [TestCase(0)]
        [TestCase(-2)]
        public void Wrap_NonPositiveCount_Throws(int count)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => InfiniteScrollMath.Wrap(1, count));
        }

        [TestCase(500f, 112f, 6)]  // 4.46 rows fit -> 5 rows + 1 partial
        [TestCase(448f, 112f, 5)]  // exactly 4 rows -> 4 + 1 when scrolled half a row
        [TestCase(50f, 112f, 2)]   // smaller than one row
        [TestCase(0f, 112f, 1)]    // collapsed window
        [TestCase(-10f, 112f, 1)]  // negative height during a layout pass
        public void ViewCount_Viewport_CoversPartialRowsAtBothEdges(float viewport, float stride, int expected)
        {
            Assert.That(InfiniteScrollMath.ViewCount(viewport, stride), Is.EqualTo(expected));
        }

        [TestCase(0f, 0)]
        [TestCase(111.9f, 0)]
        [TestCase(112f, 1)]
        [TestCase(-0.1f, -1)]
        [TestCase(-112f, -1)]
        [TestCase(-112.1f, -2)]
        public void FirstVisibleIndex_Offset_FloorsTowardNegativeInfinity(float offset, int expected)
        {
            Assert.That(InfiniteScrollMath.FirstVisibleIndex(offset, 112f), Is.EqualTo(expected));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void StrideBasedMethods_InvalidStride_Throw(float stride)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => InfiniteScrollMath.ViewCount(100f, stride));
            Assert.Throws<ArgumentOutOfRangeException>(() => InfiniteScrollMath.FirstVisibleIndex(100f, stride));
        }

        [TestCase(0, 6, 0)]
        [TestCase(5, 6, 0)]
        [TestCase(6, 6, 6)]
        [TestCase(40, 6, 36)]
        [TestCase(-1, 6, -6)]
        [TestCase(-13, 6, -18)]
        public void RebaseShift_AnyIndex_IsWholeCyclesBackToFirstCycle(int first, int count, int expected)
        {
            int shift = InfiniteScrollMath.RebaseShift(first, count);

            Assert.That(shift, Is.EqualTo(expected));
            Assert.That(shift % count, Is.EqualTo(0), "Shifting by whole cycles keeps every row's data.");
            Assert.That(first - shift, Is.InRange(0, count - 1));
        }
    }
}
