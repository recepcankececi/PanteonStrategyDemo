using System;
using System.Collections.Generic;
using NUnit.Framework;
using StrategyDemo.Core;
using StrategyDemo.Data;
using StrategyDemo.Gameplay.Board;
using StrategyDemo.Gameplay.Buildings;
using StrategyDemo.Gameplay.Production;
using StrategyDemo.Gameplay.Units;
using StrategyDemo.Tests.EditMode.Support;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode.Gameplay.Production
{
    /// <summary>
    /// Soldier production rules: instant and unlimited (R3), only listed units (R5), always on the Barracks' spawn cell,
    /// from where the soldier walks to the nearest free cell (R6, T5), and refused while no free cell is reachable.
    /// </summary>
    public class UnitProducerTests
    {
        private const int Width = 10;
        private const int Height = 8;
        // The shipped Barracks is 3×3; placed at (3, 3) its bottom-middle spawn cell is (4, 2).
        private static readonly Vector2Int BarracksFootprint = new Vector2Int(3, 3);
        private static readonly Vector2Int BarracksOrigin = new Vector2Int(3, 3);
        private static readonly Vector2Int SpawnCell = new Vector2Int(4, 2);

        private CombatWorld _world;
        private TestUnitFactory _factory;
        private UnitProducer _producer;
        private UnitDefinition _soldier;
        private BuildingDefinition _barracksDefinition;
        private Building _barracks;

        [SetUp]
        public void CreateProducer()
        {
            _world = new CombatWorld(Width, Height);
            _soldier = _world.Board.Unit("Soldier1", damage: 10);
            _barracksDefinition = _world.Board.Producer("Barracks", BarracksFootprint, _soldier);
            _factory = new TestUnitFactory(_world.Board);
            _producer = new UnitProducer(_world.Grid, _world.Registry, _world.Reservations, _factory, _world.Movement);
            _barracks = _world.Place(_barracksDefinition, BarracksOrigin, SpawnCell);
        }

        [TearDown]
        public void DestroyWorld()
        {
            _factory.Dispose();
            _world.Dispose();
        }

        [Test]
        public void Constructor_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new UnitProducer(null, _world.Registry, _world.Reservations, _factory, _world.Movement));
            Assert.Throws<ArgumentNullException>(() => new UnitProducer(_world.Grid, null, _world.Reservations, _factory, _world.Movement));
            Assert.Throws<ArgumentNullException>(() => new UnitProducer(_world.Grid, _world.Registry, _world.Reservations, null, _world.Movement));
            Assert.Throws<ArgumentNullException>(() => new UnitProducer(_world.Grid, _world.Registry, _world.Reservations, _factory, null));
            Assert.Throws<ArgumentNullException>(() => new UnitProducer(_world.Grid, _world.Registry, null, _factory, _world.Movement));
        }

        [Test]
        public void AvailabilityVersion_ChangesWithGridOrReservations_NotOnReads()
        {
            int version = _producer.AvailabilityVersion;
            _producer.CanProduce(_barracks);
            Assert.That(_producer.AvailabilityVersion, Is.EqualTo(version), "Asking changes nothing.");

            _world.Place(new Vector2Int(0, 0), Vector2Int.one, 10);
            Assert.That(_producer.AvailabilityVersion, Is.Not.EqualTo(version), "A grid change.");
            version = _producer.AvailabilityVersion;

            _world.Reservations.ReserveStopCell(new Vector2Int(Width - 1, Height - 1), _world.Registry.NextId());
            Assert.That(_producer.AvailabilityVersion, Is.Not.EqualTo(version), "A reservation change.");
        }

        [Test]
        public void TryProduce_FreeBoard_SoldierAppearsOnSpawnCellAndHoldsIt()
        {
            bool produced = _producer.TryProduce(_barracks, _soldier, out Unit unit);

            Assert.That(produced, Is.True);
            Assert.That(unit.Origin, Is.EqualTo(SpawnCell));
            Assert.That(_world.Grid.GetOccupant(SpawnCell), Is.EqualTo(unit.OccupantId));
            Assert.That(_world.IsRegistered(unit), Is.True);
        }

        [Test]
        public void TryProduce_Success_SoldierWalksOffAndSpawnCellClears()
        {
            _producer.TryProduce(_barracks, _soldier, out Unit unit);

            _world.RunUntil(() => !_world.Movement.IsMoving(unit), "the soldier has stepped aside");

            Assert.That(unit.Origin, Is.Not.EqualTo(SpawnCell));
            Assert.That(GridDistance.Chebyshev(unit.Origin, SpawnCell), Is.EqualTo(1), "The nearest free cell is next to the spawn cell.");
            Assert.That(_world.Grid.GetOccupant(SpawnCell), Is.EqualTo(GridMap.EmptyId));
            Assert.That(_world.Reservations.OwnerOf(unit.Origin), Is.EqualTo(unit.OccupantId));
            Assert.That(_world.Reservations.OwnerOf(SpawnCell), Is.EqualTo(_barracks.OccupantId), "The spawn cell stays the Barracks'.");
        }

        [Test]
        public void TryProduce_ManyInOneFrame_AllAppearOnSpawnCellThenStandOnDistinctCells()
        {
            // Unlimited and instant (R3): ten clicks before a single frame has passed.
            var units = new List<Unit>();
            for (int i = 0; i < 10; i++)
            {
                Assert.That(_producer.TryProduce(_barracks, _soldier, out Unit unit), Is.True, $"soldier {i}");
                Assert.That(unit.Origin, Is.EqualTo(SpawnCell), $"soldier {i} appears on the spawn cell");
                units.Add(unit);
            }

            _world.RunUntil(() => _world.Movement.MovingCount == 0, "every soldier has stepped aside");

            var cells = new HashSet<Vector2Int>();
            foreach (Unit unit in units)
            {
                Assert.That(cells.Add(unit.Origin), Is.True, $"{unit.name} shares {unit.Origin}.");
                Assert.That(unit.Origin, Is.Not.EqualTo(SpawnCell));
                Assert.That(_world.Grid.GetOccupant(unit.Origin), Is.EqualTo(unit.OccupantId), $"{unit.name} holds its cell.");
            }
        }

        [Test]
        public void TryProduce_WhileFirstStillOnSpawnCell_SecondHoldsNoCellUntilItLeaves()
        {
            _producer.TryProduce(_barracks, _soldier, out Unit first);

            _producer.TryProduce(_barracks, _soldier, out Unit second);

            Assert.That(_world.Grid.GetOccupant(SpawnCell), Is.EqualTo(first.OccupantId), "One grid cell, one holder.");
            Assert.That(second.Origin, Is.EqualTo(SpawnCell));
            _world.RunUntil(() => _world.Movement.MovingCount == 0, "both soldiers have stepped aside");
            Assert.That(_world.Grid.GetOccupant(second.Origin), Is.EqualTo(second.OccupantId));
        }

        [Test]
        public void TryProduce_RingAroundSpawnFullOfSoldiers_LooksPastThem()
        {
            // Soldiers can step aside, so the search looks past them; production never stalls while the board has room.
            Vector2Int[] ring = { new Vector2Int(3, 2), new Vector2Int(5, 2), new Vector2Int(4, 1) };
            foreach (Vector2Int cell in ring)
            {
                _world.Spawn(cell, _soldier);
            }

            Assert.That(_producer.CanProduce(_barracks), Is.True);
            Assert.That(_producer.TryProduce(_barracks, _soldier, out Unit unit), Is.True);
            _world.RunUntil(() => !_world.Movement.IsMoving(unit), "the soldier has found a free cell");
            Assert.That(ring, Has.No.Member(unit.Origin));
            Assert.That(unit.Origin, Is.Not.EqualTo(SpawnCell));
        }

        [Test]
        public void TryProduce_BoardFull_ReturnsFalseAndChangesNothing()
        {
            FillEveryFreeCell();
            int registered = _world.Registry.Count;

            bool produced = _producer.TryProduce(_barracks, _soldier, out Unit unit);

            Assert.That(produced, Is.False);
            Assert.That(unit, Is.Null);
            Assert.That(_factory.Created, Is.Empty);
            Assert.That(_world.Registry.Count, Is.EqualTo(registered));
            Assert.That(_world.Grid.GetOccupant(SpawnCell), Is.EqualTo(GridMap.EmptyId));
        }

        [Test]
        public void CanProduce_BoardFull_IsFalse()
        {
            FillEveryFreeCell();

            Assert.That(_producer.CanProduce(_barracks), Is.False);
        }

        [Test]
        public void CanProduce_SpawnCellWalledInByOtherBuildings_IsFalse()
        {
            // A Barracks on the bottom row whose spawn cell has buildings left and right: the spawn cell is a dead end.
            Building edgeBarracks = _world.Place(_barracksDefinition, new Vector2Int(6, 1), new Vector2Int(7, 0));
            _world.Place(new Vector2Int(6, 0), Vector2Int.one, 10);
            _world.Place(new Vector2Int(8, 0), Vector2Int.one, 10);

            Assert.That(_producer.CanProduce(edgeBarracks), Is.False);
            Assert.That(_producer.TryProduce(edgeBarracks, _soldier, out _), Is.False);
        }

        [Test]
        public void CanProduce_LastFreeCellFreesUp_TurnsTrue()
        {
            FillEveryFreeCell();
            Assert.That(_producer.CanProduce(_barracks), Is.False);

            // One soldier-sized blocker next to the spawn cell goes away (e.g. destroyed).
            Vector2Int freed = new Vector2Int(4, 1);
            _world.Grid.Release(freed, Vector2Int.one, _world.Grid.GetOccupant(freed));

            Assert.That(_producer.CanProduce(_barracks), Is.True);
        }

        [Test]
        public void CanProduce_BuildingWithoutSpawnCell_IsFalse()
        {
            Building powerPlant = _world.Place(new Vector2Int(0, 6), new Vector2Int(2, 2), 50);

            Assert.That(_producer.CanProduce(powerPlant), Is.False);
        }

        [Test]
        public void CanProduce_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _producer.CanProduce(null));
        }

        [Test]
        public void TryProduce_BuildingThatProducesNothing_Throws()
        {
            // R5: the Power Plant has no production; the rule holds even if a caller bypasses the UI.
            Building powerPlant = _world.Place(new Vector2Int(0, 6), new Vector2Int(2, 2), 50);

            Assert.Throws<ArgumentException>(() => _producer.TryProduce(powerPlant, _soldier, out _));
            Assert.That(_factory.Created, Is.Empty);
        }

        [Test]
        public void TryProduce_UnitNotListedByBuilding_Throws()
        {
            UnitDefinition other = _world.Board.Unit("Other");

            Assert.Throws<ArgumentException>(() => _producer.TryProduce(_barracks, other, out _));
        }

        [Test]
        public void TryProduce_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _producer.TryProduce(null, _soldier, out _));
            Assert.Throws<ArgumentNullException>(() => _producer.TryProduce(_barracks, null, out _));
        }

        [Test]
        public void TryProduce_FactoryThrows_GivesCellBackAndRethrows()
        {
            _factory.Fails = true;
            int registered = _world.Registry.Count;

            Assert.Throws<InvalidOperationException>(() => _producer.TryProduce(_barracks, _soldier, out _));
            Assert.That(_world.Grid.GetOccupant(SpawnCell), Is.EqualTo(GridMap.EmptyId));
            Assert.That(_world.Registry.Count, Is.EqualTo(registered));
            Assert.That(_world.Movement.MovingCount, Is.Zero);
        }

        [Test]
        public void MoveOrder_ToTheSpawnCell_StopsBesideIt()
        {
            // A spawn cell is the Barracks' reservation: a soldier ordered onto it stops on the nearest free cell instead.
            Unit unit = _world.Spawn(new Vector2Int(0, 0), _soldier);

            _world.Movement.MoveGroup(new IBoardOccupant[] { unit }, SpawnCell);
            _world.RunUntil(() => !_world.Movement.IsMoving(unit), "the soldier has arrived");

            Assert.That(unit.Origin, Is.Not.EqualTo(SpawnCell));
            Assert.That(GridDistance.Chebyshev(unit.Origin, SpawnCell), Is.EqualTo(1));
        }

        private void FillEveryFreeCell()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (_world.Grid.GetOccupant(cell) == GridMap.EmptyId)
                    {
                        // Walls, not soldiers: soldiers could be looked past. The spawn cell itself stays empty.
                        if (cell != SpawnCell)
                        {
                            _world.Place(cell, Vector2Int.one, 10);
                        }
                    }
                }
            }
        }
    }
}
