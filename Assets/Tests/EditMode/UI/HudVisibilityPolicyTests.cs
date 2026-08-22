using NUnit.Framework;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// <b>The 2026-08-22 diegetic ruling, as a truth table.</b>
    ///
    /// <para>The ruling was made in play, from the owner's eyes, and it is the kind of thing that comes
    /// undone one label at a time: someone adds a readout, nobody checks it against the rule, and the
    /// band is quietly handing out four free numbers again. So the rule is pinned here rather than
    /// eyeballed — the same treatment <c>HelmHudSuppressionTests</c> gives the nav cluster.</para>
    ///
    /// <para>Pure: <see cref="HudVisibilityPolicy.MayShow(HudReadout, HudInstruments, bool)"/> takes what
    /// is owned and the override as arguments, so none of this needs a canvas, a service or a frame.</para>
    /// </summary>
    public sealed class HudVisibilityPolicyTests
    {
        [TearDown]
        public void TearDown() => HudVisibilityPolicy.Reset();

        // ---- the shipped state: a new game shows none of it ------------------------------------

        [Test]
        public void NoInstrument_HidesEveryBandReadout()
        {
            foreach (HudReadout readout in System.Enum.GetValues(typeof(HudReadout)))
            {
                Assert.IsFalse(HudVisibilityPolicy.MayShow(readout, HudInstruments.None, devShowAll: false),
                    $"{readout} must be hidden with no instrument owned — that is the whole ruling");
            }
        }

        [Test]
        public void ShippedSeam_OwnsNothing_AndDoesNotOverride()
        {
            // The live seam's defaults ARE the shipped game: nothing grants an instrument yet, so a new
            // game is quiet without anything having to remember to turn the band off.
            Assert.AreEqual(HudInstruments.None, HudVisibilityPolicy.Owned);
            Assert.IsFalse(HudVisibilityPolicy.DevShowAll);
            Assert.IsFalse(HudVisibilityPolicy.MayShow(HudReadout.Tide));
            Assert.IsFalse(HudVisibilityPolicy.MayShow(HudReadout.Money));
        }

        // ---- an instrument buys back exactly its own read ---------------------------------------

        [Test]
        public void ATideClock_ShowsTheTide_AndNothingElse()
        {
            const HudInstruments owned = HudInstruments.TideClock;

            Assert.IsTrue(HudVisibilityPolicy.MayShow(HudReadout.Tide, owned, false),
                "a tide clock is exactly the instrument the tide read is allowed to come from");
            Assert.IsFalse(HudVisibilityPolicy.MayShow(HudReadout.Wind, owned, false));
            Assert.IsFalse(HudVisibilityPolicy.MayShow(HudReadout.Sea, owned, false));
        }

        [Test]
        public void AnAnemometer_ShowsTheWind_AndNothingElse()
        {
            const HudInstruments owned = HudInstruments.Anemometer;

            Assert.IsTrue(HudVisibilityPolicy.MayShow(HudReadout.Wind, owned, false));
            Assert.IsFalse(HudVisibilityPolicy.MayShow(HudReadout.Tide, owned, false));
            Assert.IsFalse(HudVisibilityPolicy.MayShow(HudReadout.Sea, owned, false));
        }

        [Test]
        public void ASeaGauge_ShowsTheSeaState_AndNothingElse()
        {
            const HudInstruments owned = HudInstruments.SeaGauge;

            Assert.IsTrue(HudVisibilityPolicy.MayShow(HudReadout.Sea, owned, false));
            Assert.IsFalse(HudVisibilityPolicy.MayShow(HudReadout.Tide, owned, false));
            Assert.IsFalse(HudVisibilityPolicy.MayShow(HudReadout.Wind, owned, false));
        }

        [Test]
        public void InstrumentsCombine_WithoutUnlockingEachOther()
        {
            const HudInstruments owned = HudInstruments.TideClock | HudInstruments.SeaGauge;

            Assert.IsTrue(HudVisibilityPolicy.MayShow(HudReadout.Tide, owned, false));
            Assert.IsTrue(HudVisibilityPolicy.MayShow(HudReadout.Sea, owned, false));
            Assert.IsFalse(HudVisibilityPolicy.MayShow(HudReadout.Wind, owned, false),
                "owning two instruments must not grant a third read she has no gauge for");
        }

        // ---- money is not an instrument question ------------------------------------------------

        [Test]
        public void Money_IsNeverOnTheBand_WhateverSheOwns()
        {
            // There is no gauge that could earn the balance back onto the band: the notebook is where
            // money is kept, so this holds for EVERY combination of instruments.
            HudInstruments all = HudInstruments.TideClock | HudInstruments.Anemometer | HudInstruments.SeaGauge;

            Assert.IsFalse(HudVisibilityPolicy.MayShow(HudReadout.Money, HudInstruments.None, false));
            Assert.IsFalse(HudVisibilityPolicy.MayShow(HudReadout.Money, HudInstruments.TideClock, false));
            Assert.IsFalse(HudVisibilityPolicy.MayShow(HudReadout.Money, all, false),
                "money lives in the notebook — no instrument puts it back on the band");
        }

        // ---- the debugging door ------------------------------------------------------------------

        [Test]
        public void DevOverride_ShowsEverything_OwningNothing()
        {
            foreach (HudReadout readout in System.Enum.GetValues(typeof(HudReadout)))
            {
                Assert.IsTrue(HudVisibilityPolicy.MayShow(readout, HudInstruments.None, devShowAll: true),
                    $"{readout} must come back under the dev override — that is what it is for");
            }
        }

        [Test]
        public void Reset_PutsTheSeamBackToShipped()
        {
            HudVisibilityPolicy.DevShowAll = true;
            HudVisibilityPolicy.Owned = HudInstruments.TideClock | HudInstruments.Anemometer;
            Assert.IsTrue(HudVisibilityPolicy.MayShow(HudReadout.Money), "precondition: the override is up");

            HudVisibilityPolicy.Reset();

            Assert.AreEqual(HudInstruments.None, HudVisibilityPolicy.Owned);
            Assert.IsFalse(HudVisibilityPolicy.DevShowAll);
            Assert.IsFalse(HudVisibilityPolicy.MayShow(HudReadout.Tide));
        }

        [Test]
        public void TheLiveSeam_AgreesWithThePureRule()
        {
            HudVisibilityPolicy.Owned = HudInstruments.Anemometer;

            foreach (HudReadout readout in System.Enum.GetValues(typeof(HudReadout)))
            {
                Assert.AreEqual(
                    HudVisibilityPolicy.MayShow(readout, HudVisibilityPolicy.Owned, HudVisibilityPolicy.DevShowAll),
                    HudVisibilityPolicy.MayShow(readout),
                    $"the one-argument overload must be the pure rule read against the seam, for {readout}");
            }
        }

        // ---- ⚠ the boat's instruments are NOT this policy's business -----------------------------

        [Test]
        public void HelmInstruments_AreUnaffectedByTheBandPolicy()
        {
            // The nav cluster (heading, rose, set-and-drift, apparent wind) is the BOAT's instruments —
            // exactly the thing the ruling says a number may come from — so hiding it here would be
            // reading the ruling backwards. Its placement is HelmHudSuppression's answer alone, and
            // that answer must not move when the band policy does.
            var tillerFit = HelmFit.None;
            var dashNoCompass = new HelmFit(ConsoleRigKind.Console, SounderKind.None, CompassMount.None,
                                            radar: false, gps: false);
            var dashWithCompass = new HelmFit(ConsoleRigKind.Console, SounderKind.None, CompassMount.Dome,
                                              radar: false, gps: false);

            for (int pass = 0; pass < 2; pass++)
            {
                // pass 0 = shipped (everything hidden), pass 1 = the dev override (everything shown).
                HudVisibilityPolicy.Reset();
                if (pass == 1) HudVisibilityPolicy.DevShowAll = true;

                Assert.AreEqual(NavClusterPlacement.Hidden,
                    HelmHudSuppression.NavCluster(aboard: false, HelmControlStyle.None, in tillerFit),
                    "ashore the cluster is hidden, whatever the band is doing");

                Assert.AreEqual(NavClusterPlacement.BottomCentre,
                    HelmHudSuppression.NavCluster(aboard: true, HelmControlStyle.Tiller, in tillerFit),
                    "a tiller hull keeps its cluster at home, whatever the band is doing");

                Assert.AreEqual(NavClusterPlacement.ClearOfTheDash,
                    HelmHudSuppression.NavCluster(aboard: true, HelmControlStyle.Lever, in dashNoCompass),
                    "a dash with no compass still moves the cluster clear rather than deleting it");

                Assert.AreEqual(NavClusterPlacement.Hidden,
                    HelmHudSuppression.NavCluster(aboard: true, HelmControlStyle.Lever, in dashWithCompass),
                    "a dash that carries a compass still hides the duplicate");
            }
        }
    }
}
