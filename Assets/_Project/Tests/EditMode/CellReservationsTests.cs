using System;
using NUnit.Framework;
using StrategyDemo.Core;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Stop-cell reservations (ADR-008): one cell per owner, one owner per cell.</summary>
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
        public void Reserve_RecordsBothDirections()
        {
            _reservations.Reserve(new Vector2Int(2, 1), 7);

            Assert.That(_reservations.OwnerOf(new Vector2Int(2, 1)), Is.EqualTo(7));
            Assert.That(_reservations.TryGetCell(7, out Vector2Int cell), Is.True);
            Assert.That(cell, Is.EqualTo(new Vector2Int(2, 1)));
        }

        [Test]
        public void Reserve_SecondCellForSameOwner_LetsGoOfTheFirst()
        {
            _reservations.Reserve(new Vector2Int(0, 0), 7);

            _reservations.Reserve(new Vector2Int(3, 3), 7);

            Assert.That(_reservations.OwnerOf(new Vector2Int(0, 0)), Is.EqualTo(GridMap.EmptyId));
            Assert.That(_reservations.OwnerOf(new Vector2Int(3, 3)), Is.EqualTo(7));
            Assert.That(_reservations.Count, Is.EqualTo(1));
        }

        [Test]
        public void Reserve_SameCellAgain_ChangesNothing()
        {
            _reservations.Reserve(new Vector2Int(1, 1), 7);

            Assert.DoesNotThrow(() => _reservations.Reserve(new Vector2Int(1, 1), 7));
            Assert.That(_reservations.Count, Is.EqualTo(1));
        }

        [Test]
        public void Reserve_CellOfAnotherOwner_ThrowsAndKeepsBoth()
        {
            _reservations.Reserve(new Vector2Int(1, 1), 7);
            _reservations.Reserve(new Vector2Int(2, 2), 8);

            Assert.Throws<InvalidOperationException>(() => _reservations.Reserve(new Vector2Int(1, 1), 8));
            Assert.That(_reservations.OwnerOf(new Vector2Int(1, 1)), Is.EqualTo(7));
            Assert.That(_reservations.OwnerOf(new Vector2Int(2, 2)), Is.EqualTo(8), "A failed reserve must not drop the old cell.");
        }

        [Test]
        public void Reserve_InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _reservations.Reserve(Vector2Int.zero, GridMap.EmptyId));
            Assert.Throws<ArgumentOutOfRangeException>(() => _reservations.Reserve(new Vector2Int(5, 0), 7));
            Assert.Throws<ArgumentOutOfRangeException>(() => _reservations.OwnerOf(new Vector2Int(0, -1)));
        }

        [Test]
        public void Release_FreesTheCell_AndReportsWhetherAnythingWasHeld()
        {
            _reservations.Reserve(new Vector2Int(4, 3), 7);

            Assert.That(_reservations.Release(7), Is.True);
            Assert.That(_reservations.OwnerOf(new Vector2Int(4, 3)), Is.EqualTo(GridMap.EmptyId));
            Assert.That(_reservations.Release(7), Is.False);
        }
    }
}
