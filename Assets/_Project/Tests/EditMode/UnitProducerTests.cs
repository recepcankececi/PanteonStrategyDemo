using System;
using System.Collections.Generic;
using NUnit.Framework;
using StrategyDemo.Core;
using StrategyDemo.Data;
using StrategyDemo.Gameplay;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Soldier production rules: instant and unlimited (R3), only listed units (R5), spawn point and fallback (R6, T5).</summary>
    public class UnitProducerTests
    {
        private const int Width = 10;
        private const int Height = 8;
        // Barracks is 3×2 and spawns below the middle of its bottom row (the shipped asset; see M4AssetTests).
        private static readonly Vector2Int BarracksFootprint = new Vector2Int(3, 2);
        private static readonly Vector2Int BarracksSpawnOffset = new Vector2Int(1, -1);

        private TestBoard _board;
        private GridMap _grid;
        private OccupantRegistry _registry;
        private RecordingUnitFactory _factory;
        private UnitProducer _producer;
        private UnitDefinition _soldier;
        private BuildingDefinition _barracks;
        private BuildingDefinition _powerPlant;

        [SetUp]
        public void CreateProducer()
        {
            _board = new TestBoard(Vector3.zero, Width, Height);
            _soldier = _board.Unit("Soldier1", damage: 10);
            _barracks = _board.Producer("Barracks", BarracksFootprint, BarracksSpawnOffset, _soldier);
            _powerPlant = _board.Definition("PowerPlant", new Vector2Int(2, 2));
            _grid = new GridMap(Width, Height);
            _registry = new OccupantRegistry();
            _factory = new RecordingUnitFactory();
            _producer = new UnitProducer(_grid, _registry, _factory);
        }

        [TearDown]
        public void DestroyBoard()
        {
            _board.Dispose();
        }

        [Test]
        public void Constructor_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new UnitProducer(null, _registry, _factory));
            Assert.Throws<ArgumentNullException>(() => new UnitProducer(_grid, null, _factory));
            Assert.Throws<ArgumentNullException>(() => new UnitProducer(_grid, _registry, null));
        }

        [Test]
        public void TryProduce_FreeSpawnCell_SpawnsBelowBottomMiddleOfBuilding()
        {
            Vector2Int origin = PlaceBarracks(new Vector2Int(3, 3));

            bool produced = _producer.TryProduce(_barracks, origin, _soldier, out Vector2Int cell);

            Assert.That(produced, Is.True);
            Assert.That(cell, Is.EqualTo(new Vector2Int(4, 2)));
            Assert.That(_factory.Created, Has.Count.EqualTo(1));
            Assert.That(_factory.Created[0].Cell, Is.EqualTo(cell));
        }

        [Test]
        public void TryProduce_Success_OccupiesCellAndRegistersSoldierUnderThatId()
        {
            Vector2Int origin = PlaceBarracks(new Vector2Int(3, 3));

            _producer.TryProduce(_barracks, origin, _soldier, out Vector2Int cell);

            int id = _grid.GetOccupant(cell);
            Assert.That(id, Is.Not.EqualTo(GridMap.EmptyId));
            Assert.That(_registry.TryGet(id, out IBoardOccupant soldier), Is.True);
            Assert.That(soldier.Origin, Is.EqualTo(cell));
        }

        [Test]
        public void TryProduce_ManyTimes_NeverStacksTwoSoldiersOnOneCell()
        {
            Vector2Int origin = PlaceBarracks(new Vector2Int(3, 3));
            var cells = new HashSet<Vector2Int>();

            for (int i = 0; i < 10; i++)
            {
                Assert.That(_producer.TryProduce(_barracks, origin, _soldier, out Vector2Int cell), Is.True);
                Assert.That(cells.Add(cell), Is.True, $"Soldier {i} landed on an occupied cell {cell}.");
            }
        }

        [Test]
        public void TryProduce_SpawnCellTaken_UsesNearestFreeCell()
        {
            Vector2Int origin = PlaceBarracks(new Vector2Int(3, 3));
            _grid.Occupy(new Vector2Int(4, 2), Vector2Int.one, _registry.NextId());

            _producer.TryProduce(_barracks, origin, _soldier, out Vector2Int cell);

            Assert.That(cell, Is.EqualTo(new Vector2Int(4, 1)));
        }

        [Test]
        public void TryProduce_BuildingOnBottomEdge_SpawnsNextToItOnTheBoard()
        {
            // The spawn cell (4, -1) is off the board; the nearest free cell beside the Barracks is used.
            Vector2Int origin = PlaceBarracks(new Vector2Int(3, 0));

            bool produced = _producer.TryProduce(_barracks, origin, _soldier, out Vector2Int cell);

            Assert.That(produced, Is.True);
            Assert.That(_grid.IsInside(cell), Is.True);
            Assert.That(cell, Is.EqualTo(new Vector2Int(2, 0)).Or.EqualTo(new Vector2Int(6, 0)));
        }

        [Test]
        public void TryProduce_RingAroundBuildingFullOfSoldiers_KeepsProducingBeyondThem()
        {
            // Soldiers can step aside, so the search looks past them; production never stalls while the board has room.
            Vector2Int origin = PlaceBarracks(new Vector2Int(3, 3));
            var cells = new HashSet<Vector2Int>();

            for (int i = 0; i < 30; i++)
            {
                Assert.That(_producer.TryProduce(_barracks, origin, _soldier, out Vector2Int cell), Is.True, $"soldier {i}");
                cells.Add(cell);
            }

            Assert.That(cells, Has.Count.EqualTo(30));
        }

        [Test]
        public void TryProduce_OtherBuildingsAroundSpawnCell_NeverSpawnsBehindThem()
        {
            // Bottom-edge Barracks with a House left and right: the only way out is through the Barracks itself.
            Vector2Int origin = PlaceBarracks(new Vector2Int(3, 0));
            _grid.Occupy(new Vector2Int(1, 0), new Vector2Int(2, 2), _registry.NextId());
            _grid.Occupy(new Vector2Int(6, 0), new Vector2Int(2, 2), _registry.NextId());

            _producer.TryProduce(_barracks, origin, _soldier, out Vector2Int cell);

            // Free cells at (0, 0) and (8, 0) are as close in steps but walled off by the houses; above the Barracks is not.
            Assert.That(cell.y, Is.EqualTo(2));
            Assert.That(cell.x, Is.InRange(3, 5));
        }

        [Test]
        public void TryProduce_BoardFull_ReturnsFalseAndChangesNothing()
        {
            Vector2Int origin = PlaceBarracks(new Vector2Int(3, 3));
            FillEveryFreeCell();
            int registered = _registry.Count;

            bool produced = _producer.TryProduce(_barracks, origin, _soldier, out _);

            Assert.That(produced, Is.False);
            Assert.That(_factory.Created, Is.Empty);
            Assert.That(_registry.Count, Is.EqualTo(registered));
        }

        [Test]
        public void TryProduce_BuildingThatProducesNothing_Throws()
        {
            // R5: the Power Plant has no production; the rule holds even if a caller bypasses the UI.
            Assert.Throws<ArgumentException>(() => _producer.TryProduce(_powerPlant, Vector2Int.zero, _soldier, out _));
            Assert.That(_factory.Created, Is.Empty);
        }

        [Test]
        public void TryProduce_UnitNotListedByBuilding_Throws()
        {
            UnitDefinition other = _board.Unit("Other");

            Assert.Throws<ArgumentException>(() => _producer.TryProduce(_barracks, Vector2Int.zero, other, out _));
        }

        [Test]
        public void TryProduce_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _producer.TryProduce(null, Vector2Int.zero, _soldier, out _));
            Assert.Throws<ArgumentNullException>(() => _producer.TryProduce(_barracks, Vector2Int.zero, null, out _));
        }

        [Test]
        public void TryProduce_FactoryThrows_GivesCellBackAndRethrows()
        {
            var producer = new UnitProducer(_grid, _registry, new ThrowingUnitFactory());
            Vector2Int origin = PlaceBarracks(new Vector2Int(3, 3));

            Assert.Throws<InvalidOperationException>(() => producer.TryProduce(_barracks, origin, _soldier, out _));
            Assert.That(_grid.GetOccupant(new Vector2Int(4, 2)), Is.EqualTo(GridMap.EmptyId));
        }

        [Test]
        public void SpawnCellOf_Barracks_IsCellBelowBottomMiddle()
        {
            Assert.That(UnitProducer.SpawnCellOf(_barracks, new Vector2Int(5, 5)), Is.EqualTo(new Vector2Int(6, 4)));
        }

        private Vector2Int PlaceBarracks(Vector2Int origin)
        {
            _grid.Occupy(origin, BarracksFootprint, _registry.NextId());
            return origin;
        }

        private void FillEveryFreeCell()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (_grid.GetOccupant(cell) == GridMap.EmptyId)
                    {
                        _grid.Occupy(cell, Vector2Int.one, _registry.NextId());
                    }
                }
            }
        }

        /// <summary>Records what the producer asked to create instead of creating scene objects.</summary>
        private sealed class RecordingUnitFactory : IUnitFactory
        {
            public List<(UnitDefinition Definition, Vector2Int Cell, int OccupantId)> Created { get; } =
                new List<(UnitDefinition, Vector2Int, int)>();

            public IBoardOccupant Create(UnitDefinition definition, Vector2Int cell, int occupantId)
            {
                Created.Add((definition, cell, occupantId));
                return new FakeOccupant(occupantId, cell, Vector2Int.one, isMobile: true);
            }
        }

        private sealed class ThrowingUnitFactory : IUnitFactory
        {
            public IBoardOccupant Create(UnitDefinition definition, Vector2Int cell, int occupantId)
            {
                throw new InvalidOperationException("Simulated failure while creating the soldier.");
            }
        }
    }
}
