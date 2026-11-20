using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>A small deposit reaches the same buffer as a hull's</b> (the one foam, the owner 2026-11-19). An
    /// oar blade's foam laid through <see cref="FoamSplashes"/> is offered to the foam buffer's slots by the
    /// very gatherer the wake goes through, so it is drawn with the wake's lace, bands and ageing.
    /// </summary>
    public class FoamSplashRegistryTests
    {
        [SetUp] public void SetUp() => FoamSplashes.Clear();
        [TearDown] public void TearDown() => FoamSplashes.Clear();

        [Test]
        public void AnOarDeposit_IsGatheredIntoTheSharedFoamBuffer()
        {
            Assume.That(FoamInjectionRegistry.Count, Is.EqualTo(0), "this test wants no hull in the scene");
            var into = new FoamInjection[FoamBuffer.MaxInjectors];
            Rect window = Rect.MinMaxRect(-10f, -10f, 10f, 10f);
            Assert.AreEqual(0, FoamInjectionRegistry.CollectInjections(into, window));

            Assert.IsTrue(FoamSplashes.Add(new Vector2(1f, 2f), new Vector2(1.1f, 1.8f), 0.22f, 0.3f, 0.8f));
            int n = FoamInjectionRegistry.CollectInjections(into, window);
            Assert.AreEqual(1, n, "the blade's deposit takes a slot in the buffer");
            Assert.AreEqual(0.22f, into[0].Radius, 1e-6f);
            Assert.AreEqual(0.3f, into[0].Amount, 1e-6f);
            Assert.AreEqual(0.8f, into[0].Vigour, 1e-6f, "a working blade marks its water fresh, as a hull does");
            // With no displaced sea there is no drawing lift to take off: laid where it is seen.
            Assert.AreEqual(1f, into[0].From.x, 1e-5f);
            Assert.AreEqual(2f, into[0].From.y, 1e-5f);
        }

        [Test]
        public void ADepositOutsideTheCamerasWindow_TakesNoSlot()
        {
            var into = new FoamInjection[FoamBuffer.MaxInjectors];
            FoamSplashes.Add(new Vector2(100f, 100f), new Vector2(100f, 100f), 0.22f, 0.3f, 0.8f);
            Assert.AreEqual(0, FoamInjectionRegistry.CollectInjections(into, Rect.MinMaxRect(-10f, -10f, 10f, 10f)));
        }

        [Test]
        public void AFrameWithNoDeposits_GathersNothing_EvenAfterOneWasLaid()
        {
            var into = new FoamInjection[FoamBuffer.MaxInjectors];
            Rect window = Rect.MinMaxRect(-10f, -10f, 10f, 10f);
            FoamSplashes.Add(Vector2.zero, Vector2.zero, 0.22f, 0.3f, 0.8f);
            Assert.AreEqual(1, FoamInjectionRegistry.CollectInjections(into, window));
            FoamSplashes.Clear();
            Assert.AreEqual(0, FoamInjectionRegistry.CollectInjections(into, window),
                "nothing is left standing once the oars stop laying");
        }
    }
}
