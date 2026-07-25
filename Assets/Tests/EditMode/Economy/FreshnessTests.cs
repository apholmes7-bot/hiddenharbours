using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The freshness clock (M1 §7.3). These guard the properties the arc leans on: a bucket left out
    /// visibly costs you, the freezer and a wet bucket arrest it, putting it away LATE doesn't undo the
    /// hours already lost, and a save/reload or a sleep-skip lands on exactly the number an unbroken
    /// session would.
    /// </summary>
    public class FreshnessTests
    {
        // One in-game day of clock time (GameConfig.SecondsPerDay default). Passed in, never assumed.
        private const float SecondsPerDay = 1200f;

        // A brisk perisher: ruined in two in-game days left in the open.
        private const float SpoilPerDay = 0.5f;

        private static SpoilPolicy Policy => SpoilPolicy.Default;

        private static float SpoilAfter(FreshnessState s, double now)
            => Freshness.SpoilAt(s, now, SpoilPerDay, SecondsPerDay, Policy);

        // ---- the basic clock -------------------------------------------------------------------

        [Test]
        public void JustLanded_IsPerfectlyFresh()
        {
            var s = Freshness.Landed(1000d);
            Assert.AreEqual(0f, SpoilAfter(s, 1000d), 1e-6f);
            Assert.AreEqual(1f, Freshness.Remaining(SpoilAfter(s, 1000d)), 1e-6f);
        }

        [Test]
        public void LeftInTheOpen_SpoilsAtTheSpeciesRate()
        {
            var s = Freshness.Landed(0d);
            Assert.AreEqual(0.5f, SpoilAfter(s, SecondsPerDay), 1e-5f, "one day at 0.5/day");
            Assert.AreEqual(0.25f, SpoilAfter(s, SecondsPerDay * 0.5f), 1e-5f, "half a day");
        }

        [Test]
        public void SpoilNeverExceedsGone()
        {
            var s = Freshness.Landed(0d);
            Assert.AreEqual(1f, SpoilAfter(s, SecondsPerDay * 10f), 1e-6f);
        }

        // ---- the two ways to arrest it ---------------------------------------------------------

        [Test]
        public void Frozen_ArrestsSpoil()
        {
            var s = Freshness.Landed(0d, StorageMode.Frozen);
            Assert.AreEqual(0f, SpoilAfter(s, SecondsPerDay * 5f), 1e-6f, "Ginny's freezer holds it");
        }

        [Test]
        public void KeptAlive_ArrestsSpoil()
        {
            var s = Freshness.Landed(0d, StorageMode.Live);
            Assert.AreEqual(0f, SpoilAfter(s, SecondsPerDay * 5f), 1e-6f, "shellfish in a wet bucket");
        }

        // ---- the beat the arc depends on -------------------------------------------------------

        [Test]
        public void FreezingLate_DoesNotUndoTheHoursAlreadyLost()
        {
            var s = Freshness.Landed(0d);                                    // out in the open...
            s = Freshness.WithMode(s, SecondsPerDay, SpoilPerDay, SecondsPerDay, Policy, StorageMode.Frozen);

            Assert.AreEqual(0.5f, s.SpoilAccrued, 1e-5f, "a day in the open is banked");
            Assert.AreEqual(0.5f, SpoilAfter(s, SecondsPerDay * 9f), 1e-5f,
                            "and eight more days frozen add nothing — but give nothing back either");
        }

        [Test]
        public void ThawingResumesFromWhereItStopped()
        {
            var s = Freshness.Landed(0d, StorageMode.Frozen);
            s = Freshness.WithMode(s, SecondsPerDay * 3f, SpoilPerDay, SecondsPerDay, Policy, StorageMode.Ambient);

            Assert.AreEqual(0f, s.SpoilAccrued, 1e-6f, "three days frozen banked nothing");
            Assert.AreEqual(0.5f, SpoilAfter(s, SecondsPerDay * 4f), 1e-5f, "then one day out spoils normally");
        }

        // ---- determinism: the whole reason it's settle-on-read ---------------------------------

        [Test]
        public void SleepSkip_MatchesAnUnbrokenSession()
        {
            // Read every hour across two days...
            var stepped = Freshness.Landed(0d);
            for (int h = 1; h <= 48; h++)
            {
                double t = SecondsPerDay * (h / 24d);
                stepped = Freshness.Settle(stepped, t, SpoilPerDay, SecondsPerDay, Policy);
            }

            // ...versus sleeping straight through to the same instant.
            var skipped = Freshness.Landed(0d);
            skipped = Freshness.Settle(skipped, SecondsPerDay * 2d, SpoilPerDay, SecondsPerDay, Policy);

            Assert.AreEqual(skipped.SpoilAccrued, stepped.SpoilAccrued, 1e-5f,
                            "settling often must equal settling once — no drift, no per-frame countdown");
        }

        [Test]
        public void SaveAndReload_RestoresExactly()
        {
            var before = Freshness.Landed(0d);
            before = Freshness.Settle(before, SecondsPerDay * 0.75d, SpoilPerDay, SecondsPerDay, Policy);

            // The three fields a save would carry, round-tripped.
            var reloaded = new FreshnessState(before.SpoilAccrued, before.LastSettleGameSeconds, before.Mode);

            Assert.AreEqual(SpoilAfter(before, SecondsPerDay * 2d),
                            SpoilAfter(reloaded, SecondsPerDay * 2d), 1e-6f);
        }

        [Test]
        public void SettleIsIdempotentAtAFixedInstant()
        {
            var once = Freshness.Settle(Freshness.Landed(0d), SecondsPerDay, SpoilPerDay, SecondsPerDay, Policy);
            var twice = Freshness.Settle(once, SecondsPerDay, SpoilPerDay, SecondsPerDay, Policy);
            Assert.AreEqual(once.SpoilAccrued, twice.SpoilAccrued, 1e-6f);
        }

        [Test]
        public void AClockThatGoesBackwards_BanksNothing()
        {
            var s = Freshness.Settle(Freshness.Landed(0d), SecondsPerDay, SpoilPerDay, SecondsPerDay, Policy);
            Assert.AreEqual(s.SpoilAccrued, SpoilAfter(s, 0d), 1e-6f, "it must never un-rot");
            var back = Freshness.Settle(s, 0d, SpoilPerDay, SecondsPerDay, Policy);
            Assert.AreEqual(SecondsPerDay, back.LastSettleGameSeconds, 1e-6f, "and never rewind its stamp");
        }

        // ---- the money consequence -------------------------------------------------------------

        [Test]
        public void FreshSellsAtFullValue()
            => Assert.AreEqual(1f, Freshness.ValueMultiplier(0f, Policy), 1e-6f);

        [Test]
        public void SpoiledIsWorthNothing()
            => Assert.AreEqual(0f, Freshness.ValueMultiplier(1f, Policy), 1e-6f);

        [Test]
        public void ValueFallsMonotonicallyWithSpoil()
        {
            float previous = float.MaxValue;
            for (int i = 0; i <= 10; i++)
            {
                float v = Freshness.ValueMultiplier(i / 10f, Policy);
                Assert.Less(v, previous, "each step of rot must be worth strictly less");
                previous = v;
            }
        }

        // ---- refusal: rubbish you have to carry home and dump ----------------------------------

        [Test]
        public void FreshIsSellable()
        {
            Assert.IsTrue(Freshness.IsSellable(0f, Policy));
            Assert.IsFalse(Freshness.IsSpoiled(0f, Policy));
        }

        [Test]
        public void PastTheThreshold_NoBuyerWillTakeIt()
        {
            float justOver = Policy.UnsellableSpoil;
            Assert.IsFalse(Freshness.IsSellable(justOver, Policy), "at the threshold he waves you off");
            Assert.IsTrue(Freshness.IsSpoiled(justOver, Policy));
            Assert.IsFalse(Freshness.IsSellable(1f, Policy), "and certainly when it's gone");
        }

        [Test]
        public void JustUnderTheThreshold_StillSells_ButPoorly()
        {
            float justUnder = Policy.UnsellableSpoil - 0.01f;
            Assert.IsTrue(Freshness.IsSellable(justUnder, Policy));
            Assert.Less(Freshness.ValueMultiplier(justUnder, Policy), 0.2f,
                        "sellable, but the trip was close to wasted");
        }

        [Test]
        public void ABucketLeftOut_BecomesRubbishOnItsOwn()
        {
            // Nobody touches it: landed ambient, forgotten for two in-game days at 0.5/day.
            var s = Freshness.Landed(0d);
            float spoil = SpoilAfter(s, SecondsPerDay * 2f);

            Assert.AreEqual(1f, spoil, 1e-5f);
            Assert.IsTrue(Freshness.IsSpoiled(spoil, Policy),
                          "and now it has to be dumped before the hold is any use");
        }
    }
}
