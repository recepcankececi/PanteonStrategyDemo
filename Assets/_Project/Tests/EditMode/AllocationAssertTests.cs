using NUnit.Framework;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>
    /// The zero-allocation check itself: it must fail on an allocation, or every "allocates nothing" test proves
    /// nothing (the old GC.GetAllocatedBytesForCurrentThread check never failed in the editor).
    /// </summary>
    public class AllocationAssertTests
    {
        private object _sink;

        [Test]
        public void CountAllocations_AllocatingAction_CountsIt()
        {
            int allocations = AllocationAssert.CountAllocations(() => _sink = new object[10]);

            Assert.That(allocations, Is.GreaterThan(0), "The check must see an allocation, or it proves nothing.");
        }

        [Test]
        public void None_NonAllocatingAction_Passes()
        {
            int total = 0;
            AllocationAssert.None(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    total += i;
                }
            });

            Assert.That(total, Is.EqualTo(45));
        }
    }
}
