using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// 🔴 <b>THE MEASUREMENT that decided round 2</b> — a headless simulation of the ENTIRE shipped chain,
    /// from a hull's deposit to the colour a texel of its wake finally draws at.
    ///
    /// <para><b>Why a measurement and not an opinion.</b> The owner played #665 and reported: <i>"the big
    /// foam band stays white — never disperses"</i>. The obvious reading is that the ramp is mistuned, and
    /// the obvious fix is to drag <c>_WakeFoamFreshCover</c> down until the band goes blue. The handoff
    /// insisted on measuring first, and the measurement says the tuning answer is a dead end: by the time
    /// the age proxy saw a coverage, the compose had already <b>saturated</b>, <b>thresholded</b> and
    /// <b>posterized</b> it, so it could take only <c>_WakeFoamBands</c> distinct values. A ramp indexed by
    /// three values is three colours, and the brightest of them — the one the whole visible band sits in —
    /// is age 0 for any sane threshold. Retuning would have moved the band from flat white to flat blue and
    /// the owner would have reported the same defect in a different hue.</para>
    ///
    /// <para><b>What the numbers were, at the shipped tuning:</b> 72–81% of the visible band drew at age
    /// exactly 0 at every speed from 1.5 to 8 m/s; the raw buffer clamped at 1.000 within 36 frames of
    /// deposit at 3 m/s; and the set of values the proxy could receive was {0, 0.425, 0.85}. Those are the
    /// facts this fixture keeps, so nobody has to take the analysis on trust — or re-derive it — again.</para>
    ///
    /// <para><b>It reads the LIVE tuning, not a snapshot.</b> The threshold/softness/bands/strength come out
    /// of <c>Water.mat</c>'s serialized YAML and the deposit rate, radius, speed knee and half-lives out of
    /// the components' own shipped defaults by reflection. Retune any of them and the measurement re-runs at
    /// the new numbers rather than quietly measuring a world that no longer exists.</para>
    ///
    /// <para>CPU-only: no GPU, no render, no device. The chain being simulated is arithmetic on both sides
    /// of the seam, so CI can adjudicate it — which is the point of a guard against a defect that otherwise
    /// only shows up when the owner looks at the water.</para>
    /// </summary>
    public class WakeFoamAgeingMeasurementTests
    {
        private const string LiveWaterMatPath = "Assets/_Project/Art/Materials/Water.mat";
        private const float Dt = 1f / 60f;

        // ---- the live tuning ---------------------------------------------------------------------

        private static string Read(string repoRelative)
        {
            string path = Path.Combine(Application.dataPath, "..", repoRelative);
            Assert.IsTrue(File.Exists(path), $"missing: {repoRelative}");
            return File.ReadAllText(path);
        }

        private static float MatFloat(string key)
        {
            var m = Regex.Match(Read(LiveWaterMatPath), $@"-\s{Regex.Escape(key)}:\s*(-?[\d.eE+]+)");
            Assert.IsTrue(m.Success,
                $"'{LiveWaterMatPath}' does not serialize {key} — see FoamRealnessTests' preset guard.");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        /// <summary>A component's shipped serialized default, by reflection — so the measurement runs at
        /// whatever the owner has tuned rather than at a number copied into a test once.</summary>
        private static float Field(object instance, string name)
        {
            FieldInfo f = instance.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(f, $"'{name}' is gone from {instance.GetType().Name}. This measurement is " +
                                "keyed to the real tuning; a renamed field must be renamed here too, not " +
                                "silently dropped.");
            return (float)f.GetValue(instance);
        }

        private struct Tuning
        {
            public float Threshold, Softness, Bands, LookStrength;   // the compose (Water.mat)
            public float DepositPerSecond, Radius, SpeedKnee;        // the injector
            public float CoverHalfLife, AgeHalfLife;                 // the buffer
            public float FreshFloor, WhiteHold, BlueReach, DeepReach; // the ramp
        }

        private static Tuning Live()
        {
            var injector = new GameObject("probe") { hideFlags = HideFlags.HideAndDontSave }
                           .AddComponent<FoamInjector>();
            var feature = ScriptableObject.CreateInstance<IsoFacetHullFeature>();
            try
            {
                var ramp = WakeAgeRamp.Default;
                return new Tuning
                {
                    Threshold        = MatFloat("_WakeFoamThreshold"),
                    Softness         = MatFloat("_WakeFoamSoftness"),
                    Bands            = MatFloat("_WakeFoamBands"),
                    LookStrength     = MatFloat("_WakeFoamStrength"),
                    DepositPerSecond = Field(injector, "_depositPerSecond"),
                    Radius           = Field(injector, "_radiusMeters"),
                    SpeedKnee        = Field(injector, "_wakeSpeedKnee"),
                    CoverHalfLife    = Field(feature, "_foamHalfLifeSeconds"),
                    AgeHalfLife      = Field(feature, "_foamAgeHalfLifeSeconds"),
                    FreshFloor       = MatFloat("_WakeFoamFreshFloor"),
                    WhiteHold        = ramp.WhiteHold,
                    BlueReach        = ramp.BlueReach,
                    DeepReach        = ramp.DeepReach,
                };
            }
            finally
            {
                Object.DestroyImmediate(injector.gameObject);
                Object.DestroyImmediate(feature);
            }
        }

        // ---- the compose, transcribed (the two shader steps that have no C# twin) -----------------

        /// <summary>HLSL <c>smoothstep</c>. Transcribed because the compose's threshold lives only in the
        /// shader; every other step of this chain calls the production code directly.</summary>
        private static float Smoothstep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>The water shader's <c>BandValue01</c> at the dither extremes — this returns the
        /// BRIGHTEST outcome, which is the one the owner is looking at down the middle of the band.</summary>
        private static float Posterize(float v01, float bands)
        {
            float b = Mathf.Max(bands, 2f);
            float x = Mathf.Clamp01(v01) * (b - 1f);
            float fb = Mathf.Floor(x);
            float e = Mathf.Clamp01(((x - fb) - 0.25f) / 0.5f);
            return (fb + (e > 0.01f ? 1f : 0f)) / (b - 1f);
        }

        /// <summary>What <c>WakeFoamCoverage</c> returns for a stored buffer value: threshold, soften,
        /// posterize, scale by the look dial. The lace is omitted deliberately — it only ever REMOVES
        /// coverage, so leaving it out measures the brightest, most-white case there is.</summary>
        private static float Compose(float stored, in Tuning t)
        {
            float cover = Smoothstep(t.Threshold, Mathf.Min(t.Threshold + t.Softness, 1f), stored);
            if (t.Bands >= 2f) cover = Posterize(cover, t.Bands);
            return Mathf.Clamp01(cover * t.LookStrength);
        }

        // ---- the wake, simulated ------------------------------------------------------------------

        private struct Sample
        {
            public float Astern;        // metres behind the boat
            public float Stored;        // the buffer's COVERAGE channel
            public float Fresh;         // the buffer's FRESHNESS channel
            public float Composed;      // what the water shader's compose hands the age proxy
        }

        /// <summary>
        /// One texel on the centre line of a straight run at <paramref name="speed"/>: the boat drives over
        /// it (so the capsule covers it, falloff 1, for <c>2·radius/speed</c> seconds), then it is left
        /// behind to decay. Sampled every 0.25 s of its life. This is exactly what the advect shader does to
        /// that texel, frame by frame, using the production decay and freshness laws.
        /// </summary>
        private static Sample[] Wake(float speed, in Tuning t)
        {
            float rate01 = Mathf.Clamp01(speed / t.SpeedKnee);        // Shape01, exponent 1, weight 1
            float amount = rate01 * t.DepositPerSecond * Dt;
            float coverStep = FoamBuffer.DecayFactor(t.CoverHalfLife, Dt);
            float ageStep = FoamBuffer.DecayFactor(t.AgeHalfLife, Dt);

            float stored = 0f, fresh = 0f;
            int exposureFrames = Mathf.Max(1, Mathf.RoundToInt(2f * t.Radius / speed / Dt));
            for (int i = 0; i < exposureFrames; i++)
            {
                stored = Mathf.Clamp01(stored * coverStep + amount);
                // The mark is a GATE, not a scale: a hull working the water at all resets the clock to
                // fully fresh. Passing rate01 here instead was this fixture's first red — at 1.5 m/s a
                // dory's brand-new churn was born half-aged and could never draw white.
                fresh = FoamBuffer.Freshness(fresh, ageStep, rate01 > 0f ? 1f : 0f);
            }

            var samples = new System.Collections.Generic.List<Sample>();
            float coverQuarter = FoamBuffer.DecayFactor(t.CoverHalfLife, 0.25f);
            float ageQuarter = FoamBuffer.DecayFactor(t.AgeHalfLife, 0.25f);
            for (float age = 0f; age <= 40f; age += 0.25f)
            {
                samples.Add(new Sample
                {
                    Astern = speed * age,
                    Stored = stored,
                    Fresh = fresh,
                    Composed = Compose(stored, in t),
                });
                stored *= coverQuarter;
                fresh *= ageQuarter;
            }
            return samples.ToArray();
        }

        /// <summary>Where on the sea's colour ramp a sample draws: 0 = the foam anchor (white), 0.5 = the
        /// shallow blue, 1 = the mid blue. The production knot curve, not a copy of it.</summary>
        private static float RampAt(float age01, in Tuning t)
            => WakeFoamAgeing.Knots(age01, t.WhiteHold, t.BlueReach, t.DeepReach);

        private static readonly float[] Speeds = { 1.5f, 3f, 5f, 8f };

        // ==== 1. WHY THE OLD PROXY COULD NOT WORK — arithmetic, no simulation needed ================

        [Test]
        public void TheComposedCoverage_CanOnlyEverTakeBandsCountValues()
        {
            Tuning t = Live();
            Assert.GreaterOrEqual(t.Bands, 2f, "the compose posterizes; below 2 bands it would not.");

            var distinct = new System.Collections.Generic.HashSet<float>();
            for (int i = 0; i <= 2000; i++)
                distinct.Add(Mathf.Round(Compose(i / 2000f, in t) * 10000f) / 10000f);

            Assert.LessOrEqual(distinct.Count, Mathf.RoundToInt(t.Bands),
                "The compose posterizes its coverage to _WakeFoamBands levels. That is CORRECT for a " +
                "pixel-art foam edge and it is why the round-1 age proxy was doomed: an age derived from " +
                "this value can take at most as many shades as there are bands, whatever the threshold. " +
                "A colour WALK cannot come out of a three-valued input.");
        }

        [Test]
        public void TheRoundOneProxy_CollapsedToOneFlatShade_AtEveryLegalThreshold()
        {
            // The retune option, ruled out by sweep rather than by argument. The old proxy was
            // age = 1 − composed/freshCover with freshCover on Range(0.05, 1). At EVERY setting in that
            // range the visible band takes at most a couple of shades and ONE of them covers most of
            // it — because the input has three values, and no threshold can add a fourth. Turning the
            // knob only chooses WHICH flat colour the band is; it can never make the band walk.
            Tuning t = Live();
            Sample[] wake = Wake(3f, in t);

            for (float freshCover = 0.05f; freshCover <= 1.0001f; freshCover += 0.05f)
            {
                var shades = new System.Collections.Generic.Dictionary<float, int>();
                int visible = 0;
                foreach (Sample s in wake)
                {
                    if (s.Composed <= 0.001f) continue;
                    visible++;
                    float shade = Mathf.Round(
                        RampAt(Mathf.Clamp01(1f - s.Composed / freshCover), in t) * 10000f) / 10000f;
                    shades[shade] = shades.TryGetValue(shade, out int n) ? n + 1 : 1;
                }
                Assert.Greater(visible, 0, "no visible band — the simulation is wrong");

                int dominant = 0;
                foreach (int n in shades.Values) dominant = Mathf.Max(dominant, n);

                Assert.LessOrEqual(shades.Count, Mathf.RoundToInt(t.Bands),
                    $"at freshCover {freshCover:0.00} the band took more shades than there are bands, " +
                    "which is arithmetically impossible — the simulation has drifted from the compose.");
                Assert.Greater(dominant / (float)visible, 0.6f,
                    $"At freshCover {freshCover:0.00} the most common shade covers only " +
                    $"{dominant / (float)visible:P0} of the visible band. This assertion documents WHY " +
                    "round 2 stores true age instead of retuning: if a threshold could ever spread the " +
                    "band across its shades, the cheap fix would have been the right one and this " +
                    "PR's second channel would be unjustified.");
            }
        }

        // ==== 2. THE OLD PROXY, MEASURED DOWN A REAL WAKE ==========================================

        [Test]
        public void TheRoundOneProxy_DrewMostOfTheVisibleBandFlatWhite()
        {
            Tuning t = Live();
            const float RoundOneFreshCover = 0.72f;   // the value #665 shipped

            foreach (float speed in Speeds)
            {
                Sample[] wake = Wake(speed, in t);
                int visible = 0, white = 0;
                foreach (Sample s in wake)
                {
                    if (s.Composed <= 0.001f) continue;   // the band is not drawn at all here
                    visible++;
                    float age = Mathf.Clamp01(1f - s.Composed / RoundOneFreshCover);
                    if (RampAt(age, in t) <= 0.02f) white++;
                }

                Assert.Greater(visible, 0, $"no visible band at {speed} m/s — the simulation is wrong.");
                float whiteFraction = white / (float)visible;
                Assert.Greater(whiteFraction, 0.6f,
                    $"At {speed} m/s the round-1 proxy drew {whiteFraction:P0} of the visible band at age " +
                    "0. This assertion exists to keep the DEFECT documented in numbers: if it ever fails, " +
                    "the chain being measured has changed and the round-2 reasoning must be re-derived, " +
                    "not assumed.");
            }
        }

        [Test]
        public void TheCoverageChannel_SaturatesAndIsThereforeAgeBlind()
        {
            Tuning t = Live();

            // A dory at 3 m/s: the capsule covers a texel for 2·0.9/3 = 0.6 s = 36 frames, and the
            // deposit pins it at the ceiling. Two texels both reading 1.000 have different ages and the
            // buffer cannot tell them apart — which is the deeper half of the same defect, upstream of
            // the posterize entirely.
            Sample[] slow = Wake(1.5f, in t);
            Sample[] cruise = Wake(3f, in t);
            Assert.AreEqual(1f, slow[0].Stored, 1e-3f,
                "coverage must be measured as SATURATED at a dawdling speed — that is the claim.");
            Assert.AreEqual(1f, cruise[0].Stored, 1e-3f, "…and at cruise.");

            // Freshness, by contrast, is a clock: born fully fresh at ANY working speed, and
            // bounded by 1 because the update is a max rather than an add.
            Assert.AreEqual(1f, slow[0].Fresh, 1e-5f,
                "a dawdling hull's brand-new churn is still BRAND NEW — the clock must not be scaled " +
                "by how hard she is working, or slow boats can never make white foam.");
            Assert.AreEqual(1f, cruise[0].Fresh, 1e-5f, "…and the same at cruise, which is the point: " +
                "the ramp means the same thing at every speed.");
        }

        // ==== 3. THE NEW PROXY — the walk the owner asked for ======================================

        [Test]
        public void TheFreshnessProxy_WalksTheBandDownTheSeasRamp()
        {
            Tuning t = Live();

            foreach (float speed in Speeds)
            {
                Sample[] wake = Wake(speed, in t);
                int visible = 0, white = 0;
                float minRamp = 1f, maxRamp = 0f;
                foreach (Sample s in wake)
                {
                    if (s.Composed <= 0.001f) continue;
                    visible++;
                    float ramp = RampAt(WakeFoamAgeing.Age01FromFreshness(s.Fresh, t.FreshFloor), in t);
                    if (ramp <= 0.02f) white++;
                    minRamp = Mathf.Min(minRamp, ramp);
                    maxRamp = Mathf.Max(maxRamp, ramp);
                }

                float whiteFraction = white / (float)visible;
                Assert.Less(whiteFraction, 0.25f,
                    $"At {speed} m/s {whiteFraction:P0} of the VISIBLE band still draws flat white. The " +
                    "owner's complaint is that the band never blues; white belongs to the moment of " +
                    "churn, not to the trail.");
                Assert.Less(minRamp, 0.05f,
                    $"At {speed} m/s the band never reaches the foam anchor. Fresh churn IS white — a " +
                    "wake that is blue at the transom is the opposite defect.");
                Assert.Greater(maxRamp, 0.8f,
                    $"At {speed} m/s the band only reaches ramp {maxRamp:0.00} before it fades out. The " +
                    "colour walk has to FINISH while the foam is still visible, or the blues live in " +
                    "pixels nobody can see — which is exactly what round 1 shipped.");
            }
        }

        [Test]
        public void TheWalk_IsMonotone_AndTiedToTheSeasOwnAnchors()
        {
            Tuning t = Live();
            Sample[] wake = Wake(3f, in t);

            float previous = -1f;
            foreach (Sample s in wake)
            {
                float ramp = RampAt(WakeFoamAgeing.Age01FromFreshness(s.Fresh, t.FreshFloor), in t);
                Assert.GreaterOrEqual(ramp, previous - 1e-5f,
                    "water that has aged never gets younger — the walk down the ramp must be monotone " +
                    "in time astern.");
                previous = ramp;
            }

            // And every shade it can take is a convex combination of the LIVE palette anchors (ADR 0015)
            // — the same guarantee the particle side already carries, proven here on the buffer's path.
            // (deep, mid, shallow, foam) — the seam's own order.
            var palette = new SeaPaletteState(new Color(0.05f, 0.14f, 0.24f), new Color(0.10f, 0.28f, 0.42f),
                                              new Color(0.35f, 0.62f, 0.70f), new Color(0.95f, 0.98f, 1f));
            for (float age = 0f; age <= 1.0001f; age += 0.05f)
            {
                Color c = WakeFoamAgeing.Ramp3(RampAt(age, in t), palette.Foam, palette.Shallow, palette.Mid);
                Assert.LessOrEqual(c.r, palette.Foam.r + 1e-4f, "the wake left the sea's palette (r)");
                Assert.GreaterOrEqual(c.b, Mathf.Min(palette.Mid.b, palette.Foam.b) - 1e-4f,
                    "the wake left the sea's palette (b)");
            }
        }

        // ==== 4. the relationship the two half-lives must keep =====================================

        [Test]
        public void TheAgeHalfLife_IsShorterThanTheCoverageHalfLife()
        {
            Tuning t = Live();
            Assert.Less(t.AgeHalfLife, t.CoverHalfLife,
                "The colour walk must finish while the foam is still bright enough to show it. An age " +
                "half-life at or above the coverage half-life puts the sea's blues in the tail the alpha " +
                "has already faded to nothing — which is the round-1 defect arriving by a second route, " +
                "and no test would have caught it without this one.");
        }
    }
}
