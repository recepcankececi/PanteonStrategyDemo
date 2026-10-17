using System;
using System.Collections.Generic;
using StrategyDemo.Data;
using StrategyDemo.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>
    /// Factory that builds real soldiers by hand (<see cref="TestBoard.CreateUnitObject"/>), records what it was asked
    /// for, and can be told to fail. Dispose destroys every soldier it made.
    /// </summary>
    internal sealed class TestUnitFactory : IUnitFactory, IDisposable
    {
        private readonly TestBoard _board;
        private readonly List<GameObject> _objects = new List<GameObject>();

        public TestUnitFactory(TestBoard board)
        {
            _board = board;
        }

        /// <summary>Every soldier created, in order.</summary>
        public List<Unit> Created { get; } = new List<Unit>();

        /// <summary>When true, Create throws as a broken prefab would.</summary>
        public bool Fails { get; set; }

        public Unit Create(UnitDefinition definition, Vector2Int cell, int occupantId)
        {
            if (Fails)
            {
                throw new InvalidOperationException("Simulated failure while creating the soldier.");
            }

            Unit unit = TestBoard.CreateUnitObject(definition.name);
            _objects.Add(unit.gameObject);
            unit.Init(definition, cell, occupantId, _board.View.CellToWorld(cell), this);
            Created.Add(unit);
            return unit;
        }

        public void Release(Unit unit)
        {
            unit.ResetState();
        }

        public void Dispose()
        {
            foreach (GameObject created in _objects)
            {
                Object.DestroyImmediate(created);
            }
        }
    }
}
