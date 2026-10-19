using System;
using NUnit.Framework;
using StrategyDemo.Data;
using StrategyDemo.Gameplay.Despawn;
using StrategyDemo.Gameplay.Units;
using StrategyDemo.Tests.EditMode.Support;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Tests.EditMode.Gameplay.Units
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
            _soldier = _board.Unit("Assault", frameCount: 3);
            _prefabObject = TestBoard.CreateUnitObject("Unit").gameObject;
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
        public void ReturnToPool_ThreeHealthListenersAfterWarmUp_AllocatesNothing()
        {
            // Warm-up: the pool's containers grow on the first release.
            var warm = (Unit)_factory.Create(_soldier, new Vector2Int(1, 1), 1);
            ((IDespawnable)warm).ReturnToPool();
            var unit = (Unit)_factory.Create(_soldier, new Vector2Int(2, 1), 2);
            // A third listener, like the information panel following a selected soldier: removing one listener of
            // three would allocate a new delegate, so the reset must drop them all at once instead.
            unit.Health.Changed += ThirdListener;

            AllocationAssert.None(() => ((IDespawnable)unit).ReturnToPool());
            Assert.That(unit.name, Is.EqualTo("Unit"), "Renamed to the prefab name from a cached string.");
        }

        [Test]
        public void Create_PutsActiveSoldierUnderUnitsWithDataNameAndCell()
        {
            var unit = (Unit)_factory.Create(_soldier, new Vector2Int(2, 1), 7);

            Assert.That(unit.transform.parent, Is.SameAs(_units));
            Assert.That(unit.gameObject.activeSelf, Is.True);
            Assert.That(unit.name, Is.EqualTo("Assault_7"));
            Assert.That(unit.Definition, Is.SameAs(_soldier));
            Assert.That(unit.OccupantId, Is.EqualTo(7));
            Assert.That(unit.Origin, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(unit.Footprint, Is.EqualTo(Vector2Int.one));
        }

        [Test]
        public void Create_PlacesPivotAtCentreOfCell()
        {
            Unit unit = (Unit)_factory.Create(_soldier, new Vector2Int(2, 1), 1);

            // Board origin (-3, 2): cell (2, 1) spans x -1..0 and y 3..4.
            Assert.That(unit.transform.position, Is.EqualTo(new Vector3(-0.5f, 3.5f, 0f)));
        }

        [Test]
        public void Create_ShowsStandingSpriteWithoutAnimation()
        {
            Unit unit = (Unit)_factory.Create(_soldier, Vector2Int.zero, 4);

            Assert.That(unit.GetComponentInChildren<SpriteFlipbook>().IsPlaying, Is.False, "A new soldier stands still.");
            Assert.That(unit.GetComponentInChildren<SpriteRenderer>().sprite, Is.SameAs(_soldier.Sprite));
            Assert.That(unit.IsWalking, Is.False);
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
            Assert.That(unit.GetComponentInChildren<SpriteRenderer>().sprite, Is.Null);
            Assert.That(unit.GetComponentInChildren<SpriteFlipbook>().IsPlaying, Is.False);
        }

        [Test]
        public void Create_AfterRelease_ReusesSoldierInsteadOfInstantiating()
        {
            Unit first = (Unit)_factory.Create(_soldier, Vector2Int.zero, 1);
            _factory.Release(first);

            Unit second = (Unit)_factory.Create(_soldier, Vector2Int.one, 2);

            Assert.That(second, Is.SameAs(first));
            Assert.That(_factory.CreatedCount, Is.EqualTo(1));
            Assert.That(second.name, Is.EqualTo("Assault_2"));
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
        public void Release_WalkingTurnedSoldier_ComesBackStandingAndFacingUp()
        {
            Unit unit = (Unit)_factory.Create(_soldier, Vector2Int.zero, 1);
            unit.SetWalking(true);
            unit.FaceTowards(Vector2.left);

            _factory.Release(unit);
            Unit reused = (Unit)_factory.Create(_soldier, Vector2Int.one, 2);

            Assert.That(reused, Is.SameAs(unit));
            Assert.That(reused.IsWalking, Is.False);
            Assert.That(reused.GetComponentInChildren<SpriteFlipbook>().IsPlaying, Is.False);
            Assert.That(reused.GetComponentInChildren<SpriteRenderer>().transform.rotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void Create_NullDefinition_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _factory.Create(null, Vector2Int.zero, 1));
        }

        private static void ThirdListener()
        {
        }
    }
}
