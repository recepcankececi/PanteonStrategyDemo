using NUnit.Framework;
using StrategyDemo.Data;
using StrategyDemo.Gameplay;
using UnityEditor;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Ground sizing and coordinate conversion of <see cref="GridView"/> on a board that does not start at the world origin.</summary>
    public class GridViewTests
    {
        private const int Width = 6;
        private const int Height = 4;
        private const float Tolerance = 1e-5f;

        private static readonly Vector3 BoardOrigin = new Vector3(10f, -2f, 0f);

        private GameObject _gridObject;
        private SpriteRenderer _ground;
        private GridView _view;
        private GridConfig _config;

        [SetUp]
        public void CreateView()
        {
            _gridObject = new GameObject(nameof(GridViewTests));
            _gridObject.transform.position = BoardOrigin;
            var groundObject = new GameObject("Ground");
            groundObject.transform.SetParent(_gridObject.transform, false);
            _ground = groundObject.AddComponent<SpriteRenderer>();
            _ground.drawMode = SpriteDrawMode.Tiled;

            _view = _gridObject.AddComponent<GridView>();
            var serializedView = new SerializedObject(_view);
            serializedView.FindProperty("_ground").objectReferenceValue = _ground;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            _config = ScriptableObject.CreateInstance<GridConfig>();
            var serializedConfig = new SerializedObject(_config);
            serializedConfig.FindProperty("_width").intValue = Width;
            serializedConfig.FindProperty("_height").intValue = Height;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void DestroyView()
        {
            Object.DestroyImmediate(_gridObject);
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void Init_SetsWorldBoundsFromOriginAndConfig()
        {
            _view.Init(_config);

            Assert.That(_view.WorldBounds, Is.EqualTo(new Rect(BoardOrigin, new Vector2(Width, Height))));
        }

        [Test]
        public void Init_StretchesGroundOverWholeBoard()
        {
            _view.Init(_config);

            Assert.That(_ground.size, Is.EqualTo(new Vector2(Width, Height)));
            Assert.That(_ground.transform.position.x, Is.EqualTo(BoardOrigin.x + Width * 0.5f).Within(Tolerance));
            Assert.That(_ground.transform.position.y, Is.EqualTo(BoardOrigin.y + Height * 0.5f).Within(Tolerance));
        }

        [Test]
        public void Init_NullConfig_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => _view.Init(null));
        }

        [Test]
        public void Init_GroundNotAssigned_Throws()
        {
            var serializedView = new SerializedObject(_view);
            serializedView.FindProperty("_ground").objectReferenceValue = null;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            Assert.Throws<System.InvalidOperationException>(() => _view.Init(_config));
        }

        [Test]
        public void Conversions_AfterTransformMoves_KeepBoardOriginFromInit()
        {
            _view.Init(_config);
            _gridObject.transform.position += Vector3.right * Width;

            Assert.That(_view.WorldToCell(_view.CellToWorld(Vector2Int.zero)), Is.EqualTo(Vector2Int.zero));
            Assert.That(_view.CellToWorld(Vector2Int.zero).x, Is.EqualTo(BoardOrigin.x + 0.5f).Within(Tolerance));
        }

        [Test]
        public void CellToWorldAndBack_UsesBoardOrigin()
        {
            _view.Init(_config);
            Vector2Int cell = new Vector2Int(Width - 1, Height - 1);

            Vector3 world = _view.CellToWorld(cell);

            Assert.That(world.x, Is.EqualTo(BoardOrigin.x + Width - 0.5f).Within(Tolerance));
            Assert.That(_view.WorldToCell(world), Is.EqualTo(cell));
        }
    }
}
