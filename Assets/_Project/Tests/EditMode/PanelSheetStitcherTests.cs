using System;
using System.IO;
using NUnit.Framework;
using StrategyDemo.EditorTools;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Stitching of 9-slice sheets into bordered UI sprites, and the generated assets staying in sync.</summary>
    public class PanelSheetStitcherTests
    {
        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        [Test]
        public void Stitch_SheetWithGaps_DropsGapsAndKeepsSliceOrder()
        {
            // 3 slices per axis: widths 2,1,3 and heights 1,2,1, separated by 2-pixel transparent gaps.
            int[] columnWidths = { 2, 1, 3 };
            int[] rowHeights = { 1, 2, 1 };
            Color32[] sheet = BuildSheet(columnWidths, rowHeights, 2, out int width, out int height);

            StitchedSheet result = PanelSheetStitcher.Stitch(sheet, width, height);

            Assert.That(result.Width, Is.EqualTo(6));
            Assert.That(result.Height, Is.EqualTo(4));
            // Every pixel of the result is opaque and carries the (column, row) slice it came from.
            for (int y = 0; y < result.Height; y++)
            {
                for (int x = 0; x < result.Width; x++)
                {
                    Color32 pixel = result.Pixels[y * result.Width + x];
                    Assert.That(pixel.a, Is.EqualTo(255));
                    Assert.That(pixel.r, Is.EqualTo(SliceOf(x, columnWidths)), $"column of ({x},{y})");
                    Assert.That(pixel.g, Is.EqualTo(SliceOf(y, rowHeights)), $"row of ({x},{y})");
                }
            }
        }

        [Test]
        public void Stitch_SheetWithGaps_BorderIsOuterSliceSizesLeftBottomRightTop()
        {
            Color32[] sheet = BuildSheet(new[] { 2, 1, 3 }, new[] { 4, 2, 1 }, 1, out int width, out int height);

            StitchedSheet result = PanelSheetStitcher.Stitch(sheet, width, height);

            Assert.That(result.Border, Is.EqualTo(new Vector4(2, 4, 3, 1)));
        }

        [Test]
        public void Stitch_TransparentMarginAroundSheet_IsTrimmed()
        {
            Color32[] sheet = BuildSheet(new[] { 1, 1, 1 }, new[] { 1, 1, 1 }, 1, out int width, out int height, 3);

            StitchedSheet result = PanelSheetStitcher.Stitch(sheet, width, height);

            Assert.That(result.Width, Is.EqualTo(3));
            Assert.That(result.Height, Is.EqualTo(3));
        }

        [Test]
        public void Stitch_TwoSlicesOnlyHorizontally_Throws()
        {
            Color32[] sheet = BuildSheet(new[] { 1, 1 }, new[] { 1, 1, 1 }, 1, out int width, out int height);

            Assert.Throws<InvalidOperationException>(() => PanelSheetStitcher.Stitch(sheet, width, height));
        }

        [Test]
        public void Stitch_FullyTransparentSheet_Throws()
        {
            var sheet = new Color32[16];

            Assert.Throws<InvalidOperationException>(() => PanelSheetStitcher.Stitch(sheet, 4, 4));
        }

        [Test]
        public void Stitch_PixelCountMismatch_Throws()
        {
            Assert.Throws<ArgumentException>(() => PanelSheetStitcher.Stitch(new Color32[10], 4, 4));
            Assert.Throws<ArgumentNullException>(() => PanelSheetStitcher.Stitch(null, 4, 4));
        }

        [Test]
        public void StitchedAssets_MatchCurrentSourceSheets()
        {
            foreach (string source in PanelSheetStitcher.SourceSheets)
            {
                StitchedSheet expected = PanelSheetStitcher.StitchFile(source);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSheetStitcher.OutputPathFor(source));

                Assert.That(sprite, Is.Not.Null, $"Run Tools > UI > Stitch Panel Sheets for '{source}'.");
                Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(expected.Width, expected.Height)), source);
                Assert.That(sprite.border, Is.EqualTo(expected.Border), source);
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(PanelSheetStitcher.PixelsPerUnit), source);
                Assert.That(ReadPixels(PanelSheetStitcher.OutputPathFor(source)), Is.EqualTo(expected.Pixels), $"{source} changed; re-run the stitcher.");
            }
        }

        private static Color32[] ReadPixels(string assetPath)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes(assetPath)), Is.True, assetPath);
                return texture.GetPixels32();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// Builds a sheet whose slice (c, r) is filled with colour (c, r, 0, 255), with transparent gaps
        /// between slices and an optional transparent margin around them. Rows are bottom-first.
        /// </summary>
        private static Color32[] BuildSheet(int[] columnWidths, int[] rowHeights, int gap, out int width, out int height, int margin = 0)
        {
            width = 2 * margin + Sum(columnWidths) + gap * (columnWidths.Length - 1);
            height = 2 * margin + Sum(rowHeights) + gap * (rowHeights.Length - 1);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Clear;
            }

            int y0 = margin;
            for (int r = 0; r < rowHeights.Length; r++)
            {
                int x0 = margin;
                for (int c = 0; c < columnWidths.Length; c++)
                {
                    for (int y = y0; y < y0 + rowHeights[r]; y++)
                    {
                        for (int x = x0; x < x0 + columnWidths[c]; x++)
                        {
                            pixels[y * width + x] = new Color32((byte)c, (byte)r, 0, 255);
                        }
                    }

                    x0 += columnWidths[c] + gap;
                }

                y0 += rowHeights[r] + gap;
            }

            return pixels;
        }

        private static int SliceOf(int position, int[] sizes)
        {
            for (int i = 0; i < sizes.Length; i++)
            {
                if (position < sizes[i])
                {
                    return i;
                }

                position -= sizes[i];
            }

            throw new ArgumentOutOfRangeException(nameof(position));
        }

        private static int Sum(int[] values)
        {
            int sum = 0;
            foreach (int value in values)
            {
                sum += value;
            }

            return sum;
        }
    }
}
