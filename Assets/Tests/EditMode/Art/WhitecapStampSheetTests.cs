using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guards for the 2026-08-05 whitecap-lattice fix — the owner's *"some of the whitecaps
    /// have a square pattern that I recognize from earlier builds"*.
    ///
    /// <para><b>The mechanism, which is not the one the brief listed first.</b> It is neither pixel
    /// grid, nor the chunk lattice, nor twin-constant drift, nor the ordered dither. The painted
    /// whitecap slot has been ON since #87 at <c>_WhitecapTexStrength</c> 0.865, which means the
    /// PAINTED art — not the procedural evolving field — is what places open-water caps; and that art
    /// was ONE mark alone in a 64 px tile sampled on the shared <c>_PaintScale</c> 0.25 grid. One mark
    /// per repeat cell IS a lattice: an identical whitecap every 4 metres, in rows. Measured on a CPU
    /// port of the fragment: the strongest autocorrelation peak of the shipped cap mask sat exactly on
    /// (4.0, 0.5) m and (0, 3.8) m — the tile grid — and the 4 m lag carried r = 0.05..0.08 where the
    /// stamp sheet now carries r = -0.012..0.004.</para>
    ///
    /// <para><b>Why untiling was never going to fix it.</b> <c>UntileSampleW</c> hides a repeat by
    /// TRANSLATING whole repeat cells by a per-cell hash. Translating a lattice yields a lattice. The
    /// trick works on <c>Foam.png</c> (58% coverage, many varied marks — you stop recognising which
    /// cell you are in) and is structurally powerless against a tile whose entire content is one dot.
    /// So the guards below pin the ART and the two RELATIONSHIPS that keep it honest, not a pixel
    /// bar — the standing lesson that absolute bars rot as the look improves.</para>
    ///
    /// <para>All of this runs on CPU: PNGs are decoded from raw bytes with
    /// <see cref="ImageConversion.LoadImage"/> (the <c>WakeArtOrientationTests</c> precedent), so
    /// these pass on CI's GPU-less agent. Nothing here proves what the sea LOOKS like — that is the
    /// owner's eyeball on his own GPU.</para>
    /// </summary>
    public class WhitecapStampSheetTests
    {
        private const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        private const string LiveWaterMatPath = "Assets/_Project/Art/Materials/Water.mat";
        private const string PresetsFolder = "Assets/_Project/Art/Materials/WaterPresets";
        private const string SheetAssetPath = "_Project/Art/Textures/Water/Whitecaps.png";
        private const string MarkAssetPath = "_Project/Art/Textures/Water/WhitecapSource~/WhitecapMark.png";

        private static readonly string[] PresetNames =
        {
            "Water_DeepBlue", "Water_FoggySmother", "Water_GlassyCalm", "Water_NorthAtlantic",
            "Water_StirredBrown", "Water_StormGrey", "Water_Tropical", "Water_WarmShelter",
        };

        // ==== helpers =============================================================================

        private static string Full(string assetsRelative) =>
            Path.Combine(Application.dataPath, assetsRelative);

        private static Texture2D LoadPng(string assetsRelative)
        {
            string full = Full(assetsRelative);
            Assert.IsTrue(File.Exists(full), $"art present on disk: Assets/{assetsRelative}");
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(ImageConversion.LoadImage(tex, File.ReadAllBytes(full)),
                          $"PNG decodes: Assets/{assetsRelative}");
            return tex;
        }

        private static float MatFloat(string matPath, string key)
        {
            Assert.IsTrue(File.Exists(matPath), $"No material at '{matPath}'.");
            var m = Regex.Match(File.ReadAllText(matPath), $@"-\s{Regex.Escape(key)}:\s*(-?[\d.eE+]+)");
            Assert.IsTrue(m.Success, $"'{matPath}' does not serialize {key}.");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Marks in a white-on-transparent tile, found by 8-connected flood fill that WRAPS
        /// (the tile is sampled on Repeat, so a mark across the seam is one mark, not two).</summary>
        private static List<Vector2> MarkCentroids(Color32[] px, int w, int h)
        {
            var seen = new bool[w * h];
            var centroids = new List<Vector2>();
            var stack = new Stack<int>();
            for (int start = 0; start < px.Length; start++)
            {
                if (seen[start] || px[start].a == 0) continue;
                stack.Push(start);
                seen[start] = true;
                int count = 0;
                // accumulate on the TORUS: sum the offsets from the seed, wrapped to the short way round
                int sx = start % w, sy = start / w;
                float ox = 0f, oy = 0f;
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    int x = i % w, y = i / w;
                    ox += Mathf.Repeat(x - sx + w * 0.5f, w) - w * 0.5f;
                    oy += Mathf.Repeat(y - sy + h * 0.5f, h) - h * 0.5f;
                    count++;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = WhitecapStampSheetMath.Wrap(x + dx, w);
                            int ny = WhitecapStampSheetMath.Wrap(y + dy, h);
                            int n = ny * w + nx;
                            if (seen[n] || px[n].a == 0) continue;
                            seen[n] = true;
                            stack.Push(n);
                        }
                    }
                }
                centroids.Add(new Vector2(Mathf.Repeat(sx + ox / count, w),
                                          Mathf.Repeat(sy + oy / count, h)));
            }
            return centroids;
        }

        private static float OpaqueFraction(Color32[] px)
        {
            int n = 0;
            foreach (var c in px) if (c.a > 0) n++;
            return n / (float)px.Length;
        }

        /// <summary>Toroidal nearest-neighbour distance for each mark, in texels.</summary>
        private static List<float> NearestNeighbourDistances(List<Vector2> pts, int size)
        {
            var outp = new List<float>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                float best = float.MaxValue;
                for (int j = 0; j < pts.Count; j++)
                {
                    if (i == j) continue;
                    float dx = Mathf.Abs(pts[i].x - pts[j].x); if (dx > size * 0.5f) dx = size - dx;
                    float dy = Mathf.Abs(pts[i].y - pts[j].y); if (dy > size * 0.5f) dy = size - dy;
                    best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dy * dy));
                }
                outp.Add(best);
            }
            return outp;
        }

        // ==== the art: the slot that PLACES caps may not be a lattice ==============================

        [Test]
        public void TheProceduralField_PlacesTheCaps_NotTheStampSheet()
        {
            // ⭐⭐ THE OWNER'S 2026-09-02 RULING: *"let the procedural field replace the caps."*
            //
            // The whole of it is one number. `capField = lerp(capField, capPat, _WhitecapTexStrength)`,
            // and at the 0.865 every shipped material carried, that is not a blend — painted slots PLACE,
            // they do not decorate. The class doc above is this file's own account of what that cost: one
            // mark alone in a repeat cell IS a lattice, and no amount of untiling can rescue a tile whose
            // entire content is one dot. The sheet was rebuilt to many scattered marks (the tests below);
            // the ruling goes further and hands the placement back to the field that knows where the
            // crests actually are.
            //
            // ⚠️ ALL NINE materials, and not for tidiness: _WhitecapTexStrength is MOOD-EASED
            // (WaterSurface.MoodFloatNames), so the live value is lerped between the PRESET anchors by the
            // weather. One preset left at 0.865 would not be a stale key — it would be a sea that goes
            // back to the stamp sheet whenever the weather leaned that way, and `Apply water preset` would
            // stamp it over the hero material besides.
            //
            // ⚠️ Whitecaps.png is NOT deleted and the slot is NOT removed. Both stay reachable, and 1
            // still hands the caps back to the sheet exactly as before: it is the owner's art, and this
            // records which source SHIPS, not which one exists.
            var offenders = new List<string>();
            foreach (string path in AllWaterMaterials())
            {
                var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.IsNotNull(mat, $"{path} must exist");
                float strength = mat.GetFloat("_WhitecapTexStrength");
                if (strength > 1e-4f)
                    offenders.Add($"{Path.GetFileName(path)}: _WhitecapTexStrength {strength}");
            }
            Assert.IsEmpty(offenders,
                "the procedural field must place the caps on every water material — these still hand the " +
                "placement to the painted sheet, so the sea they draw wears one mark repeated:\n  " +
                string.Join("\n  ", offenders));

            // …and the SHADER's own default goes with them, so a material that does not carry the key at
            // all gets the field rather than the sheet.
            string src = File.ReadAllText(ShaderPath);
            StringAssert.IsMatch(@"_WhitecapTexStrength\s*\(""[^""]*""\s*,\s*Range\(0,1\)\)\s*=\s*0\b", src,
                "the shader's _WhitecapTexStrength default must be 0 — the field, not the sheet");
        }

        /// <summary>Every water material the game ships, hero first.</summary>
        private static IEnumerable<string> AllWaterMaterials()
        {
            yield return LiveWaterMatPath;
            foreach (string name in PresetNames)
                yield return $"{PresetsFolder}/{name}.mat";
        }

        [Test]
        public void TheShippedSheet_CarriesManyScatteredMarks_NotOne()
        {
            // THE defect, stated as art rather than as a number: a placement texture with ONE mark in
            // it cannot draw anything but a grid, no matter what the shader does downstream.
            var tex = LoadPng(SheetAssetPath);
            Assert.AreEqual(WhitecapStampSheetMath.SheetSize, tex.width, "sheet width");
            Assert.AreEqual(WhitecapStampSheetMath.SheetSize, tex.height, "sheet height");

            var marks = MarkCentroids(tex.GetPixels32(), tex.width, tex.height);
            Assert.GreaterOrEqual(marks.Count, WhitecapStampSheetMath.MarkCount - 2,
                $"the whitecap slot carries {marks.Count} mark(s). This slot PLACES the open-water " +
                "caps (it blends in at _WhitecapTexStrength 0.865), so its marks ARE the whitecaps: " +
                "one per repeat cell is a whitecap every 1/_WhitecapTexScale metres, in rows, which " +
                "is the square pattern this suite exists to keep out.");
        }

        [Test]
        public void TheOwnersSingleMarkTile_WouldFailThatCheck_TheSabotageArm()
        {
            // Prove the assertion above can see the defect it exists to pin — the discipline this
            // shader's suites hold. The pre-fix art is still in the repo as the bake SOURCE, so the
            // sabotage case is the real historical artefact, not a mock of it.
            var tex = LoadPng(MarkAssetPath);
            var marks = MarkCentroids(tex.GetPixels32(), tex.width, tex.height);
            Assert.Less(marks.Count, WhitecapStampSheetMath.MarkCount - 2,
                $"SABOTAGE NOT DETECTED — the pre-fix single-mark tile reports {marks.Count} marks, " +
                "which the check above would have PASSED. The test cannot see the lattice it pins.");
        }

        [Test]
        public void TheMarks_ScatterIrregularly_RatherThanSittingOnAGrid()
        {
            // Count alone is not enough: 16 marks on a 4x4 grid would pass the count and still rule a
            // ruler across the sea. A lattice has ONE nearest-neighbour distance; a scatter has a
            // spread of them. (Deliberately a relationship — a coefficient of variation — and not an
            // absolute spacing, so re-rolling the seed or re-authoring the mark stays legal.)
            var tex = LoadPng(SheetAssetPath);
            var marks = MarkCentroids(tex.GetPixels32(), tex.width, tex.height);
            var nn = NearestNeighbourDistances(marks, tex.width);

            float mean = 0f;
            foreach (float d in nn) mean += d;
            mean /= nn.Count;
            float var2 = 0f;
            foreach (float d in nn) var2 += (d - mean) * (d - mean);
            float cv = Mathf.Sqrt(var2 / nn.Count) / Mathf.Max(mean, 1e-4f);

            // A perfect grid scores exactly 0; the shipped scatter measures 0.139. The bar sits at
            // 0.10 — decisively above a lattice, with room for a re-rolled seed (dart-throwing with a
            // hard minimum spacing IS blue noise, so its spread is naturally modest).
            Assert.Greater(cv, 0.10f,
                $"the marks' nearest-neighbour distances vary by only {cv * 100f:0.0}% about " +
                $"{mean:0.0} px — that is a grid wearing a scatter's clothes. Placement must be " +
                "irregular, or the repeat reads as rows however large the sheet is.");

            // …and the scatter must still keep the stamps apart, or they merge into rafts and the
            // coverage (= the tuned foam density) drifts.
            foreach (float d in nn)
                Assert.GreaterOrEqual(d, WhitecapStampSheetMath.MinSpacingPx * 0.9f,
                    $"two stamps are {d:0.0} px apart, inside the " +
                    $"{WhitecapStampSheetMath.MinSpacingPx} px minimum spacing — overlapping stamps " +
                    "eat coverage and clump.");
        }

        [Test]
        public void TheSheet_KeepsTheOwnersFoamDensity_MarkCountTimesTheMarksOwnArea()
        {
            // The fix is allowed to break the lattice; it is NOT allowed to restyle the sea. The sheet
            // is 4x the old tile in each axis and carries 4x4 marks, so foam per square metre — the
            // thing the owner actually tuned — is unchanged by construction. This asserts the
            // construction held.
            float sheet = OpaqueFraction(LoadPng(SheetAssetPath).GetPixels32());
            float mark = OpaqueFraction(LoadPng(MarkAssetPath).GetPixels32());
            Assert.AreEqual(mark, sheet, 0.005f,
                $"the sheet covers {sheet * 100f:0.00}% of its area but the mark it is made of covers " +
                $"{mark * 100f:0.00}% of its own — the sea just gained or lost foam, which is a look " +
                "change the owner did not ask for. Adjust MarkCount, not the coverage.");
        }

        [Test]
        public void TheShippedSheet_IsExactlyWhatTheBakerLays()
        {
            // The committed PNG and the baker cannot drift: re-compose from the owner's mark and
            // compare PIXELS (never bytes — a different PNG encoder writes different bytes for the
            // same image, and that is fine).
            var expected = WhitecapStampSheetMath.Compose(LoadPng(MarkAssetPath).GetPixels32());
            var actual = LoadPng(SheetAssetPath).GetPixels32();
            Assert.AreEqual(expected.Length, actual.Length, "sheet pixel count");
            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i].r == actual[i].r && expected[i].g == actual[i].g &&
                    expected[i].b == actual[i].b && expected[i].a == actual[i].a) continue;
                int n = WhitecapStampSheetMath.SheetSize;
                Assert.Fail($"the committed sheet differs from a fresh bake at texel " +
                            $"({i % n}, {i / n}) — re-run 'Hidden Harbours ▸ Art ▸ Bake Whitecap " +
                            "Stamp Sheet' and commit the result.");
            }
        }

        // ==== the two relationships that keep the sheet drawing the same whitecap =================

        [Test]
        public void TheSheetScale_KeepsTheOwnersMarkAtTheSizeItAlreadyWas()
        {
            // ⚠️ The units trap this repo has been bitten by before, in its own shape: a bigger sheet
            // at the SAME scale would draw the owner's mark a quarter the size (spray, not whitecaps)
            // and re-quantize it against the pixel grid. The invariant is texels-per-METRE, and it is
            // a relationship between four numbers in three files — exactly the kind of thing that
            // rots silently when someone edits one of them.
            float paintScale = MatFloat(LiveWaterMatPath, "_PaintScale");
            float sheetScale = MatFloat(LiveWaterMatPath, "_WhitecapTexScale");
            float sheetTexels = WhitecapStampSheetMath.TexelsPerMetre(
                WhitecapStampSheetMath.SheetSize, sheetScale);
            float legacyTexels = WhitecapStampSheetMath.TexelsPerMetre(
                WhitecapStampSheetMath.MarkCellSize, paintScale);

            Assert.AreEqual(legacyTexels, sheetTexels, 1e-3f,
                $"the sheet resolves at {sheetTexels:0.00} texels/m but the mark was painted for " +
                $"{legacyTexels:0.00} — every whitecap just changed size and landed on a different " +
                "pixel grid. Move _WhitecapTexScale and the sheet size together.");
        }

        [Test]
        public void TheRepeatPeriod_IsManyWhitecapsWide_NotAFewMetres()
        {
            // The other half of "why 4 m was visible": a repeat only reads as a pattern when its
            // period is close to the size of the thing being repeated. Pinned as a RATIO against the
            // mark's own world size, so it survives any future retune of either number.
            float sheetScale = MatFloat(LiveWaterMatPath, "_WhitecapTexScale");
            float periodMetres = 1f / sheetScale;

            var mark = LoadPng(MarkAssetPath);
            var px = mark.GetPixels32();
            int minX = mark.width, maxX = -1;
            for (int y = 0; y < mark.height; y++)
                for (int x = 0; x < mark.width; x++)
                    if (px[y * mark.width + x].a > 0)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                    }
            float texelsPerMetre = WhitecapStampSheetMath.TexelsPerMetre(
                WhitecapStampSheetMath.SheetSize, sheetScale);
            float markMetres = (maxX - minX + 1) / texelsPerMetre;

            Assert.Greater(periodMetres / markMetres, 8f,
                $"the whitecap slot repeats every {periodMetres:0.0} m while a whitecap is only " +
                $"{markMetres:0.00} m wide — that few caps between repeats is what the eye reads as " +
                "rows. The pre-fix slot repeated every 4.0 m at the same mark size.");
        }

        // ==== the wiring: the right key, in every material, and in the shader =====================

        [Test]
        public void TheShader_SamplesTheWhitecapSlot_AtItsOwnScale_NotThePaintGrid()
        {
            // Source-scraped for the same reason WaterBandRegularityTests scrapes: this is an
            // argument about which of two grids a layer is derived against, and a value check cannot
            // see it. If the whitecap sample ever goes back to _PaintScale, the sheet's period
            // collapses from 16 m to 4 m and the owner's squares return exactly as before.
            string src = File.ReadAllText(ShaderPath);
            Assert.IsTrue(Regex.IsMatch(src,
                    @"UntileSampleW\(TEXTURE2D_ARGS\(_WhitecapTex,\s*sampler_WhitecapTex\),\s*" +
                    @"worldXY,\s*_WhitecapTexScale,"),
                "the whitecap slot must be sampled at _WhitecapTexScale — it is the slot that PLACES " +
                "the caps, so its scale is the whitecap lattice, not a mood.");
            // and the shared grid must still own the slots it always owned
            StringAssert.Contains("worldXY, _PaintScale, fScroll, _UntileStrength", src,
                "the foam slot still rides the shared painted grid — this change is whitecap-only");
        }

        [Test]
        public void EveryPreset_SerializesTheSheetScale_AndAgreesWithTheLiveMaterial()
        {
            // The standing preset trap (the _RainRingStrength / _PixelsPerUnit precedents). "Water
            // Presets ▸ Apply" is a wholesale CopyPropertiesFromMaterial, so a preset that omits
            // _WhitecapTexScale stamps the shader default over it — and a preset that carries a
            // DIFFERENT one re-scales the whitecap sheet for the whole sea, not just its own mood.
            float live = MatFloat(LiveWaterMatPath, "_WhitecapTexScale");
            foreach (string name in PresetNames)
            {
                string path = $"{PresetsFolder}/{name}.mat";
                Assert.IsTrue(File.ReadAllText(path).Contains("- _WhitecapTexScale:"),
                    $"'{name}.mat' does not serialize _WhitecapTexScale — applying it would stamp the " +
                    "shader default over the live value.");
                Assert.AreEqual(live, MatFloat(path, "_WhitecapTexScale"), 1e-4f,
                    $"'{name}.mat' would re-scale the whitecap sheet for the entire sea when applied.");
            }
        }

        // ==== the placement math itself ===========================================================

        [Test]
        public void ThePlacement_IsDeterministic_AndFillsTheSheet()
        {
            var a = WhitecapStampSheetMath.Placements();
            var b = WhitecapStampSheetMath.Placements();
            Assert.AreEqual(WhitecapStampSheetMath.MarkCount, a.Count,
                "dart-throwing did not place every stamp inside MaxAttempts — lower MarkCount or " +
                "MinSpacingPx rather than raising the attempt ceiling.");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].X, b[i].X); Assert.AreEqual(a[i].Y, b[i].Y);
                Assert.AreEqual(a[i].FlipX, b[i].FlipX); Assert.AreEqual(a[i].FlipY, b[i].FlipY);
            }
        }

        [Test]
        public void ThePlacement_RespectsTheSpacing_OnTheTorus()
        {
            // Wrapped, because the sheet is sampled on Repeat: two stamps either side of the seam are
            // neighbours on the water even though their coordinates are far apart.
            var p = WhitecapStampSheetMath.Placements();
            int min = WhitecapStampSheetMath.MinSpacingPx * WhitecapStampSheetMath.MinSpacingPx;
            for (int i = 0; i < p.Count; i++)
                for (int j = i + 1; j < p.Count; j++)
                    Assert.GreaterOrEqual(
                        WhitecapStampSheetMath.ToroidalDistanceSq(p[i].X, p[i].Y, p[j].X, p[j].Y,
                                                                  WhitecapStampSheetMath.SheetSize),
                        min, $"stamps {i} and {j} are closer than the minimum spacing ACROSS THE SEAM");
        }

        [Test]
        public void TheMarkIsMirroredOnly_NeverResampled()
        {
            // Pixel-art law: the owner's mark ships as his pixels. Mirrors are the only variation the
            // scatter applies, and both axes must actually be used or the sheet reads as one stamp
            // repeated at irregular spacing (better, but still recognisable).
            var p = WhitecapStampSheetMath.Placements();
            bool anyX = false, anyY = false, anyNeither = false;
            foreach (var q in p)
            {
                anyX |= q.FlipX; anyY |= q.FlipY; anyNeither |= !q.FlipX && !q.FlipY;
            }
            Assert.IsTrue(anyX && anyY && anyNeither,
                "the scatter should use both mirrors and the unmirrored mark — all four orientations " +
                "are what stop 16 copies of one shape reading as 16 copies of one shape.");
        }
    }
}
