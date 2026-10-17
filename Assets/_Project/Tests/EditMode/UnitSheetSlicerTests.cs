using System;
using NUnit.Framework;
using StrategyDemo.EditorTools;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Frame-cutting maths of the soldier sheet slicer: one shared rectangle and pivot for all frames.</summary>
    public class UnitSheetSlicerTests
    {
        private const int FrameSize = 4;

        [Test]
        public void VisibleBoundsAcrossFrames_UnionOfAllFramesInFrameLocalPixels()
        {
            // Two 4×4 frames: frame 0 has a pixel at (1, 1), frame 1 at local (2, 3).
            Color32[] pixels = Sheet(2);
            Set(pixels, 2, 1, 1);
            Set(pixels, 2, FrameSize + 2, 3);

            RectInt bounds = UnitSheetSlicer.VisibleBoundsAcrossFrames(pixels, FrameSize * 2, FrameSize, FrameSize);

            Assert.That(bounds, Is.EqualTo(new RectInt(1, 1, 2, 3)));
        }

        [Test]
        public void VisibleBoundsAcrossFrames_SinglePixel_IsOnePixelRect()
        {
            Color32[] pixels = Sheet(1);
            Set(pixels, 1, 3, 0);

            Assert.That(UnitSheetSlicer.VisibleBoundsAcrossFrames(pixels, FrameSize, FrameSize, FrameSize), Is.EqualTo(new RectInt(3, 0, 1, 1)));
        }

        [Test]
        public void VisibleBoundsAcrossFrames_FullyTransparent_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => UnitSheetSlicer.VisibleBoundsAcrossFrames(Sheet(2), FrameSize * 2, FrameSize, FrameSize));
        }

        [TestCase(7, 4)]   // width not a whole number of frames
        [TestCase(8, 5)]   // more than one row of frames
        public void VisibleBoundsAcrossFrames_NotOneRowOfWholeFrames_Throws(int width, int height)
        {
            var pixels = new Color32[width * height];

            Assert.Throws<ArgumentException>(() => UnitSheetSlicer.VisibleBoundsAcrossFrames(pixels, width, height, FrameSize));
        }

        [Test]
        public void PivotFor_IsFrameCentreHorizontallyAndBelowFeetVertically()
        {
            // 192 px frame cut to x 60..140 (80 wide), 100 high starting at the feet.
            var crop = new RectInt(60, 50, 80, 100);

            Vector2 pivot = UnitSheetSlicer.PivotFor(crop, 192);

            Assert.That(pivot.x, Is.EqualTo((96f - 60f) / 80f).Within(1e-5f));
            Assert.That(pivot.y * crop.height, Is.EqualTo(-UnitSheetSlicer.FeetAboveCellBottom).Within(1e-4f));
        }

        private static Color32[] Sheet(int frames)
        {
            return new Color32[FrameSize * frames * FrameSize];
        }

        private static void Set(Color32[] pixels, int frames, int x, int y)
        {
            pixels[y * FrameSize * frames + x] = new Color32(255, 255, 255, 255);
        }
    }
}
