using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE LOOK of the bore (ADR 0040 revision 3, the crashing-washes PR): does the surf on screen
    /// BEAT — one bore per crest, born at the break line, advancing at the bore speed, running up the
    /// beach and draining — and is every one of the three new dials an exact passthrough at 0?</b>
    ///
    /// <para><b>How the beat is measured without a clock.</b> The bore has no <c>_Time</c> in it: it
    /// advances because the bridge advances the published phases. So this fixture publishes the SAME sea
    /// at chosen moments — the trains rebuilt with their travel baked into <c>PhaseOffset</c> exactly as
    /// <c>WaveFieldAnimator</c> does — and freezes the shader's own churn (its evolve speed and shoreward
    /// drift are the only <c>_Time</c> terms on the surf path). Two shots then differ ONLY by what the
    /// bore did between them, and a per-pixel difference is finally a measurement rather than a read of
    /// <c>_Time</c> (the standing lesson of <see cref="BreakerSurfRenderTests"/>).</para>
    ///
    /// <para><b>The cover map.</b> The surf composites as <c>lerp(col, _SurfColor, cover)</c>, so a shot
    /// with the surf painted pure red minus the same shot with the surf OFF recovers <c>cover</c> per
    /// pixel, to the quantization of the shipped bands: <c>(red.r − base.r) / (255 − base.r)</c>. Every
    /// metric below is on that map, not on white-on-white.</para>
    ///
    /// <para><b>⚠ Self-skips without a graphics device</b> — the standing CI law: a skip is "NOT
    /// VERIFIED", never "passed". The source and material guards at the bottom run everywhere.</para>
    /// </summary>
    public class BreakerBoreLookTests
    {
        const float FrameMetres = 70f;
        const int ShotPx = 1200;
        const float PxPerMetre = ShotPx / FrameMetres;
        const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        const string ArtifactDir = "artifacts/crashing-washes";

        /// <summary>The same working sea <see cref="BreakerSurfRenderTests"/> shoots: an 8 m/s onshore
        /// breeze at a middling sea state.</summary>
        static readonly Vector2 ShotWind = new Vector2(6f, -5.3f);
        const float ShotSeaState = 0.55f;
        static readonly Color DebugRed = new Color(1f, 0f, 0f, 1f);

        /// <summary>Every _Time-driven knob the whiteout fixture's MakeStatic() zeroes (its list, verbatim —
        /// see that method's history for why each one is here), minus the two the surf owns (set above) and
        /// minus the four _Surf* dials that method also zeroes (_SurfBeatStrength, _SurfRunUpStrength,
        /// _SurfFrontSlope, _SurfDepositStrength) — this fixture exists to drive those, so freezing them
        /// here would freeze the thing under test.</summary>
        static readonly string[] FrozenLayers =
        {
            "_WindChopSpeed", "_CrossSwellSpeed", "_OceanSwellSpeed", "_FbmDriftSpeed", "_FoamEvolveSpeed",
            "_SwashSpeed", "_ReflectionStrength", "_SkyReflectionStrength", "_SpecAmount", "_CausticAmount",
            "_RainRingStrength", "_DriftLineStrength", "_StormFoamLaneStrength", "_DispersionScale",
            "_RippleSpeed", "_WhitecapCollapseRate", "_RippleStrength", "_WakeFoamStrength",
        };

        static readonly string[] PhaseReaders =
        {
            "_SwellReadStrength", "_SwellFaceShade", "_EnvelopeBandStrength", "_FoamConvergenceStrength",
            "_Roughness", "_SunGlitterStrength",
        };

        GameObject _terrainGo, _seaGo, _camGo;
        Camera _cam;
        RenderTexture _rt;
        ITidalTerrain _previousTerrain;
        readonly List<GameObject> _built = new List<GameObject>();
        Color _shippedSurfColor = Color.white, _shippedLipColor = Color.white;
        WaveTrains _trainsNow;

        [SetUp]
        public void SetUp()
        {
            _previousTerrain = GameServices.TidalTerrain;
            _singleTrain = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            foreach (var go in _built) if (go != null) Object.DestroyImmediate(go);
            _built.Clear();
            foreach (var go in new[] { _seaGo, _camGo, _terrainGo })
                if (go != null) Object.DestroyImmediate(go);
            _seaGo = _camGo = _terrainGo = null;
            _cam = null;
            GameServices.TidalTerrain = _previousTerrain;
            WaveFieldBridge.PublishGlobals(PackedWaveField.Empty);
            WaveFieldBridge.PublishBreakersOff();
        }

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore(
                    "SKIPPED, NOT VERIFIED — no graphics device (Null Device), so nothing rendered and " +
                    "nothing was proved. Expected on CI; the drawn bore needs a GPU.");
        }

        // =============================================================================================
        //  The beat
        // =============================================================================================

        [Test]
        public void TheBeat_AdvancesTheSheetShoreward_AtTheBoreSpeed_AndRepeatsEveryPeriod()
        {
            RequireAGraphicsDevice();
            BuildTheShore();
            (Vector2 aim, Vector2 shoreDir) = AimAtTheSurf(NineMileCreekMainland.SpringLowWater);

            float period = DominantPeriod();
            Assert.That(period, Is.GreaterThan(1f).And.LessThan(20f), $"an implausible period: {period:F2} s");

            // The run-up dial too: on the local whitewater law the sheet is a metre-wide band that
            // brightens in TIME as each front crosses it; the travel law gives the sheet the zone, and
            // with it a sawtooth in SPACE that can be seen to move.
            var dials = new Dials { Beat = 1f, RunUp = 1f, SolidSheet = true, Unsaturated = true };
            _singleTrain = true;    // the mechanism arm: one train, one period, born full every crest
            RequireAStaticSea(dials);
            float[] c0 = CoverMap("beat-t0", 0f, dials);
            float[] cQ = CoverMap("beat-tQ", period * 0.25f, dials);
            float[] cH = CoverMap("beat-tH", period * 0.5f, dials);
            float[] c3 = CoverMap("beat-t3Q", period * 0.75f, dials);
            float[] c1 = CoverMap("beat-t1", period, dials);

            float total = Sum(c0);
            Assert.That(total, Is.GreaterThan(ShotPx * ShotPx * 0.002f),
                $"the beating sheet covered only {total / (ShotPx * ShotPx):P3} of the frame — nothing to measure");

            // (i) PERIODIC: a period later the field's published phases have wrapped by exactly one turn,
            //     so the bore — a read of those phases and of geometry — is back where it was.
            float periodic = L1(c0, c1) / total;
            // (ii) …and half a period later it is somewhere else entirely: the DEAD-CONTROL arm. A metric
            //      that could not see the beat would report (i) trivially.
            float halfway = L1(c0, cH) / total;

            // (iii) THE SPEED. Sample the cover along one-pixel lines through the aim in the shoreward
            //       direction (the crests arrive obliquely and, at the drawn scale, 6.4 m apart, so any
            //       lateral width mixes a whole cycle of phase); divide each line by its own TIME-MEAN so
            //       the whitewater's stationary staircase — the height map's 2 m texels — cancels and only
            //       the beat's sawtooth remains; sum the cross-correlation over parallel lines and search
            //       within half a bore spacing, where a shift cannot alias. The bore advances at sqrt(g*d)
            //       at the depth it is running over, so the prediction is made at the mean depth under the
            //       sheet and the band is wide because the run shoals.
            var terrain = _terrainGo.GetComponent<MainlandTidalTerrain>();
            float meanDepth = MeanDepthUnder(c0, terrain, NineMileCreekMainland.SpringLowWater);
            float g = GameServices.WaveField.Gravity;
            float predicted = Mathf.Sqrt(g * Mathf.Max(meanDepth, 0.02f)) * period * 0.25f;
            float spacing = Mathf.Sqrt(g * Mathf.Max(meanDepth, 0.02f)) * period;   // one bore to the next
            float shift = BeatShiftMetres(new[] { c0, cQ, cH, c3 }, c0, cQ, aim, shoreDir, -1f, spacing * 0.5f);

            Debug.Log($"[bore-look] period {period:F2} s; sheet {total / (ShotPx * ShotPx):P2} of the frame; " +
                      $"a period later differs by {periodic:P1}, half a period later by {halfway:P1}; " +
                      $"mean depth under the sheet {meanDepth:F2} m; over a quarter period the profile shifted " +
                      $"{shift:F2} m shoreward against sqrt(g d)·T/4 = {predicted:F2} m (bore spacing {spacing:F1} m). " +
                      $"Frames in {ArtifactDir}/beat-*.png");

            Assert.That(periodic, Is.LessThan(0.04f),
                $"a full period later the sheet differs by {periodic:P1} of its cover — the bore is not " +
                "periodic at the train's period (a _Time term on the surf path, or the phases not wrapping)");
            Assert.That(halfway, Is.GreaterThan(0.25f),
                $"DEAD CONTROL: half a period later the sheet differs by only {halfway:P1} — the metric " +
                "cannot see the beat, so the periodicity claim above proves nothing");
            Assert.That(shift, Is.GreaterThan(0f),
                $"the profile shifted {shift:F2} m — SEAWARD. A bore advances toward the beach");
            Assert.That(shift, Is.InRange(predicted * 0.35f, predicted * 2.0f),
                $"the sheet advanced {shift:F2} m in a quarter period against sqrt(g d)·T/4 = {predicted:F2} m " +
                "— the bore is not travelling at the bore speed");
        }

        [Test]
        public void AtDialZero_TheBoreIsAPassthrough_ThePublishedTimeCannotMoveTheSheet()
        {
            RequireAGraphicsDevice();
            BuildTheShore();
            AimAtTheSurf(NineMileCreekMainland.SpringLowWater);
            float period = DominantPeriod();

            // The steady state the surf shipped with: the sheet is a function of depth and of the marched
            // geometry only. Advance the sea half a period and NOTHING on the surf path may move.
            // Quantized to the shipped four bands before differencing: the estimator's 8-bit rounding
            // (a level of g or b over pale water) cannot cross a band, so what remains is the sheet
            // itself — and at dials 0 the sheet is a function of depth and marched geometry only.
            var off = new Dials { SolidSheet = true, PhaseBlind = true };
            RequireAStaticSea(off);
            float[] a = Quantize(CoverMap("passthrough-t0", 0f, off), 0.25f);
            float[] b = Quantize(CoverMap("passthrough-tH", period * 0.5f, off), 0.25f);
            float total = Sum(a);
            float still = L1(a, b) / Mathf.Max(total, 1f);

            // …and the same pair with the beat up is the DEAD-CONTROL arm.
            var on = new Dials { Beat = 1f, SolidSheet = true, PhaseBlind = true };
            float[] a1 = Quantize(CoverMap("passthrough-beat-t0", 0f, on), 0.25f);
            float[] b1 = Quantize(CoverMap("passthrough-beat-tH", period * 0.5f, on), 0.25f);
            float moved = L1(a1, b1) / Mathf.Max(Sum(a1), 1f);

            Debug.Log($"[bore-look] dials at 0: half a period moves {still:P2} of the sheet's cover " +
                      $"(sheet {total / (ShotPx * ShotPx):P2} of the frame); beat at 1: {moved:P1}.");

            Assert.That(total, Is.GreaterThan(ShotPx * ShotPx * 0.002f), "there must be surf in the frame to test");
            Assert.That(still, Is.LessThan(0.01f),
                $"with every bore dial at 0 the published time moved {still:P2} of the sheet — the " +
                "passthrough is not a passthrough (the beat is leaking into the steady state)");
            Assert.That(moved, Is.GreaterThan(0.25f),
                $"DEAD CONTROL: with the beat at 1 the same half period moved only {moved:P1}");
        }

        [Test]
        public void TheRunUp_ExtendsTheSheetUpTheBeach_AndRemovesNothing()
        {
            RequireAGraphicsDevice();
            BuildTheShore();
            AimAtTheSurf(NineMileCreekMainland.SpringLowWater);
            float period = DominantPeriod();

            // The cosmetic swash is OFF in every shot here (see Shoot), so the ONLY thing that can move the
            // drawn wet edge is the bore's run-up. Where a bore is alive the edge rides up the beach and the
            // sheet rides with it; where it is not, nothing changes. Sampled at four moments because the
            // run-up pulses — at any one moment the bore may be draining.
            // The wash up the sand is thin foam over clear water, never a solid sheet, so the metric is
            // DRAWN WATER: every pixel that differs from a land-only render of the same frame is wet.
            Color32[] land = ShootLandOnly("runup-land");
            int bestAdded = 0, worstRemoved = 0;
            float bestT = 0f;
            for (int k = 0; k < 4; k++)
            {
                float t = period * k * 0.25f;
                bool[] dry = Wet(Shoot($"runup-off-{k}", t, new Dials()), land);
                bool[] wet = Wet(Shoot($"runup-on-{k}", t, new Dials { RunUp = 1f }), land);
                int added = 0, removed = 0;
                for (int i = 0; i < dry.Length; i++)
                {
                    if (wet[i] && !dry[i]) added++;
                    if (dry[i] && !wet[i]) removed++;
                }
                if (added > bestAdded) { bestAdded = added; bestT = t; }
                worstRemoved = Mathf.Max(worstRemoved, removed);
            }

            Debug.Log($"[bore-look] run-up at 1 adds up to {bestAdded} solid sheet px (at t = {bestT:F2} s) " +
                      $"and removes at most {worstRemoved}; frame {ShotPx * ShotPx} px.");

            Assert.That(bestAdded, Is.GreaterThan(ShotPx * ShotPx / 2000),
                $"the run-up dial added only {bestAdded} px of sheet up the beach — the drawn edge is not " +
                "riding the bore (the beach band is not being evaluated, or the clip still uses the old edge)");
            Assert.That(worstRemoved, Is.LessThanOrEqualTo(ShotPx * ShotPx / 2000),
                $"the run-up dial REMOVED {worstRemoved} px of sheet — the run-up can only extend the edge; " +
                "it must never drain past the still-water line");
        }

        [Test]
        public void TheFrontSlope_ShadesOnlyWhereABoreIsAlive()
        {
            RequireAGraphicsDevice();
            BuildTheShore();
            AimAtTheSurf(NineMileCreekMainland.SpringLowWater);
            float period = DominantPeriod();
            float t = period * 0.125f;

            // Where the bore is (a loose cover mask from the solid sheet, dilated a few pixels)…
            // Unsaturated: the shipped 4-band posterize zeroes any cover below an eighth, and the face's
            // faint fringes live exactly there.
            float[] cover = CoverMap("slope-mask", t, new Dials { SolidSheet = true, RunUp = 1f, Unsaturated = true });
            bool[] mask = Dilate(Threshold(cover, 0.03f), 4);

            // …and what the sun sees: the shipped patchy sheet in white with the face shade fully up, the
            // front dial at 0 and then at 2. Same moment, frozen churn — the only difference is the bore's
            // face entering the shade term.
            // Both arms carry the run-up dial, so the whitewater the face stands on is the travel-time
            // law and the bore is alive across the zone; the ONLY difference between them is the face.
            // …with the sheet itself TRANSPARENT (a surf colour of alpha 0): the face shades the water
            // UNDER the sheet, and under a solid white sheet nothing can be seen to change.
            var clear = new Color(1f, 1f, 1f, 0f);
            var flat = new Dials { FaceShade = 1f, RunUp = 1f, Surf = clear };
            var faced = new Dials { FaceShade = 1f, RunUp = 1f, FrontSlope = 2f, Surf = clear };
            Color32[] a = Shoot("slope-0", t, flat);
            Color32[] b = Shoot("slope-2", t, faced);

            int changed = 0, inside = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (!Differs(a[i], b[i], 6)) continue;
                changed++;
                if (mask[i]) inside++;
            }
            float insideShare = changed > 0 ? inside / (float)changed : 1f;
            Debug.Log($"[bore-look] the front slope changed {changed} px; {insideShare:P1} of them under the bore.");

            Assert.That(changed, Is.GreaterThan(400),
                $"the front-slope dial at 2 changed only {changed} px — the bore's face is not reaching the shade term");
            Assert.That(insideShare, Is.GreaterThan(0.95f),
                $"only {insideShare:P1} of the pixels the front slope changed lie under a live bore — " +
                "the face is shading water that has no bore on it");
        }

        // =============================================================================================
        //  The owner's check-in: the beach in BEATS, the ledge as an EVENT
        // =============================================================================================

        [Test]
        public void CheckIn_TheSpillingBeachInBeats_AndThePlungingLedgeAsAnEvent()
        {
            RequireAGraphicsDevice();
            BuildTheShore();
            float period = DominantPeriod();
            var live = new Dials { Beat = 1f, RunUp = 1f, FrontSlope = 1f, FaceShade = -1f, LiveChurn = true };

            // (a) the spilling beach — where the physics says the surf runs longest.
            (Vector2 sandAim, Vector2 sandShore) = AimAtTheSurf(NineMileCreekMainland.SpringLowWater);
            // A 35 m frame centred mid-zone: at 70 m a 9.5 m bore spacing is 80 px of a 600 px frame
            // and the run-up's excursion a dozen, which is not a check-in anyone can judge.
            Strip("checkin-sand", sandAim + sandShore * 8f, period, live);

            // (b) the plunging ledge — the steepest break-contour point on this coast, if it plunges.
            Vector2 ledge = SteepestBreakPoint(NineMileCreekMainland.SpringLowWater, out float xi, out float weight);
            Debug.Log($"[bore-look] steepest break-contour point xi = {xi:F3}, plunging weight {weight:F3} at {ledge}");
            if (weight > 0.01f)
            {
                var terrain = _terrainGo.GetComponent<MainlandTidalTerrain>();
                Vector2 ledgeShore = BreakerSurfRenderTests.ShoreGradient(terrain, ledge);
                Strip("checkin-ledge", ledge + ledgeShore * 4f, period, live);
            }
            Assert.Pass($"check-in strips written to {ArtifactDir}/checkin-*.png (8 frames across one " +
                        $"{period:F2} s period, all three bore dials at 1); the ledge strip " +
                        (weight > 0.01f ? "shot" : "SKIPPED — this coast spills everywhere"));
        }

        const float StripFrameMetres = 35f;

        void Strip(string name, Vector2 centre, float period, Dials dials)
        {
            const int frames = 8, half = ShotPx / 2;
            _cam.transform.position = new Vector3(centre.x, centre.y, -100f);
            _cam.orthographicSize = StripFrameMetres * 0.5f;
            try { StripFrames(name, centre, period, dials, frames, half); }
            finally { _cam.orthographicSize = FrameMetres * 0.5f; }
        }

        void StripFrames(string name, Vector2 aim, float period, Dials dials, int frames, int half)
        {
            var strip = new Texture2D(half * frames, half, TextureFormat.RGBA32, false);
            var manifest = new StringBuilder();
            manifest.AppendLine($"# {name}: camera at {aim}, {StripFrameMetres} m across; frame k is t = k/8 of the " +
                                $"dominant period {period:F2} s; beat 1, run-up 1, front slope 1; churn live");
            for (int k = 0; k < frames; k++)
            {
                float t = period * k / frames;
                Color32[] px = Shoot($"{name}-{k}", t, dials);
                for (int y = 0; y < half; y++)
                for (int x = 0; x < half; x++)
                {
                    // 2x2 box downsample
                    int i0 = (2 * y) * ShotPx + 2 * x;
                    Color32 p = Average(px[i0], px[i0 + 1], px[i0 + ShotPx], px[i0 + ShotPx + 1]);
                    strip.SetPixel(k * half + x, y, p);
                }
                manifest.AppendLine($"frame {k}: t = {t:F2} s");
            }
            strip.Apply();
            Directory.CreateDirectory(ArtifactDir);
            File.WriteAllBytes(Path.Combine(ArtifactDir, name + "-STRIP.png"), strip.EncodeToPNG());
            File.WriteAllText(Path.Combine(ArtifactDir, name + "-MANIFEST.txt"), manifest.ToString());
            Object.DestroyImmediate(strip);
        }

        // =============================================================================================
        //  Source and material guards (no GPU)
        // =============================================================================================

        [Test]
        public void TheThreeDials_DefaultToZero_AndAreExactPassthroughsInTheSource()
        {
            string src = File.ReadAllText(ShaderPath, Encoding.UTF8);
            StringAssert.IsMatch(@"_SurfBeatStrength\s*\(""[^""]*"",\s*Range\(0,\s*1\)\)\s*=\s*0\b", src,
                "_SurfBeatStrength must ship at 0 — today's steady boil");
            StringAssert.IsMatch(@"_SurfRunUpStrength\s*\(""[^""]*"",\s*Range\(0,\s*1\)\)\s*=\s*0\b", src,
                "_SurfRunUpStrength must ship at 0 — the cosmetic swash's edge");
            StringAssert.IsMatch(@"_SurfFrontSlope\s*\(""[^""]*"",\s*Range\(0,\s*2\)\)\s*=\s*0\b", src,
                "_SurfFrontSlope must ship at 0 — no face for the light");

            StringAssert.Contains("float beat = lerp(1.0, surfSheet, boreBeat);", src,
                "the sheet's beat blends from EXACTLY 1 (the steady state) — a multiply would not be a passthrough");
            StringAssert.Contains("float boreEvent = lerp(1.0, surfBore, boreBeat);", src,
                "the anatomy's event blends from EXACTLY 1 too");
            StringAssert.Contains("edgeSwash = lerp(edgeSwash, boreEdgeShift, boreEdgeBlend);", src,
                "the run-up blends from the previous edge, so blend 0 is that edge exactly");
            StringAssert.Contains("clamp(-dot(waveSlope + surfFrontSlope, shadeLd) * 2.0, -1.0, 1.0)", src,
                "the sun's face shade must see the bore front only as an ADDED slope (0 at the dial's 0)");
            // Pinned WITHOUT the closing bracket: the night PR (2026-09-02) added an out-parameter for the
            // beam's colour-weighted sum, and the property this guards is which SLOPE the lamp's relief
            // reads — not how many things the call returns.
            StringAssert.Contains("BoatLightTerm(worldXY, waveSlope + surfFrontSlope, waveHeight", src,
                "the lamp's relief must see the same added slope");
            StringAssert.Contains("float2 surfFrontSlope = float2(0.0, 0.0);", src,
                "the front slope must be declared zero before any gate can leave it unset");
        }

        [Test]
        public void TheBore_HasNoClockOfItsOwn_InTheShader()
        {
            string src = File.ReadAllText(ShaderPath, Encoding.UTF8);
            int start = src.IndexOf("==== THE BORE (ADR 0040 rev 3)", StringComparison.Ordinal);
            int end = src.IndexOf("float SurfIribarren(", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThan(0), "the bore's helper block is missing");
            Assert.That(end, Is.GreaterThan(start), "the bore's helper block must precede SurfIribarren");
            string bore = CodeOnly(src.Substring(start, end - start));
            StringAssert.DoesNotContain("_Time", bore,
                "the bore advances because the bridge advances the published phases — a _Time term here " +
                "would be a second clock, and one MakeStatic() cannot freeze");
            StringAssert.Contains("float SurfBorePhaseDeg(float2 breakLinePoint, float travelS, float freqScale)", bore);
            StringAssert.Contains("float WaveFieldSampleAt(float2 worldXY, float freqScale, float fetchEnv, float timeBack)", bore);
        }

        [Test]
        public void TheMarch_IntegratesSecondsAndMetres_InOneLoop()
        {
            string src = File.ReadAllText(ShaderPath, Encoding.UTF8);
            StringAssert.Contains("void SurfMarch(float2 worldXY, float2 travelDir, float fetchEnv, out float ageM, out float travelS)", src,
                "one march, both integrals — a second march was refused (ADR 0040 rev 3)");
            Assert.That(CountOf(src, "[unroll] for (int i = 1; i <= FETCH_MARCH_STEPS; i++)"), Is.LessThanOrEqualTo(2),
                "a third march loop appeared — the surf must stay at one 16-tap march (the whitewater and the " +
                "bore share it); the fetch's own march is the other");
        }

        [Test]
        public void EveryWaterMaterial_CarriesEverySurfDial_Serialized()
        {
            // 'Apply water preset' is a WHOLESALE copy: a preset missing a key stamps the shader default —
            // or 0 — over the hero material. So every _Surf* dial is serialized on Water.mat and on all
            // eight presets, at the current defaults, and this reads the YAML rather than asking the
            // Material (which answers the shader default for a key it does not carry).
            string[] floats =
            {
                "_SurfStrength", "_SurfCrestBoost", "_SurfCrestWidth", "_SurfNoiseScale", "_SurfEvolveSpeed",
                "_SurfThreshold", "_SurfThresholdSoft", "_SurfBands", "_SurfBandDither", "_SurfSupersedeFringe",
                "_SurfPlungeStrength", "_SurfLipThrow", "_SurfLipWidth", "_SurfBarrelShade", "_SurfPocketWidth",
                "_SurfPocketBoost", "_SurfBeatStrength", "_SurfRunUpStrength", "_SurfFrontSlope",
                "_SurfDepositStrength",
            };
            string[] colours = { "_SurfColor", "_SurfLipColor", "_SurfBarrelColor" };
            string[] files =
            {
                "Assets/_Project/Art/Materials/Water.mat",
                "Assets/_Project/Art/Materials/WaterPresets/Water_DeepBlue.mat",
                "Assets/_Project/Art/Materials/WaterPresets/Water_FoggySmother.mat",
                "Assets/_Project/Art/Materials/WaterPresets/Water_GlassyCalm.mat",
                "Assets/_Project/Art/Materials/WaterPresets/Water_NorthAtlantic.mat",
                "Assets/_Project/Art/Materials/WaterPresets/Water_StirredBrown.mat",
                "Assets/_Project/Art/Materials/WaterPresets/Water_StormGrey.mat",
                "Assets/_Project/Art/Materials/WaterPresets/Water_Tropical.mat",
                "Assets/_Project/Art/Materials/WaterPresets/Water_WarmShelter.mat",
            };
            var missing = new List<string>();
            foreach (string file in files)
            {
                Assert.That(File.Exists(file), $"{file} is missing");
                string yaml = File.ReadAllText(file, Encoding.UTF8);
                foreach (string key in floats)
                    if (!yaml.Contains($"- {key}: ")) missing.Add($"{Path.GetFileName(file)}: {key}");
                foreach (string key in colours)
                    if (!yaml.Contains($"- {key}: {{")) missing.Add($"{Path.GetFileName(file)}: {key}");
                // The three new dials ship at today's look on every material.
                foreach (string key in new[] { "_SurfBeatStrength", "_SurfRunUpStrength", "_SurfFrontSlope", "_SurfDepositStrength" })
                    if (!yaml.Contains($"- {key}: 0\n") && !yaml.Contains($"- {key}: 0\r\n"))
                        missing.Add($"{Path.GetFileName(file)}: {key} is not serialized at 0");
            }
            Assert.That(missing, Is.Empty, "surf dials missing from a water material:\n" + string.Join("\n", missing));
        }

        [Test]
        public void TheBridge_PublishesTheBoresPhysics_ForTheShader()
        {
            string bridge = File.ReadAllText("Assets/_Project/Code/Art/WaveFieldBridge.cs", Encoding.UTF8);
            StringAssert.Contains("Shader.PropertyToID(\"_BreakerBore\")", bridge);
            StringAssert.Contains(".BorePulseSharpness", bridge, "the pulse sharpness must be published");
            StringAssert.Contains(".BoreSetStrength", bridge, "the set strength must be published");
            StringAssert.Contains(".RunUpCoefficient", bridge, "Hunt's coefficient must be published");
            StringAssert.Contains(".RunUpCapMeters", bridge, "the run-up cap must be published");
            string src = File.ReadAllText(ShaderPath, Encoding.UTF8);
            StringAssert.Contains("float4 _BreakerBore;", src);
        }

        // =============================================================================================
        //  Publishing a sea at a moment
        // =============================================================================================

        /// <summary>The shot sea, with <paramref name="seconds"/> of travel baked into every train's
        /// <c>PhaseOffset</c> — exactly what <c>WaveFieldAnimator</c> hands the bridge, so the shader
        /// samples at t = 0 and the bore's clock reads the field as it is <paramref name="seconds"/> in.</summary>
        /// <summary>ONE train (the dominant alone) for the mechanism arms: every crest is the envelope, so
        /// the bore is born full every period and is exactly periodic in the train's own period. The
        /// eight-train sea — whose other seven trains never share that period, so its births swing with
        /// the set — is what the owner sees in the strip and what the dead-control arms run on.</summary>
        bool _singleTrain;

        WaveTrains TrainsAt(float seconds)
        {
            WaveTrains now = WaveMath.TrainsFrom(ShotWind, ShotSeaState, GameServices.WaveField);
            if (_singleTrain)
            {
                WaveTrain dominant = now.Dominant;
                now = WaveTrains.From(new[] { dominant }, 1, now.CrestSharpening, 0);
            }
            float gravity = GameServices.WaveField.Gravity;
            var advanced = new WaveTrain[WaveTrains.MaxTrains];
            for (int i = 0; i < now.Count; i++)
            {
                WaveTrain tr = now[i];
                float k = 2f * Mathf.PI / Mathf.Max(tr.Wavelength, WaveTrain.MinWavelengthMeters);
                // theta = k*(d.x - c*t) + phi  =>  the travel rides in phi as -k*c*t
                float phase = tr.PhaseOffset - k * tr.PhaseSpeed * seconds;
                advanced[i] = new WaveTrain(tr.Direction, tr.Wavelength, tr.Amplitude, phase, gravity);
                Assert.That(advanced[i].PhaseSpeed, Is.EqualTo(tr.PhaseSpeed).Within(1e-5f),
                    "rebuilding a train with the field's gravity must not change its celerity");
            }
            return WaveTrains.From(advanced, now.Count, now.CrestSharpening, now.DominantIndex);
        }

        void PublishTheSea(float seconds)
        {
            _trainsNow = TrainsAt(seconds);
            WaveFieldBridge.PublishGlobals(WaveFieldBridge.Pack(in _trainsNow));
            WaveFieldBridge.PublishFetchGlobals(GameServices.WaveFetch, ShotWind);
            WaveFieldBridge.PublishBreakerGlobals(_trainsNow.Dominant, GameServices.WaveFetch, GameServices.Breakers);
        }

        float DominantPeriod()
        {
            WaveTrains now = WaveMath.TrainsFrom(ShotWind, ShotSeaState, GameServices.WaveField);
            return BreakerMath.PeriodSeconds(now.Dominant);
        }

        // =============================================================================================
        //  Shots and maps
        // =============================================================================================

        struct Dials
        {
            public float Beat, RunUp, FrontSlope;
            public bool SolidSheet;        // threshold 0: the sheet is exactly {alive}, for the cover map
            public bool Unsaturated;       // crest boost 1, smooth bands: cover = alive * beat, never clamped
            public bool PhaseBlind;        // every layer UNDER the sheet that reads the wave field off
            public bool LiveChurn;         // the shipped evolve/drift (the check-in strip); else frozen
            public float FaceShade;        // >= 0 overrides _SwellFaceShade; -1 leaves the material's
            public Color? Surf;            // the debug colour; null = the shipped white
        }

        Color32[] Shoot(string name, float seconds, Dials d)
        {
            PublishTheSea(seconds);

            var surface = _seaGo.GetComponent<WaterSurface>();
            if (surface != null)
            {
                var so = new SerializedObject(surface);
                var preview = so.FindProperty("_previewWaterLevel");
                if (preview != null)
                {
                    preview.floatValue = NineMileCreekMainland.SpringLowWater;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // ⚠ Through a MaterialPropertyBlock, never onto Water.mat; and EVERY key written on EVERY
            // shot, because a block is sticky (the BreakerSurfRenderTests scarlet-check-in lesson).
            var sr = _seaGo.GetComponent<SpriteRenderer>();
            var block = new MaterialPropertyBlock();
            sr.GetPropertyBlock(block);
            block.SetFloat("_WaterLevel", NineMileCreekMainland.SpringLowWater);
            block.SetFloat("_SurfStrength", 1f);
            block.SetColor("_SurfColor", d.Surf ?? _shippedSurfColor);
            block.SetColor("_SurfLipColor", d.Surf ?? _shippedLipColor);
            block.SetFloat("_SurfBeatStrength", d.Beat);
            block.SetFloat("_SurfRunUpStrength", d.RunUp);
            block.SetFloat("_SurfFrontSlope", d.FrontSlope);
            // The surf path's only _Time terms: its boil clock and its shoreward drift. Frozen, two shots
            // differ by what the BORE did and nothing else.
            block.SetFloat("_SurfEvolveSpeed", d.LiveChurn ? ShippedFloat("_SurfEvolveSpeed") : 0f);
            block.SetFloat("_Flow", d.LiveChurn ? ShippedFloat("_Flow") : 0f);
            // …and EVERY other layer that scrolls, boils or collapses on _Time — the MakeStatic() list of
            // WaterWhiteoutShoreSwirlAcceptanceTests, verbatim. The cover map subtracts a base shot from a
            // red shot taken a few frames apart; any live layer between them is measured as "surf". A
            // fixture must own its clock (the #697 lesson), and the precondition below proves it does.
            if (!d.LiveChurn)
                foreach (string key in FrozenLayers)
                    block.SetFloat(key, 0f);
            // The passthrough arms: the water UNDER the sheet must not read the published phases either
            // (the swell read, the face shade, the envelope bands, the convergence foam, the wind's caps and
            // the glitter all do), or the estimator sees THEIR motion through the sheet's partial cover
            // wherever the composite is not linear. With them off, the only thing that can move is the surf.
            if (d.PhaseBlind)
                foreach (string key in PhaseReaders)
                    block.SetFloat(key, 0f);
            // The cosmetic swash off: the only thing that may move the drawn edge is the bore's run-up.
            block.SetFloat("_SwashAmplitude", 0f);
            block.SetFloat("_SwashEdgeShift", 0f);
            // The shipped crest boost (1.6) and the 4-band posterize clamp the cover to 1 across most
            // of the zone, which hides every modulation the beat makes; the mechanism arms want cover =
            // alive * beat as a NUMBER, so they shoot at boost 1 and smooth bands.
            block.SetFloat("_SurfCrestBoost", d.Unsaturated ? 1f : ShippedFloat("_SurfCrestBoost"));
            block.SetFloat("_SurfBands", d.Unsaturated ? 0f : ShippedFloat("_SurfBands"));
            block.SetFloat("_SurfThreshold", d.SolidSheet ? 0f : ShippedFloat("_SurfThreshold"));
            block.SetFloat("_SurfThresholdSoft", d.SolidSheet ? 0.001f : ShippedFloat("_SurfThresholdSoft"));
            block.SetFloat("_SwellFaceShade", d.FaceShade >= 0f ? d.FaceShade : ShippedFloat("_SwellFaceShade"));
            if (d.FaceShade >= 0f)
            {
                // The face shade shares the modelled swell's sea-state gate (smoothstep of _Chop, which
                // no environment service pushes in this fixture). Both props at 0 disables the gate — the
                // shader's own documented off switch — so the slope probe measures the face, not _Chop.
                block.SetFloat("_SwellReadSeaStateLo", 0f);
                block.SetFloat("_SwellReadSeaStateHi", 0f);
            }
            sr.SetPropertyBlock(block);

            _cam.Render();
            _cam.Render();   // the second is read: a cold shader cache has faked a regression here before

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(ShotPx, ShotPx, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, ShotPx, ShotPx), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Color32[] px = tex.GetPixels32();

            Directory.CreateDirectory(ArtifactDir);
            File.WriteAllBytes(Path.Combine(ArtifactDir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return px;
        }

        /// <summary>The precondition of every difference below: the SAME sea, shot twice, must come back
        /// byte-identical, or a live time-driven layer is what the difference measures (the whiteout
        /// fixture's sanity check, and the reason MakeStatic() grew its list).</summary>
        void RequireAStaticSea(Dials d)
        {
            d.Surf = DebugRed;
            Color32[] first = Shoot("static-check-1", 0f, d);
            Color32[] second = Shoot("static-check-2", 0f, d);
            int differing = 0;
            for (int i = 0; i < first.Length; i++) if (Differs(first[i], second[i], 0)) differing++;
            Assert.That(differing, Is.EqualTo(0),
                $"the 'frozen' sea drifted on {differing} px between two renders of the same moment — a " +
                "time-driven layer is still live and every difference below would measure it, not the bore");
        }

        float ShippedFloat(string key)
        {
            var mat = _seaGo.GetComponent<SpriteRenderer>().sharedMaterial;
            return mat != null && mat.HasProperty(key) ? mat.GetFloat(key) : 0f;
        }

        /// <summary>Per-pixel surf cover from a red shot and its surf-off twin at the same moment:
        /// <c>lerp(col, red, cover).r − col.r = cover · (255 − col.r)</c>.</summary>
        float[] CoverMap(string name, float seconds, Dials d)
        {
            d.Surf = DebugRed;
            Color32[] red = Shoot(name + "-red", seconds, d);
            Color32[] baseShot = ShootSurfOff(name + "-base", seconds);
            // lerp(col, red, c): r = col.r + c(255 − col.r), g = col.g(1 − c), b = col.b(1 − c). Over the
            // pale shallows col.r is ~235 and the red channel has twenty levels of headroom — blind — while
            // g and b carry the cover in full; over dark water it is the other way round. Each channel's
            // estimate, weighted by its own headroom, reads the cover everywhere.
            var cover = new float[red.Length];
            for (int i = 0; i < red.Length; i++)
            {
                Color32 b = baseShot[i], r = red[i];
                float wr = 255f - b.r, wg = b.g, wb = b.b;
                float cr = wr > 1f ? (r.r - b.r) / wr : 0f;
                float cg = wg > 1f ? 1f - r.g / wg : 0f;
                float cb = wb > 1f ? 1f - r.b / wb : 0f;
                float w = wr + wg + wb;
                cover[i] = w > 1f ? Mathf.Clamp01((wr * cr + wg * cg + wb * cb) / w) : 0f;
            }
            return cover;
        }

        Color32[] ShootSurfOff(string name, float seconds)
        {
            var d = new Dials();
            Color32[] px = Shoot(name, seconds, d);   // publishes and sets the block…
            var sr = _seaGo.GetComponent<SpriteRenderer>();
            var block = new MaterialPropertyBlock();
            sr.GetPropertyBlock(block);
            block.SetFloat("_SurfStrength", 0f);     // …then the surf off, same everything else
            sr.SetPropertyBlock(block);
            _cam.Render();
            _cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(ShotPx, ShotPx, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, ShotPx, ShotPx), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            px = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            return px;
        }

        /// <summary>The same frame with the sea switched off: what the land looks like where no water
        /// is drawn over it, so that a pixel that differs from it in a water shot is a WET pixel.</summary>
        Color32[] ShootLandOnly(string name)
        {
            _seaGo.SetActive(false);
            try
            {
                _cam.Render();
                _cam.Render();
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = _rt;
                var tex = new Texture2D(ShotPx, ShotPx, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, ShotPx, ShotPx), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                Color32[] px = tex.GetPixels32();
                Directory.CreateDirectory(ArtifactDir);
                File.WriteAllBytes(Path.Combine(ArtifactDir, name + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return px;
            }
            finally { _seaGo.SetActive(true); }
        }

        static bool[] Wet(Color32[] shot, Color32[] land)
        {
            var wet = new bool[shot.Length];
            for (int i = 0; i < shot.Length; i++) wet[i] = Differs(shot[i], land[i], 2);
            return wet;
        }

        static bool[] SolidSheet(Color32[] px)
        {
            var solid = new bool[px.Length];
            for (int i = 0; i < px.Length; i++)
                solid[i] = px[i].r >= 250 && px[i].g <= 8 && px[i].b <= 8;
            return solid;
        }

        static float Sum(float[] a) { double s = 0; for (int i = 0; i < a.Length; i++) s += a[i]; return (float)s; }

        static float[] Quantize(float[] a, float step)
        {
            var o = new float[a.Length];
            for (int i = 0; i < a.Length; i++) o[i] = Mathf.Round(a[i] / step) * step;
            return o;
        }

        static float L1(float[] a, float[] b)
        {
            double s = 0;
            for (int i = 0; i < a.Length; i++) s += Mathf.Abs(a[i] - b[i]);
            return (float)s;
        }

        static bool Differs(Color32 a, Color32 b, int tol)
            => Mathf.Abs(a.r - b.r) > tol || Mathf.Abs(a.g - b.g) > tol || Mathf.Abs(a.b - b.b) > tol;

        static bool[] Threshold(float[] cover, float at)
        {
            var m = new bool[cover.Length];
            for (int i = 0; i < cover.Length; i++) m[i] = cover[i] >= at;
            return m;
        }

        static bool[] Dilate(bool[] m, int r)
        {
            var o = new bool[m.Length];
            for (int y = 0; y < ShotPx; y++)
            for (int x = 0; x < ShotPx; x++)
            {
                if (!m[y * ShotPx + x]) continue;
                for (int dy = -r; dy <= r; dy++)
                {
                    int yy = y + dy; if (yy < 0 || yy >= ShotPx) continue;
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int xx = x + dx; if (xx < 0 || xx >= ShotPx) continue;
                        o[yy * ShotPx + xx] = true;
                    }
                }
            }
            return o;
        }

        static Vector2 WorldOf(int i, Vector2 aim)
        {
            int x = i % ShotPx, y = i / ShotPx;   // ReadPixels row 0 is the bottom of the frame
            return new Vector2(aim.x + ((x + 0.5f) / ShotPx - 0.5f) * FrameMetres,
                               aim.y + ((y + 0.5f) / ShotPx - 0.5f) * FrameMetres);
        }

        const float LineBinM = 0.25f, LineFromM = -20f, LineToM = 45f;

        /// <summary>The cover along a one-pixel line through <paramref name="aim"/> in the shoreward
        /// direction, offset <paramref name="lateral"/> metres across it, nearest pixel every quarter metre.</summary>
        static float[] LineProfile(float[] cover, Vector2 aim, Vector2 shoreDir, float lateral)
        {
            var across = new Vector2(-shoreDir.y, shoreDir.x);
            int n = Mathf.CeilToInt((LineToM - LineFromM) / LineBinM);
            var p = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 w = aim + shoreDir * (LineFromM + i * LineBinM) + across * lateral;
                Vector2 uv = (w - aim) / FrameMetres + new Vector2(0.5f, 0.5f);
                int x = Mathf.FloorToInt(uv.x * ShotPx), y = Mathf.FloorToInt(uv.y * ShotPx);
                p[i] = (x >= 0 && x < ShotPx && y >= 0 && y < ShotPx) ? cover[y * ShotPx + x] : 0f;
            }
            return p;
        }

        /// <summary>The shift (metres, shoreward positive) that best aligns the beat of <paramref name="later"/>
        /// with that of <paramref name="first"/>: each line's cover divided by its mean over
        /// <paramref name="allMoments"/> (the stationary envelope, staircase and all), summed over lines
        /// 2 m apart from −6 to +6 m across, searched over [<paramref name="lo"/>, <paramref name="hi"/>].</summary>
        static float BeatShiftMetres(float[][] allMoments, float[] first, float[] later, Vector2 aim, Vector2 shoreDir,
                                     float lo, float hi)
        {
            int kLo = Mathf.FloorToInt(lo / LineBinM), kHi = Mathf.CeilToInt(hi / LineBinM);
            var total = new double[kHi - kLo + 1];
            for (float lateral = -6f; lateral <= 6f; lateral += 2f)
            {
                float[] pf = LineProfile(first, aim, shoreDir, lateral);
                float[] pl = LineProfile(later, aim, shoreDir, lateral);
                var env = new float[pf.Length];
                foreach (float[] m in allMoments)
                {
                    float[] pm = LineProfile(m, aim, shoreDir, lateral);
                    for (int i = 0; i < env.Length; i++) env[i] += pm[i] / allMoments.Length;
                }
                var nf = new float[pf.Length]; var nl = new float[pl.Length];
                for (int i = 0; i < env.Length; i++)
                {
                    bool live = env[i] > 0.03f;
                    nf[i] = live ? pf[i] / env[i] - 1f : 0f;
                    nl[i] = live ? pl[i] / env[i] - 1f : 0f;
                }
                for (int k = kLo; k <= kHi; k++)
                {
                    double score = 0;
                    for (int i = 0; i < nf.Length; i++)
                    {
                        int j = i + k;
                        if (j < 0 || j >= nl.Length) continue;
                        score += nf[i] * nl[j];
                    }
                    total[k - kLo] += score;
                }
            }
            int bestK = kLo; double best = double.NegativeInfinity;
            for (int k = kLo; k <= kHi; k++) if (total[k - kLo] > best) { best = total[k - kLo]; bestK = k; }
            return bestK * LineBinM;
        }

        float MeanDepthUnder(float[] cover, ITidalTerrain terrain, float waterLevel)
        {
            Vector2 aim = new Vector2(_cam.transform.position.x, _cam.transform.position.y);
            double sum = 0, w = 0;
            for (int i = 0; i < cover.Length; i += 7)     // every 7th pixel is plenty
            {
                if (cover[i] <= 0.5f) continue;
                float depth = waterLevel - terrain.ElevationAt(WorldOf(i, aim));
                if (depth <= 0f) continue;
                sum += depth * cover[i]; w += cover[i];
            }
            return w > 0 ? (float)(sum / w) : 0f;
        }

        static Color32 Average(Color32 a, Color32 b, Color32 c, Color32 d)
            => new Color32((byte)((a.r + b.r + c.r + d.r) / 4), (byte)((a.g + b.g + c.g + d.g) / 4),
                           (byte)((a.b + b.b + c.b + d.b) / 4), 255);

        /// <summary>The source with every <c>//</c> comment stripped — a guard on what the code DOES
        /// must not trip on a comment that says what it does not do.</summary>
        static string CodeOnly(string src)
        {
            var sb = new StringBuilder();
            foreach (string raw in src.Split('\n'))
            {
                int at = raw.IndexOf("//", StringComparison.Ordinal);
                sb.Append(at >= 0 ? raw.Substring(0, at) : raw).Append('\n');
            }
            return sb.ToString();
        }

        static int CountOf(string s, string needle)
        {
            int n = 0, at = 0;
            while ((at = s.IndexOf(needle, at, StringComparison.Ordinal)) >= 0) { n++; at += needle.Length; }
            return n;
        }

        // =============================================================================================
        //  The scene — the BreakerSurfRenderTests pattern, the region as it ships
        // =============================================================================================

        (Vector2 aim, Vector2 shoreDir) AimAtTheSurf(float waterLevel)
        {
            var terrain = _terrainGo.GetComponent<MainlandTidalTerrain>();
            WaveTrains trains = WaveMath.TrainsFrom(ShotWind, ShotSeaState, GameServices.WaveField);
            BreakerContour contour = BreakerMath.ContourFor(trains.Dominant,
                WaveFetch.Envelope01(0f, GameServices.WaveFetch), GameServices.Breakers);
            Assert.IsTrue(contour.Breaks, "the shot sea must break somewhere, or there is nothing to photograph");

            Vector2 at = BreakerSurfRenderTests.FindTheSurfZone(terrain, waterLevel, contour.BreakDepths.x);
            _cam.transform.position = new Vector3(at.x, at.y, -100f);
            Vector2 shoreDir = BreakerSurfRenderTests.ShoreGradient(terrain, at);
            if (shoreDir == Vector2.zero) shoreDir = Vector2.up;
            Debug.Log($"[bore-look] aimed at {at}, shoreward {shoreDir} — break depth {contour.BreakDepths.x:F2} m, " +
                      $"water level {waterLevel:F2} m");
            return (at, shoreDir);
        }

        Vector2 SteepestBreakPoint(float waterLevel, out float bestXi, out float weight)
        {
            var terrain = _terrainGo.GetComponent<MainlandTidalTerrain>();
            WaveTrains trains = WaveMath.TrainsFrom(ShotWind, ShotSeaState, GameServices.WaveField);
            var settings = GameServices.Breakers;
            BreakerContour contour = BreakerMath.ContourFor(trains.Dominant,
                WaveFetch.Envelope01(0f, GameServices.WaveFetch), settings);
            float h0 = 2f * trains.Dominant.Amplitude;
            Vector2 centre = NineMileCreekBuilder.NineMileCreekSeaCenter;
            Vector2 size = NineMileCreekBuilder.NineMileCreekSeaSize;
            bestXi = 0f;
            Vector2 bestAt = centre;
            const int steps = 160;
            for (int iy = 0; iy <= steps; iy++)
            for (int ix = 0; ix <= steps; ix++)
            {
                var at = new Vector2(centre.x + size.x * (ix / (float)steps - 0.5f),
                                     centre.y + size.y * (iy / (float)steps - 0.5f));
                float depth = waterLevel - terrain.ElevationAt(at);
                if (depth <= 0f || Mathf.Abs(depth - contour.BreakDepths.x) > 0.08f) continue;
                float sx = BreakerMath.BedSlopeAlong(at, Vector2.right, settings.SlopeProbeMeters, terrain);
                float sy = BreakerMath.BedSlopeAlong(at, Vector2.up, settings.SlopeProbeMeters, terrain);
                float xi = BreakerMath.Iribarren(Mathf.Sqrt(sx * sx + sy * sy), h0, trains.Dominant.Wavelength);
                if (xi > bestXi) { bestXi = xi; bestAt = at; }
            }
            weight = BreakerMath.PlungingWeight01(bestXi, in settings);
            return bestAt;
        }

        void BuildTheShore()
        {
            _terrainGo = new GameObject("TidalTerrain");
            var terrain = _terrainGo.AddComponent<MainlandTidalTerrain>();
            NineMileCreekBuilder.ConfigureNineMileCreekTerrain(terrain);
            GameServices.TidalTerrain = terrain;

            var region = AssetDatabase.LoadAssetAtPath<RegionDef>(WaterSceneTemplate.RegionAssetPathFor("NineMileCreek"));
            Assert.IsNotNull(region, "Data/Regions/NineMileCreek.asset must exist to size the ground");
            Assert.That(NineMileCreekBuilder.BuildSplatGround(region), Is.True, "the painted ground must build");
            var ground = GameObject.Find("TerrainSplat");
            if (ground != null) _built.Add(ground);

            var waterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Water.mat");
            Assert.IsNotNull(waterMat, "Water.mat must exist");
            _shippedSurfColor = waterMat.HasProperty("_SurfColor") ? waterMat.GetColor("_SurfColor") : Color.white;
            _shippedLipColor = waterMat.HasProperty("_SurfLipColor") ? waterMat.GetColor("_SurfLipColor") : Color.white;

            _seaGo = new GameObject("Sea");
            _seaGo.SetActive(false);
            _seaGo.transform.position = new Vector3(NineMileCreekBuilder.NineMileCreekSeaCenter.x,
                                                    NineMileCreekBuilder.NineMileCreekSeaCenter.y, 0f);
            var sr = _seaGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = -5;
            sr.sharedMaterial = waterMat;
            var seaTile = WaterSceneTemplate.LoadSpriteAny("Assets/_Project/Art/Tilesets/Water/SeaTile.png");
            if (seaTile != null) sr.sprite = seaTile;
            WaterSceneTemplate.ConfigureSeaPlane(sr, NineMileCreekBuilder.NineMileCreekSeaSize);
            _seaGo.AddComponent<WaterSurface>();
            WaterSceneTemplate.ConfigureLandRegionWater(
                _seaGo, NineMileCreekBuilder.NineMileCreekSeaCenter, NineMileCreekBuilder.NineMileCreekSeaSize,
                NineMileCreekBuilder.NineMileCreekHeightResolution, NineMileCreekBuilder.NineMileCreekHeightMin,
                NineMileCreekBuilder.NineMileCreekHeightMax, terrain.MaxShoreGradient());
            _seaGo.SetActive(true);

            _camGo = new GameObject("BoreShotCam");
            _cam = _camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = FrameMetres * 0.5f;
            _cam.nearClipPlane = 1f;
            _cam.farClipPlane = 400f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.05f, 0.07f, 0.09f, 1f);
            _cam.allowMSAA = false;
            _rt = new RenderTexture(ShotPx, ShotPx, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            _cam.targetTexture = _rt;
        }
    }
}
