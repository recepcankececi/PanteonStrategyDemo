using UnityEngine;

namespace StrategyDemo.Core
{
    /// <summary>
    /// Fits a world rectangle into a sub-area of the screen (the gap between the side panels)
    /// with a full-screen orthographic camera (T2, I4).
    /// </summary>
    public static class CameraFraming
    {
        private const float MinimumPixels = 1f;

        /// <summary>
        /// Returns the camera frame that shows <paramref name="board"/> as large as possible inside
        /// <paramref name="viewArea"/>, keeping <paramref name="padding"/> world units around it.
        /// </summary>
        /// <param name="board">World rectangle that must be visible.</param>
        /// <param name="screenSize">Screen size in pixels.</param>
        /// <param name="viewArea">Screen-pixel rectangle the board must appear in.</param>
        /// <param name="padding">World units kept free on every side; negative values count as zero.</param>
        public static CameraFrame FitBoard(Rect board, Vector2 screenSize, Rect viewArea, float padding)
        {
            // A window shrunk to nothing would give zero sizes; clamping keeps the result finite.
            float screenHeight = Mathf.Max(screenSize.y, MinimumPixels);
            float areaWidth = Mathf.Max(viewArea.width, MinimumPixels);
            float areaHeight = Mathf.Max(viewArea.height, MinimumPixels);
            float margin = Mathf.Max(padding, 0f) * 2f;

            // The limiting axis decides the scale: whichever needs more world units per pixel.
            float worldPerPixel = Mathf.Max((board.width + margin) / areaWidth, (board.height + margin) / areaHeight);
            float orthographicSize = worldPerPixel * screenHeight * 0.5f;

            // The camera looks at the screen centre; shift it so the board centre lands on the area centre.
            Vector2 areaOffsetFromScreenCentre = viewArea.center - screenSize * 0.5f;
            Vector2 center = board.center - areaOffsetFromScreenCentre * worldPerPixel;

            return new CameraFrame(orthographicSize, center);
        }
    }
}
