using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The production plumbing of the DISPLACED water surface (ADR 0023 phase 2, step 1), pinned
    /// headless:
    ///
    ///  (1) MESH SIZING — the 8 px grid default, chunking that tiles the water rect exactly (no
    ///      crack, no overlap) with every chunk under the 16-bit index limit (rule 7).
    ///  (2) BAND LOCKSTEP — the plumbed band (coefficient as a parameter) is BIT-EQUAL to Core's
    ///      proven <see cref="ShoreFadeMath.RecommendedBandMeters"/> at the canonical coefficient,
    ///      so the production path can never drift from the tear-safety derivation.
    ///  (3) THE SEAM THROUGH THE PRODUCTION PATH — the reference sea's 100%-envelope event
    ///      (t = 1513.5 s at world (−6.5, 2.1); wind (−5.4, −9.33) ≈ 10.78 m/s, seaState 0.75,
    ///      WaveFieldSettings.Default) driven through <see cref="DisplacedWaterMath.VertexLift"/>
    ///      with the production-derived band: exactly 0 at the waterline, bit-identical
    ///      full ×1.5 in open water.
    ///  (4) TWIN + CONTRACT GUARDS — the shader carries the line-for-line ShoreFade01 twin of
    ///      <see cref="ShoreFadeMath.Fade01"/>, the displaced pass exists with the off-screen
    ///      LightMode, and the fragment's clip() contract is untouched (the walkable waterline
    ///      stays byte-identical to the flat pass's).
    /// </summary>
    public class DisplacedWaterMathTests
    {
        // ---- the reference sea (the spike's deterministic scenario — mirrors ShoreFadeMathTests)
        private static readonly Vector2 Wind = new Vector2(-5.4f, -9.33f);
        private const float SeaState = 0.75f;
        private const double EventTime = 1513.5;
        private static readonly Vector2 EventPos = new Vector2(-6.5f, 2.1f);
        private const float Exaggeration = 1.5f;

        private const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";

        // ---- (1) mesh sizing ---------------------------------------------------------------

        [Test]
        public void CellMeters_EightPixelsAtProjectPpu_IsAQuarterMetre()
        {
            Assert.AreEqual(0.25f, DisplacedWaterMath.CellMeters(8, 32f));
            Assert.AreEqual(8, DisplacedWaterMath.DefaultGridPixels,
                "ADR 0023's perf envelope: production STARTS at an 8 px grid");
        }

        [Test]
        public void CellCount_CoversTheRect_NeverUndershoots()
        {
            // The St Peters water rect at the default density.
            Assert.AreEqual(640, DisplacedWaterMath.CellCount(160f, 0.25f));
            Assert.AreEqual(480, DisplacedWaterMath.CellCount(120f, 0.25f));
            // Non-divisible sizes round UP (overhang beats a bare strip of coast).
            Assert.AreEqual(41, DisplacedWaterMath.CellCount(10.1f, 0.25f));
            // An exact fit stays exact (the epsilon guard must not add a phantom cell).
            Assert.AreEqual(64, DisplacedWaterMath.CellCount(16f, 0.25f));
        }

        [Test]
        public void Chunks_TileTheCellCountExactly([Values(1, 63, 64, 65, 129, 480, 640)] int cells)
        {
            int max = DisplacedWaterMath.MaxChunkCells;
            int chunks = DisplacedWaterMath.ChunkCount(cells, max);
            int sum = 0;
            for (int i = 0; i < chunks; i++)
            {
                int c = DisplacedWaterMath.ChunkCells(cells, max, i);
                Assert.Greater(c, 0, $"chunk {i} of {chunks} must hold at least one cell");
                Assert.LessOrEqual(c, max);
                if (i < chunks - 1)
                    Assert.AreEqual(max, c, "only the LAST chunk may be a remainder");
                sum += c;
            }
            Assert.AreEqual(cells, sum, "chunks must tile the grid EXACTLY — no crack, no overlap");
            Assert.AreEqual(0, DisplacedWaterMath.ChunkCells(cells, max, chunks), "out of range = 0");
        }

        [Test]
        public void ChunkMeshes_StayUnderTheSixteenBitIndexLimit()
        {
            int max = DisplacedWaterMath.MaxChunkCells;
            Assert.Less(DisplacedWaterMath.ChunkVertexCount(max, max), 65535,
                "a full chunk must fit 16-bit mesh indices");
            Assert.AreEqual(4225, DisplacedWaterMath.ChunkVertexCount(64, 64));
            Assert.AreEqual(64 * 64 * 6, DisplacedWaterMath.ChunkIndexCount(64, 64));
        }

        // ---- (2) band lockstep with the proven Core derivation -------------------------------

        [Test]
        public void BandMeters_AtTheCanonicalCoefficient_IsBitEqualToCore()
        {
            foreach (float envelope in new[] { 0f, 0.5f, 1.047f, 2.4f })
            foreach (float exag in new[] { 1f, 1.5f, 2f })
            foreach (float gradient in new[] { 0.05f, 0.15f, 0.5f })
                Assert.AreEqual(
                    ShoreFadeMath.RecommendedBandMeters(envelope, exag, gradient),
                    DisplacedWaterMath.BandMeters(envelope, exag, gradient,
                                                  ShoreFadeMath.RecommendedBandCoefficient),
                    $"the plumbed band drifted from Core's tear-safe derivation " +
                    $"(envelope {envelope}, exag {exag}, gradient {gradient})");
        }

        [Test]
        public void BandMeters_NegativeInputs_ClampToZero_LikeCore()
        {
            Assert.AreEqual(0f, DisplacedWaterMath.BandMeters(-1f, 1.5f, 0.5f, 2f));
            Assert.AreEqual(0f, DisplacedWaterMath.BandMeters(1f, -1.5f, 0.5f, 2f));
            Assert.AreEqual(0f, DisplacedWaterMath.BandMeters(1f, 1.5f, -0.5f, 2f));
            Assert.AreEqual(0f, DisplacedWaterMath.BandMeters(1f, 1.5f, 0.5f, -2f));
        }

        // ---- (3) the seam through the production parameter path ------------------------------

        [Test]
        public void TheEnvelopeEvent_ThroughTheProductionPlumbing_KeepsTheSeamContract()
        {
            WaveTrains trains = WaveMath.TrainsFrom(Wind, SeaState, WaveFieldSettings.Default);
            float envelope = trains.TotalAmplitude;
            Assert.That(envelope, Is.InRange(1.0f, 1.1f),
                "reference-sea envelope moved — the ADR 0023 scenario is no longer reproduced");

            float h = WaveMath.Sample(EventPos, EventTime, in trains).Height;
            Assert.Greater(h, 0.99f * envelope,
                "the found event must still be a ~100%-envelope crest (spike: 1.045 m of 1.047 m)");

            // The band exactly as DisplacedWaterSurface derives it each tick (live envelope,
            // inspector gradient, the canonical coefficient).
            float band = DisplacedWaterMath.BandMeters(envelope, Exaggeration, 0.5f,
                                                       ShoreFadeMath.RecommendedBandCoefficient);
            Assert.Greater(band, 0f);

            // The waterline pin: EXACTLY zero at and beyond the depth-0 contour.
            Assert.AreEqual(0f, DisplacedWaterMath.VertexLift(h, 0f, band, Exaggeration),
                "displacement must be EXACTLY 0 at the walkable waterline");
            Assert.AreEqual(0f, DisplacedWaterMath.VertexLift(h, -0.2f, band, Exaggeration),
                "…and 0 on exposed ground past it");

            // Open sea untouched: at and past the band the event displaces at FULL ×1.5,
            // bit-identical to the un-seamed value.
            foreach (float depth in new[] { band, band + 0.01f, 4f, 40f })
                Assert.AreEqual(h * Exaggeration,
                    DisplacedWaterMath.VertexLift(h, depth, band, Exaggeration),
                    $"offshore (depth {depth}) the seam must not change the displaced height AT ALL");
        }

        // ---- (4) twin + contract guards on the production shader -----------------------------

        [Test]
        public void WaterShader_CarriesTheShoreFadeTwin_LineForLine()
        {
            string src = File.ReadAllText(ShaderPath);

            // The exact Fade01 shape (ShoreFadeMath.Fade01's HLSL twin — the lockstep discipline:
            // change the C# and this fails until the shader moves in the SAME commit).
            StringAssert.Contains("float ShoreFade01(float depth, float band)", src);
            StringAssert.Contains("if (depth <= 0.0) return 0.0;", src);
            StringAssert.Contains("float t = saturate(depth / max(band, 1e-4));", src);
            StringAssert.Contains("return t * t * (3.0 - 2.0 * t);", src);

            // The vertex stage lifts by DisplacedHeight, verbatim: height × exaggeration × fade.
            StringAssert.Contains("_WaveExaggeration * ShoreFade01(stillDepth, _ShoreFadeBand)", src);
        }

        [Test]
        public void WaterShader_HasTheDisplacedPass_AndTheClipContractIsUntouched()
        {
            string src = File.ReadAllText(ShaderPath);

            // The off-screen displaced pass exists with the feature's LightMode…
            StringAssert.Contains("\"LightMode\" = \"HHWater\"", src);
            StringAssert.Contains("Name \"HHWaterDisplaced\"", src);
            // …and never ZTests the scene's way around (render-graph path is plain LEqual).
            StringAssert.Contains("ZTest LEqual", src);

            // The walkable-waterline contract: the fragment clips on the REAL depth at the
            // UNDISPLACED ground position — byte-identical to the flat pass (P1 integrity).
            //
            // ⚠️ AMENDED 2026-08-01, deliberately, and the amendment is the point. The owner asked for
            // "the swash in and out of the water along with the tides", which requires the DRAWN edge to
            // advance and recede — so the single `clip(depth + 1e-4)` became a coarse pre-clip widened by
            // the swash's maximum reach, then an exact clip carrying the swash offset. What this test
            // guards is NOT the literal line; it is the invariant that line stood for, and that invariant
            // now has to be stated rather than pattern-matched:
            //
            //   1. the clip is still driven by the REAL `depth` (never _WaterLevel directly, never a
            //      displaced or wave-lifted height) — the vertex stage still hands the fragment
            //      undisplaced ground;
            //   2. the only thing allowed to move it is the swash, and only by a HARD-CAPPED amount
            //      (_SwashMaxEdgeShift, shipped 0.35 m, inside the standing "wade ~0.5 m" tolerance);
            //   3. _SwashEdgeShift = 0 restores the previous edge exactly, so the divergence is revertible
            //      from the material with no code change.
            //
            // The gameplay waterline is untouched either way: nothing in the sim reads this fragment.
            // WaterShoreBandAndSwashTests pins the bound arithmetically; this pins the shader SHAPE.
            StringAssert.Contains("clip(depth + swashEdgeReach + 1e-4);", src);
            StringAssert.Contains("clip(depth + edgeSwash + 1e-4);", src);
            StringAssert.Contains("float depth = _WaterLevel - elevation;", src);
            // The cap is applied where the offset is built — a clamp that could be edited away without
            // failing anything would make the bound above a comment rather than a guarantee.
            StringAssert.Contains("float cap = saturate(_SwashMaxEdgeShift);", src);
            Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(src, @"edgeSwash\s*=\s*clamp\("),
                "the drawn-edge swash must be CLAMPED at its cap where it is computed — the bounded " +
                "SEE-not-FEEL divergence is only acceptable because the bound is structural");
        }

        [Test]
        public void TheSwash_NeverReachesTheVertexPathOrTheFetchMarch()
        {
            // The EXCLUSION half of the SEE≠FEEL bargain, and the half a "does the clip look right?" test
            // cannot express. The swash is allowed to move the drawn edge in the FRAGMENT and nowhere else:
            //
            //   • the VERTEX stages decide real geometry. `vertDisplaced` lifts the mesh the hull rides and
            //     publishes the iso-depth frame's z convention; a swash term there would put a cosmetic
            //     wash into the surface the boat physically sits on — SEE≠FEEL stops being bounded and
            //     becomes a lie.
            //   • the FETCH MARCH walks real distances over real water to decide the lee. Swash-perturbing
            //     its depth reads would shelter a hull behind a headland that is not there.
            //
            // Both are prohibitions, and a prohibition needs a test that FAILS WHEN VIOLATED, not a comment
            // that reads well. Scan the bodies and assert the swash symbols are absent from them.
            string src = File.ReadAllText(ShaderPath);

            string[] swashSymbols =
            {
                "BeachSwash", "edgeSwash", "swashEdgeReach", "swashSlope", "swashGate",
                "_SwashAmplitude", "_SwashEdgeShift", "_SwashMaxEdgeShift", "_SwashSpeed",
            };

            foreach (string fn in new[] { "Varyings vertDisplaced(Attributes IN)", "Varyings vert(Attributes IN)" })
            {
                string body = BalancedBody(src, fn);
                Assert.IsNotNull(body, $"could not locate '{fn}' — the vertex path moved; re-point this scan " +
                    "rather than deleting it (an exclusion test that cannot find its target guards nothing).");
                foreach (string sym in swashSymbols)
                    StringAssert.DoesNotContain(sym, body,
                        $"'{sym}' appears in {fn}. The swash may move the DRAWN edge in the fragment only — " +
                        "in a vertex stage it would move the geometry the hull rides, which is the exact " +
                        "thing the bounded SEE-not-FEEL divergence promises it does not do.");
            }

            // The fetch march is a macro body, so it is scanned as text between its #define and the blank
            // line that ends it — same intent, different shape.
            int marchStart = src.IndexOf("#define FETCH_MARCH_BODY", System.StringComparison.Ordinal);
            Assert.Greater(marchStart, 0, "FETCH_MARCH_BODY not found — re-point this scan.");
            int marchEnd = src.IndexOf("\n\n", marchStart, System.StringComparison.Ordinal);
            if (marchEnd < 0) marchEnd = System.Math.Min(marchStart + 4000, src.Length);
            string march = src.Substring(marchStart, marchEnd - marchStart);
            foreach (string sym in swashSymbols)
                StringAssert.DoesNotContain(sym, march,
                    $"'{sym}' appears in the fetch march. The march walks REAL distances over REAL water to " +
                    "decide the lee the hull feels; a cosmetic wash in it would shelter a boat behind a " +
                    "headland that is not there.");
        }

        /// <summary>Extract the brace-balanced body that follows a signature, or null if absent. Used by the
        /// exclusion scan so it reads a FUNCTION rather than a line range that silently rots.</summary>
        private static string BalancedBody(string src, string signature)
        {
            int i = src.IndexOf(signature, System.StringComparison.Ordinal);
            if (i < 0) return null;
            int open = src.IndexOf('{', i);
            if (open < 0) return null;
            int depth = 0;
            for (int j = open; j < src.Length; j++)
            {
                if (src[j] == '{') depth++;
                else if (src[j] == '}' && --depth == 0) return src.Substring(open, j - open + 1);
            }
            return null;

            // The flat pass is still the plain vertex (the A side of the A/B must be today's
            // water exactly). Whitespace-anchored so "vertDisplaced" cannot satisfy it.
            Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(src, @"#pragma vertex vert\s"),
                "the flat pass must keep its plain (undisplaced) vertex stage");
        }

        // ---- component defaults pinned to the ADR --------------------------------------------

        [Test]
        public void DisplacedWaterSurface_Defaults_MatchTheAdr()
        {
            var go = new GameObject("DisplacedWaterDefaultsProbe", typeof(SpriteRenderer));
            try
            {
                var surface = go.AddComponent<DisplacedWaterSurface>();
                Assert.IsFalse(surface.Displaced,
                    "the A/B must START on the flat side — today's water is the default contract");
                Assert.AreEqual(DisplacedWaterMath.DefaultGridPixels, surface.GridPixels,
                    "production starts at the ADR's 8 px grid");
                Assert.AreEqual(1.5f, surface.Exaggeration,
                    "ADR 0023 §(2): ×1.5 is the default readability exaggeration");
                Assert.AreEqual(ShoreFadeMath.RecommendedBandCoefficient, surface.BandCoefficient,
                    "the band coefficient defaults to Core's proven constant");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
