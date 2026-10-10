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

        /// <summary>Where on the walk a sample draws: 0 = the foam anchor (white), 0.5 = the lifted water,
        /// 1 = the water itself (W4-1, design 3A; it was the shallow and the mid blue). The production knot
        /// curve, not a copy of it.</summary>
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
                    "owner's complaint is that the band never leaves white; white belongs to the moment " +
                    "of churn, not to the trail.");
                Assert.Less(minRamp, 0.05f,
                    $"At {speed} m/s the band never reaches the foam anchor. Fresh churn IS white — a " +
                    "wake that is already its water at the transom is the opposite defect.");
                Assert.Greater(maxRamp, 0.8f,
                    $"At {speed} m/s the band only reaches ramp {maxRamp:0.00} before it fades out. The " +
                    "colour walk has to come most of the way into its water (W4-1: the walk ends ON the " +
                    "water) while the foam is still visible, or the walk lives in pixels nobody can see — " +
                    "which is exactly what round 1 shipped.");
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

            // And every shade it can take lies between the sea's foam (ADR 0015) and THE WATER THE BAND
            // RIDES ON — the live material's own water at the report's depths (W4-1, design 3A; this was a
            // convex combination of an invented foam, shallow and mid) — the same guarantee the particle
            // side carries, proven here on the buffer's path.
            MoodWater live = Water(LiveWaterMatPath);
            SeaPaletteState palette = live.Palette();
            foreach (float depth in Depths)
            {
                Color water = live.At(depth);
                Color lift = WakeFoamAgeing.LiftOf(water, in palette);
                for (float age = 0f; age <= 1.0001f; age += 0.05f)
                {
                    Color c = WakeFoamAgeing.Ramp3(RampAt(age, in t), palette.Foam, lift, water);
                    for (int ch = 0; ch < 3; ch++)
                    {
                        Assert.LessOrEqual(c[ch], Mathf.Max(palette.Foam[ch], water[ch]) + 1e-4f,
                            $"at {depth} m the wake left the span from its water to the sea's foam (ch {ch})");
                        Assert.GreaterOrEqual(c[ch], Mathf.Min(palette.Foam[ch], water[ch]) - 1e-4f,
                            $"at {depth} m the wake left the span from its water to the sea's foam (ch {ch})");
                    }
                }
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

        // ==== 5. W4-1 (design 3A): the wake ends as the water it rides on ===========================
        //
        // The owner's 2026-10-09 finding 3: an old wake sat on the sea as a fixed light marine blue (the
        // shallow and mid anchors), a pale stripe over darker water. Ruled 2026-10-10: the walk goes from
        // the sea's foam, through the water lifted, into THE WATER ITSELF. Every expected value below comes
        // from the materials, the depth ramp's pixels or the Phase A report's measured frame; none comes
        // from the function under test.

        /// <summary>The nine waters the game can draw: the live material and the eight weather presets.</summary>
        private static readonly string[] NineWaters =
        {
            LiveWaterMatPath,
            "Assets/_Project/Art/Materials/WaterPresets/Water_DeepBlue.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_FoggySmother.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_GlassyCalm.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_NorthAtlantic.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_StirredBrown.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_StormGrey.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_Tropical.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_WarmShelter.mat",
        };

        private const string WaterShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        private const string DepthRampPath = "Assets/_Project/Art/Textures/Water/DepthRamp.png";

        /// <summary>The Phase A report's two depths, 1 m and 4 m, and the middle of the ramp between them.</summary>
        private static readonly float[] Depths = { 1f, 2.5f, 4f };

        [Test]
        public void TheWake_EndsAsTheWaterItRidesOn_InEveryMood()
        {
            Assert.IsTrue(Regex.IsMatch(Read(DepthRampPath + ".meta"), @"\n\s*isReadable:\s*1\b"),
                "DepthRamp.png must import readable: a wake SPRITE gets its water from the CPU, which reads " +
                "the ramp's texels once. Unreadable, the sprites end on the mid anchor again while the band " +
                "ends on its water, and the two halves of one wake disagree.");

            foreach (string path in NineWaters)
            {
                MoodWater w = Water(path);
                SeaPaletteState palette = w.Palette();

                // The band's own walk at age 1 — FoamAgedColor(1, _FoamColor, _WakeFoamAgeStrength, body) is
                // Shade at life 1 with no scatter and no jitter, at the material's own age strength.
                var ramp = WakeAgeRamp.Default;
                ramp.AgeScatter = 0f;
                ramp.ShadeJitter = 0f;
                ramp.Strength = w.AgeStrength;

                foreach (float depth in Depths)
                {
                    Color water = w.At(depth);

                    Color sprite = palette.BodyAt(depth);
                    Assert.AreEqual(0f, DeltaE(sprite, water), 0.01f,
                        $"{path} at {depth} m: the water handed to a wake sprite is not the water the " +
                        $"shader draws there ({Hex(sprite)} against {Hex(water)}).");

                    Color end = WakeFoamAgeing.Shade(w.Legacy, 1f, 0.5f, in ramp, in palette, sprite);
                    Assert.AreEqual(0f, DeltaE(end, water), 0.01f,
                        $"{path} at {depth} m: the wake ends at {Hex(end)}, not as its water {Hex(water)}. " +
                        "An old wake IS the water it rides on (the owner's 2026-10-09 finding 3); a mood " +
                        "whose _WakeFoamAgeStrength is below 1 leaves part of the legacy white in it.");
                }
            }
        }

        /// <summary>
        /// The mid stop (age at <c>BlueReach</c>, 3.45 s astern) is the water lifted and barely whitened,
        /// so it keeps the water's hue. The bar is in the Phase A report's own measure, the hue of display
        /// sRGB ("water #597e81 against the wake's #357fb3 … hue 205° against 185°", base mood, 1 m): within
        /// half of that 20° gap, the defect the owner saw. The report measured graded; this is the walk
        /// before the grade, which maps the water and the wake through the same curve.
        /// </summary>
        [Test]
        public void TheWakesMidStop_KeepsTheWatersHue()
        {
            const float defectGap = 205f - 185f;    // PHASE-A-REPORT §2 (3), the base mood at 1 m
            const float bar = defectGap * 0.5f;

            foreach (string path in NineWaters)
            {
                MoodWater w = Water(path);
                SeaPaletteState palette = w.Palette();
                var ramp = WakeAgeRamp.Default;
                ramp.AgeScatter = 0f;
                ramp.ShadeJitter = 0f;
                ramp.Strength = w.AgeStrength;

                foreach (float depth in Depths)
                {
                    Color water = w.At(depth);
                    Color mid = WakeFoamAgeing.Shade(w.Legacy, ramp.BlueReach, 0.5f, in ramp, in palette, water);
                    Assert.LessOrEqual(HueGap(mid, water), bar,
                        $"{path} at {depth} m: the wake's mid stop {Hex(mid)} sits {HueGap(mid, water):0.0}° " +
                        $"of hue from its water {Hex(water)}. Lifted water is the same water, brighter; a " +
                        "different hue is the fixed-blue stripe coming back.");
                }
            }

            // The control: the bar sees the defect. Main's walk stopped on the shallow anchor, which at the
            // report's own spot is further off its water than the bar allows.
            MoodWater live = Water(LiveWaterMatPath);
            Color anchor = MatColorOr(Read(LiveWaterMatPath), Read(WaterShaderPath), "_PaletteShallow");
            Assert.Greater(HueGap(anchor, live.At(1f)), bar,
                "The shallow anchor no longer fails this bar at the base mood's 1 m, so the bar no longer " +
                "proves anything about the defect it was set from.");
        }

        [Test]
        public void WakeBodyDials_OnAllNineMaterials_AndInMoodFloatNames()
        {
            // The owner's ruled starting arm (2026-10-10): the mid stop is the water ×2, whitened 5% toward
            // the foam. On every material, because WaterPresetMenu copies every property on apply and a
            // dial on Water.mat alone is lost to the first preset; as the shader's default, so a new
            // material starts on the ruled look.
            string shader = Read(WaterShaderPath);
            string[] dials = { "_WakeBodyLift", "_WakeBodyWhiten" };
            float[] ruled = { 2f, 0.05f };
            for (int i = 0; i < dials.Length; i++)
            {
                Assert.AreEqual(ruled[i], ShaderFloatDefault(shader, dials[i]), 1e-6f,
                    $"the shader's default {dials[i]} is not the ruled arm");
                foreach (string path in NineWaters)
                {
                    Match m = Regex.Match(Read(path), $@"\n\s*-\s{Regex.Escape(dials[i])}:\s*(-?[\d.eE+-]+)");
                    Assert.IsTrue(m.Success, $"{path} does not serialize {dials[i]}.");
                    Assert.AreEqual(ruled[i], float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), 1e-6f,
                        $"{path} carries {dials[i]} {m.Groups[1].Value}, not the ruled arm.");
                }
            }

            // Weather eases them: a fog can hush the lift, a calm can show it.
            var field = typeof(WaterSurface).GetField("MoodFloatNames", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "WaterSurface.MoodFloatNames is gone.");
            var names = (string[])field.GetValue(null);
            foreach (string dial in dials)
                Assert.That(names, Does.Contain(dial),
                    $"{dial} is not in MoodFloatNames, so a weather change snaps it instead of easing it.");
        }

        // ---- the water, as the shader draws it --------------------------------------------------

        /// <summary>
        /// One material's water before any light layer, read from its YAML (a property the material does
        /// not serialize takes the shader's declared default, as Unity does) and from the depth ramp's own
        /// pixels. <see cref="At"/> is THIS TEST'S transcription of the shader's depth block (frag, from
        /// <c>float dt = saturate(…)</c> to the deep-blue lerp), so the expected water never comes from
        /// <c>SeaWaterBody</c>.
        /// </summary>
        private sealed class MoodWater
        {
            public bool UsesRamp;
            public Color32[] Ramp;
            public Color Shallow, Deep, DeepBlue, Legacy;
            public Color PaletteDeep, PaletteMid, PaletteShallow, PaletteFoam;
            public float ShallowDepth, DeepDepth, Bands, DeepBlueStart, DeepBlueStrength;
            public float BodyLift, BodyWhiten, AgeStrength;

            /// <summary>The water at <paramref name="depth"/>, display sRGB, alpha 1.</summary>
            public Color At(float depth)
            {
                float dt = Mathf.Clamp01((depth - ShallowDepth) / Mathf.Max(DeepDepth - ShallowDepth, 1e-3f));
                if (Bands >= 1f) dt = Mathf.Floor(dt * Bands + 0.5f) / Bands;

                // Point sampling with clamp: u = dt falls in texel ⌊dt·n⌋, and u = 1 in the last one.
                Vector3 col = UsesRamp
                    ? Linear(Ramp[Mathf.Min((int)(dt * Ramp.Length), Ramp.Length - 1)])
                    : Vector3.Lerp(Linear(Shallow), Linear(Deep), dt);

                if (DeepBlueStrength > 0.001f)
                {
                    float e0 = Mathf.Min(Mathf.Clamp01(DeepBlueStart), 0.99f);
                    float x = Mathf.Clamp01((dt - e0) / (1f - e0));
                    col = Vector3.Lerp(col, Linear(DeepBlue), x * x * (3f - 2f * x) * Mathf.Clamp01(DeepBlueStrength));
                }
                return new Color(Encode(col.x), Encode(col.y), Encode(col.z), 1f);
            }

            public SeaPaletteState Palette() => new SeaPaletteState(
                PaletteDeep, PaletteMid, PaletteShallow, PaletteFoam, BodyLift, BodyWhiten,
                new SeaWaterBody(UsesRamp ? Ramp : null, Shallow, Deep, ShallowDepth, DeepDepth, Bands,
                                 DeepBlue, DeepBlueStart, DeepBlueStrength));
        }

        private static MoodWater Water(string matPath)
        {
            string mat = Read(matPath), shader = Read(WaterShaderPath);
            Match keywords = Regex.Match(mat, @"m_ValidKeywords:([\s\S]*?)m_InvalidKeywords:");
            Assert.IsTrue(keywords.Success, $"{matPath} has no m_ValidKeywords block.");

            var w = new MoodWater
            {
                UsesRamp = Regex.IsMatch(keywords.Groups[1].Value, @"-\s*_USE_DEPTHRAMP\b"),
                Shallow = MatColorOr(mat, shader, "_ShallowColor"),
                Deep = MatColorOr(mat, shader, "_DeepColor"),
                DeepBlue = MatColorOr(mat, shader, "_DeepBlueColor"),
                Legacy = MatColorOr(mat, shader, "_FoamColor"),
                PaletteDeep = MatColorOr(mat, shader, "_PaletteDeep"),
                PaletteMid = MatColorOr(mat, shader, "_PaletteMid"),
                PaletteShallow = MatColorOr(mat, shader, "_PaletteShallow"),
                PaletteFoam = MatColorOr(mat, shader, "_PaletteFoam"),
                ShallowDepth = MatFloatOr(mat, shader, "_ShallowDepth"),
                DeepDepth = MatFloatOr(mat, shader, "_DeepDepth"),
                Bands = MatFloatOr(mat, shader, "_DepthBands"),
                DeepBlueStart = MatFloatOr(mat, shader, "_DeepBlueStart"),
                DeepBlueStrength = MatFloatOr(mat, shader, "_DeepBlueStrength"),
                BodyLift = MatFloatOr(mat, shader, "_WakeBodyLift"),
                BodyWhiten = MatFloatOr(mat, shader, "_WakeBodyWhiten"),
                AgeStrength = MatFloatOr(mat, shader, "_WakeFoamAgeStrength"),
            };

            if (w.UsesRamp)
            {
                // The ramp this test decodes must be the one the material samples.
                Match tex = Regex.Match(mat, @"-\s_DepthRamp:\s*\n\s*m_Texture:\s*\{[^}]*guid:\s*([0-9a-f]{32})");
                Match guid = Regex.Match(Read(DepthRampPath + ".meta"), @"\nguid:\s*([0-9a-f]{32})");
                Assert.IsTrue(tex.Success && guid.Success && tex.Groups[1].Value == guid.Groups[1].Value,
                    $"{matPath} samples a depth ramp other than {DepthRampPath}; this test decodes that one.");
                w.Ramp = DepthRampRow();
            }
            return w;
        }

        /// <summary>The ramp row the shader samples (v = 0.5, point: row ⌊h/2⌋ from the bottom), shallow
        /// end first, decoded from the PNG itself rather than through its import.</summary>
        private static Color32[] DepthRampRow()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(ImageConversion.LoadImage(tex, File.ReadAllBytes(
                    Path.Combine(Application.dataPath, "..", DepthRampPath))), "DepthRamp.png did not decode.");
                Color32[] all = tex.GetPixels32();
                int row = Mathf.Min(tex.height / 2, tex.height - 1);
                var texels = new Color32[tex.width];
                System.Array.Copy(all, row * tex.width, texels, 0, tex.width);
                return texels;
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        private static float MatFloatOr(string mat, string shader, string key)
        {
            Match m = Regex.Match(mat, $@"\n\s*-\s{Regex.Escape(key)}:\s*(-?[\d.eE+-]+)");
            return m.Success ? float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)
                             : ShaderFloatDefault(shader, key);
        }

        private static Color MatColorOr(string mat, string shader, string key)
        {
            Match m = Regex.Match(mat,
                $@"\n\s*-\s{Regex.Escape(key)}:\s*\{{r:\s*([^,]+),\s*g:\s*([^,]+),\s*b:\s*([^,]+),\s*a:\s*([^}}]+)\}}");
            if (!m.Success)
            {
                m = Regex.Match(shader, $@"(?<![A-Za-z0-9_]){Regex.Escape(key)}\s*\(\s*""[^""]*""\s*,\s*Color\s*\)" +
                                        @"\s*=\s*\(\s*([^,]+),\s*([^,]+),\s*([^,]+),\s*([^)]+)\)");
                Assert.IsTrue(m.Success, $"{key} is neither on the material nor declared in the water shader.");
            }
            float F(int g) => float.Parse(m.Groups[g].Value.Trim(), CultureInfo.InvariantCulture);
            return new Color(F(1), F(2), F(3), F(4));
        }

        private static float ShaderFloatDefault(string shader, string key)
        {
            Match m = Regex.Match(shader,
                $@"(?<![A-Za-z0-9_]){Regex.Escape(key)}\s*\(\s*""[^""]*""\s*,[^=\n]*=\s*(-?[0-9.eE+-]+)");
            Assert.IsTrue(m.Success, $"{key} is neither on the material nor declared in the water shader.");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        // ---- colour science, the test's own (sRGB transfer, CIELAB D65, ΔE76, HSV hue) ------------

        private static float Decode(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

        private static float Encode(float c)
        {
            c = Mathf.Clamp01(c);
            return c <= 0.0031308f ? c * 12.92f : 1.055f * Mathf.Pow(c, 1f / 2.4f) - 0.055f;
        }

        private static Vector3 Linear(Color c) => new Vector3(Decode(c.r), Decode(c.g), Decode(c.b));

        private static Vector3 Linear(Color32 c) => Linear((Color)c);

        private static Vector3 Lab(Color srgb)
        {
            Vector3 l = Linear(srgb);
            float x = (0.4124f * l.x + 0.3576f * l.y + 0.1805f * l.z) / 0.95047f;
            float y = 0.2126f * l.x + 0.7152f * l.y + 0.0722f * l.z;
            float z = (0.0193f * l.x + 0.1192f * l.y + 0.9505f * l.z) / 1.08883f;
            float F(float t) => t > 0.008856f ? Mathf.Pow(t, 1f / 3f) : 7.787f * t + 16f / 116f;
            return new Vector3(116f * F(y) - 16f, 500f * (F(x) - F(y)), 200f * (F(y) - F(z)));
        }

        private static float DeltaE(Color a, Color b) => Vector3.Distance(Lab(a), Lab(b));

        private static float Hue(Color c)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            float d = max - min;
            if (d <= 0f) return 0f;
            float h = max == c.r ? (c.g - c.b) / d : max == c.g ? 2f + (c.b - c.r) / d : 4f + (c.r - c.g) / d;
            return (h * 60f + 360f) % 360f;
        }

        private static float HueGap(Color a, Color b)
        {
            float d = Mathf.Abs(Hue(a) - Hue(b)) % 360f;
            return Mathf.Min(d, 360f - d);
        }

        private static string Hex(Color c)
        {
            int B(float v) => Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
            return $"#{B(c.r):x2}{B(c.g):x2}{B(c.b):x2}";
        }
    }
}
