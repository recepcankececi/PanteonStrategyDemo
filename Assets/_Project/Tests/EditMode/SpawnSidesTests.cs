using System;
using System.Collections.Generic;
using NUnit.Framework;
using StrategyDemo.Core;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Candidate spawn cells of a producing building and the order they are tried in (R6).</summary>
    public class SpawnSidesTests
    {
        private static readonly Vector2Int Origin = new Vector2Int(3, 3);
        private static readonly Vector2Int Barracks = new Vector2Int(3, 3);

        [Test]
        public void Candidate_ThreeByThree_MiddleOfBottomRightTopLeftInThatOrder()
        {
            Assert.That(SpawnSides.Candidate(Origin, Barracks, 0), Is.EqualTo(new Vector2Int(4, 2)), "bottom");
            Assert.That(SpawnSides.Candidate(Origin, Barracks, 1), Is.EqualTo(new Vector2Int(6, 4)), "right");
            Assert.That(SpawnSides.Candidate(Origin, Barracks, 2), Is.EqualTo(new Vector2Int(4, 6)), "top");
            Assert.That(SpawnSides.Candidate(Origin, Barracks, 3), Is.EqualTo(new Vector2Int(2, 4)), "left");
        }

        [Test]
        public void Candidate_EvenSide_TakesCellRightOfOrAboveCentreLine()
        {
            var footprint = new Vector2Int(2, 2);

            Assert.That(SpawnSides.Candidate(Vector2Int.zero, footprint, 0), Is.EqualTo(new Vector2Int(1, -1)));
            Assert.That(SpawnSides.Candidate(Vector2Int.zero, footprint, 1), Is.EqualTo(new Vector2Int(2, 1)));
        }

        [Test]
        public void Candidate_EveryFootprint_IsJustOutsideIt()
        {
            // Chebyshev distance 1 from the footprint and never diagonal from it: the soldier appears touching a side.
            foreach (Vector2Int footprint in new[] { Vector2Int.one, new Vector2Int(2, 1), new Vector2Int(3, 3), new Vector2Int(4, 4) })
            {
                for (int i = 0; i < SpawnSides.Count; i++)
                {
                    Vector2Int cell = SpawnSides.Candidate(Origin, footprint, i);
                    Assert.That(GridDistance.ToArea(cell, Origin, footprint), Is.EqualTo(1), $"{footprint} side {i}");
                    Vector2Int closest = GridDistance.ClosestCellInArea(Origin, footprint, cell);
                    Assert.That(cell.x == closest.x || cell.y == closest.y, Is.True, $"{footprint} side {i} is diagonal");
                }
            }
        }

        [TestCase(-1)]
        [TestCase(SpawnSides.Count)]
        public void Candidate_IndexOutOfRange_Throws(int index)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SpawnSides.Candidate(Origin, Barracks, index));
        }

        [Test]
        public void TryChoose_FirstUsable_IsTaken()
        {
            var unusable = new HashSet<Vector2Int> { new Vector2Int(4, 2), new Vector2Int(6, 4) };

            bool found = SpawnSides.TryChoose(Origin, Barracks, cell => !unusable.Contains(cell), out Vector2Int spawn);

            Assert.That(found, Is.True);
            Assert.That(spawn, Is.EqualTo(new Vector2Int(4, 6)), "Bottom and right unusable: top is next.");
        }

        [Test]
        public void TryChoose_NoneUsable_ReturnsFalse()
        {
            Assert.That(SpawnSides.TryChoose(Origin, Barracks, _ => false, out _), Is.False);
        }

        [Test]
        public void TryChoose_NullPredicate_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => SpawnSides.TryChoose(Origin, Barracks, null, out _));
        }
    }
}
