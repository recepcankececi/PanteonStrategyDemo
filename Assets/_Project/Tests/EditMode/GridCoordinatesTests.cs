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

        [Test]
        public void FootprintOriginAt_OddFootprint_PutsCursorCellInTheMiddle()
        {
            // Cursor in cell (5, 4) relative to the board origin; a 3×3 area centred on it starts at (4, 3).
            Vector3 cursor = Origin + new Vector3(5.3f, 4.7f, 0f);

            Vector2Int areaOrigin = GridCoordinates.FootprintOriginAt(cursor, Origin, CellSize, new Vector2Int(3, 3));

            Assert.That(areaOrigin, Is.EqualTo(new Vector2Int(4, 3)));
        }

        [TestCase(5.3f, 4)]
        [TestCase(5.7f, 5)]
        public void FootprintOriginAt_EvenFootprint_SnapsToNearestCellCorner(float cursorX, int expectedX)
        {
            // A 2-wide area is centred on a cell edge: x = 5.3 is nearest edge 5 (cells 4..5), 5.7 nearest edge 6 (cells 5..6).
            Vector3 cursor = Origin + new Vector3(cursorX, 0.5f, 0f);

            Vector2Int areaOrigin = GridCoordinates.FootprintOriginAt(cursor, Origin, CellSize, new Vector2Int(2, 1));

            Assert.That(areaOrigin.x, Is.EqualTo(expectedX));
        }

        [Test]
        public void FootprintOriginAt_CursorLeftOfBoard_ReturnsNegativeOrigin()
        {
            Vector3 cursor = Origin + new Vector3(-0.2f, 0.5f, 0f);

            Vector2Int areaOrigin = GridCoordinates.FootprintOriginAt(cursor, Origin, CellSize, new Vector2Int(3, 1));

            Assert.That(areaOrigin, Is.EqualTo(new Vector2Int(-2, 0)));
        }

        [Test]
        public void FootprintBaseCenter_ReturnsMiddleOfBottomEdge()
        {
            Vector3 baseCenter = GridCoordinates.FootprintBaseCenter(new Vector2Int(2, 1), new Vector2Int(3, 2), Origin, CellSize);

            Assert.That(baseCenter.x, Is.EqualTo(Origin.x + 3.5f).Within(Tolerance));
            Assert.That(baseCenter.y, Is.EqualTo(Origin.y + 1f).Within(Tolerance));
        }
    }
}
