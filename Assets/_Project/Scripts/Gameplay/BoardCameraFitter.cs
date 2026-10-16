using System;
using StrategyDemo.Core;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Keeps the board fitted inside the UI's board area (between the side panels) for any window
    /// size or aspect ratio (T2). Re-fits only when the area or the camera's pixel rect changes.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class BoardCameraFitter : MonoBehaviour
    {
        private const int CornerCount = 4;
        private const int BottomLeftCorner = 0;
        private const int TopRightCorner = 2;

        [SerializeField, Tooltip("UI rect the board must appear in.")]
        private RectTransform _boardArea;
        [SerializeField, Min(0f), Tooltip("World units kept free around the board.")]
        private float _padding = 0.5f;

        // Reused every frame: GetWorldCorners fills an array, and allocating one per frame would feed the GC.
        private readonly Vector3[] _corners = new Vector3[CornerCount];
        private Camera _camera;
        private Canvas _boardCanvas;
        private Rect _board;
        private Rect _lastArea;
        private Rect _lastViewport;
        private bool _hasBoard;

        /// <summary>Starts fitting the camera to <paramref name="boardWorldBounds"/>.</summary>
        /// <exception cref="InvalidOperationException">The board area is not assigned or not on a canvas.</exception>
        public void Init(Rect boardWorldBounds)
        {
            if (_boardArea == null)
            {
                throw new InvalidOperationException($"{nameof(BoardCameraFitter)} needs a board area RectTransform.");
            }

            Canvas canvas = _boardArea.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                throw new InvalidOperationException($"Board area '{_boardArea.name}' is not under a Canvas.");
            }

            _boardCanvas = canvas.rootCanvas;
            _camera = GetComponent<Camera>();
            // The framing maths is for an orthographic projection; a perspective camera would ignore the size.
            _camera.orthographic = true;
            _board = boardWorldBounds;
            _hasBoard = true;
            // An empty "last viewport" guarantees the first LateUpdate fits.
            _lastViewport = Rect.zero;
        }

        private void LateUpdate()
        {
            if (!_hasBoard)
            {
                return;
            }

            Rect viewport = _camera.pixelRect;
            if (viewport != _lastViewport)
            {
                // After a resize the canvas lays out later in the frame; force it so the area read below is current.
                Canvas.ForceUpdateCanvases();
            }

            Rect area = ReadBoardAreaInPixels();
            if (area == _lastArea && viewport == _lastViewport)
            {
                return;
            }

            _lastArea = area;
            _lastViewport = viewport;

            // FitBoard works in the camera's own pixel space, whose origin is the viewport's bottom-left corner.
            Rect areaInViewport = new Rect(area.position - viewport.position, area.size);
            CameraFrame frame = CameraFraming.FitBoard(_board, viewport.size, areaInViewport, _padding);
            _camera.orthographicSize = frame.OrthographicSize;
            transform.position = new Vector3(frame.Center.x, frame.Center.y, transform.position.z);
        }

        private Rect ReadBoardAreaInPixels()
        {
            _boardArea.GetWorldCorners(_corners);
            // Overlay canvases have no camera (null = world corners are already pixels); camera-space canvases do.
            Camera canvasCamera = _boardCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _boardCanvas.worldCamera;
            Vector2 min = RectTransformUtility.WorldToScreenPoint(canvasCamera, _corners[BottomLeftCorner]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(canvasCamera, _corners[TopRightCorner]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
