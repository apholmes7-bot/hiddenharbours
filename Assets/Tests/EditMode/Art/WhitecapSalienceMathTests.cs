using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Pins the ENVELOPE SALIENCE twin (ADR 0023 phase 2 step 2 — <see cref="WhitecapSalienceMath"/>,
    /// the C# side of the shader's CapEnvelopeGate / BandValue01 / BayerWorld) to the spike-tuned
    /// thresholds and to the reference sea's numbers, and scrapes the shader source so the HLSL
    /// property DEFAULTS cannot drift from the twin constants silently (the WaveMath twin
    /// discipline, enforced).
    ///
    /// The reference sea (spike/3d-water VERDICT.md / spike-log.txt, reproduced by ShoreFadeMathTests):
    /// wind 10.78 m/s, seaState 0.75, envelope A = 1.047 m, crest sharpening p = 2.2. The
    /// 100%-envelope event is t = 1513.5 s, h = 1.045 m; the TYPICAL tallest in-view crest over the
    /// half-hour window is 0.834 m (the big one is ×1.25 the everyday tallest). The retune's whole
    /// point, pinned here: the EVENT wears the solid foam core on every dither cell; the everyday
    /// tallest crest wears none.
    /// </summary>
    public class WhitecapSalienceMathTests
    {
        // The reference sea (the spike's deterministic scenario — the same numbers ShoreFadeMathTests pins).
        private const float EnvelopeMeters = 1.047f;
        private const float CrestSharpening = 2.2f;
        private const float EventHeightMeters = 1.045f;    // t = 1513.5 s — the 100%-envelope event
        private const float TypicalTallestMeters = 0.834f; // rms of per-frame in-view maxima

        /// <summary>The field's sharpened crest factor — WaveMath's normalization, locally.</summary>
        private static float CrestFactor(float heightMeters)
            => Mathf.Pow(Mathf.Clamp01(heightMeters / EnvelopeMeters), CrestSharpening);

        private static float[] AllBayerThresholds()
        {
            var all = new float[16];
            int i = 0;
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    all[i++] = WhitecapSalienceMath.BayerThreshold(x, y);
            return all;
        }

        // ---- the solid-core gate --------------------------------------------------------------

        [Test]
        public void EnvelopeEvent_WearsTheSolidCore_OnEveryDitherCell()
        {
            float crest = CrestFactor(EventHeightMeters);   // ~0.996 — deep inside the solid margin
            foreach (float bayer in AllBayerThresholds())
            {
                float gate = WhitecapSalienceMath.CapEnvelopeGate(
                    crest,
                    WhitecapSalienceMath.DefaultEnvelopeThreshold,
                    WhitecapSalienceMath.DefaultSolidMargin,
                    WhitecapSalienceMath.DefaultDitherBand,
                    bayer);
                Assert.That(gate, Is.EqualTo(1f),
                    "The 100%-envelope event (t = 1513.5 s, h = 1.045 m of a 1.047 m envelope) must " +
                    "wear the SOLID foam core on every dither cell — this is the acceptance bar of " +
                    "the retune (ADR 0023 §Whitecap salience).");
            }
        }

        [Test]
        public void TypicalTallestCrest_EarnsNoSolidCore_OnAnyDitherCell()
        {
            // The everyday tallest in-view crest: crestF = (0.834/1.047)^2.2 ≈ 0.61 — just UNDER the
            // spike-tuned 0.62 threshold. This is the retune's whole point: the everyday sea keeps
            // its thin milky streaks, and the solid core is reserved for the rare near-envelope wave.
            float crest = CrestFactor(TypicalTallestMeters);
            Assert.That(crest, Is.LessThan(WhitecapSalienceMath.DefaultEnvelopeThreshold),
                "The reference sea's typical tallest crest must sit below the envelope threshold — " +
                "if the wave model moved this number, the spike tuning needs re-derivation, loudly.");
            foreach (float bayer in AllBayerThresholds())
            {
                float gate = WhitecapSalienceMath.CapEnvelopeGate(
                    crest,
                    WhitecapSalienceMath.DefaultEnvelopeThreshold,
                    WhitecapSalienceMath.DefaultSolidMargin,
                    WhitecapSalienceMath.DefaultDitherBand,
                    bayer);
                Assert.That(gate, Is.EqualTo(0f),
                    "An everyday crest must NOT wear the solid core — uniform speckle salience is " +
                    "exactly what the retune retires.");
            }
        }

        [Test]
        public void FringeCrest_IsBayerDithered_NotSmooth()
        {
            // A crest inside the fringe (sig = 0.12 of a 0.25 dither band → coverage ratio 0.48):
            // the gate must be BINARY per cell (solid pixels and empty pixels — the style law's
            // dithered edge), and both values must occur across the 16-cell matrix.
            float crest = WhitecapSalienceMath.DefaultEnvelopeThreshold + 0.12f;
            int solid = 0, empty = 0;
            foreach (float bayer in AllBayerThresholds())
            {
                float gate = WhitecapSalienceMath.CapEnvelopeGate(
                    crest,
                    WhitecapSalienceMath.DefaultEnvelopeThreshold,
                    WhitecapSalienceMath.DefaultSolidMargin,
                    WhitecapSalienceMath.DefaultDitherBand,
                    bayer);
                Assert.That(gate == 0f || gate == 1f,
                    "The fringe is ORDERED DITHER — binary per cell, never a smooth alpha (the " +
                    "style law: solid bands, dithered edges; full-range smoothness is the airbrush trap).");
                if (gate > 0.5f) solid++; else empty++;
            }
            Assert.That(solid, Is.GreaterThan(0), "A mid-fringe crest must light SOME dither cells.");
            Assert.That(empty, Is.GreaterThan(0), "A mid-fringe crest must leave SOME cells empty — " +
                "if every cell is solid the fringe has collapsed into a hard step.");
        }

        [Test]
        public void Gate_IsMonotone_InCrestFactor()
        {
            // For any fixed dither cell, a taller crest can never LOSE foam (no salience inversions).
            foreach (float bayer in AllBayerThresholds())
            {
                float prev = 0f;
                for (float c = 0f; c <= 1.0001f; c += 0.01f)
                {
                    float gate = WhitecapSalienceMath.CapEnvelopeGate(
                        c,
                        WhitecapSalienceMath.DefaultEnvelopeThreshold,
                        WhitecapSalienceMath.DefaultSolidMargin,
                        WhitecapSalienceMath.DefaultDitherBand,
                        bayer);
                    Assert.That(gate, Is.GreaterThanOrEqualTo(prev),
                        $"Salience must be monotone in crest factor (bayer {bayer:F4}, crest {c:F2}).");
                    prev = gate;
                }
            }
        }

        // ---- the envelope value bands ---------------------------------------------------------

        [Test]
        public void TopValueBand_IsReachedByTheEnvelopeEvent_OnEveryDitherCell()
        {
            // The event's envelope-relative value: vN = h/A × 0.5 + 0.5 ≈ 0.999 → the TOP band,
            // solid across the whole dither matrix — the big wave is marked by SHADE (ADR 0023 §(4)).
            float vN = Mathf.Clamp01(EventHeightMeters / EnvelopeMeters * 0.5f + 0.5f);
            foreach (float bayer in AllBayerThresholds())
            {
                float q = WhitecapSalienceMath.BandValue01(
                    vN,
                    WhitecapSalienceMath.DefaultBandCount,
                    WhitecapSalienceMath.DefaultBandDitherWindow,
                    bayer);
                Assert.That(q, Is.EqualTo(1f).Within(1e-5f),
                    "The 100%-envelope crest must land the TOP value band on every dither cell.");
            }
        }

        [Test]
        public void ValueBands_AreSolidAwayFromEdges_DitheredOnlyInTheWindow()
        {
            // The style law, pinned: away from a rounding boundary the band is SOLID (identical on
            // every dither cell); inside the window it dithers (cells disagree). Band span at 7
            // bands: x = v × 6; the rounding boundary sits at frac(x) = 0.5, the 0.4 window covers
            // frac 0.3..0.7.
            float bands = WhitecapSalienceMath.DefaultBandCount;
            float win = WhitecapSalienceMath.DefaultBandDitherWindow;

            float vSolid = (2f + 0.1f) / (bands - 1f);   // frac 0.1 — outside the window: solid
            float vEdge = (2f + 0.5f) / (bands - 1f);    // frac 0.5 — dead on the boundary: dithered

            float first = WhitecapSalienceMath.BandValue01(vSolid, bands, win, AllBayerThresholds()[0]);
            foreach (float bayer in AllBayerThresholds())
                Assert.That(WhitecapSalienceMath.BandValue01(vSolid, bands, win, bayer),
                    Is.EqualTo(first),
                    "Away from a band edge the shade must be SOLID — identical on every dither cell " +
                    "(full-range dither = airbrush, forbidden by the style law).");

            int distinct = 0;
            float q0 = WhitecapSalienceMath.BandValue01(vEdge, bands, win, AllBayerThresholds()[0]);
            foreach (float bayer in AllBayerThresholds())
                if (!Mathf.Approximately(WhitecapSalienceMath.BandValue01(vEdge, bands, win, bayer), q0))
                    distinct++;
            Assert.That(distinct, Is.GreaterThan(0),
                "Dead on a band boundary the cells must DISAGREE — that disagreement IS the dithered edge.");
        }

        [Test]
        public void BandValue_IsMonotone_InValue()
        {
            foreach (float bayer in AllBayerThresholds())
            {
                float prev = 0f;
                for (float v = 0f; v <= 1.0001f; v += 0.005f)
                {
                    float q = WhitecapSalienceMath.BandValue01(
                        v, WhitecapSalienceMath.DefaultBandCount,
                        WhitecapSalienceMath.DefaultBandDitherWindow, bayer);
                    Assert.That(q, Is.GreaterThanOrEqualTo(prev - 1e-6f),
                        $"Band value must be monotone in the input (bayer {bayer:F4}, v {v:F3}).");
                    prev = q;
                }
            }
        }

        // ---- the Bayer matrix -----------------------------------------------------------------

        [Test]
        public void BayerMatrix_IsTheRigMatrix_AndWorldWrapIsSeamless()
        {
            // All 16 thresholds distinct, each of the form (v + 0.5)/16 for v = 0..15 — the boat
            // rigs' own ordered-dither matrix (ADR 0022 discipline), byte-identical to the shader.
            var seen = new bool[16];
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                {
                    float t = WhitecapSalienceMath.BayerThreshold(x, y);
                    float v = t * 16f - 0.5f;
                    int vi = Mathf.RoundToInt(v);
                    Assert.That(v, Is.EqualTo(vi).Within(1e-4f),
                        "Every threshold must be (v + 0.5)/16 — the rig matrix's exact values.");
                    Assert.That(vi, Is.InRange(0, 15));
                    Assert.IsFalse(seen[vi], "Bayer thresholds must be all-distinct.");
                    seen[vi] = true;
                }

            // World-locked wrap: negative cells continue the same 4-cell lattice (two's-complement
            // & in C# and HLSL agree), so the dither pattern is seamless across the world origin.
            Assert.That(WhitecapSalienceMath.BayerThreshold(-1, 0),
                Is.EqualTo(WhitecapSalienceMath.BayerThreshold(3, 0)));
            Assert.That(WhitecapSalienceMath.BayerThreshold(0, -1),
                Is.EqualTo(WhitecapSalienceMath.BayerThreshold(0, 3)));
            Assert.That(WhitecapSalienceMath.BayerThreshold(-4, -8),
                Is.EqualTo(WhitecapSalienceMath.BayerThreshold(0, 0)));
        }

        // ---- the near-shore salience fade -----------------------------------------------------

        [Test]
        public void CapSalience_DiesWithTheSeam_AndStrengthZeroIsAnExactPassthrough()
        {
            const float band = 1.57f;   // ≈ the derived band at the reference sea × 1.5 × gradient 0.5

            // At the walkable waterline the caps are GONE — the dying displaced edge must not wear
            // open-sea caps (ADR 0023 §Whitecap salience).
            Assert.That(WhitecapSalienceMath.CapShoreSalience(0f, band, 1f), Is.EqualTo(0f));
            // Past the band the open sea is untouched.
            Assert.That(WhitecapSalienceMath.CapShoreSalience(band, band, 1f), Is.EqualTo(1f));
            Assert.That(WhitecapSalienceMath.CapShoreSalience(band * 4f, band, 1f), Is.EqualTo(1f));
            // Strength 0 = the legacy look EXACTLY, at any depth (the master's passthrough contract).
            for (float d = 0f; d <= 3f; d += 0.25f)
                Assert.That(WhitecapSalienceMath.CapShoreSalience(d, band, 0f), Is.EqualTo(1f),
                    "_CapSalienceStrength 0 must be an exact passthrough — the A/B's safe side.");
            // And the fade IS the seam's own curve (one contour, never a second).
            Assert.That(WhitecapSalienceMath.CapShoreSalience(band * 0.5f, band, 1f),
                Is.EqualTo(ShoreFadeMath.Fade01(band * 0.5f, band)));
        }

        // ---- shader-source lockstep (the twin discipline, enforced) ---------------------------

        [Test]
        public void ShaderPropertyDefaults_MatchTheSpikeTunedTwinConstants()
        {
            string path = Path.Combine(Application.dataPath,
                "_Project/Art/Shaders/HiddenHarboursWater.shader");
            Assert.IsTrue(File.Exists(path), "HiddenHarboursWater.shader not found at " + path);
            string src = File.ReadAllText(path);

            AssertDefault(src, "_CapEnvelopeThreshold", WhitecapSalienceMath.DefaultEnvelopeThreshold);
            AssertDefault(src, "_CapSolidMargin", WhitecapSalienceMath.DefaultSolidMargin);
            AssertDefault(src, "_CapDitherBand", WhitecapSalienceMath.DefaultDitherBand);
            AssertDefault(src, "_EnvelopeBands", WhitecapSalienceMath.DefaultBandCount);
            AssertDefault(src, "_EnvelopeBandDitherWin", WhitecapSalienceMath.DefaultBandDitherWindow);
        }

        private static void AssertDefault(string shaderSource, string property, float expected)
        {
            // Matches the Properties-block line:  _Name ("label", Range(a,b)) = 0.62
            var m = Regex.Match(shaderSource,
                property + @"\s*\(""[^""]*"",\s*(?:Range\([^)]*\)|Float)\)\s*=\s*([0-9.]+)");
            Assert.IsTrue(m.Success,
                $"Shader property '{property}' not found in HiddenHarboursWater.shader's Properties " +
                "block — the envelope-salience stage must keep its named, spike-cited material knobs " +
                "(rule 6).");
            float actual = float.Parse(m.Groups[1].Value,
                System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(actual, Is.EqualTo(expected).Within(1e-5f),
                $"Shader default for '{property}' has drifted from the C# twin constant — the two " +
                "sides are LINE-FOR-LINE twins (change both in the same commit, with the spike " +
                "provenance re-cited).");
        }
    }
}
