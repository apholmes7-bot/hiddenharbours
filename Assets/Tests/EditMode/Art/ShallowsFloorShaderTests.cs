using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The floor in the shallows (W4-4), read from the shader sources: CI has no GPU, so the source is where
    /// the render's arithmetic can be pinned. Ruling 7 of 2026-10-10 ("Shallows: 2B") has the sea draw the
    /// ground's OWN textures as the floor of clear shallow water, so the ground's albedo lives in one include
    /// both shaders read, and the ground skips its shading wherever the sea covers it opaquely.
    ///
    /// <para>Whether the shaders COMPILE is <c>TerrainSplatShaderCompileGuardTests</c>' and
    /// <c>WaterShaderCompileGuardTests</c>' job; they force-compile both, include and all. The pixels are the
    /// granted slot's (W4-4 Phase B): a text guard holds the arithmetic, never how it looks.</para>
    ///
    /// <para><b>On the base</b> (b8710c59) the include does not exist and the terrain frag starts at its
    /// height read: every test here fails at its first assert.</para>
    /// </summary>
    public sealed class ShallowsFloorShaderTests
    {
        private const string ShaderDir = "Assets/_Project/Art/Shaders";
        private const string IncludePath = ShaderDir + "/Include/TerrainSplatAlbedo.hlsl";
        private const string TerrainPath = ShaderDir + "/HiddenHarboursTerrainSplat.shader";
        private const string OverlayPath = ShaderDir + "/HiddenHarboursWaterOverlay.shader";
        private const string FeaturePath = "Assets/_Project/Code/Art/IsoFacetHullFeature.cs";
        private const string DisplacedPath = "Assets/_Project/Code/Art/DisplacedWaterSurface.cs";
        private const string IncludeLine = "#include \"Assets/_Project/Art/Shaders/Include/TerrainSplatAlbedo.hlsl\"";
        private const string TerrainFrag = "half4 frag (Varyings i) : SV_Target";

        /// <summary>The include's functions: the base terrain's seven helpers under the TSA_ prefix, then the
        /// five the frag's inline albedo became (the hem, the weights, the kit sum, the macro drift, the wet
        /// band). Source: this PR's include, against b8710c59's terrain frag (:441-:695).</summary>
        private static readonly string[] SharedFunctions =
        {
            "TSA_Hash21", "TSA_VNoise", "TSA_Fbm3", "TSA_DistToSegment", "TSA_Band", "TSA_MatUV", "TSA_SampleMat",
            "TSA_Hem", "TSA_GroundWeights", "TSA_KitAlbedo", "TSA_Macro", "TSA_Wet",
        };

        /// <summary>The base terrain's helpers that moved into the include. Source: b8710c59's
        /// HiddenHarboursTerrainSplat.shader, :305 Hash21, :315 VNoise, :329 Fbm3, :337 DistToSegment,
        /// :345 Band, :354 MatUV, :371 SampleMat. RelightMat (:395) stays the terrain's own.</summary>
        private static readonly string[] RetiredHelpers =
        {
            "Hash21", "VNoise", "Fbm3", "DistToSegment", "Band", "MatUV", "SampleMat",
        };

        [Test]
        public void TheTerrain_DrawsTheSharedAlbedo_AndKeepsNoCopyOfItsOwn()
        {
            Assert.IsTrue(File.Exists(IncludePath), "the ground's albedo is shared through " + IncludePath);
            string terrain = Code(TerrainPath);
            string include = Code(IncludePath);

            Assert.AreEqual(1, Count(terrain, Regex.Escape(IncludeLine)), "the terrain includes the albedo once");
            int at = terrain.IndexOf(IncludeLine, System.StringComparison.Ordinal);
            foreach (string before in new[]
                     {
                         "#define TSA_SAMPLE_SPLAT(", "#define TSA_SAMPLE_KIT(", "#define TSA_TABLES_DEFINED",
                         "#define HH_MAT_COUNT 21", "static const float MAT_SLICE[21]",
                     })
            {
                int i = terrain.IndexOf(before, System.StringComparison.Ordinal);
                Assert.IsTrue(i >= 0 && i < at, "'" + before + "' is defined before the include reads it");
            }
            // The terrain reads each texture through its own sampler (its import state), as it always has.
            StringAssert.Contains(
                "#defineTSA_SAMPLE_SPLAT(tex,smp,uv)SAMPLE_TEXTURE2D(tex,smp,uv)", Squash(terrain));
            StringAssert.Contains(
                "#defineTSA_SAMPLE_KIT(uv,slice,duvx,duvy)SAMPLE_TEXTURE2D_ARRAY_GRAD(_DetailArr256," +
                "sampler_DetailArr256,uv,slice,duvx,duvy)", Squash(terrain));

            foreach (string name in SharedFunctions)
            {
                Assert.AreEqual(1, Definitions(include, name), name + " is defined once, in the include");
                Assert.AreEqual(0, Definitions(terrain, name), name + " has no second body in the terrain");
            }
            foreach (string name in RetiredHelpers)
                Assert.AreEqual(0, Definitions(terrain, name) + Definitions(include, name),
                    name + " moved into the include as TSA_" + name + "; an un-prefixed copy is a second ground");

            // The frag draws the ground through the shared functions: its weights, its albedo (the relight
            // path keeps its own sum of RelightMat), its macro drift and its wet band at the sea's level.
            string frag = Squash(Body(terrain, TerrainFrag));
            foreach (string call in new[]
                     {
                         "floatalpha=TSA_Hem(e);", "TSA_GroundWeights(wp,wpx,uv,e,w,intensity);",
                         "col=TSA_KitAlbedo(wp,w,intensity,dwx,dwy);", "col=TSA_Macro(col,wp);",
                         "col=TSA_Wet(col,e,_WaterLevel);",
                     })
                StringAssert.Contains(call, frag);
        }

        [Test]
        public void TheIncludesTables_AreTheTerrainsTables_EntryForEntry()
        {
            string terrain = Code(TerrainPath);
            string include = Code(IncludePath);

            // The include's copy stands down for the terrain and serves every other includer.
            Match guarded = Regex.Match(include,
                @"#ifndef\s+TSA_TABLES_DEFINED\s+#define\s+TSA_TABLES_DEFINED\s+(?<tables>.*?)#endif",
                RegexOptions.Singleline);
            Assert.IsTrue(guarded.Success, "the include's tables sit under #ifndef TSA_TABLES_DEFINED");
            string copy = guarded.Groups["tables"].Value;

            foreach (string table in new[] { "MAT_SLICE", "MAT_METRES", "MAT_OFFSET" })
                CollectionAssert.AreEqual(Table(terrain, table), Table(copy, table),
                    table + ": the include's copy is the terrain's, entry for entry");
            // Twenty-one materials, 0 grass .. 20 path (the terrain's material table comment).
            Assert.AreEqual(21, Table(copy, "MAT_SLICE").Length);
            StringAssert.Contains("#define HH_MAT_COUNT 21", copy);
        }

        /// <summary>
        /// W4-4a, the ground's underwater clip. The bar is the sea's OWN coverage this camera, not a level:
        /// the overlay composes <c>_HHWaterScreenTex</c> over the ground with <c>Blend SrcAlpha
        /// OneMinusSrcAlpha</c>, so where that alpha is 1 the ground's weight is 1 − 1 = 0 and its pixel is
        /// never seen. Still water, the swash, the shore fringe, the lift and the hull guard are all in that
        /// coverage by construction, because they are what the sea drew. The margin is the overlay's blend
        /// weight: zero metres, and the clip stops wherever the sea is translucent (alpha below 1) or absent.
        /// </summary>
        [Test]
        public void TheTerrainClips_OnlyUnderTheSeasOwnWaterLevel()
        {
            string terrain = Code(TerrainPath);
            string frag = Body(terrain, TerrainFrag);

            // The clip is the frag's FIRST statement, gated by the camera's flag, reading the coverage at the
            // pixel exactly as the overlay does. Bar source: HiddenHarboursWaterOverlay.shader :63 (the read)
            // and :26 (the blend that makes alpha 1 a full replacement).
            const string clip =
                "[branch]if(_HHTerrainUnderwaterClip>0.5)" +
                "{if(_HHWaterScreenTex.Load(int3(int2(i.positionCS.xy),0)).a>=1.0)discard;}";
            Assert.IsTrue(Squash(frag).StartsWith(clip, System.StringComparison.Ordinal),
                "the terrain frag opens with the gated coverage clip, before its first texture read");

            // No level of its own: nothing before the frag's first line of ground reads a height, a tide or
            // the still water, so the sea's drawn coverage is the only bar.
            int ground = frag.IndexOf("float2 wp = i.worldXY;", System.StringComparison.Ordinal);
            Assert.Greater(ground, 0, "the ground's own work starts at its world position");
            string head = frag.Substring(0, ground);
            foreach (string level in new[] { "_WaterLevel", "_HeightTex", "_HHStillTex", "StillLevelAt", "_HeightMin" })
                StringAssert.DoesNotContain(level, head, "the clip reads no level: " + level);
            Assert.AreEqual(1, Count(head, @"\bdiscard\b"), "one discard, the clip's");

            // The terrain declares the coverage and the flag as plain globals (no Properties entry, so no
            // material can carry a stale flag), and reads each in the clip alone.
            StringAssert.Contains("Texture2D<float4>_HHWaterScreenTex;", Squash(terrain));
            StringAssert.Contains("float_HHTerrainUnderwaterClip;", Squash(terrain));
            Assert.AreEqual(2, Count(terrain, @"\b_HHTerrainUnderwaterClip\b"), "declared once, read once");
            Assert.AreEqual(2, Count(terrain, @"\b_HHWaterScreenTex\b"), "declared once, read once");

            // The overlay that composes the coverage: the same Load, alpha-blended (a = 1 replaces the ground).
            string overlay = Squash(Code(OverlayPath));
            StringAssert.Contains("BlendSrcAlphaOneMinusSrcAlpha", overlay);
            StringAssert.Contains("return_HHWaterScreenTex.Load(int3(int2(i.positionCS.xy),0));", overlay);

            // A pixel the sea did not draw this camera reads alpha 0: the target clears to clear on first use.
            string feature = Code(FeaturePath);
            int pass = feature.IndexOf("\"HH Displaced Water\"", System.StringComparison.Ordinal);
            Assert.Greater(pass, 0, "the displaced water pass");
            int water = feature.LastIndexOf("if (DrawWater)", pass, System.StringComparison.Ordinal);
            Assert.Greater(water, 0, "the displaced water block");
            string import = Squash(feature.Substring(water, pass - water));
            StringAssert.Contains("clearOnFirstUse=true,", import);
            StringAssert.Contains("clearColor=Color.clear,", import);

            // Every pixel the chunks draw lies under the overlay quad: both are padded by the same
            // _overlayPadMeters past the water rect (the chunk bounds by Expand's 2x, half per side), so the
            // clip can never open a hole the overlay does not cover.
            string displaced = Squash(Code(DisplacedPath));
            StringAssert.Contains("b.Expand(2f*_overlayPadMeters);", displaced);
            StringAssert.Contains("Vector2min=_meshWorldCenter-_meshWorldSize*0.5f-Vector2.one*_overlayPadMeters;", displaced);
            StringAssert.Contains("Vector2max=_meshWorldCenter+_meshWorldSize*0.5f+Vector2.one*_overlayPadMeters;", displaced);
        }

        // ---- source helpers ------------------------------------------------------------------------------

        /// <summary>A source file with its block and line comments stripped and CRLF folded, so prose can
        /// neither satisfy nor break a guard.</summary>
        internal static string Code(string path)
        {
            string s = File.ReadAllText(path, Encoding.UTF8).Replace("\r", "");
            s = Regex.Replace(s, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(s, @"//[^\n]*", "");
        }

        /// <summary>Whitespace removed, so a guard reads the statement and not its layout.</summary>
        internal static string Squash(string s) => Regex.Replace(s, @"\s+", "");

        internal static int Count(string s, string pattern) => Regex.Matches(s, pattern).Count;

        /// <summary>How many function bodies named exactly <paramref name="name"/> the source defines.</summary>
        private static int Definitions(string source, string name) =>
            Count(source, @"(?<![A-Za-z0-9_])(?:void|float|float2|float3|float4|half|half3|half4|int)\s+" +
                          Regex.Escape(name) + @"\s*\([^;{]*\)\s*\{");

        /// <summary>One function's body (between its first { and its matching }).</summary>
        internal static string Body(string source, string signature)
        {
            int at = source.IndexOf(signature, System.StringComparison.Ordinal);
            Assert.Greater(at, -1, "'" + signature + "' is gone");
            int open = source.IndexOf('{', at);
            Assert.Greater(open, -1);
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0) return source.Substring(open + 1, i - open - 1);
            }
            Assert.Fail("Unbalanced braces after '" + signature + "'.");
            return null;
        }

        /// <summary>A literal-sized static table's entries, in order.</summary>
        private static float[] Table(string source, string name)
        {
            Match m = Regex.Match(source, @"static\s+const\s+float\s+" + name + @"\[\d+\]\s*=\s*\{(?<v>[^}]*)\}");
            Assert.IsTrue(m.Success, name + " is declared");
            return m.Groups["v"].Value.Split(',')
                .Select(v => float.Parse(v.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        }
    }
}
