using System;
using System.Collections.Generic;
using NUnit.Framework;
using StrategyDemo.Core;

namespace StrategyDemo.Tests.EditMode.Core
{
    /// <summary>Reuse, callbacks and misuse guards of the generic pool (U1).</summary>
    public class ObjectPoolTests
    {
        private sealed class Item
        {
            public bool Active;
        }

        [Test]
        public void Get_EmptyPool_CreatesNewItem()
        {
            var pool = new ObjectPool<Item>(() => new Item());

            Item item = pool.Get();

            Assert.That(item, Is.Not.Null);
            Assert.That(pool.CountAll, Is.EqualTo(1));
            Assert.That(pool.CountActive, Is.EqualTo(1));
            Assert.That(pool.CountInactive, Is.EqualTo(0));
        }

        [Test]
        public void Get_AfterRelease_ReusesItemWithoutCreating()
        {
            int created = 0;
            var pool = new ObjectPool<Item>(() => { created++; return new Item(); });
            Item first = pool.Get();
            pool.Release(first);

            Item second = pool.Get();

            Assert.That(second, Is.SameAs(first));
            Assert.That(created, Is.EqualTo(1));
        }

        [Test]
        public void GetAndRelease_InvokeCallbacksEveryTime()
        {
            var pool = new ObjectPool<Item>(() => new Item(), item => item.Active = true, item => item.Active = false);

            Item item = pool.Get();
            Assert.That(item.Active, Is.True);
            pool.Release(item);
            Assert.That(item.Active, Is.False);
            Assert.That(pool.Get().Active, Is.True, "onGet must also run for reused items.");
        }

        [Test]
        public void Release_SameItemTwice_Throws()
        {
            var pool = new ObjectPool<Item>(() => new Item());
            Item item = pool.Get();
            pool.Release(item);

            Assert.Throws<InvalidOperationException>(() => pool.Release(item));
            Assert.That(pool.CountInactive, Is.EqualTo(1));
        }

        [Test]
        public void Release_ItemNotFromThisPool_Throws()
        {
            var pool = new ObjectPool<Item>(() => new Item());
            pool.Get();

            Assert.Throws<InvalidOperationException>(() => pool.Release(new Item()));
            Assert.That(pool.CountActive, Is.EqualTo(1), "A rejected release must not change the counts.");
        }

        [Test]
        public void Release_Null_Throws()
        {
            var pool = new ObjectPool<Item>(() => new Item());

            Assert.Throws<ArgumentNullException>(() => pool.Release(null));
        }

        [Test]
        public void Constructor_NullCreate_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ObjectPool<Item>(null));
        }

        [Test]
        public void Get_CreateReturnsNull_Throws()
        {
            var pool = new ObjectPool<Item>(() => null);

            Assert.Throws<InvalidOperationException>(() => pool.Get());
            Assert.That(pool.CountAll, Is.EqualTo(0));
        }

        [Test]
        public void Counts_AfterManyGetsAndReleases_StayConsistent()
        {
            var pool = new ObjectPool<Item>(() => new Item());
            var taken = new List<Item>();
            for (int i = 0; i < 5; i++)
            {
                taken.Add(pool.Get());
            }

            pool.Release(taken[0]);
            pool.Release(taken[1]);
            pool.Get();

            Assert.That(pool.CountAll, Is.EqualTo(5));
            Assert.That(pool.CountActive, Is.EqualTo(4));
            Assert.That(pool.CountInactive, Is.EqualTo(1));
        }
    }
}
