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
        private BuildingFactory _factory;
        private BuildingDefinition _barracks;

        [SetUp]
        public void CreateFactory()
        {
            _board = new TestBoard(BoardOrigin, 10, 8);
            _barracks = _board.Definition("Barracks", new Vector2Int(3, 2));
            // Stands in for the Building prefab: a template object the factory clones.
            _prefabObject = new GameObject("BuildingTemplate", typeof(SpriteRenderer), typeof(Building));
            _container = new GameObject("Buildings").transform;
            _factory = new BuildingFactory(_prefabObject.GetComponent<Building>(), _container, _board.View);
        }

        [TearDown]
        public void DestroyObjects()
        {
            Object.DestroyImmediate(_container.gameObject);
            Object.DestroyImmediate(_prefabObject);
            _board.Dispose();
        }

        [Test]
        public void Constructor_NullArgument_Throws()
        {
            Building prefab = _prefabObject.GetComponent<Building>();

            Assert.Throws<ArgumentNullException>(() => new BuildingFactory(null, _container, _board.View));
            Assert.Throws<ArgumentNullException>(() => new BuildingFactory(prefab, null, _board.View));
            Assert.Throws<ArgumentNullException>(() => new BuildingFactory(prefab, _container, null));
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
        public void Create_PlacesPivotAtBottomMiddleOfFootprint()
        {
            _factory.Create(_barracks, new Vector2Int(4, 2), 1);

            // Cells (4..6, 2..3): bottom edge middle at x = 4 + 1.5, y = 2.
            Assert.That(_container.GetChild(0).position, Is.EqualTo(BoardOrigin + new Vector3(5.5f, 2f, 0f)));
        }

        [Test]
        public void Building_InitNullDefinition_Throws()
        {
            Building building = _prefabObject.GetComponent<Building>();

            Assert.Throws<ArgumentNullException>(() => building.Init(null, Vector2Int.zero, 1, Vector3.zero));
        }
    }
}
