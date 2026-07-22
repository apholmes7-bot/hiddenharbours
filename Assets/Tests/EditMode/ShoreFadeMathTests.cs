using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The SHORE SEAM proof (ADR 0023): displaced water must reach exactly zero displacement at the
    /// walkable waterline — at every tide — or the coast tears. These tests are the deterministic,
    /// headless half of the evidence (the render half lives in the ShoreSeamProof editor harness
    /// and its Evidence~ captures):
    ///
    ///  (1) CONTOUR-PINNED — displacement is exactly 0 at and beyond the depth-0 contour, for the
    ///      real production wave field, at three tide levels (the contour the fade pins to IS the
    ///      moving waterline, because depth is the one shared depth rule).
    ///  (2) NO TEAR — along shore transects of the reference sea (gentle flats, a beach, a steep
    ///      shelf, both shore orientations): no displaced water pixel ever crosses the waterline
    ///      (no overlap), and the screen-y mapping stays strictly monotone (no fold/shear) —
    ///      including with the 100%-envelope event deliberately parked mid-band (the adversarial
    ///      placement).
    ///  (3) OPEN SEA UNTOUCHED — past the falloff band the fade is exactly 1, so the spike's
    ///      readability event (t = 1513.5 s, the found-not-authored 100%-envelope crest) displaces
    ///      at full exaggeration, bit-identical to the un-seamed value.
    ///
    /// The scenario is the 3D-water spike's deterministic sea (branch spike/3d-water, VERDICT.md):
    /// wind (−5.4, −9.33) m/s, seaState 0.75, WaveFieldSettings.Default → 4 trains, envelope
    /// ≈ 1.047 m. Pinning the event here also turns the ADR 0023 acceptance bar into a regression
    /// guard: if the wave model changes so the event moves, these tests say so.
    /// </summary>
    public class ShoreFadeMathTests
    {
        // ---- the reference sea (the spike's deterministic scenario — found, not authored) --------
        private static readonly Vector2 Wind = new Vector2(-5.4f, -9.33f);
        private const float SeaState = 0.75f;
        private const double EventTime = 1513.5;                    // the 100%-envelope moment
        private static readonly Vector2 EventPos = new Vector2(-6.5f, 2.1f);
        private const float Exaggeration = 1.5f;                    // the ADR 0023 sweet spot

        private static WaveTrains Trains() =>
            WaveMath.TrainsFrom(Wind, SeaState, WaveFieldSettings.Default);

        // ---- Fade01: the contract in isolation ---------------------------------------------------

        [Test]
        public void Fade_IsExactlyZero_AtAndBeyondTheWaterline()
        {
            foreach (float band in new[] { 0.05f, 0.5f, 3f })
            foreach (float depth in new[] { 0f, -0.001f, -2f, -100f })
                Assert.AreEqual(0f, ShoreFadeMath.Fade01(depth, band),
                    $"fade must be EXACTLY 0 at depth {depth} (band {band}) — the waterline pin");
        }

        [Test]
        public void Fade_IsExactlyOne_AtAndPastTheBand()
        {
            foreach (float band in new[] { 0.05f, 0.5f, 3f })
            foreach (float factor in new[] { 1f, 1.001f, 4f, 1000f })
                Assert.AreEqual(1f, ShoreFadeMath.Fade01(band * factor, band),
                    $"fade must be EXACTLY 1 at depth {band * factor} (band {band}) — open sea untouched");
        }

        [Test]
        public void Fade_IsMonotone_AcrossTheBand()
        {
            const float band = 1.25f;
            float prev = -1f;
            for (int i = 0; i <= 200; i++)
            {
                float fade = ShoreFadeMath.Fade01(band * (i / 200f), band);
                Assert.GreaterOrEqual(fade, prev, "fade must never decrease with depth");
                Assert.That(fade, Is.InRange(0f, 1f));
                prev = fade;
            }
        }

        [Test]
        public void Fade_DegenerateBand_IsAHardStep_NeverDivergent()
        {
            Assert.AreEqual(0f, ShoreFadeMath.Fade01(0f, 0f));
            Assert.AreEqual(1f, ShoreFadeMath.Fade01(0.01f, 0f), "tiny depth past a zeroed band saturates");
            Assert.AreEqual(1f, ShoreFadeMath.Fade01(5f, -3f), "a negative band is clamped, not NaN");
        }

        // ---- (1) contour-pinned against the REAL field, at three tides ---------------------------

        [Test]
        public void DisplacedHeight_IsExactlyZero_OnTheWaterlineContour_AtEveryTide()
        {
            WaveTrains trains = Trains();
            float band = ShoreFadeMath.RecommendedBandMeters(trains.TotalAmplitude, Exaggeration, 0.15f);

            foreach (float tide in new[] { -0.6f, 0f, 0.75f })       // the contour MOVES with tide…
            foreach (double t in new[] { 0.0, 777.25, EventTime })
            foreach (float x in new[] { -6.5f, 0f, 9.75f })
            {
                // …but wherever it sits, depth there is 0 by definition — and the fade pins to depth.
                float h = WaveMath.Sample(new Vector2(x, tide), t, in trains).Height;
                Assert.AreEqual(0f, ShoreFadeMath.DisplacedHeight(h, 0f, band, Exaggeration),
                    $"displacement must be EXACTLY 0 at the waterline (tide {tide}, t {t}, x {x})");
                Assert.AreEqual(0f, ShoreFadeMath.DisplacedHeight(h, -0.2f, band, Exaggeration),
                    "…and 0 on exposed ground past it");
            }
        }

        // ---- (3) open sea untouched: the readability event displaces at FULL exaggeration --------

        [Test]
        public void TheEnvelopeEvent_StillReadsAtFullExaggeration_ThroughTheSeam()
        {
            WaveTrains trains = Trains();
            float envelope = trains.TotalAmplitude;
            Assert.That(envelope, Is.InRange(1.0f, 1.1f),
                "reference-sea envelope moved — the spike scenario is no longer reproduced");

            float h = WaveMath.Sample(EventPos, EventTime, in trains).Height;
            Assert.Greater(h, 0.99f * envelope,
                "the found event must still be a ~100%-envelope crest (spike: 1.045 m of 1.047 m)");

            // Open water: any depth at/past the band. The seam must be BIT-invisible there.
            float band = ShoreFadeMath.RecommendedBandMeters(envelope, Exaggeration, 0.5f);
            foreach (float depth in new[] { band, band + 0.01f, 4f, 40f })
                Assert.AreEqual(h * Exaggeration,
                    ShoreFadeMath.DisplacedHeight(h, depth, band, Exaggeration),
                    $"offshore (depth {depth}) the seam must not change the displaced height AT ALL");
        }

        // ---- (2) the transect proof: no overlap, no fold, along real shores ----------------------
        //
        // Model (the exact production mapping, ADR 0023): a wet ground point p on a shore transect
        // draws at screenY = p.y + DisplacedHeight(h(p,t), depth(p), band, s); land tiles draw at
        // their ground y. A tear is EITHER a water sample crossing the waterline's screen position
        // (overlap onto dry land / the contour advancing) OR the mapping folding over itself
        // (d screenY / d groundY ≤ 0 — the shear that visually detaches crests). The transect runs
        // shore-normal because displacement only moves pixels along screen-y, so each screen column
        // is exactly a 1D problem.

        private static void RunTransect(float gradient, float tideLevel, float shoreBase,
                                        out float maxOverlap, out float minSlope, out float minFade,
                                        out float maxFade)
        {
            WaveTrains trains = Trains();
            float envelope = trains.TotalAmplitude;
            float band = ShoreFadeMath.RecommendedBandMeters(envelope, Exaggeration, Mathf.Abs(gradient));

            // elevation(y) = shoreBase + gradient·y  →  waterline where depth = 0.
            float waterlineY = (tideLevel - shoreBase) / gradient;
            float side = Mathf.Sign(gradient);      // +1: land toward +y (north shore); −1: south shore
            const float step = 0.05f;
            const int samples = 600;                 // 30 m of transect from the waterline seaward

            maxOverlap = float.MinValue;             // signed metres past the contour, toward land
            minSlope = float.MaxValue;              // min d(screenY)/d(groundY) — ≤0 would be a fold
            minFade = float.MaxValue;
            maxFade = float.MinValue;

            // Times: two dominant periods around the envelope event, plus the event instant itself.
            for (int ti = 0; ti <= 24; ti++)
            {
                double t = ti == 24 ? EventTime : EventTime - 4.0 + ti * (8.0 / 24.0);
                foreach (float x in new[] { EventPos.x, 3.0f })
                {
                    float prevScreenY = float.NaN;
                    for (int i = samples; i >= 0; i--)   // seaward → shoreward, ending ON the contour
                    {
                        float y = waterlineY - side * i * step;
                        float depth = tideLevel - (shoreBase + gradient * y);
                        Assert.GreaterOrEqual(depth, -1e-4f, "transect must stay on the wet side");

                        float h = WaveMath.Sample(new Vector2(x, y), t, in trains).Height;
                        float fade = ShoreFadeMath.Fade01(depth, band);
                        float screenY = y + ShoreFadeMath.DisplacedHeight(h, depth, band, Exaggeration);

                        minFade = Mathf.Min(minFade, fade);
                        maxFade = Mathf.Max(maxFade, fade);
                        // Overlap: how far past the waterline (toward land) this water sample draws.
                        maxOverlap = Mathf.Max(maxOverlap, (screenY - waterlineY) * side);
                        // Fold: screenY must advance monotonically along the ground.
                        if (!float.IsNaN(prevScreenY))
                            minSlope = Mathf.Min(minSlope, (screenY - prevScreenY) * side / step);
                        prevScreenY = screenY;
                    }
                }
            }
        }

        [Test]
        public void Transects_NeverOverlapTheWaterline_AndNeverFold(
            [Values(0.05f, 0.15f, 0.5f, -0.15f)] float gradient,
            [Values(-0.6f, 0f, 0.75f)] float tideLevel)
        {
            RunTransect(gradient, tideLevel, shoreBase: 0f,
                        out float maxOverlap, out float minSlope, out float minFade, out float maxFade);

            Assert.LessOrEqual(maxOverlap, 1e-4f,
                $"water crossed the waterline by {maxOverlap:0.0000} m (gradient {gradient}, tide {tideLevel}) — a torn coast");
            Assert.Greater(minSlope, 0.05f,
                $"screen mapping nearly folds (min slope {minSlope:0.000}) — the shear that detaches crests");
            Assert.AreEqual(0f, minFade, "the transect must actually touch the waterline (fade 0)");
            Assert.AreEqual(1f, maxFade, "the transect must actually reach open water (fade 1)");
        }

        [Test]
        public void TheEnvelopeEvent_ParkedMidBand_StillCannotTearTheCoast()
        {
            // Adversarial placement: choose the shore so the 100%-envelope crest lands exactly at
            // HALF the falloff band — the analytically worst point for the fold hazard (the fade's
            // steepest slope under the tallest possible crest; see ShoreFadeMath's class doc).
            WaveTrains trains = Trains();
            const float gradient = 0.15f;
            const float tide = 0f;
            float band = ShoreFadeMath.RecommendedBandMeters(trains.TotalAmplitude, Exaggeration, gradient);
            // depth(EventPos.y) = tide − base − gradient·y = band/2  →  base = −gradient·y − band/2.
            float shoreBase = -gradient * EventPos.y - band * 0.5f;

            RunTransect(gradient, tide, shoreBase,
                        out float maxOverlap, out float minSlope, out _, out _);

            Assert.LessOrEqual(maxOverlap, 1e-4f,
                $"the envelope event mid-band pushed water {maxOverlap:0.0000} m past the waterline");
            Assert.Greater(minSlope, 0.05f,
                $"the envelope event mid-band nearly folds the mapping (min slope {minSlope:0.000})");
        }

        // ---- determinism (rule 5): pure functions, bit-stable ------------------------------------

        [Test]
        public void SeamMath_IsBitStable_AcrossRepeatedCalls()
        {
            foreach (float depth in new[] { -1f, 0f, 0.123f, 0.777f, 5f })
            foreach (float band in new[] { 0.3f, 1.5f })
            {
                Assert.AreEqual(ShoreFadeMath.Fade01(depth, band), ShoreFadeMath.Fade01(depth, band));
                Assert.AreEqual(
                    ShoreFadeMath.DisplacedHeight(0.9f, depth, band, Exaggeration),
                    ShoreFadeMath.DisplacedHeight(0.9f, depth, band, Exaggeration));
            }
        }

        [Test]
        public void RecommendedBand_ScalesWithSteepness_ButFootprintDoesNot()
        {
            const float envelope = 1.047f;
            float gentle = ShoreFadeMath.RecommendedBandMeters(envelope, Exaggeration, 0.05f);
            float steep = ShoreFadeMath.RecommendedBandMeters(envelope, Exaggeration, 0.5f);
            Assert.Greater(steep, gentle, "steeper shores need a deeper band");
            // The seam's ground width is band/gradient — the same for every coast.
            Assert.AreEqual(gentle / 0.05f, steep / 0.5f, 1e-3f, "ground footprint is steepness-independent");
            Assert.AreEqual(ShoreFadeMath.GroundFootprintMeters(envelope, Exaggeration),
                            gentle / 0.05f, 1e-3f);
        }
    }
}
