using System;
using NUnit.Framework;
using StrategyDemo.Data;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Cell footprint derived from pixel size (Can's decision for M1).</summary>
    public class FootprintTests
    {
        private const int CellPixels = 64;

        [TestCase(192f, 256f, 3, 4)]
        [TestCase(64f, 64f, 1, 1)]
        [TestCase(320f, 256f, 5, 4)]
        public void FromPixelSize_ExactMultiples_GiveWholeCells(float width, float height, int cellsX, int cellsY)
        {
            Assert.That(Footprint.FromPixelSize(new Vector2(width, height), CellPixels), Is.EqualTo(new Vector2Int(cellsX, cellsY)));
        }

        [TestCase(65f, 64f, 2, 1)]
        [TestCase(186f, 189f, 3, 3)]
        [TestCase(128.01f, 1f, 3, 1)]
        public void FromPixelSize_PartialCell_RoundsUp(float width, float height, int cellsX, int cellsY)
        {
            Assert.That(Footprint.FromPixelSize(new Vector2(width, height), CellPixels), Is.EqualTo(new Vector2Int(cellsX, cellsY)));
        }

        [TestCase(0f, 0f)]
        [TestCase(10f, 0f)]
        public void FromPixelSize_EmptyImage_IsAtLeastOneCell(float width, float height)
        {
            Assert.That(Footprint.FromPixelSize(new Vector2(width, height), CellPixels), Is.EqualTo(Vector2Int.one));
        }

        [TestCase(0)]
        [TestCase(-64)]
        public void FromPixelSize_NonPositiveCellSize_Throws(int cellPixels)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Footprint.FromPixelSize(new Vector2(64f, 64f), cellPixels));
        }
    }
}
