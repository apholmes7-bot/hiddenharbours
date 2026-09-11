using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// FISH REACT TO IT LANDING (owner's ask, 2026-09-09) — the pure half of the gull-to-fish wiring:
    /// how far a splash reaches, how hard it hits, how long the fish run, and what the listener does
    /// before and after it exists.
    ///
    /// <para><b>The load-bearing claims, in order.</b> A bird that comes down ON a school scatters it; a
    /// school out of reach never hears her; a listener that is not subscribed hears nothing at all, and
    /// one that has unsubscribed stops hearing; and the whole thing is a closed-form function of
    /// <c>(where, when)</c>, so the same splash scatters the same fish the same way on every run.</para>
    ///
    /// <para><b>⚠️ What is NOT here, deliberately.</b> There is no assertion about bite rate, catch
    /// weight or the species roll, because the scatter touches none of them (owner's ruling 2026-09-09:
    /// fish bite rates are LEAVE). The gull moves the picture. If a future edit gives her a hand on the
    /// bite, nothing in this file will go red — which is why the ruling is also written into the tooltip
    /// of the knob and the doc of the publisher, where an author actually stands.</para>
    /// </summary>
    public sealed class GullSplashScatterTests
    {
        // The owner's two knobs, at the values this PR ships in GameConfig.asset.
        const float Reach = 2.5f;
        const float Strength = 0.5f;

        static readonly string[] Herring = { "fish.atlantic_herring" };

        static FishSchool SchoolAt(float x, float y, float radius = 4f, float depth = 2f)
            => new FishSchool(new Vector2(x, y), radius, depth, 6, Herring, 0.0, 10000.0);

        static GullSplashed SplashAt(float x, float y)
            => new GullSplashed(new Vector2(x, y), GullSplashKind.Alight);

        [SetUp]
        public void ClearTheChannel() => EventBus.Clear<GullSplashed>();

        [TearDown]
        public void ClearTheChannelAgain() => EventBus.Clear<GullSplashed>();

        // ── the school she landed on ────────────────────────────────────────────────────────────────

        /// <summary>
        /// <b>A bird lands on the school and the school bolts.</b> The splash is at the centre, so the
        /// distance to the school's edge is negative and the hit is the full strength the owner tuned —
        /// and every fish drawn in it is pushed AWAY from her, not toward her.
        /// </summary>
        [Test]
        public void ASplashOnTheSchoolScattersIt()
        {
            var log = new GullSplashLog();
            log.RecordAt(SplashAt(10f, 10f), 100.0);

            FishSchool school = SchoolAt(10f, 10f);
            double now = 100.0 + ShoalEventMath.DurationSeconds(ShoalEventKind.Dart) * 0.5;

            Assert.IsTrue(log.TryScatter(school, now, Reach, Strength,
                                         out float strength, out double start, out Vector2 from),
                          "a bird landed in the middle of the school and nothing moved");
            Assert.AreEqual(Strength, strength, 1e-5f, "a hit at the centre is the full strength");
            Assert.AreEqual(100.0, start, 1e-9, "the dart runs from when she hit the water");
            Assert.AreEqual(new Vector2(10f, 10f), from);

            // A fish two metres east of her goes further east; the offset is radial and outward.
            ShoalEventMath.ScatterOffset(from.x, from.y, 12f, 10f, strength, 3f, start, now,
                                         out float dx, out float dy);
            Assert.Greater(dx, 0f, "the fish swam toward the bird instead of away from her");
            Assert.AreEqual(0f, dy, 1e-5f, "a fish due east of the splash bolts due east");
        }

        /// <summary>
        /// <b>A school out of reach never hears her.</b> The reach is measured to the school's EDGE — a
        /// school is eight to fourteen metres across and its fish are drawn over the whole disc, so a
        /// splash three metres off the rim is three metres from the nearest fish, not from the anchor.
        /// </summary>
        [Test]
        public void ADistantSchoolIsUntouched()
        {
            var log = new GullSplashLog();
            log.RecordAt(SplashAt(0f, 0f), 100.0);

            // Radius 4, reach 2.5: a centre at 40 m is nowhere near, and one at 7 m is still outside.
            FishSchool faraway = SchoolAt(40f, 0f);
            FishSchool justOutside = SchoolAt(7f, 0f);
            double now = 100.5;

            Assert.IsFalse(log.TryScatter(faraway, now, Reach, Strength, out _, out _, out _),
                           "a school forty metres away bolted from a splash it cannot have heard");
            Assert.IsFalse(log.TryScatter(justOutside, now, Reach, Strength, out _, out _, out _),
                           "the reach is measured to the school's edge and 7 - 4 = 3 is outside 2.5");

            // And the offset law agrees: no strength, no movement.
            ShoalEventMath.ScatterOffset(0f, 0f, 40f, 0f, 0f, 3f, 100.0, now,
                                         out float dx, out float dy);
            Assert.AreEqual(0f, dx);
            Assert.AreEqual(0f, dy);
        }

        /// <summary>
        /// <b>The dart ends where it began.</b> The travel rides the same half-sine the jump lifts on, so
        /// a scattered fish eases back into the shoal instead of snapping back to its solved place the
        /// instant the event expires — and after the dart is over the school is exactly where the closed
        /// form always said it was.
        /// </summary>
        [Test]
        public void TheScatterReturnsTheFishToTheShoal()
        {
            double dart = ShoalEventMath.DurationSeconds(ShoalEventKind.Dart);

            ShoalEventMath.ScatterOffset(0f, 0f, 2f, 0f, Strength, 3f, 100.0, 100.0,
                                         out float atStart, out _);
            ShoalEventMath.ScatterOffset(0f, 0f, 2f, 0f, Strength, 3f, 100.0, 100.0 + dart,
                                         out float atEnd, out _);
            ShoalEventMath.ScatterOffset(0f, 0f, 2f, 0f, Strength, 3f, 100.0, 100.0 + dart * 0.5,
                                         out float atPeak, out _);

            Assert.AreEqual(0f, atStart, 1e-5f, "the bolt started part-way out");
            Assert.AreEqual(0f, atEnd, 1e-5f, "the fish snapped back when the dart expired");
            Assert.Greater(atPeak, 0f, "the fish never actually moved");

            // #802: a scattered fish stays inside its own school's disc — the travel is one shoal-width
            // at full strength, so nothing here can re-place a school or push a fish into the next cell.
            const float Spread = 3f;
            Assert.LessOrEqual(atPeak, Spread + 1e-5f,
                               "a bolting fish left its own shoal, which would re-place the school");
        }

        // ── the listener ────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// <b>A signal is heard only by a listener that already exists</b>, and only until it stops
        /// listening. <see cref="EventBus"/> has no replay, so a splash published before the subscribe is
        /// gone forever — and one published after the unsubscribe must not reach a presenter that has
        /// been switched off, or a disabled component would still be moving fish.
        /// </summary>
        [Test]
        public void OnlyALiveListenerHearsTheSplash()
        {
            var log = new GullSplashLog();

            EventBus.Publish(SplashAt(1f, 1f));
            Assert.AreEqual(0, log.Held, "a splash from before the subscribe was replayed");
            Assert.IsFalse(log.Listening);

            log.Subscribe();
            Assert.IsTrue(log.Listening);
            EventBus.Publish(SplashAt(2f, 2f));
            Assert.AreEqual(1, log.Held, "the live listener missed the splash");

            log.Subscribe();                       // idempotent: a second subscribe is not a second ear
            EventBus.Publish(SplashAt(3f, 3f));
            Assert.AreEqual(2, log.Held, "subscribing twice doubled every delivery");

            log.Unsubscribe();
            Assert.IsFalse(log.Listening);
            EventBus.Publish(SplashAt(4f, 4f));
            Assert.AreEqual(2, log.Held, "a presenter that had been switched off was still hearing gulls");

            log.Unsubscribe();                     // idempotent the other way too
            Assert.IsFalse(log.Listening);
        }

        /// <summary>
        /// <b>The strongest splash wins, not the average.</b> A second bird landing beside the first
        /// makes the fish run from the nearer of the two — an averaged origin would send them between
        /// two birds, which is the one direction a fish would never pick.
        /// </summary>
        [Test]
        public void TheNearerBirdIsTheOneTheyRunFrom()
        {
            var log = new GullSplashLog();
            log.RecordAt(SplashAt(-5f, 0f), 100.0);      // one metre off the western rim
            log.RecordAt(SplashAt(4.5f, 0f), 100.1);     // half a metre off the eastern rim — nearer

            FishSchool school = SchoolAt(0f, 0f);
            Assert.IsTrue(log.TryScatter(school, 100.2, Reach, Strength,
                                         out float strength, out double start, out Vector2 from));
            Assert.AreEqual(new Vector2(4.5f, 0f), from, "the fish ran from the wrong bird");
            Assert.AreEqual(100.1, start, 1e-9);
            Assert.Greater(strength, 0f);
        }

        // ── determinism (rule 5) ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// <b>The same splash scatters the same fish the same way, twice.</b> Two logs, the same two
        /// recorded splashes, the same instant: identical strength, identical origin, identical offsets
        /// to the bit. Nothing here integrates, accumulates or reads a clock of its own, which is what
        /// lets a save/load or a region stream-in land the fish mid-bolt in the same place.
        /// </summary>
        [Test]
        public void TheSameSplashScattersTheSameWayTwice()
        {
            FishSchool school = SchoolAt(0f, 0f);
            double now = 100.0 + ShoalEventMath.DurationSeconds(ShoalEventKind.Dart) * 0.37;

            float[] xs = new float[2];
            float[] ys = new float[2];
            float[] strengths = new float[2];

            for (int run = 0; run < 2; run++)
            {
                var log = new GullSplashLog();
                log.RecordAt(SplashAt(-9f, 3f), 99.0);
                log.RecordAt(SplashAt(1.5f, -0.5f), 100.0);

                Assert.IsTrue(log.TryScatter(school, now, Reach, Strength,
                                             out strengths[run], out double start, out Vector2 from),
                              "run " + run + " did not scatter at all");
                ShoalEventMath.ScatterOffset(from.x, from.y, 0.4f, 0.9f, strengths[run], 3f, start, now,
                                             out xs[run], out ys[run]);
            }

            Assert.AreEqual(strengths[0], strengths[1], "the same splash hit with a different strength");
            Assert.AreEqual(xs[0], xs[1], "the same splash moved the same fish a different way");
            Assert.AreEqual(ys[0], ys[1], "the same splash moved the same fish a different way");
            Assert.AreNotEqual(0f, xs[0], "the fixture proved determinism on a fish that never moved");
        }

        /// <summary>
        /// <b>The roll behind the dive yield is a hash, not a die.</b> No <c>UnityEngine.Random</c>
        /// anywhere near the flock (rule 5): the same seed, place and moment give the same answer, a
        /// different moment gives a different one, and the two ends of the probability are absolute.
        /// </summary>
        [Test]
        public void TheCatchRollIsClosedForm()
        {
            float a = SeagullSplashMath.Roll01(7, 12.5, -3.25, 4321.0);
            float b = SeagullSplashMath.Roll01(7, 12.5, -3.25, 4321.0);
            float c = SeagullSplashMath.Roll01(7, 12.5, -3.25, 4322.0);
            float d = SeagullSplashMath.Roll01(8, 12.5, -3.25, 4321.0);

            Assert.AreEqual(a, b, "the same strike rolled two different answers");
            Assert.AreNotEqual(a, c, "a strike one millisecond later rolled the identical answer");
            Assert.AreNotEqual(a, d, "two flock seeds roll in lockstep");
            Assert.GreaterOrEqual(a, 0f);
            Assert.Less(a, 1f);

            Assert.IsFalse(SeagullSplashMath.RollsCatch(7, 1.0, 2.0, 3.0, 0f),
                           "a yield turned off still caught a fish");
            Assert.IsTrue(SeagullSplashMath.RollsCatch(7, 1.0, 2.0, 3.0, 1f),
                          "a yield of 1 let a fish go");
        }

        /// <summary>
        /// <b>The burst fires on the frame the rig names, and never past the end of the row.</b> The
        /// drop says frame 2 and <c>SeagullRigKitTests.TheWaterSectionNamesTheFrameTheSplashFiresOn</c>
        /// pins it; the clamp is for the day a shorter arrival is baked, where an unclamped burst frame
        /// would simply never arrive and the fish would quietly stop reacting.
        /// </summary>
        [Test]
        public void TheBurstFrameNeverFallsOffTheEndOfTheRow()
        {
            Assert.AreEqual(2, SeagullSplashMath.BurstFrame(2, 6), "the rig's own frame is honoured");
            Assert.AreEqual(3, SeagullSplashMath.BurstFrame(9, 4), "a burst past the end never arrives");
            Assert.AreEqual(0, SeagullSplashMath.BurstFrame(2, 1), "a one-frame arrival bursts on it");
            Assert.AreEqual(0, SeagullSplashMath.BurstFrame(-1, 6), "a negative frame bursts at the top");
        }
    }
}
