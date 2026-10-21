using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// THE PASS-4 WIND TWIN, ON ITS OWN: <see cref="TreeWindMath"/> is the C# copy of
    /// <c>TreeWindMaps.hlsl</c>, so the HLSL's constants and its load-bearing lines are pinned to the twin
    /// here, and the twin's arithmetic is pinned to hand-worked values: the byte round trip, JavaScript's
    /// rounding, the flutter slot, the word's fields, the colour pick, the texel maths and the frame.
    /// The twin against the RIG is <c>TreeWindMathRigParityTests</c>; this file needs no V8 host.
    ///
    /// <para>Then the pieces the path runs through in the editor: the trunk anchor binds a complete set of
    /// maps and refuses a partial one, the catalog loads no maps for a pass-3 tree, the tree material ships
    /// with the path off, the importer reads every new sheet but the albedo as data, a pass-4 sheet is
    /// sliced full-rect, and only the snow sheet imports as one channel.</para>
    /// </summary>
    public class TreeWindMathTests
    {
        const string HlslPath = "Assets/_Project/Art/Shaders/Include/TreeWindMaps.hlsl";

        static string ReadHlsl() =>
            File.ReadAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, HlslPath));

        // =====================================================================================
        // the HLSL is the twin's
        // =====================================================================================

        /// <summary>Every value <c>#define TREE_WIND_*</c> the shader has, with the twin's value for it.</summary>
        static readonly (string name, double value)[] TwinDefines =
        {
            ("LOOP", TreeWindMath.Loop),
            ("X_SPAN", TreeWindMath.XSpan),
            ("J_SPAN", TreeWindMath.JSpan),
            ("R_MAX", TreeWindMath.RMax),
            ("MARGIN", TreeWindMath.Margin),
            ("REACH", TreeWindMath.Reach),
            ("ITERATIONS", TreeWindMath.Iterations),
            ("FLUTTER_SLOTS", TreeWindMath.FlutterSlots),
            ("GAP_SNOW_MIN", TreeWindMath.GapSnowMin),
            ("SNOW_SCALE", TreeWindMath.SnowScale),
            ("SNOW_ON", TreeWindMath.SnowOn),
            ("CLASS_OUTSIDE", TreeWindMath.ClassOutside),
            ("CLASS_WOOD", TreeWindMath.ClassWood),
            ("CLASS_BETWEEN", TreeWindMath.ClassBetween),
            ("CLASS_LEAF", TreeWindMath.ClassLeaf),
            ("DITHER", TreeWindMath.Dither),
            ("FLUTTER_FRAMES", TreeWindMath.FlutterFrames),
            ("TAU", TreeWindMath.Tau),
            ("PICK_ALBEDO", (int)TreeWindMath.Source.Albedo),
            ("PICK_SNOW_ROW", (int)TreeWindMath.Source.SnowRow),
            ("PICK_GAP_ROW", (int)TreeWindMath.Source.GapRow),
        };

        /// <summary>The defines that disagree with the twin: a missing name, a different value (compared
        /// as the float both sides compute in), or a numeric <c>TREE_WIND_*</c> define the twin does not
        /// carry. The two macros (<c>TREE_WIND_MAPS_MATERIAL_ROWS</c>, <c>TREE_WIND_MAPS_PARAMS(p)</c>)
        /// have no number, so they are not constants.</summary>
        static List<string> DefineMismatches(string hlsl)
        {
            var found = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(hlsl, @"^#define\s+TREE_WIND_([A-Z0-9_]+)\s+(\S+)", RegexOptions.Multiline))
                found[m.Groups[1].Value] = m.Groups[2].Value;

            var bad = new List<string>();
            foreach (var (name, want) in TwinDefines)
            {
                if (!found.TryGetValue(name, out string text))
                {
                    bad.Add($"{name}: missing from the HLSL");
                    continue;
                }
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double got) ||
                    (float)got != (float)want)
                    bad.Add($"{name}: the HLSL says {text}, the twin {want.ToString("R", CultureInfo.InvariantCulture)}");
            }
            var known = new HashSet<string>(TwinDefines.Select(d => d.name), StringComparer.Ordinal);
            foreach (var kv in found)
                if (!known.Contains(kv.Key) &&
                    double.TryParse(kv.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    bad.Add($"{kv.Key}: a numeric define the twin does not carry");
            return bad;
        }

        [Test]
        public void TheHlsl_DefinesTheTwinsConstants()
        {
            string hlsl = ReadHlsl();
            CollectionAssert.IsEmpty(DefineMismatches(hlsl), $"{HlslPath} and TreeWindMath disagree.");

            string sabotaged = Regex.Replace(hlsl, @"(#define\s+TREE_WIND_MARGIN\s+)3\b", "${1}4");
            Assert.AreNotEqual(hlsl, sabotaged, "The sabotage found no MARGIN define to change.");
            Assert.IsTrue(DefineMismatches(sabotaged).Any(s => s.StartsWith("MARGIN:", StringComparison.Ordinal)),
                          "SABOTAGE: a margin of 4 in the HLSL must be caught.");
        }

        /// <summary>The HLSL lines the twin's arithmetic stands on, verbatim: the rounding, the hash, the byte
        /// rebuild, the frame, the word, the depth test, the snow pick, the texel maths, the field and the
        /// flutter gate. Edit one in the shader and this names it; then edit the twin to match.</summary>
        static readonly string[] LoadBearing =
        {
            "return (int)f + ((x - f) >= 0.5 ? 1 : 0);",
            "uint h = ((uint)n ^ ((uint)(k + 1) * 0x9e3779b9u)) * 0x85ebca6bu;",
            "return (uint)(u * 255.0 + 0.5);",
            "fr.ww = saturate(len * p.response);",
            "fr.dir = windWorld.x < 0.0 ? -1.0 : 1.0;",
            "fr.side = abs(windWorld.x) / max(len, 1e-4);",
            "t.word = (TreeByte(w.b) << 16) | (TreeByte(w.a) << 8) | TreeByte(f.a);",
            "if (j.dep > b.dep + TREE_WIND_MARGIN) return true;",
            "int kk = TreeJsRound(cover * TREE_WIND_SNOW_SCALE);",
            "bool on = snowing && (int)max(snowByte, (uint)TREE_WIND_GAP_SNOW_MIN) <= kk;",
            "return int2(cellX0 + rig.x, cellH - 1 - rig.y);",
            "return (float2(texel) + 0.5) / cell.zw;",
            "fr.ph = f16 / (float)TREE_WIND_LOOP;",
            "fr.env = 1.0 + fr.gust * 0.9 * sin(TREE_WIND_TAU * fr.ph + 0.7);",
            "fr.trunk = fr.ww * fr.ww * p.bendPx * (0.8 + 0.2 * fr.env) * fr.side",
            "+ fr.ww * p.bendPx * 0.42 * sin(TREE_WIND_TAU * fr.ph + 0.3) * (0.55 + 0.45 * fr.env);",
            "fr.la = p.limbPx * fr.ww * (0.6 + 0.4 * fr.env);",
            "fr.limbBias = 0.5 * fr.side;",
            "fr.dRate = min(1.0, (p.flutter * resp * 0.35 + p.shimmerCalm * 0.03) * TREE_WIND_LOOP / TREE_WIND_FLUTTER_FRAMES);",
            "fr.still = fr.ww <= 0.0 && fr.dRate <= 0.0;",
            "return word >> 22;",
            "return (word >> 19) & 7u;",
            "return (word >> 16) & 7u;",
            "return (word >> 13) & 7u;",
            "return word & 0x1FFFu;",
            "float p = 2.0 * TREE_WIND_TAU * fr.ph - fr.dir * 0.9 * t.x + t.j;",
            "float fx = fr.dir * (fr.trunk * t.lean + fr.la * reach * (sin(p) + fr.limbBias));",
            "float fy = -fr.la * fr.bob * 0.5 * t.sway * sin(p + 1.1);",
            "int s0 = (int)(TreeHash(gs, 10 + e) >> 28);",
            "float gate = fr.dRate * (1.0 + fr.gust * 0.9 * sin(TREE_WIND_TAU * s0 / TREE_WIND_LOOP + 0.7 - fr.dir * t.x * 1.3));",
            "bool snowing = cover > TREE_WIND_SNOW_ON;",
            "bool lit = snowing && (int)snowByte <= kk;",
            "float reach = t.sway > 0.0 ? pow(abs(t.sway), 1.2) : 0.0;",
            "int age = (int)((uint)(fr.f16 - s0 + TREE_WIND_LOOP) % (uint)TREE_WIND_LOOP);",
        };

        static string[] Missing(string hlsl, IEnumerable<string> lines) =>
            lines.Where(l => hlsl.IndexOf(l, StringComparison.Ordinal) < 0).ToArray();

        [Test]
        public void TheHlsl_KeepsTheLoadBearingExpressions()
        {
            string hlsl = ReadHlsl();
            CollectionAssert.IsEmpty(Missing(hlsl, LoadBearing),
                                     $"{HlslPath} no longer says what TreeWindMath computes.");

            string sabotaged = hlsl.Replace("0x85ebca6bu", "0x85ebca6cu");
            Assert.AreNotEqual(hlsl, sabotaged, "The sabotage found no hash multiplier to change.");
            CollectionAssert.Contains(Missing(sabotaged, LoadBearing), LoadBearing[1],
                                      "SABOTAGE: a hash one bit off must be caught.");
        }

        // =====================================================================================
        // the twin's arithmetic
        // =====================================================================================

        /// <summary>The shader reads a UNORM texel as <c>byte / 255</c> and rebuilds the byte with
        /// <see cref="TreeWindMath.ByteOf"/>: it must round, not truncate, so a quarter-texel of float error
        /// either way still lands on the byte.</summary>
        [Test]
        public void ByteOf_RoundTripsEveryByte()
        {
            int truncated = 0;
            for (int b = 0; b <= 255; b++)
            {
                Assert.AreEqual(b, TreeWindMath.ByteOf(b / 255f), $"byte {b}");
                Assert.AreEqual(b, TreeWindMath.ByteOf((b + 0.25f) / 255f), $"byte {b} + ¼");
                Assert.AreEqual(b, TreeWindMath.ByteOf((b - 0.25f) / 255f), $"byte {b} − ¼");
                if ((int)((b - 0.25f) / 255f * 255f) != b) truncated++;
            }
            Assert.Greater(truncated, 200, $"SABOTAGE: truncation loses only {truncated} of 256 bytes a quarter-texel low.");
        }

        /// <summary>The rig rounds with JavaScript's <c>Math.round</c> (half up, toward +∞); Unity's
        /// <c>Mathf.RoundToInt</c> rounds half to even and would move the snow threshold.</summary>
        [Test]
        public void JsRound_RoundsHalfUp_LikeJavaScript()
        {
            Assert.AreEqual(3, TreeWindMath.JsRound(2.5f));
            Assert.AreEqual(-1, TreeWindMath.JsRound(-1.5f));
            Assert.AreEqual(-2, TreeWindMath.JsRound(-2.5f));
            Assert.AreEqual(0, TreeWindMath.JsRound(-0.5f));
            Assert.AreEqual(1, TreeWindMath.JsRound(1.4f));
            Assert.AreEqual(2, TreeWindMath.JsRound(1.6f));

            Assert.AreEqual(2, Mathf.RoundToInt(2.5f), "SABOTAGE: RoundToInt takes 2.5 to 2");
            Assert.AreEqual(-2, Mathf.RoundToInt(-1.5f), "SABOTAGE: RoundToInt takes -1.5 to -2");
        }

        /// <summary>A leaf's flutter slot is the hash's top four bits, exactly the rig's
        /// <c>floor(hsh · 16)</c>. Going through a float instead rounds the largest hashes up to 1 and
        /// gives a 17th slot.</summary>
        [Test]
        public void TheFlutterSlot_IsTheTopFourBits_AndTheFloatPathIsNot()
        {
            int checkedPairs = 0;
            for (int n = -1; n < 8192; n += 13)
                for (int k = 0; k < 18; k++)
                {
                    uint h = TreeWindMath.Hash(n, k);
                    Assert.AreEqual((int)Math.Floor(h / 4294967296.0 * 16), (int)(h >> 28), $"hash({n}, {k})");
                    checkedPairs++;
                }
            Assert.Greater(checkedPairs, 10000);

            uint top = uint.MaxValue;
            Assert.AreEqual(15, (int)(top >> 28));
            Assert.AreEqual(16, (int)((float)top * (1f / 4294967296f) * 16f),
                            "SABOTAGE: through a float the largest hash lands in slot 16, one past the loop's last.");
        }

        [Test]
        public void Decode_PicksAlbedoSnowOrGap_ByCoverAndClass()
        {
            const int Leaf = (3 << 22) | (5 << 19) | (6 << 16) | (2 << 13) | 123;
            const int Between = (2 << 22) | (5 << 19) | (6 << 16) | (2 << 13) | 123;

            void Is(TreeWindMath.Source source, int index, TreeWindMath.Pick pick, string why)
            {
                Assert.AreEqual(source, pick.Source, why);
                if (source != TreeWindMath.Source.Albedo) Assert.AreEqual(index, pick.Index, why);
            }

            var albedo = TreeWindMath.Source.Albedo;
            var snow = TreeWindMath.Source.SnowRow;
            var gap = TreeWindMath.Source.GapRow;

            // No snow at a cover of 0.02 or less: the albedo, or the gap row for a leaf that fell back.
            Is(albedo, 0, TreeWindMath.Decode(Leaf, 0, false, 0.02f), "cover 0.02");
            Is(gap, 6, TreeWindMath.Decode(Leaf, 0, true, 0.02f), "cover 0.02, fallback");

            // Just over: kk = round(0.021 · 254) = 5, so byte 5 is lit and byte 6 is not.
            Is(snow, 2, TreeWindMath.Decode(Leaf, 5, false, 0.021f), "cover 0.021, byte 5");
            Is(albedo, 0, TreeWindMath.Decode(Leaf, 6, false, 0.021f), "cover 0.021, byte 6");

            // A fallback leaf snows only once kk reaches the gap-snow floor, 153, whatever its own byte.
            Is(gap, 6, TreeWindMath.Decode(Leaf, 10, true, 0.6f), "cover 0.6 (kk 152), fallback");
            Is(snow, 5, TreeWindMath.Decode(Leaf, 10, true, 0.61f), "cover 0.61 (kk 155), fallback, byte 10");
            Is(gap, 6, TreeWindMath.Decode(Leaf, 200, true, 0.61f), "cover 0.61 (kk 155), fallback, byte 200");

            // Full cover lights byte 254; byte 255 never snows, and cover past 1 is clamped.
            Is(snow, 2, TreeWindMath.Decode(Leaf, 254, false, 1f), "cover 1, byte 254");
            Is(albedo, 0, TreeWindMath.Decode(Leaf, 255, false, 1f), "cover 1, byte 255");
            Is(snow, 2, TreeWindMath.Decode(Leaf, 254, false, 2f), "cover 2, byte 254");
            Is(albedo, 0, TreeWindMath.Decode(Leaf, 255, false, 2f), "cover 2, byte 255");

            // Only a LEAF falls back to the gap row: anything else takes the ordinary path.
            Is(snow, 2, TreeWindMath.Decode(Between, 5, true, 0.021f), "between, fallback, byte 5");
            Is(albedo, 0, TreeWindMath.Decode(Between, 6, true, 0.021f), "between, fallback, byte 6");
        }

        [Test]
        public void TheWord_SplitsIntoItsFields()
        {
            int word = TreeWindMath.WordOf(238, 64, 123);
            Assert.AreEqual(15614075, word);
            Assert.AreEqual(0xEE407B, word);
            Assert.AreEqual(3, TreeWindMath.ClassOf(word), "class");
            Assert.AreEqual(5, TreeWindMath.GapSnowIndexOf(word), "gap snow");
            Assert.AreEqual(6, TreeWindMath.GapIndexOf(word), "gap");
            Assert.AreEqual(2, TreeWindMath.SnowIndexOf(word), "snow");
            Assert.AreEqual(123, TreeWindMath.IdOf(word), "id");
            Assert.AreEqual(122, TreeWindMath.StampOf(word), "stamp");

            var t = TreeWindMath.TexelOf(51, 255, 238, 64, 0, 255, 200, 123);
            Assert.AreEqual(0.2f, t.Lean, 1e-6f, "lean");
            Assert.AreEqual(1.3f, t.Sway, 1e-6f, "sway");
            Assert.AreEqual(-2f, t.X, 1e-6f, "x");
            Assert.AreEqual(0.25f, t.J, 1e-6f, "j");
            Assert.AreEqual(0xEE407B, t.Word, "word");
            Assert.AreEqual(200, t.Dep, "depth");
        }

        /// <summary>A 20×7 sheet of four 5×7 cells: a sheet texel goes to its cell and rig texel and back,
        /// and through its centre uv and back; a uv on or past the edge stays on the sheet.</summary>
        [Test]
        public void TexelMath_RoundTripsBetweenRigSheetAndUv()
        {
            const int CellW = 5, CellH = 7, SheetW = 20, SheetH = 7;
            for (int ty = 0; ty < SheetH; ty++)
                for (int tx = 0; tx < SheetW; tx++)
                {
                    var texel = new Vector2Int(tx, ty);
                    var rig = TreeWindMath.RigOfTexel(texel, CellW, CellH, out int cellX0);
                    Assert.AreEqual(tx / CellW * CellW, cellX0, $"{texel}: cell");
                    Assert.AreEqual(new Vector2Int(tx - cellX0, CellH - 1 - ty), rig, $"{texel}: rig texel");
                    Assert.AreEqual(texel, TreeWindMath.TexelOfRig(cellX0, CellH, rig.x, rig.y), $"{texel}: back from the rig");
                    Assert.AreEqual(texel, TreeWindMath.TexelOfUv(TreeWindMath.SourceUv(texel, SheetW, SheetH), SheetW, SheetH),
                                    $"{texel}: back from its uv");
                }
            Assert.AreEqual(new Vector2Int(19, 6), TreeWindMath.TexelOfUv(new Vector2(1f, 1f), SheetW, SheetH), "uv 1 stays on the sheet");
            Assert.AreEqual(new Vector2Int(0, 6), TreeWindMath.TexelOfUv(new Vector2(-0.1f, 1.5f), SheetW, SheetH), "a uv off the sheet is kept on it");
        }

        [Test]
        public void FrameOf_ReadsTheWindsDirectionAndStrength()
        {
            var aspen = new TreeWindMath.Params
            {
                BendPx = 3f, LimbPx = 2f, Bob = 0.5f, Flutter = 1f, ShimmerCalm = 0.35f,
                Conifer = false, Gust = 0.4f, Response = 1f,
            };

            var north = TreeWindMath.FrameOf(new Vector2(0f, 1f), 0, aspen);
            Assert.AreEqual(0f, north.Side, "north: no share along x");
            Assert.AreEqual(0f, north.LimbBias, "north: no steady limb lean");
            Assert.AreEqual(1f, north.Dir, "north: +x by default");
            Assert.AreEqual(1f, north.Ww, 1e-6f, "north: a full gale");

            var east = TreeWindMath.FrameOf(new Vector2(1f, 0f), 0, aspen);
            Assert.AreEqual(1f, east.Side, 1e-6f, "east: all of it along x");
            Assert.AreEqual(0.5f, east.LimbBias, 1e-6f, "east: the rig's +0.5");
            Assert.AreEqual(1f, east.Dir, "east");

            var west = TreeWindMath.FrameOf(new Vector2(-1f, 0f), 0, aspen);
            Assert.AreEqual(-1f, west.Dir, "west");
            Assert.AreEqual(1f, west.Side, 1e-6f, "west");

            var conifer = aspen;
            conifer.Flutter = 0.1f;
            conifer.ShimmerCalm = 0f;
            conifer.Conifer = true;
            var calmConifer = TreeWindMath.FrameOf(Vector2.zero, 0, conifer);
            Assert.AreEqual(0f, calmConifer.Ww, "calm");
            Assert.AreEqual(0f, calmConifer.DRate, "a conifer in calm does not flutter");
            Assert.IsTrue(calmConifer.Still, "a calm conifer draws every texel from itself");

            var calmAspen = TreeWindMath.FrameOf(Vector2.zero, 0, aspen);
            Assert.AreEqual(0.35f * 0.03f * TreeWindMath.Loop / TreeWindMath.FlutterFrames, calmAspen.DRate, 1e-6f, "shimmer in calm");
            Assert.AreEqual(0.05793f, calmAspen.DRate, 1e-4f, "shimmer in calm");
            Assert.IsFalse(calmAspen.Still, "an aspen shimmers in calm");

            Assert.Throws<ArgumentOutOfRangeException>(() => TreeWindMath.FrameOf(Vector2.right, TreeWindMath.Loop, aspen));
            Assert.Throws<ArgumentOutOfRangeException>(() => TreeWindMath.FrameOf(Vector2.right, -1, aspen));
        }

        [Test]
        public void F16_WrapsTheLoopPosition()
        {
            Assert.AreEqual(0, TreeWindMath.F16(1f));
            Assert.AreEqual(15, TreeWindMath.F16(-0.01f));
            Assert.AreEqual(8, TreeWindMath.F16(2.5f));
            Assert.AreEqual(0, TreeWindMath.F16(0f));
            Assert.AreEqual(15, TreeWindMath.F16(0.99f));
        }

        // =====================================================================================
        // the anchor, the catalog, the material, the importer
        // =====================================================================================

        [Test]
        public void TheAnchor_BindsACompleteSetOfWindMaps_AndRefusesAnIncompleteOne()
        {
            var made = new List<UnityEngine.Object>();
            Texture2D Tex(int w, int h)
            {
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
                made.Add(t);
                return t;
            }

            try
            {
                var complete = new TreeWindMaps
                {
                    Wind = Tex(8, 4), Phase = Tex(8, 4), Snow = Tex(8, 4), Palette = Tex(8, 3),
                    BendPx = 3.5f, LimbPx = 1.25f, Bob = 0.4f, Flutter = 0.9f, ShimmerCalm = 0.35f, Conifer = true,
                    PaletteRow = 1, CellW = 2, CellH = 4, SheetW = 8, SheetH = 4,
                };
                Assert.IsTrue(complete.IsComplete, "the complete set");
                WithAnchor(complete, (sr, anchor) =>
                {
                    Assert.IsTrue(anchor.HasWindMaps, "complete: HasWindMaps");
                    Assert.AreEqual(1f, TreeTrunkAnchor.TreeMapsOn(sr), "complete: the maps flag");
                    Assert.AreEqual(new Vector4(3.5f, 1.25f, 0.4f, 0.9f), TreeTrunkAnchor.Wind0On(sr), "complete: _TreeWind0");
                    Assert.AreEqual(new Vector4(0.35f, 1f, 1f, 0f), TreeTrunkAnchor.Wind1On(sr), "complete: _TreeWind1");
                    Assert.AreEqual(new Vector4(2f, 4f, 8f, 4f), TreeTrunkAnchor.CellOn(sr), "complete: the cell");
                    Assert.AreEqual(complete.Wind, TreeTrunkAnchor.WindTexOn(sr), "complete: the wind map");
                    Assert.AreEqual(complete.Phase, TreeTrunkAnchor.PhaseTexOn(sr), "complete: the phase map");
                    Assert.AreEqual(complete.Snow, TreeTrunkAnchor.SnowTexOn(sr), "complete: the snow map");
                    Assert.AreEqual(complete.Palette, TreeTrunkAnchor.PaletteOn(sr), "complete: the palette");
                });

                var snowRowPicked = complete;
                snowRowPicked.PaletteRow = 0;
                var pastThePalette = complete;
                pastThePalette.PaletteRow = 3;
                var notOneRow = complete;
                notOneRow.SheetH = 3;
                var ragged = complete;
                ragged.SheetW = 7;
                var noSnow = complete;
                noSnow.Snow = null;
                var incomplete = new (string why, TreeWindMaps maps)[]
                {
                    ("palette row 0 is the snow row", snowRowPicked),
                    ("palette row 3 is past a 3-row palette", pastThePalette),
                    ("a sheet 3 high over a 4-high cell", notOneRow),
                    ("a sheet 7 wide of 2-wide cells", ragged),
                    ("no snow map", noSnow),
                    ("nothing at all", default),
                };
                foreach (var (why, maps) in incomplete)
                {
                    Assert.IsFalse(maps.IsComplete, why);
                    WithAnchor(maps, (sr, anchor) =>
                    {
                        Assert.IsFalse(anchor.HasWindMaps, $"{why}: HasWindMaps");
                        Assert.AreEqual(0f, TreeTrunkAnchor.TreeMapsOn(sr), $"{why}: the maps flag");
                    });
                }
            }
            finally
            {
                foreach (var o in made)
                    if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }
        }

        static void WithAnchor(TreeWindMaps maps, Action<SpriteRenderer, TreeTrunkAnchor> check)
        {
            var go = new GameObject("TreeWindMathTests_Anchor");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                var anchor = go.AddComponent<TreeTrunkAnchor>();
                anchor.SetWindMaps(maps);
                check(sr, anchor);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void LoadWindMaps_IsEmptyForPass3_AndCompleteForPass4()
        {
            Assert.IsFalse(AcadianTreeCatalog.LoadWindMaps(default(AcadianTreeCatalog.Placement)).IsComplete,
                           "An invalid placement loads no maps.");
            var placements = AcadianTreeCatalog.Scan();
            Assert.IsNotEmpty(placements, "The kit has no trees to place.");
            int pass4 = 0;
            foreach (var pl in placements)
            {
                var maps = AcadianTreeCatalog.LoadWindMaps(pl);
                string who = $"{pl.Entry.species}/{pl.Season}";
                if (TreeKitCatalog.HasPass4(pl.Entry))
                {
                    pass4++;
                    Assert.IsTrue(maps.IsComplete, $"{who}: a pass-4 tree loads its full set of maps");
                }
                else
                {
                    Assert.IsNull(maps.Wind, $"{who}: a pass-3 tree loads no wind map");
                    Assert.IsFalse(maps.IsComplete, $"{who}: a pass-3 tree has no maps");
                }
            }
            Debug.Log($"[TreeWindMath] {placements.Count} placements, {pass4} with pass-4 maps.");
        }

        [Test]
        public void TheTreeMaterial_ShipsWithTheMapsOff()
        {
            var material = AcadianTreeCatalog.LoadMaterial();
            Assert.IsNotNull(material, $"{AcadianTreeCatalog.MaterialPath} is missing.");
            Assert.IsTrue(material.HasProperty(TreeTrunkAnchor.MapsProperty),
                          $"The tree shader has no {TreeTrunkAnchor.MapsProperty}.");
            Assert.AreEqual(0f, material.GetFloat(TreeTrunkAnchor.MapsProperty),
                            "The shared material turns the maps path on for every tree, bound or not.");
        }

        [Test]
        public void TheImporter_TreatsTheNewSheetsAsData()
        {
            foreach (var c in TreeKitCatalog.Pass4Channels)
            {
                string path = TreeKitCatalog.SheetPath("RedSpruce", "mature", "summer", c);
                Assert.AreEqual(!TreeKitCatalog.IsColourChannel(c), ArtImportPipeline.IsDataChannel(path), path);
            }
            string winter = TreeKitCatalog.SheetPath("RedSpruce", "mature", "winter", TreeKitCatalog.Channel.Albedo);
            Assert.IsFalse(ArtImportPipeline.IsDataChannel(winter), winter);
            Assert.IsFalse(ArtImportPipeline.IsDataChannel(TreeKitCatalog.PalettePath),
                           "The palette holds colours, so it imports as sRGB.");
        }

        [Test]
        public void MeshTypeFor_IsTightForPass3_AndFullRectForPass4()
        {
            var seasons = new[] { "summer", "winter" };
            var pass3 = new TreeKitCatalog.Entry
            {
                species = "Probe", stage = "mature", seasons = seasons,
                seasonRows = new TreeKitCatalog.SeasonRow[0],
            };
            Assert.AreEqual(SpriteMeshType.Tight, TreeKitCatalog.MeshTypeFor(pass3), "pass 3");

            var pass4 = new TreeKitCatalog.Entry
            {
                species = "Probe", stage = "mature", seasons = seasons,
                wind = new TreeKitCatalog.WindBlock { H = 60f },
                seasonRows = seasons.Select(s => new TreeKitCatalog.SeasonRow { season = s, maps = s, albedo = s }).ToArray(),
            };
            Assert.IsTrue(TreeKitCatalog.HasPass4(pass4));
            Assert.AreEqual(SpriteMeshType.FullRect, TreeKitCatalog.MeshTypeFor(pass4),
                            "A tight mesh would cut off the texels the wind carries a leaf into.");
        }

        [Test]
        public void OnlyTheSnowSheet_IsSingleChannel()
        {
            foreach (TreeKitCatalog.Channel c in Enum.GetValues(typeof(TreeKitCatalog.Channel)))
                Assert.AreEqual(c == TreeKitCatalog.Channel.Snow, TreeKitCatalog.IsSingleChannel(c), c.ToString());
            Assert.AreEqual("Standalone", TreeKitCatalog.SingleChannelPlatform);
        }
    }
}
