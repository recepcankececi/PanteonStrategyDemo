using System;
using NUnit.Framework;
using StrategyDemo.Data;
using StrategyDemo.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Pooled soldier scene objects: parenting, naming, position, animation and reuse.</summary>
    public class UnitFactoryTests
    {
        private TestBoard _board;
        private GameObject _prefabObject;
        private Transform _units;
        private Transform _pool;
        private UnitFactory _factory;
        private UnitDefinition _soldier;

        [SetUp]
        public void CreateFactory()
        {
            _board = new TestBoard(new Vector3(-3f, 2f, 0f), 10, 8);
            _soldier = _board.Unit("Soldier1", frameCount: 3);
            _prefabObject = new GameObject("Unit", typeof(SpriteRenderer), typeof(SpriteFlipbook), typeof(Unit));
            _units = new GameObject("Units").transform;
            _pool = new GameObject("Pool_Unit").transform;
            _factory = new UnitFactory(_prefabObject.GetComponent<Unit>(), _units, _pool, _board.View);
        }

        [TearDown]
        public void DestroyObjects()
        {
            Object.DestroyImmediate(_units.gameObject);
            Object.DestroyImmediate(_pool.gameObject);
            Object.DestroyImmediate(_prefabObject);
            _board.Dispose();
        }

        [Test]
        public void Constructor_NullArgument_Throws()
        {
            var prefab = _prefabObject.GetComponent<Unit>();
            Assert.Throws<ArgumentNullException>(() => new UnitFactory(null, _units, _pool, _board.View));
            Assert.Throws<ArgumentNullException>(() => new UnitFactory(prefab, null, _pool, _board.View));
            Assert.Throws<ArgumentNullException>(() => new UnitFactory(prefab, _units, null, _board.View));
            Assert.Throws<ArgumentNullException>(() => new UnitFactory(prefab, _units, _pool, null));
        }

        [Test]
        public void Create_PutsActiveSoldierUnderUnitsWithDataNameAndCell()
        {
            var unit = (Unit)_factory.Create(_soldier, new Vector2Int(2, 1), 7);

            Assert.That(unit.transform.parent, Is.SameAs(_units));
            Assert.That(unit.gameObject.activeSelf, Is.True);
            Assert.That(unit.name, Is.EqualTo("Soldier1_7"));
            Assert.That(unit.Definition, Is.SameAs(_soldier));
            Assert.That(unit.OccupantId, Is.EqualTo(7));
            Assert.That(unit.Origin, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(unit.Footprint, Is.EqualTo(Vector2Int.one));
        }

        [Test]
        public void Create_PlacesPivotAtBottomMiddleOfCell()
        {
            Unit unit = (Unit)_factory.Create(_soldier, new Vector2Int(2, 1), 1);

            // Board origin (-3, 2): cell (2, 1) spans x -1..0 and y 3..4.
            Assert.That(unit.transform.position, Is.EqualTo(new Vector3(-0.5f, 3f, 0f)));
        }

        [Test]
        public void Create_StartsIdleAnimationOnAFrameOfTheClip()
        {
            Unit unit = (Unit)_factory.Create(_soldier, Vector2Int.zero, 4);
            var flipbook = unit.GetComponent<SpriteFlipbook>();

            Assert.That(flipbook.IsPlaying, Is.True);
            Assert.That(_soldier.IdleFrames, Has.Member(unit.GetComponent<SpriteRenderer>().sprite));
        }

        [Test]
        public void Release_MovesSoldierToPoolInactiveAndCleared()
        {
            Unit unit = (Unit)_factory.Create(_soldier, Vector2Int.zero, 1);

            _factory.Release(unit);

            Assert.That(unit.transform.parent, Is.SameAs(_pool));
            Assert.That(unit.gameObject.activeSelf, Is.False);
            Assert.That(unit.Definition, Is.Null);
            Assert.That(unit.OccupantId, Is.Zero);
            Assert.That(unit.GetComponent<SpriteRenderer>().sprite, Is.Null);
            Assert.That(unit.GetComponent<SpriteFlipbook>().IsPlaying, Is.False);
        }

        [Test]
        public void Create_AfterRelease_ReusesSoldierInsteadOfInstantiating()
        {
            Unit first = (Unit)_factory.Create(_soldier, Vector2Int.zero, 1);
            _factory.Release(first);

            Unit second = (Unit)_factory.Create(_soldier, Vector2Int.one, 2);

            Assert.That(second, Is.SameAs(first));
            Assert.That(_factory.CreatedCount, Is.EqualTo(1));
            Assert.That(second.name, Is.EqualTo("Soldier1_2"));
        }

        [Test]
        public void Release_Twice_Throws()
        {
            Unit unit = (Unit)_factory.Create(_soldier, Vector2Int.zero, 1);
            _factory.Release(unit);

            Assert.Throws<InvalidOperationException>(() => _factory.Release(unit));
        }

        [Test]
        public void Create_DefinitionWithoutFrames_ThrowsAndLeavesNoSoldierOnBoard()
        {
            UnitDefinition broken = _board.Unit("Broken", frameCount: 0);

            Assert.Throws<ArgumentException>(() => _factory.Create(broken, Vector2Int.zero, 1));
            Assert.That(_units.childCount, Is.Zero);
            Assert.That(_pool.childCount, Is.EqualTo(1), "The soldier went back to the pool for reuse.");
        }

        [Test]
        public void ContainsPoint_OnSoldierImage_TrueAndAfterReleaseFalse()
        {
            // Test frames are 1×1 world unit with the pivot at the bottom middle: the image spans x -1..0, y 3..4.
            Unit unit = (Unit)_factory.Create(_soldier, new Vector2Int(2, 1), 1);

            Assert.That(unit.ContainsPoint(new Vector2(-0.5f, 3.5f)), Is.True);
            Assert.That(unit.ContainsPoint(new Vector2(-0.5f, 4.5f)), Is.False);
            Assert.That(unit.IsMobile, Is.True);

            _factory.Release(unit);

            Assert.That(unit.ContainsPoint(new Vector2(-0.5f, 3.5f)), Is.False, "A pooled soldier cannot be clicked.");
        }

        [Test]
        public void Create_NullDefinition_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _factory.Create(null, Vector2Int.zero, 1));
        }
    }
}
