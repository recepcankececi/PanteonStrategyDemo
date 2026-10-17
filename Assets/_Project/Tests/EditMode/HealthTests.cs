using System;
using NUnit.Framework;
using StrategyDemo.Gameplay;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Hit points (R7, R8, R10): damage clamps at 0, only the fatal hit reports death, nothing after death.</summary>
    public class HealthTests
    {
        private Health _health;
        private int _changedCount;
        private int _damagedTotal;

        [SetUp]
        public void CreateHealth()
        {
            _health = new Health();
            _health.Reset(10);
            _changedCount = 0;
            _damagedTotal = 0;
            _health.Changed += () => _changedCount++;
            _health.Damaged += amount => _damagedTotal += amount;
        }

        [Test]
        public void New_HasNoHitPointsAndIsNotAlive()
        {
            var health = new Health();

            Assert.That(health.IsAlive, Is.False);
            Assert.That(health.Fraction, Is.Zero);
        }

        [Test]
        public void Reset_StartsAtFullHealth()
        {
            Assert.That(_health.Current, Is.EqualTo(10));
            Assert.That(_health.Max, Is.EqualTo(10));
            Assert.That(_health.IsAlive, Is.True);
            Assert.That(_health.Fraction, Is.EqualTo(1f));
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void Reset_NonPositiveMax_Throws(int max)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _health.Reset(max));
        }

        [Test]
        public void TakeDamage_NotFatal_LowersHealthRaisesEventsAndReturnsFalse()
        {
            bool killed = _health.TakeDamage(4);

            Assert.That(killed, Is.False);
            Assert.That(_health.Current, Is.EqualTo(6));
            Assert.That(_health.Fraction, Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(_changedCount, Is.EqualTo(1));
            Assert.That(_damagedTotal, Is.EqualTo(4));
        }

        [Test]
        public void TakeDamage_MoreThanLeft_ClampsAtZeroAndReturnsTrue()
        {
            _health.TakeDamage(7);

            bool killed = _health.TakeDamage(10);

            Assert.That(killed, Is.True);
            Assert.That(_health.Current, Is.Zero);
            Assert.That(_health.IsAlive, Is.False);
            Assert.That(_damagedTotal, Is.EqualTo(10), "Only the hit points that were left are reported as taken.");
        }

        [Test]
        public void TakeDamage_WhenDead_IsIgnoredAndNeverReportsDeathTwice()
        {
            _health.TakeDamage(10);
            _changedCount = 0;

            bool killedAgain = _health.TakeDamage(5);

            Assert.That(killedAgain, Is.False);
            Assert.That(_changedCount, Is.Zero);
        }

        [Test]
        public void TakeDamage_Zero_ChangesNothing()
        {
            Assert.That(_health.TakeDamage(0), Is.False);
            Assert.That(_changedCount, Is.Zero);
        }

        [Test]
        public void TakeDamage_Negative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _health.TakeDamage(-1));
        }

        [Test]
        public void Clear_DropsHitPointsAndListeners()
        {
            _health.Clear();
            _health.Reset(5);
            _health.TakeDamage(1);

            Assert.That(_health.IsAlive, Is.True);
            Assert.That(_changedCount, Is.Zero, "A pooled object's old views must not hear the new object.");
            Assert.That(_damagedTotal, Is.Zero);
        }
    }
}
