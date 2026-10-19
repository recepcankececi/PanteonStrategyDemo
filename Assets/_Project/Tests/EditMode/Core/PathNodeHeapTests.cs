using System.Collections.Generic;
using NUnit.Framework;
using StrategyDemo.Core;

namespace StrategyDemo.Tests.EditMode.Core
{
    /// <summary>A*'s open list: smallest f first, h breaks ties, decrease-key keeps order, Clear reuses the arrays.</summary>
    public class PathNodeHeapTests
    {
        [Test]
        public void Pop_AfterPushes_ReturnsNodesBySmallestF()
        {
            var heap = new PathNodeHeap(8);
            int[] fs = { 50, 10, 40, 30, 20, 70, 60, 0 };
            for (int node = 0; node < fs.Length; node++)
            {
                heap.Push(node, fs[node], 0);
            }

            var order = new List<int>();
            while (heap.Count > 0)
            {
                order.Add(heap.Pop());
            }

            Assert.That(order, Is.EqualTo(new[] { 7, 1, 4, 3, 2, 0, 6, 5 }));
        }

        [Test]
        public void Pop_EqualF_SmallerHFirst()
        {
            var heap = new PathNodeHeap(3);
            heap.Push(0, 30, 20);
            heap.Push(1, 30, 5);
            heap.Push(2, 30, 10);

            Assert.That(heap.Pop(), Is.EqualTo(1));
            Assert.That(heap.Pop(), Is.EqualTo(2));
            Assert.That(heap.Pop(), Is.EqualTo(0));
        }

        [Test]
        public void DecreaseF_MovesNodeToFront()
        {
            var heap = new PathNodeHeap(4);
            heap.Push(0, 10, 0);
            heap.Push(1, 20, 0);
            heap.Push(2, 30, 0);
            heap.Push(3, 40, 0);

            heap.DecreaseF(3, 5);

            Assert.That(heap.Pop(), Is.EqualTo(3));
            Assert.That(heap.Pop(), Is.EqualTo(0));
        }

        [Test]
        public void Clear_ThenReuse_ForgetsOldNodes()
        {
            var heap = new PathNodeHeap(3);
            heap.Push(0, 10, 0);
            heap.Push(1, 20, 0);

            heap.Clear();
            heap.Push(1, 5, 0);

            Assert.That(heap.Count, Is.EqualTo(1));
            Assert.That(heap.Pop(), Is.EqualTo(1), "Nodes of the last search are gone.");
        }
    }
}
