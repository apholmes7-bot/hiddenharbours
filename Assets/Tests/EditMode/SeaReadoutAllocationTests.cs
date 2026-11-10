using System;
using System.Globalization;
using HiddenHarbours.Core;
using HiddenHarbours.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace HiddenHarbours.Tests.EditMode
{
    public class SeaReadoutAllocationTests
    {
        [Test]
        public void EverySeaState_KeepsItsExistingTextAndReusesTheString()
        {
            string[] expected =
            {
                "Sea: Glass (0/7)", "Sea: Calm (1/7)", "Sea: Light (2/7)", "Sea: Moderate (3/7)",
                "Sea: Lively (4/7)", "Sea: Rough (5/7)", "Sea: Gale (6/7)", "Sea: Storm (7/7)"
            };
            Assert.AreEqual(expected.Length, Enum.GetValues(typeof(SeaState)).Length,
                "A new sea state needs an explicit text expectation.");
            for (int i = 0; i < expected.Length; i++)
            {
                string text = HudFormat.Sea((SeaState)i);
                Assert.AreEqual(expected[i], text);
                Assert.AreSame(text, HudFormat.Sea((SeaState)i));
            }
        }

        [Test]
        public void InvalidSeaState_KeepsTheUnknownLabelAndCurrentNumericCulture()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                culture.NumberFormat.NegativeSign = "~";
                CultureInfo.CurrentCulture = culture;
                Assert.AreEqual("Sea: -- (~1/7)", HudFormat.Sea((SeaState)(-1)));
                Assert.AreEqual("Sea: -- (8/7)", HudFormat.Sea((SeaState)8));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Test]
        public void WarmSeaReadouts_AllocateZeroManagedBytes()
        {
            string last = null;
            for (int i = 0; i < 1000; i++) last = HudFormat.Sea((SeaState)(i % 8));
            Assert.That(() => { _allocationSink = new object[64]; }, Is.AllocatingGCMemory(),
                "harness: the GC.Alloc recorder must detect a known allocation");
            Assert.That(() => { for (int i = 0; i < 10000; i++) last = HudFormat.Sea((SeaState)(i % 8)); },
                Is.Not.AllocatingGCMemory(), "The displayed sea values must reuse their cached strings.");
            Assert.AreEqual("Sea: Storm (7/7)", last);

            // Some Unity runtimes report zero for every thread-counter read (the CI editor does). The GC.Alloc
            // recorder above is the proof; the byte count is only asserted where this counter works.
            long probeStart = GC.GetAllocatedBytesForCurrentThread();
            _allocationSink = new byte[1024];
            if (GC.GetAllocatedBytesForCurrentThread() - probeStart <= 0)
            {
                Debug.Log("[SeaReadoutAllocationTests] GC.GetAllocatedBytesForCurrentThread counts nothing on this " +
                    "runtime. The GC.Alloc recorder above is the allocation measurement.");
                return;
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) last = HudFormat.Sea((SeaState)(i % 8));
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated, "The displayed sea values must reuse their cached strings.");
        }

        private static object _allocationSink;
    }
}
