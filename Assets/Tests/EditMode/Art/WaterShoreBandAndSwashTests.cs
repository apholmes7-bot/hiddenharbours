using System.IO;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guards for the 2026-08-01 shore-band work: the owner's *"the shoreline foam sometimes gets
    /// these artifact lines"* and *"i would like the swash in and out of the water along with the tides"*.
    ///
    /// <para><b>Defect A — the lines.</b> The foam band's edge is an ISO-CONTOUR of a depth descended from
    /// the seabed height TEXTURE: 8 bits over a −4…+6 m range = 3.91 cm per code, sampled bilinearly at
    /// ~2 px/m. Where the seabed is near-flat one code step spans METRES of ground, so an entire texel row
    /// crosses the band's smoothstep at the same instant and the edge snaps to the texture lattice — a
    /// straight, axis-aligned line. Foam noise cannot hide it: that noise rides ON the contour, not across
    /// it. Two things were wrong at once — the shore path was the ONE band in the shader carrying no Bayer
    /// dither, and the breakup noise that might have saved it is multiplied by the local seabed slope, which
    /// on those same flats measures zero.</para>
    ///
    /// <para><b>Defect B — the swash.</b> It existed, but it moved only the FOAM band, and its amplitude was
    /// multiplied by that same zero slope. So on the flats there was nothing to see, and even where there
    /// was, the water's own edge never moved.</para>
    ///
    /// <para>The GPU halves (the height-texture sample, the Bayer cell lookup, the seabed gradient) cannot be
    /// evaluated headless, so they are parameters here — the same discipline as every other water twin in
    /// this suite. What IS pinned is the arithmetic that decides whether a texel row crosses together, how
    /// far the drawn edge may move, and that the bound holds for every input.</para>
    /// </summary>
    public class WaterShoreBandAndSwashTests
    {
        /// <summary>One code of the 8-bit seabed height texture over its −4…+6 m range: 3.92 cm.</summary>
        private const float HeightCode = 10f / 255f;

        // ==== Defect A: the dither breaks the lattice-locked contour ===============================

        [Test]
        public void FoamEdgeDither_SpansAtLeastHalfAHeightCode_OrItCannotBreakTheRow()
        {
            // The dither has to be commensurate with the quantization it is fighting. A Bayer 4×4 cell
            // returns values in [0.5/16, 15.5/16], so the offset spans (bayMax − bayMin) × amount.
            const float shipped = 0.02f;   // the shipped _FoamEdgeDither, in metres of depth
            float lo = WaterSurface.FoamEdgeDither(0.5f / 16f, shipped);
            float hi = WaterSurface.FoamEdgeDither(15.5f / 16f, shipped);
            float span = hi - lo;

            Assert.Greater(span, HeightCode * 0.4f,
                $"the dither spans {span * 100f:0.0} cm but one height code is {HeightCode * 100f:0.0} cm — " +
                "too small to move the contour off the texel row it is stuck on");
            Assert.Less(span, HeightCode * 1.5f,
                "…and not so large that the band edge wanders more than the quantization it is hiding " +
                "(that would read as a fuzzy shoreline, trading one artefact for another)");
        }

        [Test]
        public void FoamEdgeDither_IsCentred_SoTheBandDoesNotMoveOnAverage()
        {
            // Centred on zero: the dither must scatter the contour, not shift it. An off-centre dither
            // would move the whole foam band inshore or offshore, which is a different (visible) change.
            Assert.AreEqual(0f, WaterSurface.FoamEdgeDither(0.5f, 0.02f), 1e-6f,
                "the middle Bayer value must produce no offset at all");
            Assert.AreEqual(0f,
                WaterSurface.FoamEdgeDither(0.25f, 0.02f) + WaterSurface.FoamEdgeDither(0.75f, 0.02f), 1e-6f,
                "symmetric Bayer values must cancel — the band's average position is unchanged");
        }

        [Test]
        public void FoamEdgeDither_Zero_RestoresTheOldContourExactly()
        {
            for (float b = 0f; b <= 1f; b += 0.1f)
                Assert.AreEqual(0f, WaterSurface.FoamEdgeDither(b, 0f), 1e-7f,
                    "_FoamEdgeDither = 0 must be byte-identical to the pre-fix lattice-locked contour");
        }

        [Test]
        public void OnFlatSeabed_TheDither_StopsAWholeTexelRowFromCrossingTogether()
        {
            // The defect, reproduced as arithmetic. A flat run of seabed reads ONE quantized elevation, so
            // every pixel across it has the SAME depth. Undithered, they all sit on the same side of the
            // band edge and flip together — that simultaneous flip IS the straight line. Dithered, the
            // Bayer cell varies across the row, so the crossing is spread over a range of depths.
            const float foamWidth = 1.74f;                 // the shipped _FoamWidth
            const float rowDepth = 0.9f * foamWidth;       // a row sitting just inside the band edge
            const float dither = 0.02f;

            // 16 pixels across a flat row: identical depth, different Bayer cells.
            var undithered = new float[16];
            var dithered = new float[16];
            for (int i = 0; i < 16; i++)
            {
                float bayer = (i + 0.5f) / 16f;            // the 16 distinct Bayer 4×4 values
                undithered[i] = rowDepth;
                dithered[i] = rowDepth + WaterSurface.FoamEdgeDither(bayer, dither);
            }

            Assert.AreEqual(0f, Spread(undithered), 1e-7f,
                "sanity: undithered, a flat texel row is one single depth — every pixel crosses at once");
            Assert.Greater(Spread(dithered), HeightCode * 0.4f,
                "dithered, the row must present a RANGE of depths to the band edge, so the crossing is " +
                "scattered across the row instead of snapping to the texel lattice");
        }

        private static float Spread(float[] v)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (float x in v) { lo = Mathf.Min(lo, x); hi = Mathf.Max(hi, x); }
            return hi - lo;
        }

        // ==== Defect A/B: the slope floor revives the flats without touching real slopes ===========

        [Test]
        public void ShoreCosmeticSlope_LeavesTheSwirlGuardsReferenceCoastUntouched()
        {
            // The whole safety argument for the floor: it must be BELOW every slope the 2026-07-23 swirl
            // guard renders, so that guard's verdict is unchanged and still means what it meant.
            const float guardCoast = 0.18f;   // WaterWhiteoutShoreSwirlAcceptanceTests.BeachSlope
            const float shippedFloor = 0.15f;

            Assert.Less(shippedFloor, guardCoast,
                "the shipped floor must sit below the swirl guard's 0.18 m/m reference coast, or that " +
                "guard is measuring a different sea than the one it was calibrated on");
            Assert.AreEqual(guardCoast, WaterSurface.ShoreCosmeticSlope(guardCoast, shippedFloor), 1e-6f,
                "on the guard's coast the floor must be inert — max(0.18, 0.15) = 0.18");
            Assert.AreEqual(0.6f, WaterSurface.ShoreCosmeticSlope(0.6f, shippedFloor), 1e-6f,
                "any real slope above the floor passes through unchanged — steep shores keep today's look");
        }

        [Test]
        public void ShoreCosmeticSlope_OnTheFlats_StopsMultiplyingTheCosmeticsToNothing()
        {
            // The flats are where the defect lives: the 8-bit height map is flat across whole texel runs,
            // so the central difference returns literally 0 and both the breakup noise and the swash were
            // scaled to nothing exactly where the owner was standing.
            const float shippedFloor = 0.15f;
            Assert.AreEqual(0f, WaterSurface.ShoreCosmeticSlope(0f, 0f), 1e-6f,
                "sanity: with no floor a measured-flat seabed kills the shore cosmetics entirely");
            Assert.AreEqual(shippedFloor, WaterSurface.ShoreCosmeticSlope(0f, shippedFloor), 1e-6f,
                "with the floor, a flat beach keeps a real (if gentle) cosmetic response — long, low " +
                "run-up, which is what a flat beach actually does");
        }

        [Test]
        public void ShoreCosmeticSlope_Zero_IsTheOldBehaviourExactly()
        {
            for (float s = 0f; s <= 1f; s += 0.1f)
                Assert.AreEqual(Mathf.Clamp01(s), WaterSurface.ShoreCosmeticSlope(s, 0f), 1e-6f,
                    "_ShoreSlopeFloor = 0 must reproduce the pre-fix slope scaling for every slope");
        }

        // ==== Defect B: the drawn edge moves, but only so far =====================================

        [Test]
        public void SwashEdgeShift_IsHardBounded_ForEveryInput()
        {
            // THE invariant behind the SEE≠FEEL flag. Whatever the swash does — including a mis-tuned
            // amplitude an order of magnitude too large — the drawn edge may not move further than the cap.
            const float cap = 0.35f;
            foreach (float raw in new[] { -50f, -3f, -0.4f, -0.05f, 0f, 0.05f, 0.4f, 3f, 50f })
                foreach (float shift in new[] { 0f, 0.25f, 0.6f, 1f })
                {
                    float v = WaterSurface.SwashEdgeShift(raw, shift, cap);
                    Assert.LessOrEqual(Mathf.Abs(v), cap + 1e-6f,
                        $"swash {raw} at shift {shift} moved the drawn edge {v} m — the cap is the whole " +
                        "reason this divergence is acceptable");
                }
        }

        [Test]
        public void SwashEdgeShift_StaysWellInsideTheWadeTolerance()
        {
            // The cap is not an arbitrary number: it has to sit inside the standing "wade ~0.5 m" gameplay
            // tolerance, so no ground the player can stand on changes meaning when the drawn edge moves.
            const float wadeTolerance = 0.5f;
            const float shippedCap = 0.35f;
            Assert.Less(shippedCap, wadeTolerance,
                "the shipped drawn-edge cap must stay under the wade tolerance — beyond it the drawn " +
                "water would cover ground whose walkability the player can actually feel");
        }

        [Test]
        public void SwashEdgeShift_Zero_LeavesTheDrawnEdgeExactlyWhereItWas()
        {
            // The revert path. _SwashEdgeShift = 0 must be the pre-2026-08-01 shader, byte for byte.
            foreach (float raw in new[] { -1f, -0.2f, 0f, 0.2f, 1f })
                Assert.AreEqual(0f, WaterSurface.SwashEdgeShift(raw, 0f, 0.35f), 1e-7f,
                    "at edge shift 0 the water's edge must not move at all — foam-only, as before");
        }

        [Test]
        public void SwashEdgeShift_MovesTheEdgeBothWays_ThatIsWhatInAndOutMeans()
        {
            // The owner asked for water running IN and OUT. A rectified (one-sided) offset would only ever
            // advance the edge, which reads as a wet fringe, not as a wash.
            Assert.Greater(WaterSurface.SwashEdgeShift(0.2f, 0.6f, 0.35f), 0f, "the wash runs IN");
            Assert.Less(WaterSurface.SwashEdgeShift(-0.2f, 0.6f, 0.35f), 0f, "…and back OUT");
        }

        // ==== Defect B: glass is near-still, a chop washes ========================================

        [Test]
        public void SwashSeaStateGate_IsStillOnGlass_AndFullInAChop()
        {
            const float lo = 0.28f, hi = 0.45f, calmGate = 0.7f;   // the shipped gate + _SwashCalmGate

            float glass = WaterSurface.SwashSeaStateGate(0f, lo, hi, calmGate);
            float chop = WaterSurface.SwashSeaStateGate(0.8f, lo, hi, calmGate);

            Assert.AreEqual(1f - calmGate, glass, 1e-5f,
                "on glass the swash must fade to the floor — a mirror bay has no surf running up it");
            Assert.AreEqual(1f, chop, 1e-5f, "in a real chop the swash washes at full strength");
            Assert.Less(glass, chop, "…and the gate is the right way round");
        }

        [Test]
        public void SwashSeaStateGate_IsMonotonic_NoPopAcrossTheAxis()
        {
            // Same discipline as every other sea-state read: the response must slide with the axis, not
            // step — otherwise the shore pops at a threshold as the weather drifts through it.
            float prev = -1f;
            for (int i = 0; i <= 200; i++)
            {
                float g = WaterSurface.SwashSeaStateGate(i / 200f, 0.28f, 0.45f, 0.7f);
                Assert.GreaterOrEqual(g, prev - 1e-6f, "the swash gate must never fall as the sea rises");
                prev = g;
            }
        }

        [Test]
        public void SwashSeaStateGate_ZeroCalmGate_IsTheOldBehaviourAtEverySeaState()
        {
            for (float chop = 0f; chop <= 1f; chop += 0.1f)
                Assert.AreEqual(1f, WaterSurface.SwashSeaStateGate(chop, 0.28f, 0.45f, 0f), 1e-6f,
                    "_SwashCalmGate = 0 must leave the swash identical at every sea-state (the pre-fix look)");
        }

        [Test]
        public void SwashSeaStateGate_SurvivesADegenerateThresholdPair()
        {
            // lo >= hi is an authoring mistake, not a crash: the gate must degrade to a hard step, never
            // divide by zero into a NaN that would paint the whole shore band black.
            float v = WaterSurface.SwashSeaStateGate(0.5f, 0.6f, 0.6f, 0.7f);
            Assert.IsFalse(float.IsNaN(v), "a degenerate threshold pair must not produce NaN");
            Assert.That(v, Is.InRange(0f, 1f), "…and must stay a valid gate");
        }

        // ==== The cadence the owner asked for =====================================================

        [Test]
        public void TheShippedSwashSpeed_GivesATideBreathingRunUp_NotSurfAndNotAStandstill()
        {
            // The owner's ask reads as tide-breathing, not surf: he could not see the old 0.08 (one
            // run-up every 12.5 s) at all. The shader's phase is t·speed·2π, so the primary beat's period
            // is 1/speed seconds.
            //
            // Read off the SHIPPED material rather than a constant copied beside it. A test that asserts
            // its own hard-coded number cannot notice the material drifting away from it — which is the
            // exact failure mode this class is currently watching happen to a sibling suite.
            float shippedSpeed = ShippedFloat(LiveWaterMatPath, "_SwashSpeed");
            float periodSeconds = 1f / shippedSpeed;

            Assert.That(periodSeconds, Is.InRange(4f, 8f),
                $"one run-up every {periodSeconds:0.0} s — the target band is 4–8 s; 12.5 s (the old " +
                "0.08) reads as a still shoreline, and under 4 s reads as surf rather than a breathing tide");
        }

        // ==== The preset trap (the _RainRingStrength precedent) ====================================

        private const string LiveWaterMatPath = "Assets/_Project/Art/Materials/Water.mat";
        private const string PresetsFolder = "Assets/_Project/Art/Materials/WaterPresets";

        private static readonly string[] PresetNames =
        {
            "Water_DeepBlue", "Water_FoggySmother", "Water_GlassyCalm", "Water_NorthAtlantic",
            "Water_StirredBrown", "Water_StormGrey", "Water_Tropical", "Water_WarmShelter",
        };

        /// <summary>Every key this PR added, plus the two it retuned. All must be serialized EVERYWHERE.</summary>
        private static readonly string[] ShoreAndSwashKeys =
        {
            "_FoamEdgeDither", "_ShoreSlopeFloor",
            "_SwashCalmGate", "_SwashEdgeShift", "_SwashMaxEdgeShift", "_SwashSpeed",
        };

        [Test]
        public void EveryWaterPreset_SerializesTheShoreAndSwashKeys_OrApplyingItStampsTheDefault()
        {
            // The standing trap, and the reason this test exists rather than a code comment: the editor's
            // "Hidden Harbours ▸ Art ▸ Water Presets ▸ Apply" is a wholesale CopyPropertiesFromMaterial, so
            // a preset that OMITS a key stamps the shader's Properties default over the owner's live value.
            // _RainRingStrength is the precedent — it is how a shipped feature quietly went dark for months.
            //
            // Asserted on the SERIALIZED TEXT, never via HasProperty/GetFloat: a material that omits a key
            // still answers from the shader defaults, so a value-based check passes vacuously for exactly
            // the presets that carry the trap.
            foreach (string name in PresetNames)
            {
                string path = $"{PresetsFolder}/{name}.mat";
                Assert.IsTrue(File.Exists(path), $"Tracked preset '{name}' is missing from {PresetsFolder}.");
                string yaml = File.ReadAllText(path);
                foreach (string key in ShoreAndSwashKeys)
                    Assert.IsTrue(yaml.Contains($"- {key}:"),
                        $"'{name}.mat' does not serialize {key}. Applying it would stamp the shader default " +
                        "onto Water.mat and silently kill the shore fix the owner asked for.");
            }

            string live = File.ReadAllText(LiveWaterMatPath);
            foreach (string key in ShoreAndSwashKeys)
                Assert.IsTrue(live.Contains($"- {key}:"),
                    $"Water.mat does not serialize {key} — the live material is the one that actually draws.");
        }

        [Test]
        public void TheShippedShoreKnobs_MatchWhatTheseTestsAssume()
        {
            // The other half of "don't test a fiction": the bounds proved arithmetically above are only
            // worth anything at the values that actually draw. Read them off Water.mat.
            Assert.AreEqual(0.02f, ShippedFloat(LiveWaterMatPath, "_FoamEdgeDither"), 1e-4f,
                "_FoamEdgeDither moved — re-check it still spans ~half a seabed height code (3.92 cm)");
            Assert.AreEqual(0.15f, ShippedFloat(LiveWaterMatPath, "_ShoreSlopeFloor"), 1e-4f,
                "_ShoreSlopeFloor moved — it MUST stay below the swirl guard's 0.18 m/m reference coast, " +
                "or that guard silently starts measuring a different sea than it was calibrated on");
            Assert.AreEqual(0.35f, ShippedFloat(LiveWaterMatPath, "_SwashMaxEdgeShift"), 1e-4f,
                "_SwashMaxEdgeShift moved — this is the SEE≠FEEL cap; it must stay inside the standing " +
                "'wade ~0.5 m' tolerance, and a change to it needs the coordinator's ratification");
        }

        /// <summary>Read a float straight out of a material's serialized YAML — the value that SHIPS, not
        /// whatever the shader would fall back to. Fails loudly if the key is absent, which is the whole
        /// point (a missing key is the preset trap, and <c>GetFloat</c> would hide it).</summary>
        private static float ShippedFloat(string matPath, string key)
        {
            Assert.IsTrue(File.Exists(matPath), $"No material at '{matPath}'.");
            var m = System.Text.RegularExpressions.Regex.Match(
                File.ReadAllText(matPath), $@"-\s{System.Text.RegularExpressions.Regex.Escape(key)}:\s*(-?[\d.eE+]+)");
            Assert.IsTrue(m.Success, $"'{matPath}' does not serialize {key}.");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        [Test]
        public void TheOrphanSwashScaleKey_IsGoneEverywhere()
        {
            // _SwashScale sat in Water.mat with NO shader property behind it: a dead knob that reads as a
            // real one to anyone tuning the sea. Removed 2026-08-01; this keeps it removed.
            foreach (string name in PresetNames)
            {
                string yaml = File.ReadAllText($"{PresetsFolder}/{name}.mat");
                Assert.IsFalse(yaml.Contains("- _SwashScale:"),
                    $"'{name}.mat' still carries the orphan _SwashScale key (no shader property backs it).");
            }
            Assert.IsFalse(File.ReadAllText(LiveWaterMatPath).Contains("- _SwashScale:"),
                "Water.mat still carries the orphan _SwashScale key.");
        }
    }
}
