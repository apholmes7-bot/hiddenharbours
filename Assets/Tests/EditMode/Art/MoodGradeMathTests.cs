using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>The grade's blend is a pure partition of the day, and the weather lays over it</b> (juice
    /// charter PR 1). These drive <see cref="MoodGradeMath"/> with synthetic hours and weathers and
    /// assert the law, not the look: the three time-of-day weights always sum to 1 and are never
    /// negative; night is total at 02:00, day at noon, golden hour ON the horizon crossings; fog and
    /// storm climb linearly between their two <c>GameConfig.Juice</c> thresholds and are clamped
    /// outside; a pure weight returns the authored key untouched; a region lays its offsets over the
    /// blend; and the result is the same from any starting state, in any order (rule 5 — no
    /// accumulator).
    /// </summary>
    public class MoodGradeMathTests
    {
        const float Sunrise = 6f, Sunset = 20f;
        static JuiceSettings J => JuiceSettings.Default;

        static void Partition(float hour, out float d, out float g, out float n) =>
            MoodGradeMath.TimeOfDay(hour, Sunrise, Sunset, J.GradeGoldenHourWidthHours, J.GradeNightBlendHours, out d, out g, out n);

        [Test]
        public void TimeOfDay_partition_sums_to_one_and_is_non_negative_at_every_quarter_hour()
        {
            for (float h = 0f; h < 24f; h += 0.25f)
            {
                Partition(h, out float d, out float g, out float n);
                Assert.That(d, Is.GreaterThanOrEqualTo(0f), $"day at {h:F2}");
                Assert.That(g, Is.GreaterThanOrEqualTo(0f), $"golden at {h:F2}");
                Assert.That(n, Is.GreaterThanOrEqualTo(0f), $"night at {h:F2}");
                Assert.That(d + g + n, Is.EqualTo(1f).Within(1e-5f), $"sum at {h:F2}");
            }
        }

        [Test]
        public void TimeOfDay_is_all_night_in_the_small_hours_and_all_day_at_noon()
        {
            Partition(2f, out float d, out float g, out float n);
            Assert.That(n, Is.EqualTo(1f).Within(1e-6f)); Assert.That(d, Is.EqualTo(0f).Within(1e-6f)); Assert.That(g, Is.EqualTo(0f).Within(1e-6f));

            Partition(12f, out d, out g, out n);
            Assert.That(d, Is.EqualTo(1f).Within(1e-6f)); Assert.That(n, Is.EqualTo(0f).Within(1e-6f)); Assert.That(g, Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void TimeOfDay_golden_hour_peaks_on_the_horizon_crossings_and_fades_over_its_width()
        {
            Partition(Sunset, out _, out float gSet, out _);
            Partition(Sunrise, out _, out float gRise, out _);
            Assert.That(gSet, Is.EqualTo(1f).Within(1e-6f), "sunset");
            Assert.That(gRise, Is.EqualTo(1f).Within(1e-6f), "sunrise");

            // One hour before sunset, still daylight: golden = 1 - 1/1.5, day the rest.
            Partition(Sunset - 1f, out float d, out float g, out float n);
            Assert.That(n, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(g, Is.EqualTo(1f - 1f / J.GradeGoldenHourWidthHours).Within(1e-5f));
            Assert.That(d, Is.EqualTo(1f - g).Within(1e-5f));

            // Beyond the width the golden hour is gone.
            Partition(Sunset - J.GradeGoldenHourWidthHours - 0.01f, out _, out g, out _);
            Assert.That(g, Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void TimeOfDay_night_fades_in_over_the_blend_hours_after_sunset_and_out_before_sunrise()
        {
            Partition(Sunset + 0.5f * J.GradeNightBlendHours, out _, out _, out float n);
            Assert.That(n, Is.EqualTo(0.5f).Within(1e-5f), "half a blend after sunset");
            Partition(Sunrise - 0.5f * J.GradeNightBlendHours, out _, out _, out n);
            Assert.That(n, Is.EqualTo(0.5f).Within(1e-5f), "half a blend before sunrise");
            Partition(Sunset + J.GradeNightBlendHours, out _, out _, out n);
            Assert.That(n, Is.EqualTo(1f).Within(1e-5f), "a full blend after sunset");
        }

        [Test]
        public void TimeOfDay_zero_width_is_no_golden_hour_and_zero_blend_is_a_hard_night_step()
        {
            MoodGradeMath.TimeOfDay(Sunset, Sunrise, Sunset, 0f, J.GradeNightBlendHours, out _, out float g, out _);
            Assert.That(g, Is.EqualTo(0f));
            MoodGradeMath.TimeOfDay(Sunset + 0.01f, Sunrise, Sunset, J.GradeGoldenHourWidthHours, 0f, out float d, out g, out float n);
            Assert.That(n, Is.EqualTo(1f)); Assert.That(d, Is.EqualTo(0f)); Assert.That(g, Is.EqualTo(0f));
        }

        [Test]
        public void TimeOfDay_wraps_hours_outside_the_clock()
        {
            Partition(26f, out float d1, out float g1, out float n1);
            Partition(2f, out float d2, out float g2, out float n2);
            Assert.That(d1, Is.EqualTo(d2).Within(1e-6f)); Assert.That(g1, Is.EqualTo(g2).Within(1e-6f)); Assert.That(n1, Is.EqualTo(n2).Within(1e-6f));
        }

        [Test]
        public void FogWeight_is_zero_when_clear_one_when_thick_and_linear_between()
        {
            float s = J.GradeFogVisibilityStart, f = J.GradeFogVisibilityFull;
            Assert.That(MoodGradeMath.FogWeight(1f, s, f), Is.EqualTo(0f));
            Assert.That(MoodGradeMath.FogWeight(s, s, f), Is.EqualTo(0f));
            Assert.That(MoodGradeMath.FogWeight(f, s, f), Is.EqualTo(1f));
            Assert.That(MoodGradeMath.FogWeight(0f, s, f), Is.EqualTo(1f));
            Assert.That(MoodGradeMath.FogWeight(0.5f * (s + f), s, f), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void FogWeight_survives_thresholds_authored_backwards()
        {
            // start ≤ full: a hard step, never a divide-by-zero or a negative weight.
            Assert.That(MoodGradeMath.FogWeight(0.5f, 0.2f, 0.4f), Is.EqualTo(0f));
            Assert.That(MoodGradeMath.FogWeight(0.1f, 0.2f, 0.4f), Is.EqualTo(1f));
            Assert.That(MoodGradeMath.FogWeight(0.3f, 0.3f, 0.3f), Is.EqualTo(0f));
        }

        [Test]
        public void StormWeight_is_zero_when_calm_one_when_wild_and_linear_between()
        {
            float s = J.GradeStormSeaStateStart, f = J.GradeStormSeaStateFull;
            Assert.That(MoodGradeMath.StormWeight(0f, s, f), Is.EqualTo(0f));
            Assert.That(MoodGradeMath.StormWeight(s, s, f), Is.EqualTo(0f));
            Assert.That(MoodGradeMath.StormWeight(f, s, f), Is.EqualTo(1f));
            Assert.That(MoodGradeMath.StormWeight(1f, s, f), Is.EqualTo(1f));
            Assert.That(MoodGradeMath.StormWeight(0.5f * (s + f), s, f), Is.EqualTo(0.5f).Within(1e-5f));
            // full ≤ start: a hard step AT START (the same law as FogWeight): below it calm, at/above it wild.
            Assert.That(MoodGradeMath.StormWeight(0.7f, 0.8f, 0.6f), Is.EqualTo(0f), "backwards thresholds = step at start: below start is calm");
            Assert.That(MoodGradeMath.StormWeight(0.85f, 0.8f, 0.6f), Is.EqualTo(1f), "backwards thresholds = step at start: at/above start is wild");
            Assert.That(MoodGradeMath.StormWeight(0.8f, 0.8f, 0.8f), Is.EqualTo(0f), "start == full: the step, no divide-by-zero");
        }

        static void AssertGradeEqual(in MoodGrade a, in MoodGrade b, string why, float eps = 1e-5f)
        {
            Assert.That(b.BloomIntensity, Is.EqualTo(a.BloomIntensity).Within(eps), why + " bloom");
            Assert.That(b.BloomThreshold, Is.EqualTo(a.BloomThreshold).Within(eps), why + " threshold");
            Assert.That(b.Lift, Is.EqualTo(a.Lift), why + " lift");
            Assert.That(b.Gamma, Is.EqualTo(a.Gamma), why + " gamma");
            Assert.That(b.Gain, Is.EqualTo(a.Gain), why + " gain");
            Assert.That(b.Contrast, Is.EqualTo(a.Contrast).Within(eps), why + " contrast");
            Assert.That(b.Saturation, Is.EqualTo(a.Saturation).Within(eps), why + " saturation");
            Assert.That(b.ColorFilter, Is.EqualTo(a.ColorFilter), why + " filter");
            Assert.That(b.VignetteIntensity, Is.EqualTo(a.VignetteIntensity).Within(eps), why + " vignette");
            Assert.That(b.VignetteColor, Is.EqualTo(a.VignetteColor), why + " vignette colour");
            Assert.That(b.GrainIntensity, Is.EqualTo(a.GrainIntensity).Within(eps), why + " grain");
            Assert.That(b.ChromaticAberration, Is.EqualTo(a.ChromaticAberration).Within(eps), why + " chroma");
        }

        [Test]
        public void Blend_at_a_pure_weight_returns_that_authored_key_untouched()
        {
            var p = MoodGradeProfile.CreateDefault();
            try
            {
                AssertGradeEqual(p.Day,        MoodGradeMath.Blend(p, new MoodWeights(1, 0, 0, 0, 0)), "pure day");
                AssertGradeEqual(p.GoldenHour, MoodGradeMath.Blend(p, new MoodWeights(0, 1, 0, 0, 0)), "pure golden");
                AssertGradeEqual(p.Night,      MoodGradeMath.Blend(p, new MoodWeights(0, 0, 1, 0, 0)), "pure night");
                AssertGradeEqual(p.Fog,        MoodGradeMath.Blend(p, new MoodWeights(1, 0, 0, 1, 0)), "full fog over day");
                AssertGradeEqual(p.Storm,      MoodGradeMath.Blend(p, new MoodWeights(0, 0, 1, 1, 1)), "full storm over everything");
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void Blend_at_half_fog_over_noon_is_the_midpoint_of_day_and_fog()
        {
            var p = MoodGradeProfile.CreateDefault();
            try
            {
                var g = MoodGradeMath.Blend(p, new MoodWeights(1, 0, 0, 0.5f, 0));
                Assert.That(g.Saturation, Is.EqualTo(0.5f * (p.Day.Saturation + p.Fog.Saturation)).Within(1e-4f));
                Assert.That(g.VignetteIntensity, Is.EqualTo(0.5f * (p.Day.VignetteIntensity + p.Fog.VignetteIntensity)).Within(1e-4f));
                Assert.That(g.Lift.w, Is.EqualTo(0.5f * (p.Day.Lift.w + p.Fog.Lift.w)).Within(1e-4f));
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void Mix3_normalises_its_weights_and_returns_the_first_key_when_all_are_zero()
        {
            var a = MoodGradeProfile.DefaultDay(); var b = MoodGradeProfile.DefaultNight(); var c = MoodGradeProfile.DefaultFog();
            AssertGradeEqual(a, MoodGrade.Mix3(a, 2f, b, 0f, c, 0f), "weight 2 on one key is that key");
            AssertGradeEqual(a, MoodGrade.Mix3(a, 0f, b, 0f, c, 0f), "all-zero weights");
            var m = MoodGrade.Mix3(a, 1f, b, 1f, c, 0f);
            Assert.That(m.Saturation, Is.EqualTo(0.5f * (a.Saturation + b.Saturation)).Within(1e-4f));
        }

        [Test]
        public void ApplyRegion_lays_the_offsets_over_the_blend_and_null_is_the_identity()
        {
            var g = MoodGradeProfile.DefaultDay();
            AssertGradeEqual(g, MoodGradeMath.ApplyRegion(g, null), "null region");

            var r = ScriptableObject.CreateInstance<MoodGradeRegionOverride>();
            try
            {
                r.Set("region.test", new Color(1f, 0.9f, 0.8f, 1f), 10f, -5f, 0.25f, 0.1f, 0.2f);
                var o = MoodGradeMath.ApplyRegion(g, r);
                Assert.That(o.Saturation, Is.EqualTo(g.Saturation + 10f).Within(1e-5f));
                Assert.That(o.Contrast, Is.EqualTo(g.Contrast - 5f).Within(1e-5f));
                Assert.That(o.PostExposure, Is.EqualTo(g.PostExposure + 0.25f).Within(1e-5f));
                Assert.That(o.VignetteIntensity, Is.EqualTo(g.VignetteIntensity + 0.1f).Within(1e-5f));
                Assert.That(o.BloomIntensity, Is.EqualTo(g.BloomIntensity + 0.2f).Within(1e-5f));
                Assert.That(o.ColorFilter.g, Is.EqualTo(g.ColorFilter.g * 0.9f).Within(1e-5f));
                Assert.That(o.ColorFilter.a, Is.EqualTo(1f));
                Assert.That(o.Lift, Is.EqualTo(g.Lift), "a region does not touch the trackballs");
            }
            finally { Object.DestroyImmediate(r); }
        }

        [Test]
        public void Clamped_keeps_every_field_inside_the_range_URP_accepts()
        {
            var g = MoodGrade.Neutral;
            g.Saturation = 150f; g.Contrast = -150f; g.VignetteIntensity = 1.5f; g.BloomIntensity = -1f;
            g.VignetteSmoothness = 0f; g.ChromaticAberration = 2f; g.ColorFilter = new Color(-1f, 2f, 0.5f, 0.3f);
            var c = g.Clamped();
            Assert.That(c.Saturation, Is.EqualTo(100f));
            Assert.That(c.Contrast, Is.EqualTo(-100f));
            Assert.That(c.VignetteIntensity, Is.EqualTo(1f));
            Assert.That(c.BloomIntensity, Is.EqualTo(0f));
            Assert.That(c.VignetteSmoothness, Is.EqualTo(0.01f));
            Assert.That(c.ChromaticAberration, Is.EqualTo(1f));
            Assert.That(c.ColorFilter.r, Is.EqualTo(0f));
            Assert.That(c.ColorFilter.a, Is.EqualTo(1f));
        }

        [Test]
        public void Evaluate_is_pure_the_same_facts_give_the_same_grade_in_any_order()
        {
            var p = MoodGradeProfile.CreateDefault();
            try
            {
                var j = J;
                var a = MoodGradeMath.Evaluate(p, null, 19.5f, Sunrise, Sunset, 0.3f, 0.7f, j, out var wa);
                MoodGradeMath.Evaluate(p, null, 3f, Sunrise, Sunset, 1f, 0f, j, out _);      // something else in between
                MoodGradeMath.Evaluate(p, null, 12f, Sunrise, Sunset, 0.05f, 1f, j, out _);
                var b = MoodGradeMath.Evaluate(p, null, 19.5f, Sunrise, Sunset, 0.3f, 0.7f, j, out var wb);
                AssertGradeEqual(a, b, "same facts, different history");
                Assert.That(wb.Fog, Is.EqualTo(wa.Fog)); Assert.That(wb.Storm, Is.EqualTo(wa.Storm));
                Assert.That(wb.GoldenHour, Is.EqualTo(wa.GoldenHour));
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void Evaluate_at_two_in_the_morning_clear_and_calm_is_the_night_key()
        {
            var p = MoodGradeProfile.CreateDefault();
            try
            {
                var g = MoodGradeMath.Evaluate(p, null, 2f, Sunrise, Sunset, 1f, 0f, J, out var w);
                Assert.That(w.Night, Is.EqualTo(1f).Within(1e-6f));
                Assert.That(w.Fog, Is.EqualTo(0f)); Assert.That(w.Storm, Is.EqualTo(0f));
                AssertGradeEqual(p.Night.Clamped(), g, "night");
                Assert.That(g.Lift.z, Is.GreaterThan(g.Lift.x), "night shadows are cold (bible §4.2)");
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void Evaluate_at_sunset_is_the_golden_key_and_its_gain_is_warm()
        {
            var p = MoodGradeProfile.CreateDefault();
            try
            {
                var g = MoodGradeMath.Evaluate(p, null, Sunset, Sunrise, Sunset, 1f, 0f, J, out var w);
                Assert.That(w.GoldenHour, Is.EqualTo(1f).Within(1e-6f));
                Assert.That(g.Gain.x, Is.GreaterThan(g.Gain.z), "golden-hour highlights are warm (bible §4.2)");
                Assert.That(g.Lift.x, Is.GreaterThan(g.Lift.z), "golden-hour lift is warm");
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void Evaluate_in_thick_fog_crushes_saturation_and_closes_the_vignette_relative_to_the_clear_day()
        {
            var p = MoodGradeProfile.CreateDefault();
            try
            {
                var clear = MoodGradeMath.Evaluate(p, null, 12f, Sunrise, Sunset, 1f, 0f, J, out _);
                var fog   = MoodGradeMath.Evaluate(p, null, 12f, Sunrise, Sunset, 0.05f, 0f, J, out var w);
                Assert.That(w.Fog, Is.EqualTo(1f));
                Assert.That(fog.Saturation, Is.LessThan(clear.Saturation));
                Assert.That(fog.VignetteIntensity, Is.GreaterThan(clear.VignetteIntensity));
                Assert.That(fog.Lift.w, Is.GreaterThan(clear.Lift.w), "fog lifts the black point");
                Assert.That(fog.Contrast, Is.LessThan(clear.Contrast), "fog flattens");
            }
            finally { Object.DestroyImmediate(p); }
        }
    }
}
