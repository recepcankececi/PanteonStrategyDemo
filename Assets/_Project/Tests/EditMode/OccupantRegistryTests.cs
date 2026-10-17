using System;
using NUnit.Framework;
using StrategyDemo.Gameplay;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
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
    }
}
