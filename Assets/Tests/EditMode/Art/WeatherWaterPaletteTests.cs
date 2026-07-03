using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guards for the WEATHER-DRIVEN water palette MODEL (ADR 0017): the pure 2-axis blend that turns
    /// the deterministic <see cref="EnvironmentSample"/> (sea-state + visibility) into anchor-mood weights, and
    /// the presentation-only ease/strength that <see cref="WaterSurface"/> applies. The GPU blend of the actual
    /// shader props can't be evaluated headless, but the WEIGHTS that decide the mood are a pure function of the
    /// sample + tunables (<see cref="WeatherWaterPalette"/>), locked here without opening Unity:
    ///
    ///   • CALM water -> the serene/base mood (no storm, no fog).
    ///   • RISING sea-state -> the STORM mood (monotonic; the storm weight grows).
    ///   • LOW visibility -> the FOG mood (pale/soft), and fog DOMINATES on top of any sea-state.
    ///   • The 2-axis COMBINE: a foggy storm reads mostly fog; a clear gale reads storm.
    ///   • The ease is frame-rate INDEPENDENT (one step over dt == N steps of dt/N).
    ///   • STRENGTH 0 / DISABLED == identity == today's static look (base anchor only).
    ///   • The BASE/calm anchor resolves to the renderer's LIVE Water.mat when unwired — so strength 0 is the
    ///     live material, and the owner's Water.mat tuning always drives the calm sea (ADR 0017 review fix).
    ///
    /// Determinism (rule 5): every method under test is a pure function — no time, no randomness. The ease is a
    /// presentation-only smoothing; it drives no sim and saves nothing.
    /// </summary>
    public class WeatherWaterPaletteTests
    {
        // The St Peters / component defaults (kept in sync with WaterSurface's serialized defaults) so the tests
        // exercise the shipped mapping, not an invented one.
        private const float SeaThreshold = 0.15f;
        private const float SeaCurve = 1.4f;
        private const float FogThreshold = 0.25f;
        private const float FogCurve = 1.2f;
        private const float CalmReach = 0.8f;

        private static int B => (int)WeatherWaterPalette.Anchor.Base;
        private static int C => (int)WeatherWaterPalette.Anchor.Calm;
        private static int S => (int)WeatherWaterPalette.Anchor.Storm;
        private static int F => (int)WeatherWaterPalette.Anchor.Fog;

        // BlendWeights now takes the CONTINUOUS sea-state axis (EnvironmentSample.SeaState01); the enum
        // helper converts through its band-edge value (SeaStateAxis01 == the axis at the enum flip points),
        // so these tests exercise the exact values the old stepped path produced.
        private static float[] Weights(SeaState sea, float vis) =>
            Weights(WeatherWaterPalette.SeaStateAxis01(sea), vis);

        private static float[] Weights(float sea01, float vis) =>
            WeatherWaterPalette.BlendWeights(sea01, vis, SeaThreshold, SeaCurve, FogThreshold, FogCurve, CalmReach);

        private static float Sum(float[] w)
        {
            float s = 0f;
            for (int i = 0; i < w.Length; i++) s += w[i];
            return s;
        }

        // ===== axis normalisation + shaping ===============================================================

        [Test]
        public void SeaStateAxis_GlassIsZero_StormIsOne_Monotonic()
        {
            Assert.AreEqual(0f, WeatherWaterPalette.SeaStateAxis01(SeaState.Glass), 1e-6f, "Glass = 0");
            Assert.AreEqual(1f, WeatherWaterPalette.SeaStateAxis01(SeaState.Storm), 1e-6f, "Storm = 1");
            float prev = -1f;
            foreach (SeaState st in System.Enum.GetValues(typeof(SeaState)))
            {
                float a = WeatherWaterPalette.SeaStateAxis01(st);
                Assert.GreaterOrEqual(a, prev, "sea-state axis rises monotonically with severity");
                Assert.That(a, Is.InRange(0f, 1f));
                prev = a;
            }
        }

        [Test]
        public void ShapeAxis_BelowThresholdIsZero_AboveRampsMonotonicToOne()
        {
            Assert.AreEqual(0f, WeatherWaterPalette.ShapeAxis(0.1f, 0.15f, 1.4f), 1e-6f,
                "below the threshold the axis amount is 0 (the mood doesn't engage)");
            Assert.AreEqual(1f, WeatherWaterPalette.ShapeAxis(1f, 0.15f, 1.4f), 1e-5f,
                "at raw=1 the amount is full");
            float prev = -1f;
            for (float r = 0f; r <= 1.0001f; r += 0.05f)
            {
                float v = WeatherWaterPalette.ShapeAxis(r, 0.15f, 1.4f);
                Assert.GreaterOrEqual(v, prev - 1e-6f, "shaped axis is monotonic non-decreasing in the raw value");
                Assert.That(v, Is.InRange(0f, 1f));
                prev = v;
            }
        }

        // ===== weights are a valid distribution ===========================================================

        [Test]
        public void Weights_AlwaysSumToOne_AndAreNonNegative_AcrossTheWeatherSpace()
        {
            foreach (SeaState st in System.Enum.GetValues(typeof(SeaState)))
                for (float vis = 0f; vis <= 1.0001f; vis += 0.1f)
                {
                    float[] w = Weights(st, vis);
                    Assert.AreEqual(WeatherWaterPalette.AnchorCount, w.Length);
                    Assert.AreEqual(1f, Sum(w), 1e-4f, $"weights sum to 1 at {st}, vis {vis:0.0}");
                    for (int i = 0; i < w.Length; i++)
                        Assert.GreaterOrEqual(w[i], -1e-6f, "weights are non-negative");
                }
        }

        // ===== sea-state axis: calm -> serene, rising -> storm ============================================

        [Test]
        public void CalmClearSea_ReadsSereneCalmBase_NoStormNoFog()
        {
            // Glass + full visibility: a serene calm/base mood, with zero storm + zero fog.
            float[] w = Weights(SeaState.Glass, 1f);
            Assert.AreEqual(0f, w[S], 1e-5f, "glassy water has NO storm mood");
            Assert.AreEqual(0f, w[F], 1e-5f, "clear air has NO fog mood");
            Assert.Greater(w[C] + w[B], 0.99f, "the look is essentially all calm+base");
            Assert.Greater(w[C], 0f, "with calmReach>0 the glassy sea pulls toward the pure CALM mood");
        }

        [Test]
        public void ContinuousSeaAxis_MovesTheStormMoodSmoothly_NoBandSteps()
        {
            // The de-quantization fix (the owner's "sudden shader change" pop): the target weights are now a
            // CONTINUOUS function of the axis, so a small axis change can only move the storm weight a small
            // amount — no 1/7 band jumps anywhere on the axis. Also monotonic across the fine sweep.
            const int steps = 200;
            float prevStorm = -1f;
            float maxJump = 0f;
            for (int i = 0; i <= steps; i++)
            {
                float sea01 = i / (float)steps;
                float storm = Weights(sea01, 1f)[S];
                Assert.GreaterOrEqual(storm, prevStorm - 1e-6f, $"storm weight monotonic at axis {sea01:0.000}");
                if (prevStorm >= 0f) maxJump = Mathf.Max(maxJump, storm - prevStorm);
                prevStorm = storm;
            }
            // A 1/7 enum step used to move the axis ~0.143 in ONE tick; on this fine sweep any single move
            // must be a small fraction of that (continuity — the pop is structurally gone at the source).
            Assert.Less(maxJump, 0.05f, "the storm mood moves smoothly along the axis (no band-sized jumps)");
        }

        [Test]
        public void RisingSeaState_GrowsTheStormMood_Monotonically()
        {
            // Clear air, sweep Glass -> Storm: the storm weight only ever grows.
            float prevStorm = -1f;
            foreach (SeaState st in System.Enum.GetValues(typeof(SeaState)))
            {
                float[] w = Weights(st, 1f);
                Assert.GreaterOrEqual(w[S], prevStorm - 1e-6f,
                    $"storm mood grows (or holds) as the sea-state rises (at {st})");
                prevStorm = w[S];
            }
            // The extremes: glass has none, a clear storm is dominated by the storm mood.
            Assert.AreEqual(0f, Weights(SeaState.Glass, 1f)[S], 1e-5f, "glass = no storm mood");
            float[] storm = Weights(SeaState.Storm, 1f);
            Assert.Greater(storm[S], 0.6f, "a clear STORM sea is dominated by the storm mood");
            Assert.Greater(storm[S], storm[B] + storm[C], "storm outweighs base+calm in a clear storm");
        }

        // ===== fog axis: low visibility -> pale fog mood, dominating ======================================

        [Test]
        public void LowVisibility_GrowsTheFogMood_Monotonically()
        {
            // Fixed mid sea-state, drop visibility 1 -> 0: the fog weight only ever grows.
            float prevFog = -1f;
            for (float vis = 1f; vis >= -0.0001f; vis -= 0.1f)
            {
                float[] w = Weights(SeaState.Moderate, Mathf.Clamp01(vis));
                Assert.GreaterOrEqual(w[F], prevFog - 1e-6f, $"fog mood grows as visibility falls (vis {vis:0.0})");
                prevFog = w[F];
            }
            Assert.AreEqual(0f, Weights(SeaState.Moderate, 1f)[F], 1e-5f, "clear air = no fog mood");
            Assert.Greater(Weights(SeaState.Moderate, 0f)[F], 0.7f, "a thick smother is dominated by the fog mood");
        }

        [Test]
        public void Fog_DominatesOverSeaState_AFoggyStormReadsMostlyFog()
        {
            // The 2-axis combine ordering: fog pulls the WHOLE mood toward fog on top of the sea-state lerp,
            // so a foggy storm reads mostly FOG (the smother dominates the look), not storm.
            float[] foggyStorm = Weights(SeaState.Storm, 0f);
            Assert.Greater(foggyStorm[F], foggyStorm[S], "a foggy storm reads MORE fog than storm (fog dominates)");
            Assert.Greater(foggyStorm[F], 0.7f, "a full smother dominates the look regardless of sea-state");

            // And a clear gale reads storm (fog absent), confirming the two axes are genuinely independent.
            float[] clearGale = Weights(SeaState.Gale, 1f);
            Assert.AreEqual(0f, clearGale[F], 1e-5f, "a clear gale has no fog");
            Assert.Greater(clearGale[S], clearGale[B], "a clear gale reads storm-led");
        }

        [Test]
        public void FoggyCalm_ReadsPaleSerene_FogWithoutStorm()
        {
            // A foggy CALM sea: lots of fog, but NO storm (low sea-state) — pale serene, not grey-raging.
            float[] w = Weights(SeaState.Calm, 0f);
            Assert.Greater(w[F], 0.7f, "the smother dominates");
            Assert.AreEqual(0f, w[S], 1e-5f, "a calm sea, even in fog, has NO storm mood");
        }

        // ===== calmReach + thresholds (tunables) ==========================================================

        [Test]
        public void CalmReachZero_LeavesGlassyWaterOnTheBase_NoCalmAnchor()
        {
            // calmReach = 0 => the base IS the calm look; the Calm anchor is unused at the lowest sea-state.
            float[] w = WeatherWaterPalette.BlendWeights(WeatherWaterPalette.SeaStateAxis01(SeaState.Glass), 1f,
                SeaThreshold, SeaCurve, FogThreshold, FogCurve, /*calmReach*/0f);
            Assert.AreEqual(0f, w[C], 1e-5f, "calmReach 0 => no pull toward the Calm anchor");
            Assert.AreEqual(1f, w[B], 1e-4f, "glassy clear water with calmReach 0 reads fully as the BASE preset");
        }

        [Test]
        public void RaisingSeaThreshold_DelaysTheStormMood()
        {
            // A higher sea-state threshold keeps the sea calm-ish to a higher sea-state (the storm bites later).
            float lowThr = WeatherWaterPalette.BlendWeights(WeatherWaterPalette.SeaStateAxis01(SeaState.Light), 1f,
                0.1f, SeaCurve, FogThreshold, FogCurve, CalmReach)[S];
            float highThr = WeatherWaterPalette.BlendWeights(WeatherWaterPalette.SeaStateAxis01(SeaState.Light), 1f,
                0.5f, SeaCurve, FogThreshold, FogCurve, CalmReach)[S];
            Assert.Less(highThr, lowThr, "a higher sea-state threshold delays the storm mood (less storm at a Light sea)");
        }

        // ===== the ease (presentation-only smoothing) =====================================================

        [Test]
        public void EaseWeights_MovesTowardTheTarget_AndReNormalizes()
        {
            var smoothed = new float[] { 1f, 0f, 0f, 0f };          // start: all base
            var target = new float[] { 0f, 0f, 1f, 0f };            // target: all storm
            WeatherWaterPalette.EaseWeights(smoothed, target, responseTime: 1f, dt: 0.5f);
            Assert.Less(smoothed[B], 1f, "the base weight eased down toward the target");
            Assert.Greater(smoothed[S], 0f, "the storm weight eased up toward the target");
            Assert.AreEqual(1f, Sum(smoothed), 1e-5f, "the eased weights stay a valid distribution (sum 1)");
        }

        [Test]
        public void EaseWeights_ZeroResponseTime_SnapsToTarget()
        {
            var smoothed = new float[] { 1f, 0f, 0f, 0f };
            var target = new float[] { 0f, 0f, 1f, 0f };
            WeatherWaterPalette.EaseWeights(smoothed, target, responseTime: 0f, dt: 0.1f);
            for (int i = 0; i < smoothed.Length; i++)
                Assert.AreEqual(target[i], smoothed[i], 1e-5f, "responseTime 0 snaps the mood to the target (no ease)");
        }

        [Test]
        public void EaseWeights_IsFrameRateIndependent_OneStepEqualsManySubsteps()
        {
            // The exponential ease composes: one step over dt reaches the same state as N steps of dt/N.
            const float tau = 2f, dt = 1f;
            var target = new float[] { 0f, 0f, 1f, 0f };

            var once = new float[] { 1f, 0f, 0f, 0f };
            WeatherWaterPalette.EaseWeights(once, target, tau, dt);

            var many = new float[] { 1f, 0f, 0f, 0f };
            const int n = 20;
            for (int i = 0; i < n; i++)
                WeatherWaterPalette.EaseWeights(many, target, tau, dt / n);

            for (int i = 0; i < once.Length; i++)
                Assert.AreEqual(once[i], many[i], 1e-3f,
                    "the ease is frame-rate independent (one big step == many small steps to the same place)");
        }

        // ===== master strength: 0 / disabled == today's look ==============================================

        [Test]
        public void ApplyStrength_Zero_CollapsesToTheBaseAnchor_TodaysLook()
        {
            // The headline opt-in: strength 0 => all weight on the BASE mood at EVERY weather (today's static look).
            foreach (SeaState st in System.Enum.GetValues(typeof(SeaState)))
                for (float vis = 0f; vis <= 1.0001f; vis += 0.25f)
                {
                    float[] w = Weights(st, Mathf.Clamp01(vis));
                    WeatherWaterPalette.ApplyStrengthInPlace(w, 0f);
                    Assert.AreEqual(1f, w[B], 1e-5f, $"strength 0 => base only at {st}, vis {vis:0.00} (today's look)");
                    Assert.AreEqual(0f, w[C] + w[S] + w[F], 1e-5f, "no mood departs from the base at strength 0");
                }
        }

        [Test]
        public void ApplyStrength_One_LeavesTheBlendUnchanged()
        {
            float[] full = Weights(SeaState.Gale, 0.3f);
            float[] expected = (float[])full.Clone();
            WeatherWaterPalette.ApplyStrengthInPlace(full, 1f);
            for (int i = 0; i < full.Length; i++)
                Assert.AreEqual(expected[i], full[i], 1e-5f, "strength 1 = the full weather-driven blend, unchanged");
        }

        [Test]
        public void ApplyStrength_Half_MovesPartwayFromBaseTowardTheBlend()
        {
            float[] storm = Weights(SeaState.Storm, 1f);   // storm-dominated
            float fullStorm = storm[S];
            float[] half = (float[])storm.Clone();
            WeatherWaterPalette.ApplyStrengthInPlace(half, 0.5f);
            Assert.Less(half[S], fullStorm, "half strength reads less storm than the full blend");
            Assert.Greater(half[S], 0f, "but more storm than the base-only look");
            Assert.Greater(half[B], storm[B], "half strength keeps more of the base mood than the full blend");
            Assert.AreEqual(1f, Sum(half), 1e-5f, "still a valid distribution");
        }

        // ===== degenerate safety ==========================================================================

        [Test]
        public void NormalizeInPlace_AllZero_FallsBackToBase()
        {
            var w = new float[] { 0f, 0f, 0f, 0f };
            WeatherWaterPalette.NormalizeInPlace(w);
            Assert.AreEqual(1f, w[B], 1e-6f, "a degenerate all-zero set falls back to the base mood (always a valid blend)");
            Assert.AreEqual(1f, Sum(w), 1e-6f);
        }

        // ===== BASE anchor resolves to the LIVE Water.mat when unwired (ADR 0017 review fix) ================
        // The latent trap the review caught: if the BASE/calm anchor were pinned to a preset COPY of Water.mat,
        // weather-off / strength-0 would read that COPY, not the live Water.mat — so the owner's constant
        // Water.mat tuning would silently NOT change St Peters' calm sea. The fix: leave the base anchor UNWIRED
        // so WaterSurface.ResolveBaseAnchor falls back to the renderer's own sharedMaterial (= the live
        // Water.mat). These guard that resolve decision headlessly (pure — no scene, no rendering): an unwired
        // base IS the live material, an explicit base PINS to that preset, and combined with strength 0 the
        // blend's base/calm term is therefore the live material (the owner's tuning always flows through).

        // Real Materials to assert reference identity through ResolveBaseAnchor. Built from a built-in shader and
        // only inspected by reference (never rendered), so this is safe headless. Skips cleanly if the editor
        // can't resolve a shader (defensive — shaders do compile in this project's CI).
        private Material _live;       // stands in for the Sea's live Water.mat (the renderer's sharedMaterial)
        private Material _presetCopy; // stands in for a pinned preset COPY (e.g. Water_NorthAtlantic.mat)

        [SetUp]
        public void SetUp()
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader == null) return;   // resolved/destroyed defensively; the tests Assert.Ignore if null
            _live = new Material(shader) { name = "LiveWaterMatStandIn" };
            _presetCopy = new Material(shader) { name = "PinnedPresetCopyStandIn" };
        }

        [TearDown]
        public void TearDown()
        {
            if (_live != null) Object.DestroyImmediate(_live);
            if (_presetCopy != null) Object.DestroyImmediate(_presetCopy);
            _live = _presetCopy = null;
        }

        [Test]
        public void ResolveBaseAnchor_UnwiredBase_FallsBackToTheLiveSharedMaterial()
        {
            if (_live == null) Assert.Ignore("No built-in shader to construct a stand-in Material headlessly.");
            // No explicit base preset assigned (the St Peters default) → the base anchor IS the renderer's own
            // live material (Water.mat). This is the headline fix: the calm baseline tracks the owner's tuning.
            Material resolved = WaterSurface.ResolveBaseAnchor(/*explicitBase*/ null, /*sharedMaterial*/ _live);
            Assert.AreSame(_live, resolved,
                "an UNWIRED base anchor resolves to the renderer's live Water.mat (sharedMaterial) — not a preset copy");
        }

        [Test]
        public void ResolveBaseAnchor_ExplicitBase_PinsToThatPreset_NotTheLiveMaterial()
        {
            if (_live == null || _presetCopy == null)
                Assert.Ignore("No built-in shader to construct stand-in Materials headlessly.");
            // Assigning an explicit base PINS the calm look to that preset copy (the opt-in escape) — it then
            // stops tracking the live Water.mat. The fix keeps this available; St Peters just doesn't use it.
            Material resolved = WaterSurface.ResolveBaseAnchor(/*explicitBase*/ _presetCopy, /*sharedMaterial*/ _live);
            Assert.AreSame(_presetCopy, resolved, "an explicit base anchor wins (pins the calm look to that preset)");
            Assert.AreNotSame(_live, resolved, "and is NOT the live material (the pin deliberately freezes the calm look)");
        }

        [Test]
        public void ResolveBaseAnchor_BothNull_IsNull_BlendNoOpsForTheBaseTerm()
        {
            // A partial wiring with no base AND no shared material (e.g. Water.mat not imported yet): the base
            // term simply has no material, so BlendMoodProps skips it — the surface reads its own material. No
            // throw, no false baseline.
            Assert.IsNull(WaterSurface.ResolveBaseAnchor(null, null),
                "no explicit base and no shared material → null (the base term no-ops; the surface keeps its own look)");
        }

        [Test]
        public void StrengthZero_PlusUnwiredBase_TheCalmTermIsTheLiveMaterial()
        {
            // The end-to-end guarantee the review asked for, in weight-space: at strength 0 the blend collapses
            // to ALL weight on the BASE anchor (every weather), and — because the base is UNWIRED — that anchor
            // is the live Water.mat (ResolveBaseAnchor). So weather-enabled + strength 0 reproduces the LIVE
            // material's mood at every condition, not a preset copy. (WaterSurface.BlendMoodProps then reads the
            // base anchor's per-key values; with all weight on it, the output == that material's values.)
            if (_live == null) Assert.Ignore("No built-in shader to construct a stand-in Material headlessly.");
            Material baseAnchor = WaterSurface.ResolveBaseAnchor(null, _live);
            Assert.AreSame(_live, baseAnchor, "the unwired base anchor is the live material");

            foreach (SeaState st in System.Enum.GetValues(typeof(SeaState)))
                for (float vis = 0f; vis <= 1.0001f; vis += 0.25f)
                {
                    float[] w = Weights(st, Mathf.Clamp01(vis));
                    WeatherWaterPalette.ApplyStrengthInPlace(w, 0f);
                    Assert.AreEqual(1f, w[B], 1e-5f,
                        $"strength 0 → all weight on the (live-material) base anchor at {st}, vis {vis:0.00}");
                    Assert.AreEqual(0f, w[C] + w[S] + w[F], 1e-5f,
                        "no storm/fog/calm preset contributes — the calm baseline is purely the live Water.mat");
                }
        }
    }
}
