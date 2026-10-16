using NUnit.Framework;
using StrategyDemo.Core;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Cell ↔ world conversion, including positions off the board.</summary>
    public class GridCoordinatesTests
    {
        private const float CellSize = 1f;
        private const float Tolerance = 1e-5f;

        private static readonly Vector3 Origin = new Vector3(-3f, 2f, 0f);

        [Test]
        public void CellToWorld_Cell_ReturnsCellCentre()
        {
            Vector3 world = GridCoordinates.CellToWorld(new Vector2Int(2, 1), Origin, CellSize);

            Assert.That(world.x, Is.EqualTo(-0.5f).Within(Tolerance));
            Assert.That(world.y, Is.EqualTo(3.5f).Within(Tolerance));
        }

        [Test]
        public void WorldToCell_PointInsideCell_ReturnsThatCell()
        {
            Assert.That(GridCoordinates.WorldToCell(new Vector3(-0.9f, 3.99f, 0f), Origin, CellSize), Is.EqualTo(new Vector2Int(2, 1)));
        }

        [Test]
        public void WorldToCell_PointJustLeftAndBelowOrigin_ReturnsNegativeCell()
        {
            Vector3 justOutside = Origin - new Vector3(0.5f, 0.5f, 0f);

            Assert.That(GridCoordinates.WorldToCell(justOutside, Origin, CellSize), Is.EqualTo(new Vector2Int(-1, -1)));
        }

        [TestCase(0, 0)]
        [TestCase(23, 15)]
        [TestCase(-4, 7)]
        public void WorldToCell_OfCellCentre_RoundTrips(int x, int y)
        {
            Vector2Int cell = new Vector2Int(x, y);

            Vector3 centre = GridCoordinates.CellToWorld(cell, Origin, CellSize);

            Assert.That(GridCoordinates.WorldToCell(centre, Origin, CellSize), Is.EqualTo(cell));
        }
    }
}
