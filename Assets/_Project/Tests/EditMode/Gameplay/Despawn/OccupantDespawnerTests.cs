using System;
using System.Collections.Generic;
using NUnit.Framework;
using StrategyDemo.Core;
using StrategyDemo.Data;
using StrategyDemo.Gameplay.Board;
using StrategyDemo.Gameplay.Buildings;
using StrategyDemo.Gameplay.Despawn;
using StrategyDemo.Gameplay.Movement;
using StrategyDemo.Gameplay.Placement;
using StrategyDemo.Gameplay.Selection;
using StrategyDemo.Gameplay.Units;
using StrategyDemo.Tests.EditMode.Support;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode.Gameplay.Despawn
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
            CellReservations c = _world.Reservations;
            UnitMovement m = _world.Movement;
            SelectionService s = _world.Selection;

            Assert.Throws<ArgumentNullException>(() => new OccupantDespawner(null, r, c, m, s));
            Assert.Throws<ArgumentNullException>(() => new OccupantDespawner(g, null, c, m, s));
            Assert.Throws<ArgumentNullException>(() => new OccupantDespawner(g, r, null, m, s));
            Assert.Throws<ArgumentNullException>(() => new OccupantDespawner(g, r, c, null, s));
            Assert.Throws<ArgumentNullException>(() => new OccupantDespawner(g, r, c, m, null));
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
        public void Despawn_Barracks_GivesBackItsSpawnCell()
        {
            BuildingDefinition barracks = _world.Board.Producer("Barracks", new Vector2Int(3, 3), _soldier);
            var spawnCell = new Vector2Int(4, 2);
            Building building = _world.Place(barracks, new Vector2Int(3, 3), spawnCell);
            int id = building.OccupantId;

            _world.Despawner.Despawn(building);

            Assert.That(_world.Reservations.OwnerOf(spawnCell), Is.EqualTo(GridMap.EmptyId), "An ordinary cell again: soldiers may stop there.");
            Assert.That(_world.Reservations.TryGetCell(id, out _), Is.False);
        }

        [Test]
        public void Despawn_BarracksUnderStillPreview_PreviewOverItsSpawnCellTurnsValid()
        {
            // The preview hears only grid events; the spawn cell must already be free when the grid event fires.
            BuildingDefinition barracks = _world.Board.Producer("Barracks", new Vector2Int(3, 3), _soldier);
            Building building = _world.Place(barracks, new Vector2Int(3, 3), new Vector2Int(4, 2));
            var placer = new BuildingPlacer(_world.Grid, _world.Registry, _world.Reservations, _world.BuildingFactory);
            int changed = 0;
            placer.Changed += () => changed++;
            placer.Begin(_world.Board.Definition("GuardTower", Vector2Int.one));
            placer.MoveTo(new Vector2Int(4, 2));
            Assert.That(placer.IsCandidateValid, Is.False, "On the Barracks' spawn cell.");
            changed = 0;

            _world.Despawner.Despawn(building);

            Assert.That(placer.IsCandidateValid, Is.True);
            Assert.That(changed, Is.EqualTo(1));
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
        public void Despawn_NewOccupantType_RunsItsOwnStepsInOrder()
        {
            // Open/Closed: a type the despawner has never heard of is removed through the interface alone.
            var wall = new FakeDespawnable(_world.Registry.NextId(), new Vector2Int(1, 1));
            _world.Grid.Occupy(wall.Origin, wall.Footprint, wall.OccupantId);
            _world.Registry.Add(wall);
            _world.Despawner.Despawned += gone => wall.Steps.Add("despawned");

            bool removed = _world.Despawner.Despawn(wall);

            Assert.That(removed, Is.True);
            Assert.That(wall.Steps, Is.EqualTo(new[] { "leave", "despawned", "pool" }));
            Assert.That(_world.Grid.GetOccupant(wall.Origin), Is.EqualTo(GridMap.EmptyId));
            Assert.That(_world.IsRegistered(wall), Is.False);
        }

        [Test]
        public void ReturnToPool_SoldierNotMadeByAFactory_Throws()
        {
            Unit orphan = TestBoard.CreateUnitObject("Orphan");
            try
            {
                orphan.Init(_soldier, new Vector2Int(1, 1), 99, Vector3.zero);

                Assert.Throws<InvalidOperationException>(() => ((IDespawnable)orphan).ReturnToPool());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(orphan.gameObject);
            }
        }

        /// <summary>An occupant type the despawner does not know, recording the steps it is asked to take.</summary>
        private sealed class FakeDespawnable : IDespawnable
        {
            public FakeDespawnable(int occupantId, Vector2Int origin)
            {
                OccupantId = occupantId;
                Origin = origin;
            }

            public List<string> Steps { get; } = new List<string>();
            public int OccupantId { get; }
            public Vector2Int Origin { get; }
            public Vector2Int Footprint => Vector2Int.one;
            public bool IsMobile => false;
            public Vector3 WorldCenter => Vector3.zero;

            public void LeaveBoard(DespawnContext board)
            {
                Steps.Add("leave");
                board.Grid.Release(Origin, Footprint, OccupantId);
            }

            public void ReturnToPool()
            {
                Steps.Add("pool");
            }
        }
    }
}
