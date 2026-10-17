using System;
using NUnit.Framework;
using StrategyDemo.Data;
using StrategyDemo.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Placed buildings: parent container, unique name, pivot position, sprite and runtime data.</summary>
    public class BuildingFactoryTests
    {
        private static readonly Vector3 BoardOrigin = new Vector3(2f, -3f, 0f);

        private TestBoard _board;
        private GameObject _prefabObject;
        private Transform _container;
        private Transform _pool;
        private BuildingFactory _factory;
        private BuildingDefinition _barracks;

        [SetUp]
        public void CreateFactory()
        {
            _board = new TestBoard(BoardOrigin, 10, 8);
            _barracks = _board.Definition("Barracks", new Vector2Int(3, 2));
            // Stands in for the Building prefab: a template object the factory clones.
            _prefabObject = TestBoard.CreateBuildingObject("BuildingTemplate").gameObject;
            _container = new GameObject("Buildings").transform;
            _pool = new GameObject("Pool_Building").transform;
            _factory = new BuildingFactory(_prefabObject.GetComponent<Building>(), _container, _pool, _board.View);
        }

        [TearDown]
        public void DestroyObjects()
        {
            Object.DestroyImmediate(_container.gameObject);
            Object.DestroyImmediate(_pool.gameObject);
            Object.DestroyImmediate(_prefabObject);
            _board.Dispose();
        }

        [Test]
        public void Constructor_NullArgument_Throws()
        {
            Building prefab = _prefabObject.GetComponent<Building>();

            Assert.Throws<ArgumentNullException>(() => new BuildingFactory(null, _container, _pool, _board.View));
            Assert.Throws<ArgumentNullException>(() => new BuildingFactory(prefab, null, _pool, _board.View));
            Assert.Throws<ArgumentNullException>(() => new BuildingFactory(prefab, _container, null, _board.View));
            Assert.Throws<ArgumentNullException>(() => new BuildingFactory(prefab, _container, _pool, null));
        }

        [Test]
        public void Create_PutsBuildingUnderContainerWithDataNameAndSprite()
        {
            _factory.Create(_barracks, new Vector2Int(4, 2), 7);

            Assert.That(_container.childCount, Is.EqualTo(1));
            Building building = _container.GetChild(0).GetComponent<Building>();
            Assert.That(building.Definition, Is.SameAs(_barracks));
            Assert.That(building.Origin, Is.EqualTo(new Vector2Int(4, 2)));
            Assert.That(building.OccupantId, Is.EqualTo(7));
            Assert.That(building.Footprint, Is.EqualTo(new Vector2Int(3, 2)));
            Assert.That(building.name, Is.EqualTo("Barracks_7"));
            Assert.That(building.GetComponent<SpriteRenderer>().sprite, Is.SameAs(_barracks.Sprite));
        }

        [Test]
        public void Create_PlacesPivotAtCentreOfFootprint()
        {
            var building = (Building)_factory.Create(_barracks, new Vector2Int(4, 2), 1);

            // Cells (4..6, 2..3): centre at x = 4 + 1.5, y = 2 + 1.
            Assert.That(building.transform.position, Is.EqualTo(BoardOrigin + new Vector3(5.5f, 3f, 0f)));
            Assert.That(building.IsMobile, Is.False);
        }

        [Test]
        public void Release_ThenCreate_ReusesTheBuildingUnderPoolInBetween()
        {
            var first = (Building)_factory.Create(_barracks, new Vector2Int(4, 2), 1);
            first.Health.TakeDamage(10);

            _factory.Release(first);
            Assert.That(first.transform.parent, Is.SameAs(_pool));
            Assert.That(first.gameObject.activeSelf, Is.False);
            Assert.That(first.Definition, Is.Null, "Reset on release: no stale type.");

            var second = (Building)_factory.Create(_barracks, new Vector2Int(0, 0), 2);
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.transform.parent, Is.SameAs(_container));
            Assert.That(second.Health.Current, Is.EqualTo(second.Health.Max), "Reused at full health.");
            Assert.That(_factory.CreatedCount, Is.EqualTo(1));
        }

        [Test]
        public void Footprint_OfReleasedBuilding_ThrowsClearError()
        {
            var building = (Building)_factory.Create(_barracks, new Vector2Int(4, 2), 1);
            _factory.Release(building);

            Assert.Throws<InvalidOperationException>(() => _ = building.Footprint);
        }

        [Test]
        public void Release_Twice_Throws()
        {
            var building = (Building)_factory.Create(_barracks, new Vector2Int(4, 2), 1);
            _factory.Release(building);

            Assert.Throws<InvalidOperationException>(() => _factory.Release(building));
        }

        [Test]
        public void Building_InitNullDefinition_Throws()
        {
            Building building = _prefabObject.GetComponent<Building>();

            Assert.Throws<ArgumentNullException>(() => building.Init(null, Vector2Int.zero, 1, Vector3.zero));
        }
    }
}
