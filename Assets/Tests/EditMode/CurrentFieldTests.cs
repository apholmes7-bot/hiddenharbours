using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Environment;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The tidal-current BEARING must wander gently on its OWN timescale (distinct from the wind and
    /// the 12.42 h tide) while staying DETERMINISTIC (same seed+time → identical current, no hidden RNG),
    /// SMOOTH over time (no popping at the sample cadence), and centred on the prevailing channel axis
    /// (the wander is zero-mean, so over a long window the mean bearing ≈ the channel). Tests drive the
    /// pure <see cref="CurrentModel"/> directly — no GameConfig / MonoBehaviour needed. Modelled on
    /// <see cref="WindFieldTests"/>.
    /// </summary>
    public class CurrentFieldTests
    {
        // = SecondsPerDay(1200)/24. Passed explicitly so the field is testable without a GameConfig.
        private const double SecondsPerHour = 50.0;
        private static readonly Vector2 ChannelAxis = Vector2.right;   // EnvironmentService default
        private const float CurrentFactor = 25f;                       // EnvironmentService default
        private static CurrentProfile Cove => CurrentProfile.CoddleCove;

        // A non-zero tide rate so the current has a magnitude to point. Sign/scale are the tide's job;
        // these tests assert the BEARING behaviour, so any steady positive rate works.
        private const float FloodRate = 0.01f;

        [Test]
        public void Current_IsBitStable_ForIdenticalInputs()
        {
            // The sim contract: identical (seed, gameTime) → byte-identical current, every time.
            foreach (int seed in new[] { 1, 42, 12345, -7 })
            {
                for (int i = 0; i < 200; i++)
                {
                    double t = i * 37.0;
                    Vector2 a = CurrentModel.SampleCurrent(t, seed, SecondsPerHour, ChannelAxis, CurrentFactor, FloodRate, Cove);
                    Vector2 b = CurrentModel.SampleCurrent(t, seed, SecondsPerHour, ChannelAxis, CurrentFactor, FloodRate, Cove);
                    Assert.AreEqual(a, b, $"current not reproducible at t={t}, seed={seed}");
                }
            }
        }

        [Test]
        public void Current_DiffersBySeed()
        {
            int differing = 0;
            for (int i = 0; i < 60; i++)
            {
                double t = i * 3000.0;
                Vector2 a = CurrentModel.SampleCurrent(t, 1, SecondsPerHour, ChannelAxis, CurrentFactor, FloodRate, Cove);
                Vector2 b = CurrentModel.SampleCurrent(t, 2, SecondsPerHour, ChannelAxis, CurrentFactor, FloodRate, Cove);
                if ((a - b).sqrMagnitude > 1e-9f) differing++;
            }
            Assert.Greater(differing, 45, "two different world seeds should mostly produce a different current bearing");
        }

        [Test]
        public void Current_WanderStaysWithinAmplitude()
        {
            // The bearing must never lean further off the channel than the tunable amplitude — the
            // guarantee that keeps sailing predictable (the current drives boat drift).
            float cap = Cove.WanderDeg * Mathf.Deg2Rad;
            foreach (int seed in new[] { 1, 7, 42, 12345, -9999 })
            {
                for (int i = 0; i < 6000; i++)
                {
                    double t = i * (SecondsPerHour * 0.05);
                    float ang = CurrentModel.WanderAngleRad(t, seed, SecondsPerHour, Cove);
                    Assert.IsFalse(float.IsNaN(ang) || float.IsInfinity(ang), "wander angle must be finite");
                    Assert.LessOrEqual(Mathf.Abs(ang), cap + 1e-4f,
                        $"current wandered {ang * Mathf.Rad2Deg:0.00}° beyond the ±{Cove.WanderDeg}° cap at t={t}, seed={seed}");
                }
            }
        }

        [Test]
        public void Current_IsSmooth_NoPopping()
        {
            // Step finely relative to the drift channel: adjacent bearing change must be tiny, yet over
            // the sweep the bearing must genuinely move (proving it's not just constant).
            double step = (Cove.DriftHours / 200.0) * SecondsPerHour;
            const int n = 8000;
            float prev = CurrentModel.WanderAngleRad(0.0, 7, SecondsPerHour, Cove);
            float maxDelta = 0f, lo = float.MaxValue, hi = float.MinValue;
            for (int i = 1; i <= n; i++)
            {
                float ang = CurrentModel.WanderAngleRad(i * step, 7, SecondsPerHour, Cove);
                maxDelta = Mathf.Max(maxDelta, Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, prev * Mathf.Rad2Deg)));
                lo = Mathf.Min(lo, ang);
                hi = Mathf.Max(hi, ang);
                prev = ang;
            }
            Assert.Less(maxDelta, 0.5f, $"current bearing popped: max adjacent step was {maxDelta:0.000}°");
            Assert.Greater((hi - lo) * Mathf.Rad2Deg, 5f, "current bearing barely moved over the sweep — not a living set");
        }

        [Test]
        public void Current_MeanBearing_StaysNearChannelAxis()
        {
            // The wander is zero-mean noise, so over a long window the average current vector points
            // along the channel axis — the set still "runs up the narrows", it just breathes around it.
            Vector2 sum = Vector2.zero;
            const int n = 20000;
            double step = (Cove.DriftHours * SecondsPerHour) * 0.05;   // many drift-channel cells
            for (int i = 0; i < n; i++)
                sum += CurrentModel.SampleCurrent(i * step, 3, SecondsPerHour, ChannelAxis, CurrentFactor, FloodRate, Cove);

            Vector2 mean = sum / n;
            Assert.Greater(mean.magnitude, 1e-4f, "no net current — the mean set vanished");
            float channelDeg = Mathf.Atan2(ChannelAxis.y, ChannelAxis.x) * Mathf.Rad2Deg;
            float ang = Mathf.Atan2(mean.y, mean.x) * Mathf.Rad2Deg;
            float diff = Mathf.DeltaAngle(ang, channelDeg);
            Assert.Less(Mathf.Abs(diff), 8f,
                $"mean current {ang:0}° drifted off the channel axis {channelDeg:0}°");
        }

        [Test]
        public void Current_DriftIsSlowerThanWind_OwnTimescale()
        {
            // The current must wander on its OWN, slower rate — its drift timescale exceeds the wind's
            // slow channel, so the set and the wind don't move in lockstep (P1: two independent moods).
            Assert.Greater(Cove.DriftHours, WindProfile.CoddleCove.ChangeHours,
                "current DriftHours should be slower than the wind's ChangeHours (its own, gentler rate)");
        }

        [Test]
        public void Current_FlipsSignWithTideRate()
        {
            // Flood vs ebb: the SAME bearing, opposite sense. At a given time the ebb current is the exact
            // negation of the flood current (the tide rate's sign is what flips it).
            const double t = 4321.0;
            Vector2 flood = CurrentModel.SampleCurrent(t, 5, SecondsPerHour, ChannelAxis, CurrentFactor, +FloodRate, Cove);
            Vector2 ebb   = CurrentModel.SampleCurrent(t, 5, SecondsPerHour, ChannelAxis, CurrentFactor, -FloodRate, Cove);
            Assert.AreEqual(flood, -ebb, "ebb should be the negation of flood at the same instant");
        }

        [Test]
        public void DefaultProfile_HealsToCoddleCove()
        {
            // A zero/unset CurrentProfile (e.g. a freshly-added serialized field) must not produce a
            // dead/NaN wander — it heals to the cove default rather than dividing by a zero DriftHours.
            var got = CurrentModel.SampleCurrent(1234.0, 5, SecondsPerHour, ChannelAxis, CurrentFactor, FloodRate, default);
            var expected = CurrentModel.SampleCurrent(1234.0, 5, SecondsPerHour, ChannelAxis, CurrentFactor, FloodRate, CurrentProfile.CoddleCove);
            Assert.AreEqual(expected, got);
        }
    }
}
