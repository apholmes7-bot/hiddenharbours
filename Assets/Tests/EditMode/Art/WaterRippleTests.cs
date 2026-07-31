using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The ADR 0027 #10 ripple band's laws, headless — the C# twin of the shader's
    /// <c>RippleWindGate</c> / <c>RippleWindwardGate</c> / <c>RippleFramingFade</c> / <c>BandValue01</c>
    /// composite. CI has no graphics device, so this fixture is what actually adjudicates the band's
    /// arithmetic; the GPU probe (<c>RippleBandProbeTests</c>) proves the complementary pixel claim and
    /// skips loudly where there is nothing to render on.
    ///
    /// <para><b>The passthrough contract is the load-bearing assertion here.</b> The band ships OFF
    /// (<c>_RippleStrength</c> 0 in <c>Water.mat</c>), so the owner's tuned sea must be untouched until he
    /// dials it in — and every gate must have an exactly-reachable "off" end, not an approximately-off
    /// one.</para>
    /// </summary>
    public class WaterRippleTests
    {
        const float Eps = 1e-6f;

        // ---- (1) the wind gate: glass stays glass ---------------------------------------------------

        [Test]
        public void WindGate_IsExactlyZeroOnGlass_AndAtOrBelowTheOnset()
        {
            Assert.AreEqual(0f, WaterRipple.WindGate01(0f, 0.05f, 0.45f), Eps,
                "no wind must mean NO ripples — glass stays glass (the ADR's own words).");
            Assert.AreEqual(0f, WaterRipple.WindGate01(0.05f, 0.05f, 0.45f), Eps,
                "AT the onset the gate is still exactly shut; it opens above it.");
            Assert.AreEqual(0f, WaterRipple.WindGate01(0.02f, 0.05f, 0.45f), Eps,
                "and below the onset there is nothing to open.");
        }

        [Test]
        public void WindGate_SaturatesAtFull_SoAGaleGetsNoMoreRippleThanABlow()
        {
            Assert.AreEqual(1f, WaterRipple.WindGate01(0.45f, 0.05f, 0.45f), Eps);
            Assert.AreEqual(1f, WaterRipple.WindGate01(1f, 0.05f, 0.45f), Eps,
                "by a storm the whitecaps own the surface — the ripple band must not keep climbing.");
        }

        [Test]
        public void WindGate_IsMonotoneBetweenTheEdges()
        {
            float prev = -1f;
            for (int i = 0; i <= 40; i++)
            {
                float r = i / 40f;
                float g = WaterRipple.WindGate01(r, 0.05f, 0.45f);
                Assert.GreaterOrEqual(g, prev - Eps, $"the wind gate went DOWN at roughness {r:0.000}.");
                Assert.That(g, Is.InRange(0f, 1f));
                prev = g;
            }
        }

        [Test]
        public void WindGate_SurvivesADegenerateInterval_RatherThanDividingByZero()
        {
            // An owner can drag `full` under `onset`. HLSL's smoothstep with e0 == e1 is undefined on some
            // GPUs, so both sides clamp the interval open — this must return a number, not a NaN.
            float g = WaterRipple.WindGate01(0.5f, 0.6f, 0.2f);
            Assert.IsFalse(float.IsNaN(g), "a crossed-over wind interval must not produce NaN.");
            Assert.That(g, Is.InRange(0f, 1f));
        }

        // ---- (2) the windward-face gate --------------------------------------------------------------

        [Test]
        public void WindwardGate_AtZero_IsExactlyOne_RipplesEverywhere()
        {
            // The "off" end must be EXACT — it is the knob that says "ignore the faces".
            Assert.AreEqual(1f, WaterRipple.WindwardGate01(-1f, 0f, 0f), Eps);
            Assert.AreEqual(1f, WaterRipple.WindwardGate01(0f, 0f, 0.15f), Eps);
            Assert.AreEqual(1f, WaterRipple.WindwardGate01(1f, 0f, 0f), Eps);
        }

        [Test]
        public void WindwardGate_PutsRipplesOnTheWindwardFaceAndThinsThemInTheLee()
        {
            // dot(slope, wind) > 0 = the surface climbs as you travel downwind = the windward face.
            float windward = WaterRipple.WindwardGate01(+0.6f, 1f, 0.15f);
            float lee = WaterRipple.WindwardGate01(-0.6f, 1f, 0.15f);
            Assert.Greater(windward, lee,
                "ripples sit on the faces looking INTO the wind and are absent in the lee — that is the " +
                "whole physical claim of the gate.");
            Assert.AreEqual(1f, windward, Eps,
                "a solidly windward face carries the band at full weight.");
            Assert.AreEqual(0.15f, lee, Eps,
                "…and the lee falls to the FLOOR, not to zero: a real lee is sheltered, not glass.");
        }

        [Test]
        public void WindwardGate_LeeFloorIsTheFloorEverywhereItApplies()
        {
            for (int i = 0; i <= 20; i++)
            {
                float slope = -1f + i * 0.1f;
                float g = WaterRipple.WindwardGate01(slope, 1f, 0.25f);
                Assert.GreaterOrEqual(g, 0.25f - Eps,
                    $"the lee floor must hold at slope {slope:0.00}; got {g:0.000}.");
            }
        }

        [Test]
        public void WindwardGate_UsesTheSameSlopeNormalizerAsTheSwellFaceShading()
        {
            // Both read the SAME waveSlope off the SAME field. If they scaled it differently the ripples
            // would sit on a differently-shaped "face" than the shading that draws it.
            Assert.AreEqual(2f, WaterRipple.SlopeNormalize, Eps,
                "SlopeNormalize must stay 2.0 — the shader's RIPPLE_SLOPE_NORM and the face shading's " +
                "clamp(-dot(waveSlope, light) * 2, -1, 1) are the same normalization.");
            // Saturating at 2x means half a unit of along-wind slope already reads as a full face.
            Assert.AreEqual(1f, WaterRipple.WindwardGate01(0.5f, 1f, 0f), Eps);
            Assert.AreEqual(0.5f, WaterRipple.WindwardGate01(0.25f, 1f, 0f), Eps);
        }

        // ---- (3) the framing fade — the guard that REPLACED the ADR's per-zoom-tier fade -------------

        [Test]
        public void FramingFade_UnsetGlobalMeansNoFade_NotABlankSea()
        {
            // 0 = "nothing published it" (a bare material, an art scene with no WaterSurface, edit mode).
            // Reading that as "infinitely wide framing" would render the band blank exactly where someone
            // is trying to look at it.
            Assert.AreEqual(1f, WaterRipple.FramingFade01(0f, 16f, 30f, 0.2f), Eps);
            Assert.AreEqual(1f, WaterRipple.FramingFade01(-5f, 16f, 30f, 0.2f), Eps);
        }

        [Test]
        public void FramingFade_IsFullAtTightFramings_AndReachesTheFloorAtWideOnes()
        {
            // The tightest framing the game has is the 5.625 m live haul; the widest is 33.75 m.
            Assert.AreEqual(1f, WaterRipple.FramingFade01(5.625f, 16f, 30f, 0.2f), Eps,
                "on deck, hauling, the ripple band is the point — it must be at full strength.");
            Assert.AreEqual(1f, WaterRipple.FramingFade01(14f, 16f, 30f, 0.2f), Eps,
                "and through the Dory's own framing, which is where the game is mostly played.");
            Assert.AreEqual(0.2f, WaterRipple.FramingFade01(33.75f, 16f, 30f, 0.2f), Eps,
                "at the widest framing — ~6x more ripple cycles on screen — it sits at the floor.");
        }

        [Test]
        public void FramingFade_IsMonotoneDecreasingAcrossEveryFramingTheGameHas()
        {
            float prev = 2f;
            for (float m = 5f; m <= 40f; m += 0.5f)
            {
                float f = WaterRipple.FramingFade01(m, 16f, 30f, 0.2f);
                Assert.LessOrEqual(f, prev + Eps,
                    $"more sea on screen must never mean MORE ripple; it rose at {m:0.0} m.");
                Assert.That(f, Is.InRange(0.2f - Eps, 1f + Eps));
                prev = f;
            }
        }

        [Test]
        public void FramingFade_FloorZeroTakesTheBandAllTheWayOut()
        {
            Assert.AreEqual(0f, WaterRipple.FramingFade01(33.75f, 16f, 30f, 0f), Eps,
                "a zero floor must be an EXACT zero — the owner's 'gone when wide' setting.");
        }

        [Test]
        public void FramingFade_SurvivesACrossedOverInterval()
        {
            float f = WaterRipple.FramingFade01(20f, 30f, 10f, 0.2f);
            Assert.IsFalse(float.IsNaN(f), "a crossed-over framing interval must not produce NaN.");
            Assert.That(f, Is.InRange(0f, 1f));
        }

        [Test]
        public void FramingFade_IsNotMathfSmoothStep()
        {
            // ⚠️ THE TWIN TRAP, pinned. Mathf.SmoothStep(a, b, t) is a smooth LERP between two VALUES;
            // HLSL's smoothstep(e0, e1, x) is an EDGE GATE returning 0..1. They agree only by accident.
            // Halfway between the edges the gate is exactly 0.5; Mathf.SmoothStep(16, 30, 23) is 23-ish.
            Assert.AreEqual(0.5f, WaterRipple.SmoothstepEdge(16f, 30f, 23f), 1e-5f,
                "SmoothstepEdge must be the HLSL EDGE GATE, not a value lerp.");
            Assert.AreEqual(0f, WaterRipple.SmoothstepEdge(16f, 30f, 16f), Eps);
            Assert.AreEqual(1f, WaterRipple.SmoothstepEdge(16f, 30f, 30f), Eps);
            Assert.AreEqual(0f, WaterRipple.SmoothstepEdge(16f, 30f, 2f), Eps);
            Assert.AreEqual(1f, WaterRipple.SmoothstepEdge(16f, 30f, 99f), Eps);
        }

        // ---- the quantization (ADR 0027's "every layer carries its own", default ON) ------------------

        [Test]
        public void Band01_BelowTwoSteps_LeavesTheValueSmooth()
        {
            // The _DepthBands / _SpecBands / _AbsorptionBands "0 = off" idiom.
            Assert.AreEqual(0.37f, WaterRipple.Band01(0.37f, 0f, 0.5f, 0.5f), Eps);
            Assert.AreEqual(0.37f, WaterRipple.Band01(0.37f, 1f, 0.5f, 0.5f), Eps);
        }

        [Test]
        public void Band01_PosterizesIntoSolidSteps()
        {
            // Outside the dither window every input inside a step must land on the SAME output — that is
            // what "solid step" means, and it is what stops the band reading as an airbrushed gradient.
            const float bands = 3f, win = 0.2f, bayer = 0.5f;
            float a = WaterRipple.Band01(0.02f, bands, win, bayer);
            float b = WaterRipple.Band01(0.10f, bands, win, bayer);
            Assert.AreEqual(a, b, Eps, "two values inside one step must posterize to one shade.");
            Assert.AreEqual(0f, a, Eps, "…and the bottom step is the bottom shade.");
            Assert.AreEqual(1f, WaterRipple.Band01(1f, bands, win, bayer), Eps,
                "a full-value ripple crest reaches the top step on every dither cell.");
        }

        [Test]
        public void Band01_DithersOnlyInsideTheWindow_TheStyleLaw()
        {
            // Full-range dither dissolves the steps back into a smooth gradient — measured, forbidden.
            // With a narrow window, a value well clear of a boundary must ignore the Bayer threshold.
            const float bands = 4f, win = 0.2f;
            float lowBayer = WaterRipple.Band01(0.12f, bands, win, 0.05f);
            float highBayer = WaterRipple.Band01(0.12f, bands, win, 0.95f);
            Assert.AreEqual(lowBayer, highBayer, Eps,
                "away from a step edge the dither cell must not change the shade.");

            // …but AT a rounding boundary it must, or the edges read as hard contours instead of the
            // pixel-art fringe. With 4 steps the second boundary sits at v = 0.5 (x = 1.5, half a step past
            // floor), which is the centre of the dither window.
            float atEdgeLow = WaterRipple.Band01(0.5f, bands, win, 0.05f);
            float atEdgeHigh = WaterRipple.Band01(0.5f, bands, win, 0.95f);
            Assert.AreNotEqual(atEdgeLow, atEdgeHigh,
                "at a step boundary the world-locked Bayer cell must decide which side this pixel takes.");
        }

        // ---- the composite ---------------------------------------------------------------------------

        [Test]
        public void Amplitude_IsExactlyZeroAtTheShippedStrength_ThePassthroughContract()
        {
            Assert.AreEqual(0f, WaterRipple.Amplitude01(0f, 1f, 1f, 1f), Eps,
                "_RippleStrength = 0 is the shipped Water.mat value — the sea must be byte-identical.");
            Assert.AreEqual(0f, WaterRipple.SignedAdd(1f, 0f), Eps,
                "…and a zero amplitude adds exactly nothing to col.rgb, at any band value.");
        }

        [Test]
        public void Amplitude_IsAProduct_SoAnySingleGateAtZeroKillsTheLayer()
        {
            Assert.AreEqual(0f, WaterRipple.Amplitude01(1f, 0f, 1f, 1f), Eps, "no wind kills it.");
            Assert.AreEqual(0f, WaterRipple.Amplitude01(1f, 1f, 0f, 1f), Eps, "the deep lee kills it.");
            Assert.AreEqual(0f, WaterRipple.Amplitude01(1f, 1f, 1f, 0f), Eps, "a wide framing kills it.");
            Assert.AreEqual(0.25f, WaterRipple.Amplitude01(1f, 0.5f, 1f, 0.5f), Eps,
                "and the gates compose multiplicatively, not by max or by sum.");
        }

        [Test]
        public void SignedAdd_DarkensTroughsAsWellAsLighteningCrests_AndStaysUnderTheCeiling()
        {
            Assert.AreEqual(+WaterRipple.AddCeiling, WaterRipple.SignedAdd(1f, 1f), Eps);
            Assert.AreEqual(-WaterRipple.AddCeiling, WaterRipple.SignedAdd(0f, 1f), Eps);
            Assert.AreEqual(0f, WaterRipple.SignedAdd(0.5f, 1f), Eps,
                "the mid band is the neutral level — the layer adds no net brightness.");

            // The ceiling is the reason a ripple reads as texture rather than as a second specular. It must
            // stay below the swell-read band's 0.25 and the face shading's 0.15.
            Assert.Less(WaterRipple.AddCeiling, 0.15f,
                "the finest layer on the sea must swing less than the layers it rides on.");
            for (float b = 0f; b <= 1f; b += 0.05f)
                Assert.That(Mathf.Abs(WaterRipple.SignedAdd(b, 1f)), Is.LessThanOrEqualTo(WaterRipple.AddCeiling + Eps));
        }

        [Test]
        public void TheShippedSea_IsUntouched_EndToEnd()
        {
            // The whole point of the passthrough discipline, stated as one assertion over the values the
            // shipped Water.mat actually carries: strength 0, a lively sea, a normal framing.
            float wind = WaterRipple.WindGate01(0.35f, 0.05f, 0.45f);
            float face = WaterRipple.WindwardGate01(0.4f, 0.7f, 0.15f);
            float fade = WaterRipple.FramingFade01(14f, 16f, 30f, 0.2f);
            Assert.Greater(wind * face * fade, 0f, "…with every gate genuinely open,");
            Assert.AreEqual(0f, WaterRipple.SignedAdd(0.9f, WaterRipple.Amplitude01(0f, wind, face, fade)), Eps,
                "…the shipped strength of 0 still adds exactly nothing. That is the contract.");
        }

        // ---- the shader source: the things a twin cannot see -----------------------------------------

        /// <summary>
        /// A twin cannot see a BRANCH, and the passthrough here is a branch: at
        /// <c>_RippleStrength = 0</c> the band must not merely compute the same answer, it must not run
        /// <c>RippleField</c>, the wander noise or the posterize AT ALL — or every material that never
        /// opted in pays real GPU time for a layer it does not draw. Same reason
        /// <c>WaterDriftLinesTests</c> reads the shader source, and the same reason the compile-guard
        /// fixture does. Text asserts, so they run headless on CI where nothing can be rendered.
        /// </summary>
        [Test]
        public void ShaderGuards_KeepTheBandUnreachableAtTheShippedDefaults()
        {
            string src = System.IO.File.ReadAllText(
                "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader");

            Assert.IsTrue(src.Contains("if (_RippleStrength > 0.001)"),
                "the layer must bail before ANY of it when the owner has not dialled the band in — " +
                "that early-out is what makes the shipped look untouched by inspection.");
            Assert.IsTrue(src.Contains("if (rWind > 0.001)"),
                "a windless sea must not pay for the wander noise or the posterize: glass stays glass, " +
                "and it stays free.");
            Assert.IsTrue(src.Contains("if (rAmp > 0.001)"),
                "…nor must a fully faded-out framing or a dead lee.");
        }

        /// <summary>
        /// The shader's mirrors of this twin's laws, asserted as source text. These are the lines that
        /// would silently drift apart — the arithmetic tests above pin the C# side, and nothing else on
        /// CI (which has no graphics device) can see the HLSL side at all.
        /// </summary>
        [Test]
        public void ShaderMirrorsTheTwin_GateForGate()
        {
            string src = System.IO.File.ReadAllText(
                "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader");

            Assert.IsTrue(src.Contains("return smoothstep(lo, hi, saturate(_Roughness));"),
                "the wind gate must be the HLSL EDGE gate over the sim-pushed roughness.");
            Assert.IsTrue(src.Contains("lerp(1.0, max(steep, saturate(_RippleLeeFloor)), saturate(_RippleWindwardGate))"),
                "the windward gate's lee FLOOR and its exact 'gate 0 = everywhere' off end must survive.");
            Assert.IsTrue(src.Contains("if (_SeaFramingHeight <= 0.0) return 1.0;"),
                "an UNSET framing global must mean NO fade — the branch that stops a bare material, an " +
                "art scene or edit mode from rendering the band silently blank.");
            Assert.IsTrue(src.Contains("#define RIPPLE_SLOPE_NORM 2.0"),
                $"RIPPLE_SLOPE_NORM must match WaterRipple.SlopeNormalize ({WaterRipple.SlopeNormalize}), " +
                "or the ripples sit on a differently-shaped face than the shading that draws it.");
            Assert.IsTrue(src.Contains("#define RIPPLE_ADD_CEIL   0.10"),
                $"RIPPLE_ADD_CEIL must match WaterRipple.AddCeiling ({WaterRipple.AddCeiling}).");

            // THE CRAWL LAW: the sample position snaps on the WORLD grid FIRST, then scales. Snapping in
            // screen space (or not at all) makes the band crawl under every camera pan — the one artefact
            // that would make it read as a screen filter instead of water.
            Assert.IsTrue(src.Contains("float2 p = Pixelize(worldXY);"),
                "RippleField must snap its sample position on the world PPU grid — never screen space.");
        }

        /// <summary>
        /// ⚠️ THE DOUBLE-DRIVE TRAP, pinned. <c>_SeaFramingHeight</c> is a derived-physics PUSH from the
        /// camera. Anything in <c>WaterSurface.MoodFloatNames</c> is EASED from the eight weather preset
        /// materials (ADR 0017) and would overwrite the push every tick — the same reason <c>_Chop</c>,
        /// <c>_Roughness</c> and <c>_Flow</c> are deliberately kept out of that list. It is also why the
        /// uniform is declared OUTSIDE the per-material CBUFFER: it belongs to the camera, not to the
        /// water renderer, and a preset must never be able to author it.
        /// </summary>
        [Test]
        public void TheFramingGlobal_IsAPush_NotAMoodFloat()
        {
            var field = typeof(WaterSurface).GetField("MoodFloatNames",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(field, "WaterSurface.MoodFloatNames must still exist for this guard to mean anything.");
            var names = (string[])field.GetValue(null);
            CollectionAssert.DoesNotContain(names, "_SeaFramingHeight",
                "_SeaFramingHeight is pushed from the CAMERA every frame; easing it from the weather " +
                "presets would double-drive it and the ripple density would follow the sky, not the zoom.");

            string src = System.IO.File.ReadAllText(
                "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader");
            int global = src.IndexOf("float _SeaFramingHeight;", System.StringComparison.Ordinal);
            int cbuffer = src.IndexOf("CBUFFER_START(UnityPerMaterial)", System.StringComparison.Ordinal);
            Assert.Greater(global, -1, "the framing global must be declared in the shader.");
            Assert.Greater(cbuffer, -1, "the per-material CBUFFER must still exist.");
            Assert.Less(global, cbuffer,
                "_SeaFramingHeight must be declared OUTSIDE the per-material CBUFFER, like _SunDir / " +
                "_WindWorld / _WaveFieldParams — it is a global, not a material property.");
        }
    }
}
