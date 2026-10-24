using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// NMC H4: the road floor's relight (Include/RoadFloor4.hlsl) is TerrainLight6's, statement for statement, save
    /// the swaps named here; its format is the one the bake writes and the streamer binds; and the ground draws it
    /// only when it is loaded, between the macro and the wet band.
    ///
    /// <para>Read from the sources, comments stripped and cut into tokens, so a reflowed line changes nothing and a
    /// changed statement on either side fails with the first token that differs. Each swap is a whole-token rewrite of
    /// TerrainLight6's body with its count, so a swap that stops matching fails too. When TerrainLight6's relight
    /// changes, RoadFloor4's must follow it (or this test names the statement that no longer agrees). No shader is
    /// compiled here (TerrainSplatShaderCompileGuardTests does, in the editor).</para>
    /// </summary>
    public class RoadFloor4SourceTests
    {
        const string Tl6Path = "Assets/_Project/Art/Shaders/Include/TerrainLight6.hlsl";
        const string Rf4Path = "Assets/_Project/Art/Shaders/Include/RoadFloor4.hlsl";
        const string SplatPath = "Assets/_Project/Art/Shaders/HiddenHarboursTerrainSplat.shader";
        const string BakePath = "tools/nmc-roads/bake-chunks.cjs";

        static readonly Regex Token = new Regex(
            @"[A-Za-z_][A-Za-z0-9_]*|\d+\.\d*(?:[eE][-+]?\d+)?|\.\d+(?:[eE][-+]?\d+)?|\d+(?:[eE][-+]?\d+)?" +
            @"|==|!=|<=|>=|&&|\|\||\+\+|--|\+=|-=|\*=|/=|>>|<<|\S");

        // ============================ THE RELIGHT, STATEMENT FOR STATEMENT ============================

        [Test]
        public void TheSunMarch_IsTerrainLight6s_SaveItsGridAndItsEarlyOut()
        {
            AssertSameSave("TL6SunVis", "RF4SunVis",
                ("TL6SunVis", "RF4SunVis", 1, "the name"),
                ("T", "G", 4, "a texel is on the region's one grid G, not a tile's T"),
                ("TL6Height ( s , G , prm ) + 0.25 , room = prm . x + prm . y - h0",
                 "RF4HeightIn ( s , G , prm ) + 0.25 , room = prm . w - h0", 1,
                 "the march stops at the chunk's heightMaxNear (the ramp's w), the kit's window hmax"),
                ("TL6Height ( s , q , prm )", "RF4Height ( q )", 1,
                 "a step's height is read in its own chunk's slot, nothing there when the chunk is absent"));
        }

        [Test]
        public void ThePuddle_IsTerrainLight6s_SaveItsGrid()
        {
            AssertSameSave("TL6IsPud", "RF4IsPud",
                ("TL6IsPud ( int s , int2 T , int c , float wetT , float4 prm )", "RF4IsPud ( int2 G , int c , float wetT )", 1,
                 "the signature: a texel of the grid, each pond read in its own chunk"),
                ("T", "G", 5, "the grid"),
                ("TL6Pond ( s , G , prm )", "RF4Pond ( G )", 1, "the texel's own pond"),
                ("TL6Pond ( s , G + int2 ( 1 , 0 ) , prm )", "RF4Pond ( G + int2 ( 1 , 0 ) )", 1, "east"),
                ("TL6Pond ( s , G + int2 ( - 1 , 0 ) , prm )", "RF4Pond ( G + int2 ( - 1 , 0 ) )", 1, "west"),
                ("TL6Pond ( s , G + int2 ( 0 , 1 ) , prm )", "RF4Pond ( G + int2 ( 0 , 1 ) )", 1, "south"),
                ("TL6Pond ( s , G + int2 ( 0 , - 1 ) , prm )", "RF4Pond ( G + int2 ( 0 , - 1 ) )", 1, "north"));
        }

        [Test]
        public void TheRelight_IsTerrainLight6s_SaveItsGridItsRampAndItsFaces()
        {
            AssertSameSave("TL6Relight", "RF4Relight",
                ("TL6Relight ( int s , int2 T , int2 W , TL6Sky k , float occ , float skyv )",
                 "RF4Relight ( int s , int2 G , TL6Sky k , int role )", 1,
                 "the signature: one grid G for the texel and the noise, the role for the canopy's levels"),
                ("T", "G", 10, "the texel on the grid"),
                ("W", "G", 8, "the noise and hashes on the grid, phased once for the region"),
                ("float4 prm = TL6Params ( s ) ;",
                 "float4 prm = RF4Params ( s ) ; float occ = role == RF4_ROLE_AIR ? 1.0 : 0.0 , skyv = role == RF4_ROLE_AIR ? RF4_AIR_SKYV : 1.0 ;", 1,
                 "a face under a deck is the kit's shade level 3 (occ 1, skyv 0.78); every other road texel is open sky"),
                ("int i = G . y * TL6_TILE + G . x ;", "int i = G . y * RF4Width ( ) + G . x ;", 1,
                 "the texel's index runs across the region's grid"),
                ("vis = TL6SunVis ( s , G , k . l , prm ) ;",
                 "{ vis = 1.0 ; [ branch ] if ( role == RF4_ROLE_FLOOR ) vis = RF4SunVis ( s , G , k . l , prm ) ; }", 1,
                 "a face skips the march (the kit's face buffer has hmax 0)"),
                ("TL6IsPud ( s , G , c , wetT , prm )", "RF4IsPud ( G , c , wetT )", 1, "the puddle on the grid"),
                ("float hi = TL6Height ( s , G , prm ) ;", "float hi = RF4HeightIn ( s , G , prm ) ;", 1,
                 "the texel's own height, from its own slot"),
                ("TL6Mark ( s , j ) != mark && hi - TL6Height ( s , j , prm )",
                 "RF4Mark ( j , mark ) != mark && hi - RF4Height ( j )", 2,
                 "a seam's neighbour read in its own chunk; an absent chunk is the same mark (no seam)"),
                ("TL6LoadNormal", "RF4LoadNormal", 1, "the road's arrays"),
                ("TL6LoadLight", "RF4LoadLight", 1, "the road's arrays"),
                ("TL6LoadDetail", "RF4LoadDetail", 1, "the road's arrays"),
                ("TL6Pal", "RF4Pal", 4, "the road ramp's 32 palettes"));
        }

        // ============================ THE FORMAT ============================

        [Test]
        public void TheIncludesFormat_IsTheOneTheBakeWritesAndTheStreamerBinds()
        {
            string rf4 = File.ReadAllText(Rf4Path), bake = File.ReadAllText(BakePath);
            Assert.AreEqual(RoadFloorMaps.ChunkTexels, Define(rf4, "RF4_CHUNK"), "the shader's chunk is not the streamer's slot");
            Assert.AreEqual(RoadFloorMaps.MaxPalettes, Define(rf4, "RF4_PALETTES"), "the shader's palettes are not the ramp's");
            Assert.AreEqual(RoadFloorMaps.RampWidth - 1, Define(rf4, "RF4_RAMP_PARAMS"),
                "the shader reads the chunk's parameters where the streamer does not pack them");
            Assert.AreEqual(RoadFloorMaps.MaxPalettes * RoadFloorMaps.Bands, RoadFloorMaps.RampWidth - 1,
                "the palettes do not end where the parameters begin");
            Match pal = Regex.Match(rf4, @"LOAD_TEXTURE2D\(_RoadRamp, int2\(p \* (\d+) \+ b, max\(s, 0\)\)\)");
            Assert.IsTrue(pal.Success, "RF4Pal no longer reads the ramp at x = p * bands + b");
            Assert.AreEqual(RoadFloorMaps.Bands, int.Parse(pal.Groups[1].Value), "the shader steps palettes by another length");
            Match shift = Regex.Match(rf4, @"int2 c = int2\(G\.x >> (\d+), G\.y >> (\d+)\);");
            Assert.IsTrue(shift.Success, "RF4Slot no longer finds a texel's chunk by a shift");
            Assert.AreEqual(RoadFloorMaps.ChunkTexels, 1 << int.Parse(shift.Groups[1].Value), "RF4Slot's shift is not the chunk");

            Assert.AreEqual(RoadFloorMaps.RoleNone, Define(rf4, "RF4_ROLE_NONE"), "none");
            Assert.AreEqual(RoadFloorMaps.RoleAirFace, Define(rf4, "RF4_ROLE_AIR"), "a face under a deck");
            Assert.AreEqual(RoadFloorMaps.RoleFace, Define(rf4, "RF4_ROLE_FACE"), "a face");
            Assert.AreEqual(RoadFloorMaps.RoleFloor, Define(rf4, "RF4_ROLE_FLOOR"), "a route's floor");

            Match faces = Regex.Match(bake, @"const FACE = (\d+), FACE_AIR = (\d+);");
            Assert.IsTrue(faces.Success, "the bake no longer names its face roles");
            Assert.AreEqual(RoadFloorMaps.RoleFace, int.Parse(faces.Groups[1].Value), "the bake writes another face role");
            Assert.AreEqual(RoadFloorMaps.RoleAirFace, int.Parse(faces.Groups[2].Value), "the bake writes another air-face role");
            Match grid = Regex.Match(bake, @"CHUNK_M = (\d+),[^\n]*\nconst PPM = (\d+), SIZE = CHUNK_M \* PPM");
            Assert.IsTrue(grid.Success, "the bake no longer names its chunk and its pixels per metre");
            Assert.AreEqual(RoadFloorMaps.ChunkMetres, float.Parse(grid.Groups[1].Value), "the bake's chunk is another size");
            Assert.AreEqual(RoadFloorMaps.PixelsPerMetre, int.Parse(grid.Groups[2].Value), "the bake's pixels per metre differ");
            Match max = Regex.Match(bake, @"maxPalettes: (\d+),");
            Assert.IsTrue(max.Success, "the bake no longer caps a chunk's palettes");
            Assert.AreEqual(RoadFloorMaps.MaxPalettes, int.Parse(max.Groups[1].Value), "the bake packs more palettes than the ramp holds");
        }

        // ============================ THE GROUND'S BRANCH ============================

        [Test]
        public void TheGround_DrawsTheRoadFloorOnlyWhenLoaded_AfterTheMacroAndBeforeTheWet()
        {
            string splat = File.ReadAllText(SplatPath);
            string tokens = Squash(splat);
            Assert.AreEqual(1, Count(tokens, "# include \" Assets / _Project / Art / Shaders / Include / RoadFloor4 . hlsl \""),
                "the ground includes the road floor other than once");
            Assert.Less(tokens.IndexOf("TerrainLight6 . hlsl", System.StringComparison.Ordinal),
                        tokens.IndexOf("RoadFloor4 . hlsl", System.StringComparison.Ordinal),
                "the road floor is included before TerrainLight6, whose sky and tables it reads");

            const string branch = "col = TSA_Macro ( col , wp ) ; [ branch ] if ( _RoadLoaded > 0.5 ) col = RF4Over ( col , wp , TerrainSky ( ) ) ;";
            Assert.AreEqual(1, Count(tokens, branch),
                "the frag does not draw the road floor, behind its _RoadLoaded branch, right after the macro");
            int at = tokens.IndexOf(branch, System.StringComparison.Ordinal);
            Assert.AreEqual(1, Count(tokens, "RF4Over ("), "the road floor is drawn in more than one place");
            StringAssert.Contains("TSA_Wet", tokens.Substring(at), "the wet band no longer comes after the road floor");

            foreach (string p in new[] { "_RoadNormal", "_RoadLight", "_RoadDetail", "_RoadRamp", "_RoadIndex",
                                         "_RoadLoaded", "_RoadOrigin", "_RoadGrid" })
                Assert.IsTrue(Regex.IsMatch(splat, @"^\s*(\[[^\]]*\]\s*)*" + p + @"\s*\(", RegexOptions.Multiline),
                    $"the ground's Properties do not declare {p}, which the surface binds");
            Assert.AreEqual(1, Count(tokens, "float _RoadLoaded ; float4 _RoadOrigin , _RoadGrid ;"),
                "the ground's constant buffer does not hold the road floor's scalars once (the SRP batcher needs them there)");
        }

        // ============================ helpers ============================

        static void AssertSameSave(string tl6Name, string rf4Name, params (string from, string to, int count, string why)[] swaps)
        {
            string tl6 = Body(File.ReadAllText(Tl6Path), tl6Name), rf4 = Body(File.ReadAllText(Rf4Path), rf4Name);
            foreach (var (from, to, count, why) in swaps)
            {
                var re = new Regex("(?<=^| )" + Regex.Escape(from) + "(?= |$)");
                Assert.AreEqual(count, re.Matches(tl6).Count,
                    $"{tl6Name}: the swap '{from}' ({why}) no longer matches {count} times: TerrainLight6 changed under it");
                tl6 = re.Replace(tl6, to.Replace("$", "$$"));
            }
            if (tl6 == rf4) return;
            string[] a = tl6.Split(' '), b = rf4.Split(' ');
            int i = 0;
            while (i < a.Length && i < b.Length && a[i] == b[i]) i++;
            Assert.Fail($"{rf4Name} is not {tl6Name} save its named swaps; they part at token {i}:\n" +
                        $"  TL6 (swapped): {string.Join(" ", a.Skip(System.Math.Max(0, i - 10)).Take(24))}\n" +
                        $"  RF4:           {string.Join(" ", b.Skip(System.Math.Max(0, i - 10)).Take(24))}");
        }

        /// <summary>The function from its name to its closing brace, comments stripped, as tokens joined by spaces.</summary>
        static string Body(string src, string name)
        {
            string s = StripComments(src);
            Match m = Regex.Match(s, @"\b" + name + @"\s*\(");
            Assert.IsTrue(m.Success, $"no {name} in the source");
            int k = s.IndexOf('{', m.Index), depth = 0;
            Assert.GreaterOrEqual(k, 0, $"{name} has no body");
            for (; k < s.Length; k++)
            {
                if (s[k] == '{') depth++;
                else if (s[k] == '}' && --depth == 0) break;
            }
            return Join(s.Substring(m.Index, k + 1 - m.Index));
        }

        static string Squash(string src) => Join(StripComments(src));

        static string StripComments(string src) =>
            Regex.Replace(Regex.Replace(src, @"/\*[\s\S]*?\*/", " "), @"//[^\n]*", " ");

        static string Join(string s) => string.Join(" ", Token.Matches(s).Cast<Match>().Select(m => m.Value));

        static int Count(string tokens, string what)
        {
            int n = 0;
            for (int at = tokens.IndexOf(what, System.StringComparison.Ordinal); at >= 0;
                 at = tokens.IndexOf(what, at + what.Length, System.StringComparison.Ordinal))
                n++;
            return n;
        }

        static int Define(string src, string name)
        {
            Match m = Regex.Match(src, @"#define\s+" + name + @"\s+(\d+)");
            Assert.IsTrue(m.Success, $"the include no longer defines {name}");
            return int.Parse(m.Groups[1].Value);
        }
    }
}
