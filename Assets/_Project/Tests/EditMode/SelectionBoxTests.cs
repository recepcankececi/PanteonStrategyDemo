using System;
using NUnit.Framework;
using StrategyDemo.Core;
using StrategyDemo.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>The drag box draws exactly the cells the drag covers and hides when no drag is in progress.</summary>
    public class SelectionBoxTests
    {
        private TestBoard _board;
        private SelectionInputHandler _input;
        private SelectionBox _box;
        private SpriteRenderer _renderer;

        [SetUp]
        public void CreateBox()
        {
            _board = new TestBoard(new Vector3(-3f, 2f, 0f), 10, 8);
            var grid = new GridMap(10, 8);
            _input = new SelectionInputHandler(new SelectionService(grid, new OccupantRegistry()), _board.View);
            var boxObject = new GameObject("SelectionBox");
            _renderer = boxObject.AddComponent<SpriteRenderer>();
            _renderer.drawMode = SpriteDrawMode.Tiled;
            _box = boxObject.AddComponent<SelectionBox>();
            _box.Init(_input, _board.View);
        }

        [TearDown]
        public void DestroyObjects()
        {
            Object.DestroyImmediate(_box.gameObject);
            _board.Dispose();
        }

        [Test]
        public void Init_IsHidden()
        {
            Assert.That(_renderer.enabled, Is.False);
        }

        [Test]
        public void Init_Twice_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _box.Init(_input, _board.View));
        }

        [Test]
        public void Refresh_WhileDragging_CoversTheDraggedCells()
        {
            // Board origin (-3, 2): cell (1,1) spans x -2..-1, y 3..4.
            _input.Handle(Frame(new Vector3(-1.5f, 3.5f, 0f), select: true));
            _input.Handle(Frame(new Vector3(0.5f, 4.5f, 0f)));

            _box.Refresh();

            Assert.That(_renderer.enabled, Is.True);
            Assert.That(_renderer.size, Is.EqualTo(new Vector2(3f, 2f)));
            // Cells (1..3, 1..2): x -2..1, y 3..5, centre (-0.5, 4).
            Assert.That(_box.transform.position, Is.EqualTo(new Vector3(-0.5f, 4f, 0f)));
        }

        [Test]
        public void Refresh_AfterRelease_Hides()
        {
            _input.Handle(Frame(new Vector3(-1.5f, 3.5f, 0f), select: true));
            _input.Handle(Frame(new Vector3(0.5f, 4.5f, 0f)));
            _box.Refresh();

            _input.Handle(new PointerInput(new Vector3(0.5f, 4.5f, 0f), false, false, false, false, false, false, true));
            _box.Refresh();

            Assert.That(_renderer.enabled, Is.False);
        }

        private static PointerInput Frame(Vector3 world, bool select = false)
        {
            return new PointerInput(world, false, select, false, false, false, selectHeld: true);
        }
    }
}
