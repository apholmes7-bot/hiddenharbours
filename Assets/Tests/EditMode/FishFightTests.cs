using NUnit.Framework;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// VS-13 — the fishing-fight maths (the testable POCO at the heart of the cozy mini-game). Covers
    /// the tension/landing loop, the snap-on-sustained-hold rule, that a paced reel/ease lands the fish,
    /// and that bigger fish snap tighter and take more reeling. Seeded RNG pins surge timing.
    /// </summary>
    public class FishFightTests
    {
        private static FishFight MakeFight(FishCategory cat = FishCategory.InshoreGroundfish,
            float weight = 3f, float min = 1f, float max = 8f, int seed = 12345)
        {
            var tuning = FishFightTuning.For(cat, weight, min, max);
            return new FishFight(in tuning, new System.Random(seed));
        }

        [Test]
        public void Holding_RaisesTension()
        {
            var f = MakeFight();
            for (int i = 0; i < 5; i++) f.Tick(0.1f, reeling: true);
            Assert.Greater(f.Tension01, 0f, "reeling should raise line tension");
        }

        [Test]
        public void Releasing_LowersTension()
        {
            var f = MakeFight();
            for (int i = 0; i < 5; i++) f.Tick(0.1f, true);   // build some tension
            float peak = f.Tension01;
            Assert.Greater(peak, 0f);

            for (int i = 0; i < 5; i++) f.Tick(0.1f, false);  // ease off
            Assert.Less(f.Tension01, peak, "easing should let tension fall");
        }

        [Test]
        public void Landing_FillsOnlyWhileReeling()
        {
            var f = MakeFight();
            f.Tick(0.2f, true);
            float landed = f.Landing01;
            Assert.Greater(landed, 0f, "reeling should fill the landing gauge");

            for (int i = 0; i < 5; i++) f.Tick(0.2f, false);
            Assert.AreEqual(landed, f.Landing01, 1e-6f, "landing must not progress while easing");
        }

        [Test]
        public void SustainedHold_Snaps_WithoutLanding()
        {
            var f = MakeFight();
            for (int i = 0; i < 400 && !f.IsOver; i++) f.Tick(0.05f, true);

            Assert.AreEqual(FishFightResult.Snapped, f.Result, "you can't just hold — it must snap");
            Assert.Less(f.Landing01, 1f, "it should snap before the fish is landed");
        }

        [Test]
        public void PacedReelAndEase_LandsTheFish()
        {
            var f = MakeFight();
            // A forgiving "pulse": reel when the line is slack-ish, ease when it strains.
            for (int i = 0; i < 4000 && !f.IsOver; i++)
                f.Tick(0.05f, reeling: f.Tension01 < 0.55f);

            Assert.AreEqual(FishFightResult.Landed, f.Result, "a paced reel/ease should land the fish");
        }

        [Test]
        public void BiggerFish_SnapsTighter_AndNeedsMoreReeling()
        {
            // Same species range, min vs max weight → clearly different strength.
            var small = MakeFight(weight: 1f, min: 1f, max: 8f);
            var big   = MakeFight(weight: 8f, min: 1f, max: 8f);

            // (a) Needs more reeling: a fixed safe reel accrues less landing on the bigger fish.
            small.Tick(0.5f, true);
            big.Tick(0.5f, true);
            Assert.Greater(small.Landing01, big.Landing01, "bigger fish should land slower (more reeling)");

            // (b) Snaps tighter: under a sustained hold the bigger fish snaps in fewer ticks.
            int smallTicks = TicksToSnap(MakeFight(weight: 1f, min: 1f, max: 8f));
            int bigTicks   = TicksToSnap(MakeFight(weight: 8f, min: 1f, max: 8f));
            Assert.Less(bigTicks, smallTicks, "bigger fish should snap sooner under a sustained hold");
        }

        [Test]
        public void HandGather_Tend_NeverSnaps_AndLandsByHolding()
        {
            var f = MakeFight(FishCategory.Shellfish, weight: 1f, min: 0.4f, max: 4f);
            for (int i = 0; i < 200 && !f.IsOver; i++) f.Tick(0.05f, true); // hold to tend
            Assert.AreEqual(FishFightResult.Landed, f.Result, "the tend variant lands by holding, never snaps");
            Assert.Less(f.Tension01, 1f);
        }

        private static int TicksToSnap(FishFight f)
        {
            int n = 0;
            while (!f.IsOver && n < 2000) { f.Tick(0.05f, true); n++; }
            Assert.AreEqual(FishFightResult.Snapped, f.Result, "expected a snap under sustained hold");
            return n;
        }
    }
}
