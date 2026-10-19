using System;
using NUnit.Framework;
using StrategyDemo.Core;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode.Core
{
    /// <summary>Temporary stop-cell and permanent spawn-cell reservations: one cell per owner, one owner per cell.</summary>
    public class CellReservationsTests
    {
        private CellReservations _reservations;

        [SetUp]
        public void CreateReservations()
        {
            _reservations = new CellReservations(5, 4);
        }

        [TestCase(0, 4)]
        [TestCase(5, 0)]
        [TestCase(-1, 4)]
        public void Constructor_NonPositiveSize_Throws(int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CellReservations(width, height));
        }

        [Test]
        public void OwnerOf_Unreserved_IsEmptyId()
        {
            Assert.That(_reservations.OwnerOf(new Vector2Int(2, 2)), Is.EqualTo(GridMap.EmptyId));
            Assert.That(_reservations.TryGetCell(7, out _), Is.False);
        }

        [Test]
        public void ReserveStopCell_RecordsBothDirections()
        {
            _reservations.ReserveStopCell(new Vector2Int(2, 1), 7);

            Assert.That(_reservations.OwnerOf(new Vector2Int(2, 1)), Is.EqualTo(7));
            Assert.That(_reservations.TryGetCell(7, out Vector2Int cell), Is.True);
            Assert.That(cell, Is.EqualTo(new Vector2Int(2, 1)));
        }

        [Test]
        public void ReserveStopCell_SecondCellForSameOwner_LetsGoOfTheFirst()
        {
            _reservations.ReserveStopCell(new Vector2Int(0, 0), 7);

            _reservations.ReserveStopCell(new Vector2Int(3, 3), 7);

            Assert.That(_reservations.OwnerOf(new Vector2Int(0, 0)), Is.EqualTo(GridMap.EmptyId));
            Assert.That(_reservations.OwnerOf(new Vector2Int(3, 3)), Is.EqualTo(7));
            Assert.That(_reservations.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReserveStopCell_SameCellAgain_ChangesNothing()
        {
            _reservations.ReserveStopCell(new Vector2Int(1, 1), 7);

            Assert.DoesNotThrow(() => _reservations.ReserveStopCell(new Vector2Int(1, 1), 7));
            Assert.That(_reservations.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReserveStopCell_CellOfAnotherOwner_ThrowsAndKeepsBoth()
        {
            _reservations.ReserveStopCell(new Vector2Int(1, 1), 7);
            _reservations.ReserveStopCell(new Vector2Int(2, 2), 8);

            Assert.Throws<InvalidOperationException>(() => _reservations.ReserveStopCell(new Vector2Int(1, 1), 8));
            Assert.That(_reservations.OwnerOf(new Vector2Int(1, 1)), Is.EqualTo(7));
            Assert.That(_reservations.OwnerOf(new Vector2Int(2, 2)), Is.EqualTo(8), "A failed reservation must not drop the old cell.");
        }

        [Test]
        public void ReserveStopCell_InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _reservations.ReserveStopCell(Vector2Int.zero, GridMap.EmptyId));
            Assert.Throws<ArgumentOutOfRangeException>(() => _reservations.ReserveStopCell(new Vector2Int(5, 0), 7));
            Assert.Throws<ArgumentOutOfRangeException>(() => _reservations.OwnerOf(new Vector2Int(0, -1)));
        }

        [Test]
        public void Release_FreesTheCell_AndReportsWhetherAnythingWasHeld()
        {
            _reservations.ReserveStopCell(new Vector2Int(4, 3), 7);

            Assert.That(_reservations.Release(7), Is.True);
            Assert.That(_reservations.OwnerOf(new Vector2Int(4, 3)), Is.EqualTo(GridMap.EmptyId));
            Assert.That(_reservations.Release(7), Is.False);
        }

        [Test]
        public void ReserveStopCell_IsTemporary()
        {
            _reservations.ReserveStopCell(new Vector2Int(2, 1), 7);

            Assert.That(_reservations.IsPermanent(new Vector2Int(2, 1)), Is.False);
            Assert.That(_reservations.IsPermanent(new Vector2Int(3, 1)), Is.False, "An unreserved cell is not permanent either.");
        }

        [Test]
        public void ReserveSpawnCell_IsPermanentAndRecordsBothDirections()
        {
            _reservations.ReserveSpawnCell(new Vector2Int(2, 1), 7);

            Assert.That(_reservations.IsPermanent(new Vector2Int(2, 1)), Is.True);
            Assert.That(_reservations.OwnerOf(new Vector2Int(2, 1)), Is.EqualTo(7));
            Assert.That(_reservations.TryGetCell(7, out Vector2Int cell), Is.True);
            Assert.That(cell, Is.EqualTo(new Vector2Int(2, 1)));
        }

        [Test]
        public void ReserveStopCell_OwnerHoldsSpawnCell_ThrowsAndKeepsIt()
        {
            // A spawn cell never moves: the building would silently lose it.
            _reservations.ReserveSpawnCell(new Vector2Int(2, 1), 7);

            Assert.Throws<InvalidOperationException>(() => _reservations.ReserveStopCell(new Vector2Int(3, 3), 7));
            Assert.That(_reservations.OwnerOf(new Vector2Int(2, 1)), Is.EqualTo(7));
            Assert.That(_reservations.IsPermanent(new Vector2Int(2, 1)), Is.True);
            Assert.That(_reservations.OwnerOf(new Vector2Int(3, 3)), Is.EqualTo(GridMap.EmptyId));
        }

        [Test]
        public void ReserveSpawnCell_OwnerAlreadyHoldsACell_Throws()
        {
            _reservations.ReserveStopCell(new Vector2Int(0, 0), 7);
            _reservations.ReserveSpawnCell(new Vector2Int(1, 1), 8);

            Assert.Throws<InvalidOperationException>(() => _reservations.ReserveSpawnCell(new Vector2Int(2, 2), 7));
            Assert.Throws<InvalidOperationException>(() => _reservations.ReserveSpawnCell(new Vector2Int(2, 2), 8));
            Assert.That(_reservations.OwnerOf(new Vector2Int(2, 2)), Is.EqualTo(GridMap.EmptyId));
        }

        [Test]
        public void ReserveSpawnCell_CellOfAnotherOwner_ThrowsAndKeepsIt()
        {
            _reservations.ReserveStopCell(new Vector2Int(1, 1), 7);

            Assert.Throws<InvalidOperationException>(() => _reservations.ReserveSpawnCell(new Vector2Int(1, 1), 8));
            Assert.That(_reservations.OwnerOf(new Vector2Int(1, 1)), Is.EqualTo(7));
            Assert.That(_reservations.IsPermanent(new Vector2Int(1, 1)), Is.False);
        }

        [Test]
        public void ReserveSpawnCell_InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _reservations.ReserveSpawnCell(Vector2Int.zero, GridMap.EmptyId));
            Assert.Throws<ArgumentOutOfRangeException>(() => _reservations.ReserveSpawnCell(new Vector2Int(0, 4), 7));
            Assert.Throws<ArgumentOutOfRangeException>(() => _reservations.IsPermanent(new Vector2Int(-1, 0)));
        }

        [Test]
        public void Release_SpawnCell_LeavesAnOrdinaryCell()
        {
            _reservations.ReserveSpawnCell(new Vector2Int(2, 1), 7);

            Assert.That(_reservations.Release(7), Is.True);
            Assert.That(_reservations.IsPermanent(new Vector2Int(2, 1)), Is.False);
            Assert.DoesNotThrow(() => _reservations.ReserveStopCell(new Vector2Int(2, 1), 8), "Soldiers may stop there again.");
        }
    }
}
