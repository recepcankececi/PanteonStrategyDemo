using UnityEngine;

namespace StrategyDemo.EditorTools
{
    /// <summary>A 9-slice sheet joined into one image, with the border that separates its slices.</summary>
    public readonly struct StitchedSheet
    {
        /// <param name="pixels">Row-major pixels, bottom row first (Unity's texture order).</param>
        /// <param name="width">Width in pixels.</param>
        /// <param name="height">Height in pixels.</param>
        /// <param name="border">Sprite border in pixels: left, bottom, right, top.</param>
        public StitchedSheet(Color32[] pixels, int width, int height, Vector4 border)
        {
            Pixels = pixels;
            Width = width;
            Height = height;
            Border = border;
        }

        /// <summary>Row-major pixels, bottom row first (Unity's texture order).</summary>
        public Color32[] Pixels { get; }

        /// <summary>Width in pixels.</summary>
        public int Width { get; }

        /// <summary>Height in pixels.</summary>
        public int Height { get; }

        /// <summary>Sprite border in pixels, in Unity's order: left, bottom, right, top.</summary>
        public Vector4 Border { get; }
    }
}
