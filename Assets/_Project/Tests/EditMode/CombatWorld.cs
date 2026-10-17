using System;
using System.Collections.Generic;
using NUnit.Framework;
using StrategyDemo.Core;
using StrategyDemo.Data;
using StrategyDemo.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>
    /// The services combat runs on, wired as GameBootstrap wires them, on a small board whose origin is the world
    /// origin. Soldiers and buildings are built by hand and stay in the scene after a despawn (the factories are null
    /// factories that count releases); Dispose destroys all of them.
    /// </summary>
    internal sealed class CombatWorld : IDisposable
    {
        public const float FrameTime = 0.02f;

        private readonly List<GameObject> _objects = new List<GameObject>();

        public CombatWorld(int width, int height)
        {
            Board = new TestBoard(Vector3.zero, width, height);
            Grid = new GridMap(width, height);
            Registry = new OccupantRegistry();
            Reservations = new CellReservations(width, height);
            Movement = new UnitMovement(Grid, Registry, Reservations, Board.View);
            Selection = new SelectionService(Grid, Registry);
            UnitFactory = new NullUnitFactory();
            BuildingFactory = new NullBuildingFactory();
            Despawner = new OccupantDespawner(Grid, Registry, Movement, Selection);
            Combat = new UnitCombat(Movement, new FreeForAllAttackRule(), Despawner);
        }

        public TestBoard Board { get; }
        public GridMap Grid { get; }
        public OccupantRegistry Registry { get; }
        public CellReservations Reservations { get; }
        public UnitMovement Movement { get; }
        public SelectionService Selection { get; }
        public NullUnitFactory UnitFactory { get; }
        public NullBuildingFactory BuildingFactory { get; }
        public OccupantDespawner Despawner { get; }
        public UnitCombat Combat { get; }

        /// <summary>A standing soldier on <paramref name="cell"/>, set up the way <see cref="UnitProducer"/> leaves one.</summary>
        public Unit Spawn(Vector2Int cell, UnitDefinition definition)
        {
            Unit unit = TestBoard.CreateUnitObject(definition.name);
            _objects.Add(unit.gameObject);
            int id = Registry.NextId();
            unit.Init(definition, cell, id, Board.View.CellToWorld(cell), UnitFactory);
            Grid.Occupy(cell, Vector2Int.one, id);
            Registry.Add(unit);
            Reservations.Reserve(cell, id);
            return unit;
        }

        /// <summary>A building on the area at <paramref name="origin"/>, set up the way <see cref="BuildingPlacer"/> leaves one.</summary>
        public Building Place(Vector2Int origin, Vector2Int size, int health)
        {
            BuildingDefinition definition = Board.Definition($"Building{size.x}x{size.y}", size, health);
            Building building = TestBoard.CreateBuildingObject(definition.name);
            _objects.Add(building.gameObject);
            int id = Registry.NextId();
            building.Init(definition, origin, id, Board.View.FootprintCenter(origin, size), BuildingFactory);
            Grid.Occupy(origin, size, id);
            Registry.Add(building);
            return building;
        }

        /// <summary>One frame: movement first, then combat, as the drivers' execution order runs them.</summary>
        public void Step()
        {
            Movement.Tick(FrameTime);
            Combat.Tick(FrameTime);
        }

        public void Run(float seconds)
        {
            int frames = Mathf.CeilToInt(seconds / FrameTime);
            for (int i = 0; i < frames; i++)
            {
                Step();
            }
        }

        /// <summary>Steps until <paramref name="condition"/> holds; fails the test after <paramref name="maxSeconds"/>.</summary>
        public void RunUntil(Func<bool> condition, string what, float maxSeconds = 30f)
        {
            int frames = Mathf.CeilToInt(maxSeconds / FrameTime);
            for (int i = 0; i < frames; i++)
            {
                if (condition())
                {
                    return;
                }

                Step();
            }

            Assert.That(condition(), Is.True, $"Timed out waiting until {what}.");
        }

        public bool IsRegistered(IBoardOccupant occupant)
        {
            return Registry.TryGet(occupant.OccupantId, out IBoardOccupant registered) && ReferenceEquals(registered, occupant);
        }

        public static int DistanceTo(Unit attacker, IBoardOccupant target)
        {
            return GridDistance.ToArea(attacker.Origin, target.Origin, target.Footprint);
        }

        public void Dispose()
        {
            foreach (GameObject created in _objects)
            {
                Object.DestroyImmediate(created);
            }

            Board.Dispose();
        }
    }
}
