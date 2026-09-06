using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE SEA'S SUN SIDE, MEASURED</b> — the water-fidelity register's row 11, in the owner's words:
    /// <i>"the light needs to affect the environment."</i>
    ///
    /// <para><b>What the plates showed.</b> At golden hour ADR 0013 multiplies the WHOLE frame by the
    /// tint (0.866, 0.529, 0.356) and the sea took that orange exactly as the beach and the mirror
    /// stripes did — <b>no warm/cool split, no face turned to the low sun</b>. The fragment's only sun
    /// terms were <c>_SwellFaceShade</c>, which adds the SAME amount to r, g and b (a value, not a
    /// colour), and the glitter, which the register judged illegible against row 5's stripes.</para>
    ///
    /// <para><b>Why this file is a MEASUREMENT and not an assertion.</b> A sun side is a claim about a
    /// SPLIT between two populations of pixels, and the populations are not a screen rectangle — they
    /// are the faces of a moving swell. Splitting the frame down the middle would score whatever the
    /// waves happened to be doing in the left half ([[a-moving-highlight-is-not-measured-by-a-fixed-split]]),
    /// so every number below is a <b>centroid over faces</b>: each sample is assigned to the sunward or
    /// the lee population by its OWN facing, and the two population means are compared. All of it runs
    /// on the shipped C# twin of the shader's field sampler — no GPU, so CI runs it.</para>
    ///
    /// <para><b>The sabotage arm is the point of the sign assertions.</b> A term that simply brightens
    /// the water passes any "the sea got warmer at golden hour" test and is not a sun side. So the
    /// guards below pin the SIGN in both directions — the sunward face must go warmer AND the lee face
    /// must go strictly cooler than it shipped — and the brightening arm is built here and shown to
    /// fail exactly that, at a stated ratio rather than against an absolute bar
    /// ([[a-guard-with-an-absolute-bar-rots-on-a-good-change]]).</para>
    /// </summary>
    public class SunSideMeasurementTests
    {
        const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        const string HeroMatPath = "Assets/_Project/Art/Materials/Water.mat";
        const string PresetsFolder = "Assets/_Project/Art/Materials/WaterPresets";

        // ---- the reference sea: the plate sweep's own pairing, so these numbers and the sheet's
        // ---- plates describe one world. blow = sea state 0.55, wind = the sim's own strength for it.
        static readonly Vector2 WindHeading = new Vector2(6f, -5.3f).normalized;
        const float BlowSeaState = 0.55f;
        const float NoonHour = 12f;              // the plates' noon (solar noon is 13:00 — see below)

        // The register's row-5 stripe contrast AFTER the mirror shipped (#PR 5): the row-band contrast
        // the sun side has to be legible against. Open water 0.063, over sand 0.064.
        const float MirrorStripeContrast = 0.063f;

        // 40 m across, the plate's own frame; 6 samples/m is far finer than the swell and far coarser
        // than the plate's 24 px/m, which is all a population mean needs.
        const int Grid = 240;
        const float FrameMeters = 40f;

        // =============================================================================================
        //  The term, transcribed ONCE from the shader (and pinned to it by TheShader_StillDrawsWhatThisFileMeasures)
        // =============================================================================================

        /// <summary>HLSL <c>smoothstep(edge0, edge1, x)</c>. NOT <c>Mathf.SmoothStep</c>, which
        /// interpolates BETWEEN its first two arguments rather than ramping across them.</summary>
        static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>The light's own colour as a direction in RGB: the published tint's deviation from its
        /// own grey. Sums to zero across r+g+b by construction, so it is a HUE signal — the sea is
        /// re-coloured, not lit brighter.</summary>
        static Vector3 SunChroma(Color tint)
        {
            float grey = (tint.r + tint.g + tint.b) / 3f;
            return new Vector3(tint.r - grey, tint.g - grey, tint.b - grey);
        }

        /// <summary>The elevation gate: <c>smoothstep(0, 0.12, e) * (1 - e*e)^2</c>. Since the published
        /// <c>_SunElevation</c> is <c>cos(solarX * pi/2)</c>, <c>(1 - e*e)</c> is sin² of the solar arc
        /// angle, so this is sin⁴ — 0 at solar noon, 1 at the horizon — with the sun's being ABOVE the
        /// horizon as a hard precondition (sin⁴ alone is symmetric and would fire at 02:00).</summary>
        static float ElevGate(float sunElevation)
        {
            float sinSq = 1f - sunElevation * sunElevation;
            return SmoothStep(0f, 0.12f, sunElevation) * sinSq * sinSq;
        }

        /// <summary>The shader's signed facing, on the field's own analytic slope. The surf front's
        /// slope is zero on open water (no bore), which is where these are measured.</summary>
        static float FaceSigned(Vector2 slope, Vector2 sunDir)
            => Mathf.Clamp(-Vector2.Dot(slope, sunDir) * 2f, -1f, 1f);

        // =============================================================================================
        //  The world under test
        // =============================================================================================

        static DayNightProfile Profile()
        {
            var p = Resources.Load<DayNightProfile>("DayNightProfile");
            Assert.IsNotNull(p, "the shipped DayNightProfile must load — the sun's hours come from it");
            return p;
        }

        /// <summary>The golden hour FOUND on the shipped profile the way the plate sweep finds it: the
        /// warmest still-bright afternoon tint. Asserted to land where the register says (17:00) so that
        /// a retuned profile reports "the golden hour moved" instead of quietly measuring another hour.</summary>
        static float GoldenHourFor(DayNightProfile profile)
        {
            float best = 12f, bestWarmth = float.MinValue;
            for (float hour = 12f; hour <= profile.SunsetHour; hour += 0.05f)
            {
                Color tint = DayNightMath.DayNightTint(hour, profile, 1f, 0f);
                float luma = 0.299f * tint.r + 0.587f * tint.g + 0.114f * tint.b;
                if (luma < 0.35f) continue;
                float warmth = tint.r - tint.b;
                if (warmth > bestWarmth) { bestWarmth = warmth; best = hour; }
            }
            return best;
        }

        static float ShippedFloat(string key)
        {
            var m = Regex.Match(File.ReadAllText(HeroMatPath, Encoding.UTF8),
                                "-\\s" + Regex.Escape(key) + ":\\s*(-?[\\d.eE+]+)");
            Assert.IsTrue(m.Success, $"{key} is not serialized on Water.mat");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        static IEnumerable<string> EveryWaterMaterial()
        {
            yield return HeroMatPath;
            foreach (string p in Directory.GetFiles(PresetsFolder, "Water_*.mat").OrderBy(x => x))
                yield return p;
        }

        /// <summary>The sea the plates photograph, packed exactly as the bridge publishes it.</summary>
        static PackedWaveField ReferenceField()
        {
            Vector2 wind = WindHeading * WeatherModel.WindStrengthFor(BlowSeaState);
            WaveTrains trains = WaveMath.TrainsFrom(wind, BlowSeaState, GameServices.WaveField);
            return WaveFieldBridge.Pack(trains);
        }

        /// <summary>One population's mean, over the faces that belong to it.</summary>
        struct Population
        {
            public int Count;
            public double Facing;      // mean signed facing
            public double R, G, B;     // mean colour DELTA (post day/night multiply)

            public void Add(float facing, Vector3 delta)
            {
                Count++; Facing += facing; R += delta.x; G += delta.y; B += delta.z;
            }
            public void Finish()
            {
                if (Count == 0) return;
                Facing /= Count; R /= Count; G /= Count; B /= Count;
            }
            public double WarmCool => R - B;                                   // the sun side's own axis
            public double Luma => 0.299 * R + 0.587 * G + 0.114 * B;           // what a value test would see
        }

        /// <summary>The three arms these tests compare.</summary>
        enum Arm
        {
            /// <summary>The shipped sun side: the light's own chroma, signed by the facing.</summary>
            SunSide,
            /// <summary>The SABOTAGE the handoff names — a plain brightening that lifts BOTH faces by
            /// the same amount. It raises luminance and passes any "the sea got brighter/warmer at
            /// golden hour" test, and it has no side, which is the whole point.</summary>
            BothFacesLift,
            /// <summary>The term ALREADY in the sea: <c>_SwellFaceShade</c>, a signed GREY add. It is
            /// the honest baseline for "is the sun side legible", because ADR 0013's multiply turns
            /// even a colourless add orange at golden hour — so this arm has an INCIDENTAL warm/cool
            /// split, and the sun side has to beat it, not merely beat zero.</summary>
            GreySignedShade,
        }

        /// <summary>
        /// Walk the drawn swell at one hour and split it into its sunward and lee faces, each sample
        /// assigned by its OWN facing.
        /// </summary>
        static void Measure(float hour, float strength, out Population sunward, out Population lee,
                            out float elevation, out Color tint, Arm arm = Arm.SunSide)
        {
            DayNightProfile profile = Profile();
            PackedWaveField field = ReferenceField();

            elevation = DayNightMath.SunElevation(hour, profile.SunriseHour, profile.SunsetHour);
            Vector2 sunDir = DayNightMath.SunDirection(hour, profile.SunriseHour, profile.SunsetHour,
                                                       profile.ShadowSouthBias, profile.ShadowNoonLift);
            tint = DayNightMath.DayNightTint(hour, profile, 1f, 0f);

            Vector3 chroma = SunChroma(tint);
            bool liftsBothFaces = false;
            bool elevationGated = true;
            if (arm == Arm.BothFacesLift)
            {
                // The same ENERGY, spent on all three channels equally, and spent on BOTH faces —
                // |facing| below, not facing. This is what "the sea got brighter where the sun is"
                // looks like, and it is not a sun side.
                float mag = (Mathf.Abs(chroma.x) + Mathf.Abs(chroma.y) + Mathf.Abs(chroma.z)) / 3f;
                chroma = new Vector3(mag, mag, mag);
                liftsBothFaces = true;
            }
            else if (arm == Arm.GreySignedShade)
            {
                // _SwellFaceShade: a colourless add, signed by the same facing, with NO elevation gate
                // (it has none). The caller passes its own shipped amount as `strength`.
                chroma = new Vector3(1f, 1f, 1f);
                elevationGated = false;
            }

            float gate = elevationGated ? ElevGate(elevation) : 1f;
            // The blow sea is far above the swell-read calm gate (smoothstep(0.28, 0.45, 0.55) == 1), so
            // the modelled swell is fully engaged; asserted in TheReferenceSea_IsPastTheCalmGate.
            float calmGate = SmoothStep(Mathf.Clamp01(ShaderDefault("_SwellReadSeaStateLo")),
                                        ShaderDefault("_SwellReadSeaStateHi"), BlowSeaState);
            float freqScale = Mathf.Max(ShippedFloat("_OceanSwellScale"), 1e-4f) / 0.025f;

            sunward = default; lee = default;
            float step = FrameMeters / Grid;
            for (int iy = 0; iy < Grid; iy++)
            for (int ix = 0; ix < Grid; ix++)
            {
                var p = new Vector2(ix * step, iy * step);
                // fetch envelope 1: the register's open-water control, where the sea is the sea and no
                // shore is in frame.
                WaveSample s = WaveFieldBridge.ShaderTwinSample(p, field, freqScale, 1f);
                float facing = FaceSigned(s.Slope, sunDir);
                if (Mathf.Abs(facing) < 1e-6f) continue;      // dead flat: belongs to neither face

                // The dial is 0..2 (the shader clamps with max(), not saturate()), because the measured
                // value that makes this term as strong as the mirror's own banding is 1.7.
                float drive = liftsBothFaces ? Mathf.Abs(facing) : facing;
                float amt = drive * Mathf.Max(strength, 0f) * gate * calmGate;
                // ADR 0013 multiplies the finished frame by the tint, and the plates are read back AFTER
                // that multiply — so the delta a plate can show is the added colour times the tint.
                var delta = new Vector3(chroma.x * amt * tint.r,
                                        chroma.y * amt * tint.g,
                                        chroma.z * amt * tint.b);
                if (facing > 0f) sunward.Add(facing, delta); else lee.Add(facing, delta);
            }
            sunward.Finish(); lee.Finish();
        }

        static float ShaderDefault(string key)
        {
            var m = Regex.Match(File.ReadAllText(ShaderPath, Encoding.UTF8),
                                Regex.Escape(key) + @"\s*\(""[^""]*"",\s*Range\([^)]*\)\)\s*=\s*(-?[\d.]+)");
            Assert.IsTrue(m.Success, $"{key} has no Range default in the shader");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        // =============================================================================================
        //  The measurements
        // =============================================================================================

        [Test]
        public void TheGoldenHour_AndTheNoonControl_AreTheHoursTheRegisterNames()
        {
            DayNightProfile p = Profile();
            float golden = GoldenHourFor(p);
            Assert.That(golden, Is.EqualTo(17f).Within(0.06f),
                $"the profile's warmest still-bright afternoon hour moved to {golden:F2} — every number in " +
                "this file and in register row 11 was measured at 17:00; re-measure before trusting them");

            float eGolden = DayNightMath.SunElevation(golden, p.SunriseHour, p.SunsetHour);
            float eNoon = DayNightMath.SunElevation(NoonHour, p.SunriseHour, p.SunsetHour);
            // ⚠ The trap this term was designed around: the golden hour's sun is NOT low. Sunrise 6,
            // sunset 20 puts solar noon at 13:00, so 17:00 is only 4/7 of the way to the horizon and
            // _SunElevation there is 0.62. A naive "1 - elevation" gate would be WEAKEST exactly where
            // the register complains. The sin^4 gate is what separates the two hours.
            Assert.That(eGolden, Is.EqualTo(0.6235f).Within(0.002f), "the golden hour's elevation moved");
            Assert.That(eNoon, Is.EqualTo(0.9749f).Within(0.002f), "the plates' noon elevation moved");
            Assert.That(ElevGate(eGolden) / ElevGate(eNoon), Is.GreaterThan(100f),
                "the elevation gate must separate golden hour from noon by two orders of magnitude, or " +
                "the noon control is not a control");
            Debug.Log($"[sun-side] golden {golden:F2} e={eGolden:F4} gate={ElevGate(eGolden):F5} | " +
                      $"noon {NoonHour:F2} e={eNoon:F4} gate={ElevGate(eNoon):F5} | " +
                      $"ratio {ElevGate(eGolden) / ElevGate(eNoon):F1}x");
        }

        [Test]
        public void TheReferenceSea_IsPastTheCalmGate()
        {
            float lo = Mathf.Clamp01(ShaderDefault("_SwellReadSeaStateLo"));
            float hi = ShaderDefault("_SwellReadSeaStateHi");
            Assert.That(SmoothStep(lo, hi, BlowSeaState), Is.EqualTo(1f).Within(1e-4f),
                $"the blow sea ({BlowSeaState}) must be past the modelled swell's calm gate ({lo}..{hi}) " +
                "or these measurements are scoring a melted term");
        }

        [Test]
        public void AtGoldenHour_TheSunwardFaceGoesWarm_AndTheLeeFaceGoesCool()
        {
            float strength = ShippedFloat("_SunSideStrength");
            Measure(GoldenHourFor(Profile()), strength, out Population sun, out Population lee,
                    out float e, out Color tint);

            Assert.Greater(sun.Count, 10000, "too few sunward samples to mean anything");
            Assert.Greater(lee.Count, 10000, "too few lee samples to mean anything");

            // (1) THE SIGN, BOTH WAYS. This is what a plain brightening cannot do.
            Assert.Greater(sun.WarmCool, 0d, "the sunward face must take the light's own hue (r-b up)");
            Assert.Less(lee.WarmCool, 0d,
                "the lee face must go strictly COOLER than the sea it shipped as — not merely less warm. " +
                "A term that only lifts the lit side is a highlight, not a sun side.");

            double split = sun.WarmCool - lee.WarmCool;
            double lumaSplit = sun.Luma - lee.Luma;
            Debug.Log($"[sun-side] GOLDEN e={e:F4} tint=({tint.r:F3},{tint.g:F3},{tint.b:F3}) " +
                      $"strength={strength} | sunward n={sun.Count} facing={sun.Facing:F3} " +
                      $"r-b={sun.WarmCool:F5} luma={sun.Luma:F5} | lee n={lee.Count} facing={lee.Facing:F3} " +
                      $"r-b={lee.WarmCool:F5} luma={lee.Luma:F5} | SPLIT r-b={split:F5} luma={lumaSplit:F5} | " +
                      $"vs mirror stripes {MirrorStripeContrast}: hue {split / MirrorStripeContrast:F2}x, " +
                      $"value {lumaSplit / MirrorStripeContrast:F2}x");

            // (2) LEGIBLE AGAINST ROW 5'S STRIPES, on the axis this term actually speaks. The mirror owns
            // VALUE (its row-band contrast is a luminance band); the sun side owns HUE. The split has to
            // be a real fraction of the stripe contrast or the register's "none is legible" just repeats.
            Assert.That(split / MirrorStripeContrast, Is.GreaterThan(0.5d),
                $"the warm/cool split is {split:F5}, only {split / MirrorStripeContrast:P0} of the mirror's " +
                $"{MirrorStripeContrast} row-band contrast — the stripes would drown it");

            // (3) AND IT MUST STAY A HUE SIGNAL, not become a second brightness layer competing with the
            // mirror. The sea is re-coloured, not lit.
            Assert.That(System.Math.Abs(lumaSplit), Is.LessThan(split * 0.5d),
                "the sun side has become a value term — its luminance split rivals its hue split, which is " +
                "the brightening the sabotage arm exists to catch");
        }

        [Test]
        public void AtNoon_NothingMoves_BelowOne8BitCode()
        {
            float strength = ShippedFloat("_SunSideStrength");
            Measure(NoonHour, strength, out Population sun, out Population lee, out float e, out _);

            double worst = new[]
            {
                System.Math.Abs(sun.R), System.Math.Abs(sun.G), System.Math.Abs(sun.B),
                System.Math.Abs(lee.R), System.Math.Abs(lee.G), System.Math.Abs(lee.B),
            }.Max();
            Debug.Log($"[sun-side] NOON e={e:F4} worst mean channel move {worst:F6} " +
                      $"({worst * 255d:F3} of one 8-bit code)");
            Assert.That(worst, Is.LessThan(1d / 255d),
                $"at noon the sun side moves a channel by {worst:F6} — the plates' noon column is supposed " +
                "to show NOTHING moved, and one 8-bit code is 0.00392");
        }

        [Test]
        public void AtStrengthZero_TheTermIsExactlyZero_OnEveryFace()
        {
            Measure(GoldenHourFor(Profile()), 0f, out Population sun, out Population lee, out _, out _);
            foreach (double v in new[] { sun.R, sun.G, sun.B, lee.R, lee.G, lee.B })
                Assert.That(v, Is.EqualTo(0d), "_SunSideStrength 0 must be an EXACT passthrough, not a small one");
        }

        [Test]
        public void TheTwoControls_ABrighteningAndTheGreyShadeAlreadyInTheSea_BothFailToMakeASunSide()
        {
            float strength = ShippedFloat("_SunSideStrength");
            float golden = GoldenHourFor(Profile());
            Measure(golden, strength, out Population sun, out Population lee, out _, out _);
            double split = sun.WarmCool - lee.WarmCool;

            // ---- CONTROL 1: the plain brightening the handoff names -----------------------------
            Measure(golden, strength, out Population liftSun, out Population liftLee, out _, out _,
                    Arm.BothFacesLift);
            Assert.Greater(liftSun.Luma, 0d, "the brightening arm is supposed to actually brighten");
            Assert.Greater(liftLee.Luma, 0d, "…on BOTH faces — that is what makes it not a sun side");
            double liftSplit = System.Math.Abs(liftSun.WarmCool - liftLee.WarmCool);
            Assert.That(liftSplit, Is.LessThan(split * 0.02d),
                $"a term that lifts both faces has no side: its warm/cool split is {liftSplit:F6} against " +
                $"the sun side's {split:F6}");

            // ---- CONTROL 2: the signed GREY shade already in the sea -----------------------------
            // ⚠ This is the control that matters, and it is the one a naive test would miss. ADR 0013
            // MULTIPLIES the finished frame by the tint, so at golden hour a colourless signed add comes
            // out ORANGE on the lit face and BLUE on the shaded one all by itself — _SwellFaceShade
            // already makes an incidental warm/cool split. "The sunward face got warmer" is therefore
            // NOT evidence of a sun side. The claim is that the sun side is legibly BIGGER than the
            // accident, and this measures by how much.
            float shadeDial = ShippedFloat("_SwellFaceShade");
            float shadeAmount = shadeDial * 0.15f;   // the shader's add ceiling
            Measure(golden, shadeAmount, out Population shadeSun, out Population shadeLee, out _, out _,
                    Arm.GreySignedShade);
            double shadeSplit = shadeSun.WarmCool - shadeLee.WarmCool;
            Assert.Greater(shadeSplit, 0d,
                "the existing grey shading should show the incidental warm/cool split the tint gives it — " +
                "if this is 0 the multiply is not being modelled and the comparison below is empty");

            Debug.Log($"[sun-side] CONTROLS | both-faces lift: luma sun={liftSun.Luma:F5} lee={liftLee.Luma:F5} " +
                      $"warm/cool split={liftSplit:F8} | _SwellFaceShade ({shadeDial}) " +
                      $"incidental split={shadeSplit:F5} | SUN SIDE split={split:F5} = " +
                      $"{split / shadeSplit:F1}x the shade's accident");

            // Ratioed against the controls, never against an absolute bar that rots when the art improves.
            Assert.That(split / shadeSplit, Is.GreaterThan(5d),
                $"the sun side's split ({split:F5}) is only {split / shadeSplit:F1}x the warm/cool the " +
                "existing grey shading gets for free from the tint — that is the register's 'none is " +
                "legible' repeated, not a fix for it");
        }

        // =============================================================================================
        //  The term measured here is the term that ships
        // =============================================================================================

        [Test]
        public void TheShader_StillDrawsWhatThisFileMeasures()
        {
            string src = File.ReadAllText(ShaderPath, Encoding.UTF8);
            StringAssert.Contains("float3 sunChroma = tintRGB - tintGrey;", src,
                "the sun side's colour must still be the tint's own deviation from grey");
            StringAssert.Contains("float  sinSq    = 1.0 - e * e;", src,
                "the elevation gate must still be sin^2 of the solar arc angle");
            StringAssert.Contains("float  elevGate = smoothstep(0.0, 0.12, e) * sinSq * sinSq;", src,
                "the gate must still be sin^4 with the sun ABOVE the horizon as a precondition");
            StringAssert.Contains("col.rgb += sunChroma * amt;", src,
                "the sun side must still compose into col.rgb and nothing else");

            // ONE normal: the facing is derived in exactly one place and shared.
            int facings = Regex.Matches(src, @"faceSigned\s*=\s*clamp\(-dot\(waveSlope \+ surfFrontSlope").Count;
            Assert.That(facings, Is.EqualTo(1),
                "the swell's facing must be computed ONCE and shared by the shading and the sun side — a " +
                "second derivation is a second normal, and the two would drift apart on the next retune");
        }

        [Test]
        public void EveryWaterMaterial_CarriesTheSunSide_AtTheShippedValue()
        {
            float shipped = ShippedFloat("_SunSideStrength");
            var wrong = new List<string>();
            foreach (string file in EveryWaterMaterial())
            {
                string yaml = File.ReadAllText(file, Encoding.UTF8);
                var m = Regex.Match(yaml, @"-\s_SunSideStrength:\s*(-?[\d.eE+]+)");
                if (!m.Success) { wrong.Add($"{Path.GetFileName(file)}: missing"); continue; }
                float v = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                if (!Mathf.Approximately(v, shipped)) wrong.Add($"{Path.GetFileName(file)}: {v} (expected {shipped})");
            }
            // The wholesale-preset trap: 'Apply water preset' copies every key, so a preset left without
            // this one would stamp the hero material's sun side back off.
            Assert.That(wrong, Is.Empty,
                "_SunSideStrength must be serialized at the shipped value on all nine water materials:\n"
                + string.Join("\n", wrong));
        }
    }
}
