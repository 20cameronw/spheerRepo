using System;
using NUnit.Framework;

namespace Frontier.Tests
{
    public sealed class HarvestAllocationTests
    {
        [Test]
        public void SteadyStateHarvestStepsDoNotAllocate()
        {
            long probe = GC.GetAllocatedBytesForCurrentThread();
            var sentinel = new byte[4096];
            if (GC.GetAllocatedBytesForCurrentThread() - probe < sentinel.Length)
                Assert.Ignore("Allocation counter is not supported on this runtime");

            var warmup = new Runs();
            for (int i = 0; i < 32; i++)
                warmup.Step(i);

            var runs = new Runs[128];
            for (int i = 0; i < runs.Length; i++) runs[i] = new Runs();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < runs.Length; i++)
                for (int step = 0; step < 8; step++) runs[i].Step(step);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated, "steady-state harvest simulation steps must not allocate");
        }

        sealed class Runs
        {
            readonly SiphonHarvestRun siphon = new SiphonHarvestRun(0.4f, 1f);
            readonly ExcavationHarvestRun excavation = new ExcavationHarvestRun(42);
            readonly SalvageHarvestRun salvage = new SalvageHarvestRun();
            readonly DrillHarvestRun drill = new DrillHarvestRun(1f, 0.4f, 1f);
            readonly NodeHarvestRun node = new NodeHarvestRun(1f, 0.4f);

            public void Step(int index)
            {
                siphon.Advance(0.25f);
                if (index % 2 == 0) siphon.TryPump(out _);
                excavation.TryPing(index % ExcavationHarvestRun.CellCount, out _);
                salvage.TryCast(-1);
                drill.Advance(0.25f, true);
                node.Advance(1f);
                node.TryStrike(HarvestStrike.Miss);
            }
        }
    }
}
