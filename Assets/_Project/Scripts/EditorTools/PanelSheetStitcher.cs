using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.EditorTools
{
    /// <summary>
    /// Joins Tiny Swords 9-slice panel sheets into single bordered sprites for uGUI.
    /// </summary>
    /// <remarks>
    /// Tiny Swords ships each panel as nine separate pieces laid out on a sheet with transparent gaps
    /// between them. uGUI's sliced/tiled <c>Image</c> needs one sprite plus a border, so this tool drops
    /// the gaps and records where the pieces meet as the sprite border. The source files are never
    /// changed; the results are written to <see cref="OutputFolder"/> (ADR-004).
    /// </remarks>
    public static class PanelSheetStitcher
    {
        /// <summary>Folder that receives the stitched sprites; inside the Tiny Swords folder because they are derived art.</summary>
        public const string OutputFolder = TinySwordsFolder + "/Stitched";

        /// <summary>Pixels per unit of the stitched sprites; equal to the Canvas reference, so 1 art pixel = 1 reference pixel.</summary>
        public const float PixelsPerUnit = 100f;

        private const string TinySwordsFolder = "Assets/_Project/Art/ThirdParty/TinySwords";
        private const string SheetFolder = TinySwordsFolder + "/UI Elements/UI Elements/";
        private const string OutputSuffix = "_Stitched.png";
        private const string MenuPath = "Tools/UI/Stitch Panel Sheets";
        private const int SlicesPerAxis = 3;

        /// <summary>Source sheets that the UI uses. Add a path here to stitch another panel.</summary>
        public static readonly IReadOnlyList<string> SourceSheets = Array.AsReadOnly(new[]
        {
            SheetFolder + "Papers/RegularPaper.png",
            SheetFolder + "Wood Table/WoodTable.png"
        });

        /// <summary>Asset path of the stitched sprite made from <paramref name="sourceSheet"/>.</summary>
        public static string OutputPathFor(string sourceSheet)
        {
            return $"{OutputFolder}/{Path.GetFileNameWithoutExtension(sourceSheet)}{OutputSuffix}";
        }

        /// <summary>Stitches every sheet in <see cref="SourceSheets"/> and imports the results as bordered sprites.</summary>
        [MenuItem(MenuPath)]
        public static void StitchAll()
        {
            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder(TinySwordsFolder, Path.GetFileName(OutputFolder));
            }

            foreach (string source in SourceSheets)
            {
                StitchedSheet sheet = StitchFile(source);
                string outputPath = OutputPathFor(source);
                WriteSprite(sheet, outputPath);
                Debug.Log($"Stitched '{source}' into '{outputPath}' ({sheet.Width}×{sheet.Height}, border {sheet.Border}).");
            }
        }

        /// <summary>Reads <paramref name="sourceSheet"/> from disk and stitches it; the asset itself is not touched.</summary>
        /// <exception cref="InvalidOperationException">The file is not a readable image or not a 3×3 sheet.</exception>
        public static StitchedSheet StitchFile(string sourceSheet)
        {
            // The imported texture is not CPU-readable (Read/Write off, as it should be for art), so read the PNG itself.
            byte[] png = File.ReadAllBytes(sourceSheet);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(png))
                {
                    throw new InvalidOperationException($"'{sourceSheet}' is not a readable image.");
                }

                return Stitch(texture.GetPixels32(), texture.width, texture.height);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// Removes the transparent gaps of a 3×3 sheet and returns the joined image with its 9-slice border.
        /// </summary>
        /// <param name="pixels">Row-major pixels, bottom row first.</param>
        /// <param name="width">Sheet width.</param>
        /// <param name="height">Sheet height.</param>
        /// <exception cref="ArgumentNullException"><paramref name="pixels"/> is null.</exception>
        /// <exception cref="ArgumentException">The pixel count does not match the size.</exception>
        /// <exception cref="InvalidOperationException">The sheet does not have exactly three slices per axis.</exception>
        public static StitchedSheet Stitch(Color32[] pixels, int width, int height)
        {
            if (pixels == null)
            {
                throw new ArgumentNullException(nameof(pixels));
            }

            if (width <= 0 || height <= 0 || pixels.Length != width * height)
            {
                throw new ArgumentException($"Expected {width}×{height} pixels, got {pixels.Length}.", nameof(pixels));
            }

            RangeInt[] columns = FindOpaqueBands(pixels, width, height, true);
            RangeInt[] rows = FindOpaqueBands(pixels, width, height, false);

            int outputWidth = columns[0].length + columns[1].length + columns[2].length;
            int outputHeight = rows[0].length + rows[1].length + rows[2].length;
            var output = new Color32[outputWidth * outputHeight];

            // Copy each kept row band, and inside it each kept column band, next to the previous one.
            int outputY = 0;
            foreach (RangeInt row in rows)
            {
                for (int y = row.start; y < row.end; y++, outputY++)
                {
                    int outputX = 0;
                    foreach (RangeInt column in columns)
                    {
                        Array.Copy(pixels, y * width + column.start, output, outputY * outputWidth + outputX, column.length);
                        outputX += column.length;
                    }
                }
            }

            // The outer slices are the corners and edges; the middle slice is what stretches or tiles.
            // Rows are bottom-first, so rows[0] is the bottom border.
            var border = new Vector4(columns[0].length, rows[0].length, columns[2].length, rows[2].length);
            return new StitchedSheet(output, outputWidth, outputHeight, border);
        }

        /// <summary>
        /// Finds the runs of lines (columns when <paramref name="alongX"/>, rows otherwise) that contain at
        /// least one visible pixel. On a 9-slice sheet these are the three slices; the gaps are fully transparent.
        /// </summary>
        /// <exception cref="InvalidOperationException">The number of runs is not three.</exception>
        internal static RangeInt[] FindOpaqueBands(Color32[] pixels, int width, int height, bool alongX)
        {
            int lineCount = alongX ? width : height;
            var bands = new List<RangeInt>(SlicesPerAxis);
            int bandStart = -1;

            // One step past the end closes a band that touches the sheet's edge.
            for (int line = 0; line <= lineCount; line++)
            {
                bool visible = line < lineCount && LineHasVisiblePixel(pixels, width, height, line, alongX);
                if (visible && bandStart < 0)
                {
                    bandStart = line;
                }
                else if (!visible && bandStart >= 0)
                {
                    bands.Add(new RangeInt(bandStart, line - bandStart));
                    bandStart = -1;
                }
            }

            if (bands.Count != SlicesPerAxis)
            {
                string axis = alongX ? "horizontally" : "vertically";
                throw new InvalidOperationException(
                    $"Expected {SlicesPerAxis} slices {axis} separated by transparent gaps, found {bands.Count}.");
            }

            return bands.ToArray();
        }

        private static bool LineHasVisiblePixel(Color32[] pixels, int width, int height, int line, bool alongX)
        {
            int count = alongX ? height : width;
            for (int i = 0; i < count; i++)
            {
                int index = alongX ? i * width + line : line * width + i;
                if (pixels[index].a > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void WriteSprite(StitchedSheet sheet, string assetPath)
        {
            var texture = new Texture2D(sheet.Width, sheet.Height, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(sheet.Pixels);
                File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(assetPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spritePixelsPerUnit = PixelsPerUnit;
            settings.spriteBorder = sheet.Border;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            // Full Rect keeps the quad equal to the sprite rect, which sliced and tiled images rely on.
            settings.spriteMeshType = SpriteMeshType.FullRect;
            // Pixel art: no filtering, no mipmaps.
            settings.filterMode = FilterMode.Point;
            settings.mipmapEnabled = false;
            settings.alphaIsTransparency = true;
            settings.wrapMode = TextureWrapMode.Clamp;
            importer.SetTextureSettings(settings);
            // Compression is not part of TextureImporterSettings; uncompressed avoids block artefacts on pixel art.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
