using System;
using HiddenHarbours.Tests.Support;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public class ManagedAllocationProbeTests
    {
        [Test]
        public void RetainedAllocation_IsDetectedByTheCalibratedRecorder()
        {
            using var probe = new ManagedAllocationProbe();
            probe.Begin();
            var known = new byte[1024];
            long events = probe.End();
            GC.KeepAlive(known);
            Assert.Greater(events, 0L);
        }

        [Test]
        public void EmptyScope_IsZero_AndDoesNotReuseThePreviousAllocation()
        {
            using var probe = new ManagedAllocationProbe();
            probe.Begin();
            var known = new byte[1024];
            long events = probe.End();
            GC.KeepAlive(known);
            Assert.Greater(events, 0L);
            probe.Begin();
            long empty = probe.End();
            Assert.AreEqual(0L, empty);
        }
    }
}
