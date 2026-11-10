using System;
using System.Globalization;
using HiddenHarbours.Core;
using HiddenHarbours.UI;
using NUnit.Framework;

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
            GC.GetAllocatedBytesForCurrentThread();
            long probeStart = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            long probeBytes = GC.GetAllocatedBytesForCurrentThread() - probeStart;
            GC.KeepAlive(probe);
            Assert.Greater(probeBytes, 0L, "The allocation counter must observe a known allocation.");

            string last = null;
            for (int i = 0; i < 1000; i++) last = HudFormat.Sea((SeaState)(i % 8));
            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) last = HudFormat.Sea((SeaState)(i % 8));
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated, "The displayed sea values must reuse their cached strings.");
            Assert.AreEqual("Sea: Storm (7/7)", last);
        }
    }
}
