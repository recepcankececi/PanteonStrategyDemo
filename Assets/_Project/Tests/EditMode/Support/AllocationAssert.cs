using System;
using NUnit.Framework;
using UnityEngine.Profiling;

namespace StrategyDemo.Tests.EditMode.Support
{
    /// <summary>
    /// Zero-allocation checks for the "no per-frame allocations" rule. Counts Unity's <c>GC.Alloc</c> profiler marker
    /// on this thread while the action runs: <c>GC.GetAllocatedBytesForCurrentThread</c> reports 0 even for
    /// <c>new object[10]</c> in the editor's Mono runtime, so a test built on it can never fail.
    /// </summary>
    internal static class AllocationAssert
    {
        private const string GcAllocMarker = "GC.Alloc";

        /// <summary>Fails when <paramref name="action"/> allocates managed memory.</summary>
        public static void None(Action action)
        {
            Assert.That(CountAllocations(action), Is.Zero, "Managed allocations while the action ran.");
        }

        /// <summary>How many managed allocations <paramref name="action"/> made on this thread.</summary>
        public static int CountAllocations(Action action)
        {
            Recorder recorder = Recorder.Get(GcAllocMarker);
            // An invalid recorder counts nothing, which would make every zero-allocation test pass blindly again.
            Assert.That(recorder.isValid, Is.True, $"The '{GcAllocMarker}' profiler marker cannot be recorded here.");
            recorder.FilterToCurrentThread();
            // Disabling and enabling again starts a fresh count.
            recorder.enabled = false;
            recorder.enabled = true;
            try
            {
                action();
            }
            finally
            {
                recorder.enabled = false;
            }

            int allocations = recorder.sampleBlockCount;
            recorder.CollectFromAllThreads();
            return allocations;
        }
    }
}
