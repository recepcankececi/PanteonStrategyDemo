using StrategyDemo.Core;
using UnityEngine;

namespace StrategyDemo.App
{
    /// <summary>
    /// Keeps the resizable Windows window (T2) at a minimum size in pixels: Unity has no minimum window size
    /// setting, so a window dragged smaller is grown back with <see cref="Screen.SetResolution(int, int, FullScreenMode)"/>.
    /// </summary>
    /// <remarks>
    /// Players only. In the editor the Game view is sized by the editor layout, and resizing it is how other
    /// resolutions are checked. Full screen is left alone: the display decides its size there.
    /// </remarks>
    public sealed class MinimumWindowSize : MonoBehaviour
    {
        [SerializeField, Tooltip("Smallest window in pixels; below it panel text becomes unreadable.")]
        private Vector2Int _minimum = new Vector2Int(1024, 576);

        // The window size when the last resize was asked for. SetResolution takes effect a frame or more later, so it
        // is asked for once per size; if the player keeps dragging (the size changes again), it is asked for again.
        private Vector2Int _requestedFrom;

        private void Awake()
        {
            if (Application.isEditor)
            {
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (Screen.fullScreenMode != FullScreenMode.Windowed)
            {
                return;
            }

            var current = new Vector2Int(Screen.width, Screen.height);
            if (!WindowSizeRule.TryGrowToMinimum(current, _minimum, out Vector2Int grown))
            {
                // Big enough: forget the last request, so the same too-small size is grown again next time.
                _requestedFrom = default;
                return;
            }

            if (current != _requestedFrom)
            {
                _requestedFrom = current;
                Screen.SetResolution(grown.x, grown.y, FullScreenMode.Windowed);
            }
        }
    }
}
