using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>WIND FETCH (ADR 0027 #1)</b> — the Tier B model both the water shader and every hull
    /// consumer read, so the lee the player SEES is the lee the boat FEELS. All headless: the march
    /// runs against a hand-built <see cref="ITidalTerrain"/>, never a texture or a scene.
    ///
    /// <para>What these pin, in order of what would hurt most if it broke:</para>
    /// <list type="number">
    /// <item><b>The passthrough</b> — strength 0 is EXACTLY 1, and the field sampled through it is
    /// bit-identical to the field sampled without it. This is what lets the model ship OFF with the
    /// sea (drawn AND ridden) unchanged.</item>
    /// <item><b>The [unroll] seam</b> — <see cref="WaveFetch.MarchSteps"/> against the shader's
    /// <c>FETCH_MARCH_STEPS</c>, read out of the shader source. ADR 0027 states the fixed iteration
    /// count as an implementation constraint (the #96 magenta trap); this is the tripwire that goes
    /// red if the two halves ever drift.</item>
    /// <item><b>The physics that makes it worth having</b> — land upwind shelters, land SHADOWS
    /// everything behind it, and an exposed shore is untouched.</item>
    /// <item><b>The whitecap consequence</b> — a lee loses its crest factor, because the envelope
    /// scales the height but not the amplitude normalizer.</item>
    /// <item><b>The C#-twin traps</b> — the smoothstep EDGE gate (not <c>Mathf.SmoothStep</c>), and
    /// the fact that fetch touches amplitude only, never wavelength or phase speed.</item>
    /// </list>
    /// </summary>
    public class WaveFetchTests
    {
        private const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";

        /// <summary>Flat seabed at a fixed elevation — "everywhere this deep".</summary>
        private sealed class FlatTerrain : ITidalTerrain
        {
            private readonly float _elevation;
            public FlatTerrain(float elevation) { _elevation = elevation; }
            public float ElevationAt(Vector2 worldPos) => _elevation;
        }

        /// <summary>Deep water everywhere EXCEPT a band of land: <c>x</c> in [min, max] stands proud.
        /// A wall of coast, so a march either crosses it or does not.</summary>
        private sealed class LandBandTerrain : ITidalTerrain
        {
            private readonly float _min, _max;
            public LandBandTerrain(float min, float max) { _min = min; _max = max; }
            public float ElevationAt(Vector2 worldPos)
                => worldPos.x >= _min && worldPos.x <= _max ? 5f : -10f;
        }

        private static WaveFetchSettings On(float strength = 1f)
        {
            WaveFetchSettings s = WaveFetchSettings.Default;
            s.Strength = strength;
            return s;
        }

        // Water level 0 => elevation -10 is 10 m deep (open water), +5 is 5 m of dry land.
        private const float WaterLevel = 0f;
        private static readonly Vector2 WindEast = new Vector2(6f, 0f);   // blows toward +x => upwind is -x

        // ==== (1) THE PASSTHROUGH — the contract that lets this ship OFF ==============================

        [Test]
        public void DefaultSettings_ShipOff()
        {
            Assert.AreEqual(0f, WaveFetchSettings.Default.Strength,
                "ADR 0027's discipline: every item defaults to passthrough so the shipped sea is " +
                "byte-identical until the owner dials it in. Fetch is Tier B — it moves the hull too.");
        }

        [Test]
        public void StrengthZero_IsExactlyOne_ForEveryFetch()
        {
            WaveFetchSettings off = WaveFetchSettings.Default;   // strength 0
            for (float f = 0f; f <= 1f; f += 0.05f)
                Assert.AreEqual(1f, WaveFetch.Envelope01(f, in off),
                    "strength 0 must return EXACTLY 1 — not 1±epsilon. A float round-trip here would " +
                    "make the shipped sea differ from the pre-fetch sea in the last bit.");
        }

        [Test]
        public void StrengthZero_SkipsTheMarchEntirely()
        {
            // A terrain that would fail the test if it were ever sampled proves the early-out is real
            // (it is what keeps the model free — 24 height-map taps per pixel — while it is off).
            var trap = new ThrowingTerrain();
            Assert.AreEqual(1f, WaveFetch.EnvelopeAt(Vector2.zero, WindEast, WaterLevel, trap,
                                                     WaveFetchSettings.Default));
        }

        private sealed class ThrowingTerrain : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos)
            {
                Assert.Fail("The march ran with the fetch model OFF — the strength early-out is broken.");
                return 0f;
            }
        }

        [Test]
        public void EnvelopeOne_LeavesTheFieldBitIdentical()
        {
            WaveTrains trains = WaveMath.TrainsFrom(new Vector2(5f, -3f), 0.7f,
                                                    WaveFieldSettings.Default);
            for (int i = 0; i < 12; i++)
            {
                var pos = new Vector2(i * 3.1f - 15f, i * -2.3f + 4f);
                double t = 40.0 + i * 7.5;
                WaveSample bare = WaveMath.Sample(pos, t, in trains);
                WaveSample through = WaveMath.Sample(pos, t, in trains, 1f);

                Assert.AreEqual(bare.Height, through.Height, "height");
                Assert.AreEqual(bare.Slope.x, through.Slope.x, "slope.x");
                Assert.AreEqual(bare.Slope.y, through.Slope.y, "slope.y");
                Assert.AreEqual(bare.CrestFactor, through.CrestFactor, "crest");
            }
        }

        // ==== (2) THE [unroll] SEAM — the tripwire ====================================================

        [Test]
        public void MarchStepCount_MatchesTheShader()
        {
            string path = Path.Combine(Application.dataPath, "..", ShaderPath);
            Assert.IsTrue(File.Exists(path), "Water shader not found at " + ShaderPath);

            Match m = Regex.Match(File.ReadAllText(path), @"#define\s+FETCH_MARCH_STEPS\s+(\d+)");
            Assert.IsTrue(m.Success,
                "FETCH_MARCH_STEPS is gone from the water shader. It is one half of a seam with " +
                "WaveFetch.MarchSteps — if the march moved, both halves move in the same commit.");
            Assert.AreEqual(WaveFetch.MarchSteps, int.Parse(m.Groups[1].Value),
                "The C# march and the HLSL march take a different number of steps, so the sea the " +
                "hull rides is not the sea the shader draws. ⚠️ The count must also stay a COMPILE-TIME " +
                "constant: ADR 0027 flags [unroll] over a runtime bound as a known magenta trap.");
        }

        // ==== (3) THE PHYSICS ========================================================================

        [Test]
        public void OpenWater_IsFullFetch_AndUntouched()
        {
            var deep = new FlatTerrain(-10f);
            float fetch = WaveFetch.Fetch01(Vector2.zero, WindEast, WaterLevel, deep, On());
            Assert.AreEqual(1f, fetch, 1e-4f, "nothing upwind but water => full fetch");

            // A fully exposed shore must be EXACTLY the sea the field was tuned against, at any strength.
            Assert.AreEqual(1f, WaveFetch.EnvelopeAt(Vector2.zero, WindEast, WaterLevel, deep, On()), 1e-4f);
        }

        [Test]
        public void LandImmediatelyUpwind_CollapsesToTheLeeFloor()
        {
            // Wind blows toward +x, so the march runs toward -x. Put land just upwind of the origin.
            var lee = new LandBandTerrain(-1000f, -1f);
            float fetch = WaveFetch.Fetch01(Vector2.zero, WindEast, WaterLevel, lee, On());
            Assert.Less(fetch, 0.05f, "land one step upwind => essentially no fetch");

            WaveFetchSettings s = On();
            float env = WaveFetch.Envelope01(fetch, in s);
            Assert.AreEqual(s.LeeFloor, env, 0.02f,
                "a deep lee must land on the lee floor — sheltered, deliberately NOT glass");
        }

        [Test]
        public void LandShadowsTheOpenWaterBehindIt()
        {
            // A narrow island upwind, with unlimited ocean beyond it. Fetch is what reaches YOU: the
            // sea on the far side of an island is not fetch for this position. The product accumulator
            // is what encodes that, and it is the single most load-bearing line in the march.
            var island = new LandBandTerrain(-30f, -26f);
            float shadowed = WaveFetch.Fetch01(Vector2.zero, WindEast, WaterLevel, island, On());
            float open = WaveFetch.Fetch01(Vector2.zero, WindEast, WaterLevel,
                                           new FlatTerrain(-10f), On());

            Assert.Less(shadowed, open, "the island must reduce the fetch");
            // 24 steps x 4 m = 96 m of reach; the island is first hit at step 7 (x = -28), so exactly
            // the six steps before it survive and the 17 beyond it — deep water — count for nothing.
            Assert.AreEqual(6f / WaveFetch.MarchSteps, shadowed, 0.02f,
                "only the water BEFORE the island counts — everything beyond it is shadowed");
        }

        [Test]
        public void FetchGrowsAsYouLeaveTheLee()
        {
            var coast = new LandBandTerrain(-1000f, 0f);   // land everywhere at/below x = 0
            float previous = -1f;
            for (int i = 0; i <= 24; i++)
            {
                float fetch = WaveFetch.Fetch01(new Vector2(i * 4f, 0f), WindEast, WaterLevel, coast, On());
                Assert.GreaterOrEqual(fetch, previous - 1e-5f,
                    "fetch must not fall as the position moves further out from the lee shore");
                previous = fetch;
            }
            Assert.Greater(previous, 0.9f, "far enough out, the coast stops mattering");
        }

        [Test]
        public void UpwindIsIntoTheWind_NotDownwind()
        {
            // Land to the WEST (-x). A wind blowing EAST (+x) has come over that land: sheltered.
            // The same land with the wind blowing WEST has come over open ocean: exposed. If the march
            // ran downwind these two would be swapped, and every lee in the game would be on the wrong
            // side of the island — the defect a sign error here produces.
            var westLand = new LandBandTerrain(-1000f, -1f);
            float sheltered = WaveFetch.Fetch01(Vector2.zero, WindEast, WaterLevel, westLand, On());
            float exposed = WaveFetch.Fetch01(Vector2.zero, -WindEast, WaterLevel, westLand, On());

            Assert.Less(sheltered, 0.1f, "wind off the land => sheltered");
            Assert.Greater(exposed, 0.9f, "wind off the sea => exposed");
        }

        [Test]
        public void TheShoreGateIsSmooth_SoTheTideCannotPopIt()
        {
            // Sweep the water level across a shoal at -0.4 m and assert the fetch never jumps. A hard
            // depth > 0 cutoff would step here, and the step would arrive in the waves the HULL rides,
            // on the tide's schedule.
            var shoal = new LandBandTerrain(-20f, -16f);   // elevation +5 there; deep elsewhere
            var settings = On();
            float previous = WaveFetch.Fetch01(Vector2.zero, WindEast, 4.0f, shoal, in settings);
            for (float level = 4.0f; level <= 6.0f; level += 0.02f)
            {
                float fetch = WaveFetch.Fetch01(Vector2.zero, WindEast, level, shoal, in settings);
                // A hard `depth > 0` cutoff would jump ~0.87 here; the smooth gate's worst step is ~0.06.
                Assert.Less(Mathf.Abs(fetch - previous), 0.1f,
                    $"fetch jumped by more than a smooth gate allows as the tide crossed the shoal " +
                    $"(water level {level:0.00})");
                previous = fetch;
            }
        }

        // ==== (4) THE WHITECAP CONSEQUENCE ===========================================================

        [Test]
        public void ALeeLosesItsWhitecaps()
        {
            // The crest factor is height / TotalAmplitude. The envelope scales the numerator and NOT
            // the denominator, so foam dies in a lee with nothing wired to make it happen. That is
            // correct physics AND the reason the envelope must never be folded into the trains.
            WaveTrains trains = WaveMath.TrainsFrom(new Vector2(8f, 0f), 0.9f, WaveFieldSettings.Default);

            bool sawACrest = false;
            for (int i = 0; i < 200; i++)
            {
                var pos = new Vector2(i * 0.37f, i * 0.11f);
                float full = WaveMath.Sample(pos, 12.0, in trains, 1f).CrestFactor;
                float lee = WaveMath.Sample(pos, 12.0, in trains, 0.25f).CrestFactor;
                Assert.LessOrEqual(lee, full + 1e-6f, "a lee can never foam MORE than the open sea");
                if (full > 0.2f) { sawACrest = true; Assert.Less(lee, full, "the lee must foam less"); }
            }
            Assert.IsTrue(sawACrest, "the sweep never found a crest — the fixture proves nothing");
        }

        [Test]
        public void EnvelopeScalesHeightAndSlopeTogether()
        {
            // Both terms take the SAME factor: the slope must stay the derivative of the height, which
            // is the number the deck tilt, the caustics and the ripple windward gate all read.
            WaveTrains trains = WaveMath.TrainsFrom(new Vector2(-4f, 7f), 0.65f, WaveFieldSettings.Default);
            const float env = 0.4f;
            for (int i = 0; i < 20; i++)
            {
                var pos = new Vector2(i * 1.9f - 10f, i * -0.7f);
                WaveSample full = WaveMath.Sample(pos, 33.0, in trains, 1f);
                WaveSample faded = WaveMath.Sample(pos, 33.0, in trains, env);

                Assert.AreEqual(full.Height * env, faded.Height, 1e-4f, "height");
                Assert.AreEqual(full.Slope.x * env, faded.Slope.x, 1e-4f, "slope.x");
                Assert.AreEqual(full.Slope.y * env, faded.Slope.y, 1e-4f, "slope.y");
            }
        }

        // ==== (5) THE TWIN TRAPS + the shape functions ================================================

        [Test]
        public void SmoothstepEdge_IsAnEdgeGate_NotMathfSmoothStep()
        {
            // Mathf.SmoothStep(a, b, t) is a smooth LERP BETWEEN TWO VALUES; HLSL's smoothstep(e0, e1, x)
            // is an EDGE GATE returning 0..1. Wiring the Mathf overload would silently pin the wrong law
            // everywhere — the trap WaterRipple was written around and this class inherits.
            Assert.AreEqual(0f, WaveFetch.SmoothstepEdge(2f, 6f, 1f), 1e-6f, "below the low edge => 0");
            Assert.AreEqual(0f, WaveFetch.SmoothstepEdge(2f, 6f, 2f), 1e-6f, "at the low edge => 0");
            Assert.AreEqual(0.5f, WaveFetch.SmoothstepEdge(2f, 6f, 4f), 1e-6f, "midpoint => 0.5");
            Assert.AreEqual(1f, WaveFetch.SmoothstepEdge(2f, 6f, 6f), 1e-6f, "at the high edge => 1");
            Assert.AreEqual(1f, WaveFetch.SmoothstepEdge(2f, 6f, 99f), 1e-6f, "above => 1");

            // The distinguishing value: Mathf.SmoothStep(2, 6, 4) is 6 (t=4 clamps past the top).
            Assert.AreNotEqual(Mathf.SmoothStep(2f, 6f, 4f), WaveFetch.SmoothstepEdge(2f, 6f, 4f));
        }

        [Test]
        public void Amplitude01_IsExactAtTheEnds_AndMonotoneBetween()
        {
            const float floor = 0.3f, exponent = 1.3f;
            Assert.AreEqual(floor, WaveFetch.Amplitude01(0f, exponent, floor), 1e-6f, "fetch 0 => the floor");
            Assert.AreEqual(1f, WaveFetch.Amplitude01(1f, exponent, floor), 1e-6f,
                "fetch 1 => EXACTLY 1: a fully exposed shore is the sea the field was tuned against");

            float previous = -1f;
            for (float f = 0f; f <= 1f; f += 0.02f)
            {
                float a = WaveFetch.Amplitude01(f, exponent, floor);
                Assert.GreaterOrEqual(a, previous, "amplitude must grow with fetch");
                Assert.That(a, Is.InRange(floor - 1e-5f, 1f + 1e-5f));
                previous = a;
            }
        }

        [Test]
        public void Band01_DefaultsSmooth_AndQuantizesWhenAsked()
        {
            Assert.AreEqual(0f, WaveFetchSettings.Default.Bands,
                "the fetch band ships OFF on purpose — it quantizes GEOMETRY the hull rides, not colour");

            for (float v = 0f; v <= 1f; v += 0.05f)
                Assert.AreEqual(v, WaveFetch.Band01(v, 0f), 1e-6f, "bands < 2 leaves the value smooth");

            // 3 bands => steps at 0, 0.5, 1.
            Assert.AreEqual(0f, WaveFetch.Band01(0.2f, 3f), 1e-6f);
            Assert.AreEqual(0.5f, WaveFetch.Band01(0.45f, 3f), 1e-6f);
            Assert.AreEqual(1f, WaveFetch.Band01(0.9f, 3f), 1e-6f);
        }

        [Test]
        public void FetchTouchesAmplitudeOnly_NeverWavelengthOrSpeed()
        {
            // The standing temptation, and the ADR 0027 P2 audit's warning: WaveTrain.PhaseSpeed IS the
            // dispersion relation, derived from the wavelength at construction. A fetch model that also
            // scaled wavelength would double-apply a law the field already carries — and would break the
            // analytic slope, which assumes ONE k per train across space. The envelope is a scalar on the
            // OUTPUT, so a train's wavelength and speed are untouched by construction; this pins it.
            WaveTrains trains = WaveMath.TrainsFrom(new Vector2(9f, 2f), 0.8f, WaveFieldSettings.Default);
            for (int i = 0; i < trains.Count; i++)
            {
                WaveTrain train = trains[i];
                float expected = Mathf.Sqrt(WaveFieldSettings.Default.Gravity * train.Wavelength
                                            / (2f * Mathf.PI));
                Assert.AreEqual(expected, train.PhaseSpeed, 1e-4f,
                    "the dispersion relation must remain the ONLY source of a train's speed");
            }

            // And the phase advances at exactly the same rate through the envelope: a fetched sea is a
            // SMALLER sea, never a slower or a longer one.
            var pos = new Vector2(3f, 1f);
            float a0 = WaveMath.Sample(pos, 10.0, in trains, 1f).Height;
            float a1 = WaveMath.Sample(pos, 10.0, in trains, 0.5f).Height;
            Assert.AreEqual(a0 * 0.5f, a1, 1e-4f);
        }

        // ==== (6) DETERMINISM + the guards ===========================================================

        [Test]
        public void IsDeterministic()
        {
            var coast = new LandBandTerrain(-40f, -30f);
            var settings = On(0.8f);
            for (int i = 0; i < 40; i++)
            {
                var pos = new Vector2(i * 2.3f - 40f, i * 1.7f - 20f);
                var wind = new Vector2(Mathf.Cos(i * 0.4f) * 7f, Mathf.Sin(i * 0.4f) * 7f);
                float a = WaveFetch.EnvelopeAt(pos, wind, WaterLevel, coast, in settings);
                float b = WaveFetch.EnvelopeAt(pos, wind, WaterLevel, coast, in settings);
                Assert.AreEqual(a, b, "same inputs must give the same envelope, forever (rule 5)");
                Assert.That(a, Is.InRange(0f, 1f),
                    "the envelope must stay in [0, 1] — that bound is what keeps the watertight hull " +
                    "clamp valid at every strength (it can only ever over-dry, never flood)");
            }
        }

        [Test]
        public void DeadCalmAndNoTerrain_BothPassThrough()
        {
            var coast = new LandBandTerrain(-1000f, -1f);
            Assert.AreEqual(1f, WaveFetch.Fetch01(Vector2.zero, Vector2.zero, WaterLevel, coast, On()),
                "no wind => no upwind direction to march; the field's own amplitudes silence a calm sea");
            Assert.AreEqual(1f, WaveFetch.Fetch01(Vector2.zero, WindEast, WaterLevel, null, On()),
                "no height map => open water everywhere, matching the shader without _USE_HEIGHTTEX");
        }

        [Test]
        public void MarchCoordinatesAreWorldQuantized()
        {
            // The crawl law: the shader's march samples on Pixelize'd world coordinates so the fetch
            // cannot slide under camera translation, and this twin quantizes identically so the two
            // sides march the same points.
            Assert.AreEqual(1f / WaveFetch.PixelsPerUnit,
                            WaveFetch.Pixelize(new Vector2(0.05f, 0f)).x, 1e-6f);
            Assert.AreEqual(0f, WaveFetch.Pixelize(new Vector2(0.02f, 0f)).x, 1e-6f);
            Assert.AreEqual(-1f / WaveFetch.PixelsPerUnit,
                            WaveFetch.Pixelize(new Vector2(-0.01f, 0f)).x, 1e-6f);
        }

        [Test]
        public void StaleAssetWithZeroedShape_IsSafe()
        {
            // Every field of a pre-2026-07-31 asset deserializes as 0. Strength 0 is the passthrough, so
            // the sea is safe — but dialling the strength up on such an asset must not divide by zero or
            // collapse the march onto one point either.
            var stale = default(WaveFetchSettings);
            Assert.AreEqual(1f, WaveFetch.Envelope01(0.5f, in stale), "zeroed => off => passthrough");

            stale.Strength = 1f;   // the owner dials it up on the stale asset
            float env = WaveFetch.EnvelopeAt(Vector2.zero, WindEast, WaterLevel,
                                             new FlatTerrain(-10f), in stale);
            Assert.That(env, Is.InRange(0f, 1f));
            Assert.IsFalse(float.IsNaN(env), "a zeroed shape must not produce NaN");
        }
    }
}
