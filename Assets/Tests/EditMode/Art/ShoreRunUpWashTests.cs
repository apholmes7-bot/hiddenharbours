using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guards for THE RUN-UP WASH (owner shore clip review, 2026-11-15: "just these few foam items
    /// need fixing before posting"). On the St Peters beach the bore's run-up drew four faults, each traced to
    /// a line of <c>HiddenHarboursWater.shader</c>:
    /// <list type="number">
    /// <item>a flat OPAQUE white sheet: the bore's residue in the advected foam buffer, composed at full
    /// coverage (<c>WakeFoamCoverage</c> banded to 1 × <c>_FoamColor.a</c>), and the surf's top band;</item>
    /// <item>a white SLIVER stranded past see-through water: the fringe, which yields to the surf only through
    /// <c>surfSupersede</c> (a FRAGMENT read of the break gate and the bore's age), so it came back at the
    /// wet edge wherever that read dipped;</item>
    /// <item>a 4-px SAWTOOTH seaward edge and a 1-px CHECKER: the buffer's point read on its 1/8 m lattice
    /// (4 screen px a cell), and <c>SurfBandValue</c>'s Bayer dither at low surf cover;</item>
    /// <item>square NOTCHES on the landward edge: the same point-read cells meeting the drawn edge, and the
    /// fringe drawn where boreEdgeBlend is partial. NOT fixed here: where the fetch march's lee flips
    /// boreEdgeBlend to 0 cell by cell (FetchPixelize), the drawn edge itself steps back to the still line;
    /// that is the edge law (WaterSurface twin), not the foam.</item>
    /// </list>
    /// The fix reads every wash quantity off <c>boreEdgeBlend</c>, the drawn edge's own shore quantity (constant
    /// along the shore normal, WaterDrawnEdgeOnePieceTests), so the sheet, its leading foam line and the
    /// fringe's hand-back agree with the edge. CI has no GPU, so these pin the source contract and the
    /// tunables; the look itself is verified on the plate (the PR's v3 clip).
    /// </summary>
    public class ShoreRunUpWashTests
    {
        const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";

        static readonly (string key, string value)[] Dials =
        {
            ("_WashSheetOpacity", "0.4"),
            ("_WashLineWidth", "0.1"),
            ("_WashLineOpacity", "0.85"),
            ("_WashSeawardDepth", "0.8"),
            ("_WashPresenceRamp", "0.1"),
        };

        static string Src() => File.ReadAllText(ShaderPath, Encoding.UTF8).Replace("\r\n", "\n");

        static string[] Materials() => new[] { "Assets/_Project/Art/Materials/Water.mat" }
            .Concat(Directory.GetFiles("Assets/_Project/Art/Materials/WaterPresets", "*.mat")
                .OrderBy(p => p, System.StringComparer.Ordinal))
            .ToArray();

        static int IndexOf(string src, string needle)
        {
            int at = src.IndexOf(needle, System.StringComparison.Ordinal);
            Assert.Greater(at, 0, "not found in the water shader: " + needle);
            return at;
        }

        [Test]
        public void TheWashDials_AreDeclaredOnce_AtTheirDefaults()
        {
            string src = Src();
            foreach (var (key, value) in Dials)
            {
                var decl = Regex.Matches(src, @"(?m)^ *" + Regex.Escape(key) + @" +\(""[^""]*"", Range\([^)]*\)\) = " +
                                              Regex.Escape(value) + "$");
                Assert.AreEqual(1, decl.Count, key + " must be declared once in Properties at " + value);
            }
            Assert.AreEqual(1, Regex.Matches(src,
                @"float  _WashSheetOpacity, _WashLineWidth, _WashLineOpacity, _WashSeawardDepth, _WashPresenceRamp;").Count,
                "the five wash uniforms must be declared once, beside the surf's");
        }

        [Test]
        public void EveryWaterMaterial_SerializesTheWashDials_AtTheShaderDefaults()
        {
            // 'Apply water preset' copies wholesale (BreakerBoreLookTests): a preset missing a key stamps
            // the shader default over the hero material. Serialized everywhere, at the defaults.
            string[] mats = Materials();
            Assert.AreEqual(9, mats.Length);
            foreach (string mat in mats)
            {
                string yaml = File.ReadAllText(mat, Encoding.UTF8).Replace("\r\n", "\n");
                foreach (var (key, value) in Dials)
                    Assert.AreEqual(1, Regex.Matches(yaml, @"(?m)^    - " + Regex.Escape(key) + ": " +
                                                            Regex.Escape(value) + "$").Count,
                        Path.GetFileName(mat) + " must carry " + key + ": " + value);
            }
        }

        [Test]
        public void TheWash_IsReadOffTheDrawnEdge_AfterItsClip()
        {
            string src = Src();
            int clip = IndexOf(src, "clip(depth + edgeSwash + 1e-4);");
            int presence = IndexOf(src,
                "float washPresence = smoothstep(0.0, max(_WashPresenceRamp, 1e-3), boreEdgeBlend);");
            int gate = IndexOf(src,
                "float washOnSand = washPresence * (1.0 - smoothstep(0.0, max(_WashSeawardDepth, 1e-3), depth));");
            Assert.Less(presence, gate);
            int lead = IndexOf(src, "float washLeadM  = (depth + edgeSwash) / max(swashSlope, 1e-3);");
            int line = IndexOf(src,
                "float washLine   = washOnSand * step(washLeadM, max(_WashLineWidth, 0.0)) * saturate(_WashLineOpacity);");
            Assert.That(clip < gate && gate < lead && lead < line,
                "the wash must read the SAME edgeSwash the clip draws, after it: the leading line is then " +
                "attached to the drawn edge by construction, a hard step on whole pixels with no dither");
        }

        [Test]
        public void TheFringe_YieldsToTheWash_ThroughTheEdgesShoreQuantity()
        {
            string src = Src();
            int supersede = IndexOf(src, "foamCoverage *= (1.0 - surfSupersede);");
            int yield = IndexOf(src, "foamCoverage *= 1.0 - washPresence * step(0.001, _WashLineOpacity);");
            int compose = IndexOf(src, "col.rgb = lerp(col.rgb, _FoamColor.rgb, foamCoverage * _FoamColor.a);");
            Assert.That(supersede < yield && yield < compose,
                "the fringe's hand-back to the wash must sit between the surf supersede and the fringe compose");
            // The fence the one-piece edge fix set stays: the fringe's DEPTH still rides boreFoamBlend.
            StringAssert.Contains("surfRunUpM, boreFoamBlend)", src);
        }

        [Test]
        public void TheWashSheet_HasNoBayerChecker_AndTheWashOpacity()
        {
            string src = Src();
            StringAssert.Contains("float cover = SurfBandValue(surfCover, washOnSand > 0.001 ? 0.5 : bay) * strength", src);
            StringAssert.Contains("* lerp(1.0, saturate(_WashSheetOpacity), saturate(washOnSand));", src);
            int lip = IndexOf(src, "col.a   = max(col.a, lip * _SurfLipColor.a);");
            int washLine = IndexOf(src, "col.rgb = lerp(col.rgb, _FoamColor.rgb, washLine);");
            Assert.Greater(washLine, lip, "the leading line is laid over the surf, last");
        }

        [Test]
        public void TheResidue_IsReadSmooth_OnTheWashOnly()
        {
            string src = Src();
            int fnAt = IndexOf(src, "float WashResidueCoverage(float2 worldXY)");
            string body = src.Substring(fnAt, src.IndexOf("\n            }", fnAt, System.StringComparison.Ordinal) - fnAt);
            StringAssert.Contains("SAMPLE_TEXTURE2D_LOD(_HHFoamBufferTex, sampler_LinearClamp, uv, 0)", body,
                "a LINEAR tap is what turns the buffer's 1/8 m cells into a smooth outline drawn at whole pixels");
            StringAssert.Contains("BandValue01(cover, _WakeFoamBands, 0.0, 0.5)", body, "hard bands, no Bayer");
            StringAssert.Contains("saturate(_WashSheetOpacity)", body, "the residue wears the wash's opacity");

            // Open water keeps the lattice: the wake's own read is still the buffer's point sampler.
            int wakeAt = IndexOf(src, "float WakeFoamCoverage(");
            string wake = src.Substring(wakeAt, src.IndexOf("\n            }", wakeAt, System.StringComparison.Ordinal) - wakeAt);
            StringAssert.Contains("SAMPLE_TEXTURE2D(_HHFoamBufferTex, sampler_HHFoamBufferTex, uv)", wake);

            int call = IndexOf(src, "float wakeFoam = WakeFoamCoverage(worldXY, bay, wakeFresh);");
            int zone = IndexOf(src, "float washZone = saturate(washOnSand);");
            int swap = IndexOf(src, "wakeFoam = lerp(wakeFoam, WashResidueCoverage(worldXY), washZone);");
            Assert.That(call < zone && zone < swap, "the residue swap rides the wake read, gated to the wash");
        }

        // ---- the arithmetic the no-checker claim rests on (twin of SurfBandValue / BandValue01) ----------

        static readonly float[] Bayer4 =
            { 0.5f, 8.5f, 2.5f, 10.5f, 12.5f, 4.5f, 14.5f, 6.5f, 3.5f, 11.5f, 1.5f, 9.5f, 15.5f, 7.5f, 13.5f, 5.5f };

        static float Sat(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        static float SurfBandValue(float v01, float bay, float bands, float dither)
        {
            if (bands < 2f) return Sat(v01);
            float steps = bands - 1f;
            float dithered = Sat(v01) + (bay - 0.5f) * (Sat(dither) / System.Math.Max(steps, 1f));
            return (float)System.Math.Floor(Sat(dithered) * steps + 0.5f) / steps;
        }

        static float BandValue01(float v01, float bandCount, float ditherWin, float bayer)
        {
            float bands = System.Math.Max(bandCount, 2f);
            float x = Sat(v01) * (bands - 1f);
            float fb = (float)System.Math.Floor(x);
            float win = System.Math.Min(System.Math.Max(ditherWin, 1e-3f), 1f);
            float e = Sat(((x - fb) - (0.5f - 0.5f * win)) / win);
            return (fb + (e > bayer ? 1f : 0f)) / (bands - 1f);
        }

        [Test]
        public void OnTheWash_EveryDitherCellAgrees_SoNoCheckerCanForm()
        {
            // The shipped dials (Water.mat): _SurfBands 4, _SurfBandDither 0.7, _WakeFoamBands 3.
            // The wash path hands 0.5 in place of the cell's Bayer threshold, so it is plain rounding
            // to the nearest band: one value for every pixel of a cell, whatever its Bayer slot.
            for (int i = 0; i < 512; i++)
            {
                float v = (i + 0.37f) / 512f; // off the exact ties, where BandValue01's 1e-3 window decides

                Assert.AreEqual(System.Math.Floor(v * 3f + 0.5f) / 3f, SurfBandValue(v, 0.5f, 4f, 0.7f), 1e-6, "surf v=" + v);
                Assert.AreEqual(System.Math.Floor(v * 2f + 0.5f) / 2f, BandValue01(v, 3f, 0f, 0.5f), 1e-6, "residue v=" + v);
            }

            // ...where the shipped path (each pixel's own Bayer threshold) splits a 4x4 cell into two
            // values around every surf band boundary: the 1-px checker the owner saw on the sand.
            foreach (float boundary in new[] { 1f / 6f, 0.5f, 5f / 6f })
            {
                var seen = new System.Collections.Generic.HashSet<float>();
                foreach (float b in Bayer4) seen.Add(SurfBandValue(boundary, b / 16f, 4f, 0.7f));
                Assert.AreEqual(2, seen.Count, "the shipped dither splits the cell at v=" + boundary);
            }
        }
    }
}
