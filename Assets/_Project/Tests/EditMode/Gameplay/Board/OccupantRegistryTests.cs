using System;
using NUnit.Framework;
using StrategyDemo.Gameplay.Board;
using StrategyDemo.Tests.EditMode.Support;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode.Gameplay.Board
{
    /// <summary>Id hand-out and id → occupant lookup behind selection (and later attacks).</summary>
    public class OccupantRegistryTests
    {
        private OccupantRegistry _registry;

        [SetUp]
        public void CreateRegistry()
        {
            _registry = new OccupantRegistry();
        }

        [Test]
        public void NextId_StartsAtOneAndNeverRepeats()
        {
            Assert.That(_registry.NextId(), Is.EqualTo(1));
            Assert.That(_registry.NextId(), Is.EqualTo(2));
        }

        [Test]
        public void Add_ThenTryGet_ReturnsSameOccupant()
        {
            var occupant = new FakeOccupant(_registry.NextId(), Vector2Int.zero, Vector2Int.one);

            _registry.Add(occupant);

            Assert.That(_registry.TryGet(occupant.OccupantId, out IBoardOccupant found), Is.True);
            Assert.That(found, Is.SameAs(occupant));
            Assert.That(_registry.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryGet_UnknownId_ReturnsFalse()
        {
            Assert.That(_registry.TryGet(5, out IBoardOccupant found), Is.False);
            Assert.That(found, Is.Null);
        }

        [Test]
        public void Add_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _registry.Add(null));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(2)]
        public void Add_IdNotHandedOut_Throws(int id)
        {
            _registry.NextId();

            Assert.Throws<ArgumentException>(() => _registry.Add(new FakeOccupant(id, Vector2Int.zero, Vector2Int.one)));
        }

        [Test]
        public void Add_SameIdTwice_Throws()
        {
            int id = _registry.NextId();
            _registry.Add(new FakeOccupant(id, Vector2Int.zero, Vector2Int.one));

            Assert.Throws<ArgumentException>(() => _registry.Add(new FakeOccupant(id, Vector2Int.one, Vector2Int.one)));
        }

        [Test]
        public void Remove_RegisteredId_ForgetsItButKeepsIdUsed()
        {
            int id = _registry.NextId();
            _registry.Add(new FakeOccupant(id, Vector2Int.zero, Vector2Int.one));

            Assert.That(_registry.Remove(id), Is.True);
            Assert.That(_registry.TryGet(id, out _), Is.False);
            Assert.That(_registry.Remove(id), Is.False);
            Assert.That(_registry.NextId(), Is.EqualTo(id + 1), "A removed id is never handed out again.");
        }

        [Test]
        public void TryGetAt_CellOfRegisteredOccupant_ReturnsIt()
        {
            var grid = new StrategyDemo.Core.GridMap(5, 5);
            var building = new FakeOccupant(_registry.NextId(), new Vector2Int(1, 1), new Vector2Int(2, 2));
            grid.Occupy(building.Origin, building.Footprint, building.OccupantId);
            _registry.Add(building);

            Assert.That(_registry.TryGetAt(grid, new Vector2Int(2, 2), out IBoardOccupant found), Is.True);
            Assert.That(found, Is.SameAs(building));
        }

        [Test]
        public void TryGetAt_EmptyOffBoardOrUnregisteredCell_ReturnsFalse()
        {
            var grid = new StrategyDemo.Core.GridMap(5, 5);
            grid.Occupy(new Vector2Int(4, 4), Vector2Int.one, _registry.NextId());

            Assert.That(_registry.TryGetAt(grid, new Vector2Int(0, 0), out _), Is.False);
            Assert.That(_registry.TryGetAt(grid, new Vector2Int(-1, 0), out _), Is.False);
            Assert.That(_registry.TryGetAt(grid, new Vector2Int(4, 4), out _), Is.False);
            Assert.Throws<ArgumentNullException>(() => _registry.TryGetAt(null, Vector2Int.zero, out _));
        }
    }
}
