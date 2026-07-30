using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// M1 §7.3 — the container-level freshness READ. This is what the deck tag shows the player, and
    /// <see cref="Freshness"/>'s own notes make the stakes explicit: <i>"losing one that looked fine
    /// until the buyer refused it is a bug."</i> So the countdown must be right, must agree with the
    /// sell gate, and must go quiet exactly when the catch is genuinely off the clock.
    ///
    /// <para>Pure — a <see cref="SpoilContext"/> is constructed directly, so no services are wired and
    /// nothing here depends on a clock.</para>
    /// </summary>
    public class HoldFreshnessTests
    {
        private const float SecondsPerDay = 1200f;
        private static readonly SpoilPolicy Policy = SpoilPolicy.Default;   // refusal at 0.9, ambient ×1

        private static SpoilContext At(double nowSeconds)
            => new SpoilContext(nowSeconds, SecondsPerDay, Policy);

        /// <summary>A catch landed at <paramref name="landedAt"/> that rots in one in-game day.</summary>
        private static CatchItem Fish(double landedAt, float spoilPerDay = 1f,
                                      StorageMode mode = StorageMode.Ambient, string id = "fish.cod")
            => new CatchItem(id, "Atlantic Cod", FishCategory.InshoreGroundfish, 3.4f, 40, 0.1f,
                             spoilPerDay, Freshness.Landed(landedAt, mode));

        // ---- the empty and trivial cases --------------------------------------------------------

        [Test]
        public void EmptyHold_ReadsAsEmpty_AndShowsNoCountdown()
        {
            HoldFreshnessSummary s = HoldFreshness.Summarise(new List<CatchItem>(), At(0.0));

            Assert.AreEqual(0, s.ItemCount);
            Assert.IsFalse(s.HasCountdown, "nothing aboard is nothing to count down");
            Assert.IsFalse(s.HasSpoiled);
        }

        [Test]
        public void NullHold_ReadsAsEmptyRatherThanThrowing()
        {
            Assert.AreEqual(0, HoldFreshness.Summarise((IHold)null, At(0.0)).ItemCount);
            Assert.AreEqual(0, HoldFreshness.Summarise((IReadOnlyList<CatchItem>)null, At(0.0)).ItemCount);
        }

        [Test]
        public void FreshCatch_ReadsFresh_WithTheFullRunwayAhead()
        {
            var items = new List<CatchItem> { Fish(landedAt: 0.0) };
            HoldFreshnessSummary s = HoldFreshness.Summarise(items, At(0.0));

            Assert.AreEqual(1, s.ItemCount);
            Assert.AreEqual(0f, s.WorstSpoil, 1e-5f);
            Assert.IsFalse(s.HasSpoiled);
            Assert.IsTrue(s.HasCountdown);
            // Rots in one day; refused at 0.9 ⇒ 0.9 of a day of runway.
            Assert.AreEqual(0.9 * SecondsPerDay, s.SecondsToRefusal, 1e-3);
        }

        // ---- the hold reads as its WORST item ---------------------------------------------------

        [Test]
        public void HoldReadsAsItsWorstItem_NotItsAverage()
        {
            // One landed this instant, one landed half a day ago. The read must be about the old one —
            // it is the one that is going to cost the player something.
            var items = new List<CatchItem>
            {
                Fish(landedAt: 600.0),   // fresh
                Fish(landedAt: 0.0),     // half a day old ⇒ spoil 0.5
                Fish(landedAt: 300.0),   // quarter day    ⇒ spoil 0.25
            };
            HoldFreshnessSummary s = HoldFreshness.Summarise(items, At(600.0));

            Assert.AreEqual(3, s.ItemCount);
            Assert.AreEqual(0.5f, s.WorstSpoil, 1e-4f, "the worst item sets the read");
            // 0.9 - 0.5 = 0.4 of a day left on the worst one.
            Assert.AreEqual(0.4 * SecondsPerDay, s.SecondsToRefusal, 1e-2);
        }

        [Test]
        public void SpoiledCount_CountsOnlyWhatABuyerWouldRefuse()
        {
            var items = new List<CatchItem>
            {
                Fish(landedAt: 0.0),      // a full day old ⇒ spoil 1.0 → refused
                Fish(landedAt: 120.0),    // 0.9 day        ⇒ spoil 0.9 → refused (at the threshold)
                Fish(landedAt: 600.0),    // half a day     ⇒ spoil 0.5 → still sellable
            };
            HoldFreshnessSummary s = HoldFreshness.Summarise(items, At(1200.0));

            Assert.AreEqual(2, s.SpoiledCount);
            Assert.IsTrue(s.HasSpoiled);
        }

        [Test]
        public void SpoiledCount_AgreesWithTheSellGate_ItemForItem()
        {
            // The tag and the fishmonger must never disagree — this is the whole point of folding the
            // read off the same SpoilContext the sell path uses.
            var items = new List<CatchItem>
            {
                Fish(landedAt: 0.0), Fish(landedAt: 300.0), Fish(landedAt: 900.0), Fish(landedAt: 1100.0),
            };
            SpoilContext ctx = At(1200.0);

            int refusedByGate = 0;
            foreach (CatchItem item in items)
                if (!ctx.IsSellable(item)) refusedByGate++;

            Assert.AreEqual(refusedByGate, HoldFreshness.Summarise(items, ctx).SpoiledCount);
        }

        // ---- the countdown ----------------------------------------------------------------------

        [Test]
        public void Countdown_ShrinksAsTheCatchSits()
        {
            var items = new List<CatchItem> { Fish(landedAt: 0.0) };

            double early = HoldFreshness.Summarise(items, At(100.0)).SecondsToRefusal;
            double later = HoldFreshness.Summarise(items, At(500.0)).SecondsToRefusal;

            Assert.Less(later, early, "time you have left only ever goes down while it sits out");
        }

        [Test]
        public void Countdown_IsGoneOnceTheCatchIsAlreadyRefused()
        {
            var items = new List<CatchItem> { Fish(landedAt: 0.0) };
            HoldFreshnessSummary s = HoldFreshness.Summarise(items, At(2400.0));   // two days out

            Assert.IsTrue(s.HasSpoiled);
            Assert.IsFalse(s.HasCountdown, "there is nothing left to count down to");
        }

        [Test]
        public void ArrestedStorage_HasNoCountdown_BecauseItIsNotOnAClock()
        {
            foreach (StorageMode mode in new[] { StorageMode.Frozen, StorageMode.Live, StorageMode.Iced })
            {
                var items = new List<CatchItem> { Fish(landedAt: 0.0, mode: mode) };
                HoldFreshnessSummary s = HoldFreshness.Summarise(items, At(600.0));

                Assert.IsFalse(s.HasCountdown, $"{mode} arrests spoil under the default policy");
                Assert.AreEqual(mode, s.WorstMode, "the read names how the worst item is held");
            }
        }

        [Test]
        public void ImperishableCatch_HasNoCountdown()
        {
            // The legacy constructor's SpoilPerDay = 0 must read as "never rots", not "instantly rots".
            var items = new List<CatchItem>
            {
                new CatchItem("fish.cod", "Atlantic Cod", FishCategory.InshoreGroundfish, 3.4f, 40, 0.1f),
            };
            HoldFreshnessSummary s = HoldFreshness.Summarise(items, At(100000.0));

            Assert.AreEqual(0f, s.WorstSpoil, 1e-6f);
            Assert.IsFalse(s.HasSpoiled);
            Assert.IsFalse(s.HasCountdown);
        }

        [Test]
        public void Countdown_ScalesWithTheSpeciesRate()
        {
            // A fast-rotting species must show less runway than a slow one, at the same instant.
            var quick = new List<CatchItem> { Fish(landedAt: 0.0, spoilPerDay: 2f) };
            var slow = new List<CatchItem> { Fish(landedAt: 0.0, spoilPerDay: 0.5f) };

            double quickLeft = HoldFreshness.Summarise(quick, At(0.0)).SecondsToRefusal;
            double slowLeft = HoldFreshness.Summarise(slow, At(0.0)).SecondsToRefusal;

            Assert.Less(quickLeft, slowLeft);
            Assert.AreEqual(0.9 / 2.0 * SecondsPerDay, quickLeft, 1e-3);
            Assert.AreEqual(0.9 / 0.5 * SecondsPerDay, slowLeft, 1e-3);
        }

        [Test]
        public void Countdown_ReachesZeroExactlyAtTheRefusalThreshold()
        {
            // Walk to the instant the read says refusal arrives, and check the sell gate agrees there.
            var items = new List<CatchItem> { Fish(landedAt: 0.0) };
            double runway = HoldFreshness.Summarise(items, At(0.0)).SecondsToRefusal;

            Assert.IsTrue(At(runway - 1.0).IsSellable(items[0]), "a second before, it still sells");
            Assert.IsFalse(At(runway + 1.0).IsSellable(items[0]), "a second after, nobody takes it");
        }

        [Test]
        public void ZeroDayLength_DegradesToNoCountdownRatherThanDividingByZero()
        {
            var items = new List<CatchItem> { Fish(landedAt: 0.0) };
            var broken = new SpoilContext(600.0, 0f, Policy);

            Assert.IsFalse(HoldFreshness.Summarise(items, broken).HasCountdown);
        }

        // ---- the wording ------------------------------------------------------------------------

        [Test]
        public void Countdown_WordsReadInHoursThenMinutes_AndRoundDown()
        {
            // Rounding DOWN matters: a tag that says "1h" when you have fifty minutes is a trap.
            Assert.AreEqual("⚠ 4h", HoldReadStrings.Countdown(4.9));
            Assert.AreEqual("⚠ 1h", HoldReadStrings.Countdown(1.0));
            Assert.AreEqual("⚠ 50m", HoldReadStrings.Countdown(50.0 / 60.0));
            Assert.AreEqual("⚠ 0m", HoldReadStrings.Countdown(0.0));
            Assert.AreEqual("⚠ 0m", HoldReadStrings.Countdown(-5.0), "a negative span is not a warning");
        }

        [Test]
        public void HeldAndRubbish_NameWhatIsActuallyGoingOn()
        {
            Assert.AreEqual("⚠ frozen", HoldReadStrings.Held(StorageMode.Frozen));
            Assert.AreEqual("⚠ on ice", HoldReadStrings.Held(StorageMode.Iced));
            Assert.AreEqual("⚠ alive", HoldReadStrings.Held(StorageMode.Live));

            Assert.AreEqual("⚠ spoiled", HoldReadStrings.Rubbish(1));
            Assert.AreEqual("⚠ 3 spoiled", HoldReadStrings.Rubbish(3));
        }

        [Test]
        public void EveryRead_CarriesTheWarningGlyph_SoColourIsNeverTheOnlyChannel()
        {
            // Accessibility §8: the tag escalates in colour, but the glyph and the number must carry it.
            Assert.IsTrue(HoldReadStrings.Countdown(3.0).Contains(HoldReadStrings.TurningGlyph));
            Assert.IsTrue(HoldReadStrings.Held(StorageMode.Iced).Contains(HoldReadStrings.TurningGlyph));
            Assert.IsTrue(HoldReadStrings.Rubbish(2).Contains(HoldReadStrings.TurningGlyph));
        }
    }
}
