using NUnit.Framework;
using StrategyDemo.Core;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>
    /// The board must fit, centred, inside the screen area between the side panels at any aspect ratio (T2).
    /// Results are checked by projecting the board through a real orthographic camera, not by re-deriving the formula.
    /// </summary>
    public class CameraFramingTests
    {
        private const float PixelTolerance = 0.5f;
        private const float Padding = 0.5f;
        private const float SidePanelReferenceWidth = 360f;
        private const float MinimumAreaWidth = 100f;

        // Same values as the scene's CanvasScaler.
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        private const float MatchWidthOrHeight = 0.5f;

        private static readonly Rect Board = new Rect(0f, 0f, 24f, 16f);

        private static readonly object[] Screens =
        {
            new object[] { 1920, 1080 },  // 16:9
            new object[] { 2560, 1080 },  // 21:9
            new object[] { 1024, 768 },   // 4:3
            new object[] { 1080, 1920 },  // portrait
            new object[] { 800, 600 }
        };

        private GameObject _cameraObject;
        private Camera _camera;
        private RenderTexture _target;

        [TearDown]
        public void DestroyCamera()
        {
            if (_cameraObject != null)
            {
                Object.DestroyImmediate(_cameraObject);
            }

            if (_target != null)
            {
                _target.Release();
                Object.DestroyImmediate(_target);
            }
        }

        [TestCaseSource(nameof(Screens))]
        public void FitBoard_AnyScreen_BoardAndPaddingFitInsideArea(int width, int height)
        {
            Rect area = AreaBetweenPanels(width, height);
            Camera camera = CameraFor(width, height, CameraFraming.FitBoard(Board, new Vector2(width, height), area, Padding));

            Rect boardOnScreen = ProjectToScreen(camera, Expand(Board, Padding));

            Assert.That(boardOnScreen.xMin, Is.GreaterThanOrEqualTo(area.xMin - PixelTolerance));
            Assert.That(boardOnScreen.yMin, Is.GreaterThanOrEqualTo(area.yMin - PixelTolerance));
            Assert.That(boardOnScreen.xMax, Is.LessThanOrEqualTo(area.xMax + PixelTolerance));
            Assert.That(boardOnScreen.yMax, Is.LessThanOrEqualTo(area.yMax + PixelTolerance));
        }

        [TestCaseSource(nameof(Screens))]
        public void FitBoard_AnyScreen_FitIsTightOnLimitingAxis(int width, int height)
        {
            Rect area = AreaBetweenPanels(width, height);
            Camera camera = CameraFor(width, height, CameraFraming.FitBoard(Board, new Vector2(width, height), area, Padding));

            Rect boardOnScreen = ProjectToScreen(camera, Expand(Board, Padding));

            // Largest possible board: board plus padding spans the whole area on one axis.
            bool widthTight = Mathf.Abs(boardOnScreen.width - area.width) < PixelTolerance;
            bool heightTight = Mathf.Abs(boardOnScreen.height - area.height) < PixelTolerance;
            Assert.That(widthTight || heightTight, Is.True, $"board {boardOnScreen} area {area}");
        }

        [TestCaseSource(nameof(Screens))]
        public void FitBoard_AnyScreen_BoardCentredInArea(int width, int height)
        {
            Rect area = AreaBetweenPanels(width, height);
            Camera camera = CameraFor(width, height, CameraFraming.FitBoard(Board, new Vector2(width, height), area, Padding));

            Rect boardOnScreen = ProjectToScreen(camera, Board);

            Assert.That(boardOnScreen.center.x, Is.EqualTo(area.center.x).Within(PixelTolerance));
            Assert.That(boardOnScreen.center.y, Is.EqualTo(area.center.y).Within(PixelTolerance));
        }

        [TestCaseSource(nameof(Screens))]
        public void SceneLayout_AnyScreen_LeavesUsableBoardArea(int width, int height)
        {
            // Guards the test model itself: a collapsed area would let the fit tests pass on a 1 px board.
            Assert.That(AreaBetweenPanels(width, height).width, Is.GreaterThan(MinimumAreaWidth));
        }

        [Test]
        public void FitBoard_AreaCollapsedToNothing_ReturnsFiniteFrame()
        {
            Vector2 screen = new Vector2(200f, 100f);
            Rect collapsed = new Rect(100f, 0f, 0f, 0f);

            CameraFrame frame = CameraFraming.FitBoard(Board, screen, collapsed, Padding);

            Assert.That(float.IsFinite(frame.OrthographicSize) && frame.OrthographicSize > 0f, Is.True);
            Assert.That(float.IsFinite(frame.Center.x) && float.IsFinite(frame.Center.y), Is.True);
        }

        [Test]
        public void FitBoard_NegativePadding_TreatedAsZero()
        {
            Vector2 screen = new Vector2(1920f, 1080f);
            Rect area = AreaBetweenPanels(1920, 1080);

            CameraFrame negative = CameraFraming.FitBoard(Board, screen, area, -3f);
            CameraFrame zero = CameraFraming.FitBoard(Board, screen, area, 0f);

            Assert.That(negative.OrthographicSize, Is.EqualTo(zero.OrthographicSize));
            Assert.That(negative.Center, Is.EqualTo(zero.Center));
        }

        // Mirrors the scene: two fixed-width side panels scaled by a CanvasScaler with match 0.5,
        // which is the geometric mean of the width and height ratios.
        private static Rect AreaBetweenPanels(int width, int height)
        {
            float scale = Mathf.Pow(width / ReferenceResolution.x, 1f - MatchWidthOrHeight)
                          * Mathf.Pow(height / ReferenceResolution.y, MatchWidthOrHeight);
            float panelWidth = SidePanelReferenceWidth * scale;
            return new Rect(panelWidth, 0f, width - 2f * panelWidth, height);
        }

        // A camera rendering into a texture of the screen size has exactly that pixel rect, even in edit mode.
        private Camera CameraFor(int width, int height, CameraFrame frame)
        {
            _target = new RenderTexture(width, height, 0);
            _cameraObject = new GameObject(nameof(CameraFramingTests));
            _camera = _cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.targetTexture = _target;
            _camera.orthographicSize = frame.OrthographicSize;
            _cameraObject.transform.position = new Vector3(frame.Center.x, frame.Center.y, -10f);
            return _camera;
        }

        private static Rect ProjectToScreen(Camera camera, Rect world)
        {
            Vector3 min = camera.WorldToScreenPoint(new Vector3(world.xMin, world.yMin, 0f));
            Vector3 max = camera.WorldToScreenPoint(new Vector3(world.xMax, world.yMax, 0f));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect Expand(Rect rect, float amount)
        {
            return Rect.MinMaxRect(rect.xMin - amount, rect.yMin - amount, rect.xMax + amount, rect.yMax + amount);
        }
    }
}
