using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.EditorTools
{
    /// <summary>
    /// Re-slices the Tiny Swords soldier idle sheets into animation frames that line up on the board grid.
    /// </summary>
    /// <remarks>
    /// The sheets are strips of equal square frames. Unity's automatic slicing cuts each frame tight around its
    /// pixels, so frames differ in size and pivot and the animation jitters. This tool cuts every frame with the
    /// same rectangle (the union of the visible pixels of all frames) and the same pivot, imports at the board's
    /// pixel density (64 px per cell, like the buildings) and keeps existing sprite ids, so references to frame 0
    /// survive. Only import settings change; the PNG files are not touched.
    /// </remarks>
    public static class UnitSheetSlicer
    {
        /// <summary>Art pixels per world unit; one world unit is one cell, the same density as the buildings.</summary>
        public const int PixelsPerUnit = 64;

        /// <summary>
        /// Pixels between a soldier's feet and the bottom edge of its cell. The pivot sits on that edge, like a
        /// building's, so soldiers and buildings Y-sort by the same rule (ADR-004).
        /// </summary>
        public const int FeetAboveCellBottom = 16;

        private const string UnitsFolder = "Assets/_Project/Art/ThirdParty/TinySwords/Units/Blue Units/";
        private const string MenuPath = "Tools/Art/Slice Unit Sheets";
        // Above the default 2048: the Lancer strip is 3840 px wide and would otherwise be downscaled.
        private const int MaxTextureSize = 4096;

        /// <summary>Sheets the board uses, with their square frame size in pixels.</summary>
        public static readonly IReadOnlyList<(string Path, int FrameSize)> Sheets = Array.AsReadOnly(new[]
        {
            (UnitsFolder + "Warrior/Warrior_Idle.png", 192),
            (UnitsFolder + "Lancer/Lancer_Idle.png", 320),
            (UnitsFolder + "Archer/Archer_Idle.png", 192)
        });

        /// <summary>Slices every sheet in <see cref="Sheets"/>.</summary>
        [MenuItem(MenuPath)]
        public static void SliceAll()
        {
            foreach ((string path, int frameSize) in Sheets)
            {
                SliceSheet(path, frameSize);
            }
        }

        /// <summary>Re-imports the sheet at <paramref name="assetPath"/> as equal frames of <paramref name="frameSize"/> pixels.</summary>
        /// <exception cref="InvalidOperationException">The file is not an image, not a strip of whole frames, or fully transparent.</exception>
        public static void SliceSheet(string assetPath, int frameSize)
        {
            (Color32[] pixels, int width, int height) = ReadPng(assetPath);
            RectInt crop = VisibleBoundsAcrossFrames(pixels, width, height, frameSize);
            Vector2 pivot = PivotFor(crop, frameSize);

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = MaxTextureSize;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            // Reuse the existing rect objects by name: their sprite ids, and so every reference to them, stay valid.
            Dictionary<string, SpriteRect> existing = provider.GetSpriteRects().ToDictionary(rect => rect.name);
            string baseName = Path.GetFileNameWithoutExtension(assetPath);
            int frameCount = width / frameSize;
            var rects = new SpriteRect[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                string frameName = $"{baseName}_{i}";
                SpriteRect rect = existing.TryGetValue(frameName, out SpriteRect kept) ? kept : new SpriteRect { name = frameName };
                rect.rect = new Rect(i * frameSize + crop.x, crop.y, crop.width, crop.height);
                rect.alignment = SpriteAlignment.Custom;
                rect.pivot = pivot;
                rect.border = Vector4.zero;
                rects[i] = rect;
            }

            provider.SetSpriteRects(rects);
            provider.Apply();
            importer.SaveAndReimport();
            Debug.Log($"Sliced '{assetPath}' into {frameCount} frames of {crop.width}×{crop.height} px, pivot {pivot}.");
        }

        /// <summary>
        /// Smallest rectangle, in frame-local pixels, that holds every visible pixel of every frame. Cutting all
        /// frames with it keeps them the same size and aligned, so the animation does not jitter.
        /// </summary>
        /// <param name="pixels">Row-major pixels, bottom row first.</param>
        /// <param name="width">Sheet width; a whole number of frames.</param>
        /// <param name="height">Sheet height; equal to <paramref name="frameSize"/> (one row of frames).</param>
        /// <param name="frameSize">Side of one square frame.</param>
        /// <exception cref="ArgumentException">The sizes do not describe one row of whole frames.</exception>
        /// <exception cref="InvalidOperationException">No pixel is visible.</exception>
        public static RectInt VisibleBoundsAcrossFrames(Color32[] pixels, int width, int height, int frameSize)
        {
            if (pixels == null || frameSize <= 0 || height != frameSize || width % frameSize != 0 || pixels.Length != width * height)
            {
                throw new ArgumentException($"Expected one row of {frameSize} px frames, got {width}×{height} ({pixels?.Length} pixels).");
            }

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a == 0)
                    {
                        continue;
                    }

                    // Frame-local column: the same spot in every frame maps to the same value.
                    int localX = x % frameSize;
                    minX = Mathf.Min(minX, localX);
                    maxX = Mathf.Max(maxX, localX);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (minX == int.MaxValue)
            {
                throw new InvalidOperationException("The sheet has no visible pixel.");
            }

            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>
        /// Normalised pivot for frames cut with <paramref name="crop"/>: horizontally at the frame's centre (the
        /// sheets centre the character), vertically <see cref="FeetAboveCellBottom"/> pixels below the lowest
        /// visible pixel (the feet). The pivot may lie outside the cut rectangle; Unity allows that.
        /// </summary>
        public static Vector2 PivotFor(RectInt crop, int frameSize)
        {
            float x = (frameSize * 0.5f - crop.x) / crop.width;
            float y = -(float)FeetAboveCellBottom / crop.height;
            return new Vector2(x, y);
        }

        private static (Color32[] Pixels, int Width, int Height) ReadPng(string assetPath)
        {
            // The imported texture is not CPU-readable (Read/Write off, as it should be for art), so read the PNG itself.
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(assetPath)))
                {
                    throw new InvalidOperationException($"'{assetPath}' is not a readable image.");
                }

                return (texture.GetPixels32(), texture.width, texture.height);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
