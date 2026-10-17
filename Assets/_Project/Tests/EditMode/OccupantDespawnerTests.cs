using System;
using System.Collections.Generic;
using NUnit.Framework;
using StrategyDemo.Core;
using StrategyDemo.Data;
using StrategyDemo.Gameplay;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>The one remove path (R10): grid, reservations, movement, registry, selection, event, pool — in that order.</summary>
    public class OccupantDespawnerTests
    {
        private CombatWorld _world;
        private UnitDefinition _soldier;

        [SetUp]
        public void CreateWorld()
        {
            _world = new CombatWorld(10, 8);
            _soldier = _world.Board.Unit("Soldier");
        }

        [TearDown]
        public void DestroyWorld()
        {
            _world.Dispose();
        }

        [Test]
        public void Constructor_NullArgument_Throws()
        {
            GridMap g = _world.Grid;
            OccupantRegistry r = _world.Registry;
            UnitMovement m = _world.Movement;
            SelectionService s = _world.Selection;
            IUnitFactory u = _world.UnitFactory;
            IBuildingFactory b = _world.BuildingFactory;

            Assert.Throws<ArgumentNullException>(() => new OccupantDespawner(null, r, m, s, u, b));
            Assert.Throws<ArgumentNullException>(() => new OccupantDespawner(g, null, m, s, u, b));
            Assert.Throws<ArgumentNullException>(() => new OccupantDespawner(g, r, null, s, u, b));
            Assert.Throws<ArgumentNullException>(() => new OccupantDespawner(g, r, m, null, u, b));
            Assert.Throws<ArgumentNullException>(() => new OccupantDespawner(g, r, m, s, null, b));
            Assert.Throws<ArgumentNullException>(() => new OccupantDespawner(g, r, m, s, u, null));
        }

        [Test]
        public void Despawn_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _world.Despawner.Despawn(null));
        }

        [Test]
        public void Despawn_StandingSoldier_FreesCellAndReservationAndReleasesToPool()
        {
            Unit unit = _world.Spawn(new Vector2Int(2, 2), _soldier);
            int id = unit.OccupantId;

            bool removed = _world.Despawner.Despawn(unit);

            Assert.That(removed, Is.True);
            Assert.That(_world.Grid.GetOccupant(new Vector2Int(2, 2)), Is.EqualTo(GridMap.EmptyId));
            Assert.That(_world.Reservations.TryGetCell(id, out _), Is.False);
            Assert.That(_world.Registry.TryGet(id, out _), Is.False);
            Assert.That(_world.UnitFactory.ReleasedCount, Is.EqualTo(1));
        }

        [Test]
        public void Despawn_WalkingSoldier_DropsItsMoveOrder()
        {
            Unit unit = _world.Spawn(new Vector2Int(0, 0), _soldier);
            _world.Movement.MoveGroup(new IBoardOccupant[] { unit }, new Vector2Int(9, 7));
            _world.Run(0.5f);

            _world.Despawner.Despawn(unit);

            Assert.That(_world.Movement.MovingCount, Is.Zero);
            Assert.That(_world.Reservations.OwnerOf(new Vector2Int(9, 7)), Is.EqualTo(GridMap.EmptyId));
        }

        [Test]
        public void Despawn_Building_FreesItsWholeFootprint()
        {
            Building building = _world.Place(new Vector2Int(3, 3), new Vector2Int(3, 2), 100);

            _world.Despawner.Despawn(building);

            Assert.That(_world.Grid.IsAreaFree(new Vector2Int(3, 3), new Vector2Int(3, 2)), Is.True);
            Assert.That(_world.BuildingFactory.ReleasedCount, Is.EqualTo(1));
        }

        [Test]
        public void Despawn_Selected_LeavesSelectionBeforeEventAndPool()
        {
            Building building = _world.Place(new Vector2Int(3, 3), new Vector2Int(2, 2), 100);
            _world.Selection.SelectAt(new Vector2Int(3, 3));
            var seen = new List<string>();
            _world.Selection.SelectionChanged += () => seen.Add("selection");
            _world.Despawner.Despawned += gone =>
            {
                seen.Add("despawned");
                Assert.That(gone, Is.SameAs(building));
                Assert.That(_world.IsRegistered(gone), Is.False, "Off the registry before listeners hear of it.");
                Assert.That(_world.BuildingFactory.ReleasedCount, Is.Zero, "Not pooled yet: listeners still see it intact.");
            };

            _world.Despawner.Despawn(building);

            Assert.That(seen, Is.EqualTo(new[] { "selection", "despawned" }));
            Assert.That(_world.Selection.Selected, Is.Null);
        }

        [Test]
        public void Despawn_Twice_SecondCallChangesNothing()
        {
            Unit unit = _world.Spawn(new Vector2Int(2, 2), _soldier);
            _world.Despawner.Despawn(unit);

            bool removedAgain = _world.Despawner.Despawn(unit);

            Assert.That(removedAgain, Is.False);
            Assert.That(_world.UnitFactory.ReleasedCount, Is.EqualTo(1), "A double release would hand one object to two owners.");
        }

        [Test]
        public void Despawn_ApproachedTarget_ApproachersCarryOnAsPlainMove()
        {
            Building tower = _world.Place(new Vector2Int(8, 6), Vector2Int.one, 10);
            Unit walker = _world.Spawn(new Vector2Int(0, 0), _soldier);
            _world.Movement.Approach(walker, tower);
            Assert.That(_world.Reservations.TryGetCell(walker.OccupantId, out Vector2Int stopCell), Is.True);

            _world.Despawner.Despawn(tower);
            _world.RunUntil(() => _world.Movement.MovingCount == 0, "the walker stops");

            Assert.That(walker.Origin, Is.EqualTo(stopCell), "Planning again must not read the pooled target.");
        }

        [Test]
        public void Despawn_UnknownOccupantType_Throws()
        {
            var fake = new FakeOccupant(_world.Registry.NextId(), new Vector2Int(1, 1), Vector2Int.one);
            _world.Registry.Add(fake);

            Assert.Throws<ArgumentException>(() => _world.Despawner.Despawn(fake));
        }
    }
}
