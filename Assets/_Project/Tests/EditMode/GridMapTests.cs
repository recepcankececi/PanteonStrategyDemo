using System;
using NUnit.Framework;
using StrategyDemo.Core;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Occupancy rules of <see cref="GridMap"/>: bounds, overlap, occupy/release and change events.</summary>
    public class GridMapTests
    {
        private const int Width = 24;
        private const int Height = 16;
        private const int BuildingId = 7;
        private const int OtherBuildingId = 8;

        private static readonly Vector2Int BarracksSize = new Vector2Int(3, 4);

        private GridMap _grid;

        [SetUp]
        public void CreateGrid()
        {
            _grid = new GridMap(Width, Height);
        }

        [TestCase(0, Height)]
        [TestCase(Width, 0)]
        [TestCase(-1, Height)]
        public void Constructor_NonPositiveSize_Throws(int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridMap(width, height));
        }

        [TestCase(0, 0, true)]
        [TestCase(Width - 1, Height - 1, true)]
        [TestCase(-1, 0, false)]
        [TestCase(0, -1, false)]
        [TestCase(Width, 0, false)]
        [TestCase(0, Height, false)]
        public void IsInside_Cell_MatchesBoardBounds(int x, int y, bool expected)
        {
            Assert.That(_grid.IsInside(new Vector2Int(x, y)), Is.EqualTo(expected));
        }

        [Test]
        public void IsAreaFree_EmptyGrid_ReturnsTrue()
        {
            Assert.That(_grid.IsAreaFree(Vector2Int.zero, BarracksSize), Is.True);
        }

        [Test]
        public void IsAreaFree_FootprintTouchingTopRightCorner_ReturnsTrue()
        {
            Vector2Int origin = new Vector2Int(Width, Height) - BarracksSize;

            Assert.That(_grid.IsAreaFree(origin, BarracksSize), Is.True);
        }

        [TestCase(Width - 2, 0)]
        [TestCase(0, Height - 3)]
        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        public void IsAreaFree_FootprintOutsideBounds_ReturnsFalse(int x, int y)
        {
            Assert.That(_grid.IsAreaFree(new Vector2Int(x, y), BarracksSize), Is.False);
        }

        [TestCase(0, 0)]
        [TestCase(0, 3)]
        [TestCase(-2, 2)]
        public void IsAreaFree_EmptyOrNegativeSize_ReturnsFalse(int width, int height)
        {
            Assert.That(_grid.IsAreaFree(Vector2Int.one, new Vector2Int(width, height)), Is.False);
        }

        [Test]
        public void IsAreaFree_OverlapsOccupiedCellByOne_ReturnsFalse()
        {
            _grid.Occupy(Vector2Int.zero, BarracksSize, BuildingId);

            // Starts on the last column of the first building.
            Vector2Int overlapping = new Vector2Int(BarracksSize.x - 1, 0);

            Assert.That(_grid.IsAreaFree(overlapping, BarracksSize), Is.False);
        }

        [Test]
        public void IsAreaFree_DirectlyAdjacentToOccupiedArea_ReturnsTrue()
        {
            _grid.Occupy(Vector2Int.zero, BarracksSize, BuildingId);

            Assert.That(_grid.IsAreaFree(new Vector2Int(BarracksSize.x, 0), BarracksSize), Is.True);
        }

        [Test]
        public void Occupy_Area_StoresIdInEveryCellAndNoneOutside()
        {
            Vector2Int origin = new Vector2Int(2, 3);
            _grid.Occupy(origin, BarracksSize, BuildingId);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    bool insideArea = x >= origin.x && x < origin.x + BarracksSize.x && y >= origin.y && y < origin.y + BarracksSize.y;
                    int expected = insideArea ? BuildingId : GridMap.EmptyId;
                    Assert.That(_grid.GetOccupant(new Vector2Int(x, y)), Is.EqualTo(expected), $"cell ({x},{y})");
                }
            }
        }

        [Test]
        public void Occupy_AlreadyOccupiedArea_ThrowsAndKeepsFirstOccupant()
        {
            _grid.Occupy(Vector2Int.zero, BarracksSize, BuildingId);

            Assert.Throws<InvalidOperationException>(() => _grid.Occupy(Vector2Int.one, BarracksSize, OtherBuildingId));
            Assert.That(_grid.GetOccupant(Vector2Int.one), Is.EqualTo(BuildingId));
        }

        [Test]
        public void Occupy_AreaOffBoard_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _grid.Occupy(new Vector2Int(Width - 1, 0), BarracksSize, BuildingId));
        }

        [TestCase(GridMap.EmptyId)]
        [TestCase(-1)]
        public void Occupy_NonPositiveId_Throws(int occupantId)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _grid.Occupy(Vector2Int.zero, BarracksSize, occupantId));
        }

        [Test]
        public void Release_OccupiedArea_FreesItForPlacement()
        {
            _grid.Occupy(Vector2Int.zero, BarracksSize, BuildingId);

            _grid.Release(Vector2Int.zero, BarracksSize, BuildingId);

            Assert.That(_grid.IsAreaFree(Vector2Int.zero, BarracksSize), Is.True);
        }

        [Test]
        public void Release_AreaOffBoard_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _grid.Release(new Vector2Int(-1, 0), BarracksSize, BuildingId));
        }

        [Test]
        public void Release_AreaOverlappingAnotherOccupant_ThrowsAndFreesNothing()
        {
            _grid.Occupy(Vector2Int.zero, BarracksSize, BuildingId);
            Vector2Int neighbourOrigin = new Vector2Int(BarracksSize.x, 0);
            _grid.Occupy(neighbourOrigin, BarracksSize, OtherBuildingId);

            // One column too wide: would reach into the neighbour.
            Vector2Int tooWide = BarracksSize + Vector2Int.right;

            Assert.Throws<InvalidOperationException>(() => _grid.Release(Vector2Int.zero, tooWide, BuildingId));
            Assert.That(_grid.GetOccupant(Vector2Int.zero), Is.EqualTo(BuildingId));
            Assert.That(_grid.GetOccupant(neighbourOrigin), Is.EqualTo(OtherBuildingId));
        }

        [Test]
        public void Release_EmptyArea_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _grid.Release(Vector2Int.zero, BarracksSize, BuildingId));
        }

        [Test]
        public void GetOccupant_CellOffBoard_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _grid.GetOccupant(new Vector2Int(Width, 0)));
        }

        [Test]
        public void OccupyAndRelease_RaiseAreaChangedWithAffectedRect()
        {
            Vector2Int origin = new Vector2Int(4, 5);
            int raised = 0;
            RectInt lastArea = default;
            _grid.AreaChanged += area =>
            {
                raised++;
                lastArea = area;
            };

            _grid.Occupy(origin, BarracksSize, BuildingId);
            Assert.That(lastArea, Is.EqualTo(new RectInt(origin, BarracksSize)));

            _grid.Release(origin, BarracksSize, BuildingId);
            Assert.That(raised, Is.EqualTo(2));
            Assert.That(lastArea, Is.EqualTo(new RectInt(origin, BarracksSize)));
        }

        [Test]
        public void Occupy_FailedAttempt_DoesNotRaiseAreaChanged()
        {
            _grid.Occupy(Vector2Int.zero, BarracksSize, BuildingId);
            int raised = 0;
            _grid.AreaChanged += _ => raised++;

            Assert.Throws<InvalidOperationException>(() => _grid.Occupy(Vector2Int.zero, BarracksSize, OtherBuildingId));

            Assert.That(raised, Is.Zero);
        }
    }
}
