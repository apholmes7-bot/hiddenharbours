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
    /// both shaders read, the ground skips its shading wherever the sea covers it opaquely, and the sea lays
    /// that albedo under its column as the bed: absorbed, lit by the caustics and graded as the painted bed.
    ///
    /// <para>Whether the shaders COMPILE is <c>TerrainSplatShaderCompileGuardTests</c>' and
    /// <c>WaterShaderCompileGuardTests</c>' job; they force-compile both, include and all. The pixels are the
    /// granted slot's (W4-4 Phase B): a text guard holds the arithmetic, never how it looks.</para>
    ///
    /// <para><b>On the base</b> (b8710c59) the include does not exist, the terrain frag starts at its height
    /// read and the sea's bed is the bake alone: every test here fails at its first assert.</para>
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
        internal const string WaterPath = ShaderDir + "/HiddenHarboursWater.shader";
        internal const string WaterFrag = "half4 frag(Varyings IN) : SV_Target";
        private const string FloorHelper = "half4 ShallowsFloorBed(";

        // The sea's seabed column, squashed, as b8710c59's water frag holds it: the bake's sample :5044, the
        // per-channel transmission at the fringe depth :5046 and its bands :5047, the composite :5048. The
        // floor's gate and call (this PR) sit between the bands and the composite.
        private const string BedSample = "half4bed=SAMPLE_TEXTURE2D(_SeabedTex,sampler_SeabedTex,bedUV);";
        private const string BedColumn = "float3bedT=AbsorptionTransmission(absSigma,max(depthC,0.0));";
        internal const string BedBand = "bedT=AbsorptionBand(bedT,_AbsorptionBands);";
        internal const string BedLerp = "col.rgb=lerp(col.rgb,bed.rgb,saturate(bedT)*saturate(bed.a)*bedIn);";
        internal const string FloorGate =
            "[branch]if(_ShallowsFloor>0.5&&_DetailLoaded>0.5&&bedIn>0.0&&max(bedT.r,max(bedT.g,bedT.b))>0.0)";
        internal const string FloorCall =
            "bed=ShallowsFloorBed(bed,worldXY,waveSlope,max(depthC,0.0),elevation+depth,floorDx,floorDy);";
        // b8710c59's water frag :5469 (the caustics' add) and :6025 (the palette grade).
        private const string CausticAdd =
            "col.rgb+=_CausticColor.rgb*caustic*_CausticAmount*causticGate*causticDay*causticTransmission;";
        private const string Grade = "col.rgb=PaletteGrade(col.rgb,dayNightLuma);";

        /// <summary>The values the include reads, by the type the terrain gives each: b8710c59's
        /// HiddenHarboursTerrainSplat.shader, UnityPerMaterial :208-:230 (the include's header lists the same
        /// thirty), and the six splats it declares at :194-:199.</summary>
        private static readonly string[] IncludeFloats =
        {
            "_DetailOffsetCellMetres",
            "_FloorPaint", "_FloorRipple", "_FloorSand", "_FloorMarram", "_FloorGrass", "_FloorShingle",
            "_BandBlendMetres", "_EdgeFadeMetres",
            "_BandWiggleMetres", "_BandWiggleScale", "_BandDetailMetres", "_BandDetailScale",
            "_IslandAspect", "_SectorFeather", "_SectorBlend",
            "_BarHalfWidth", "_BarSpineHalfWidth", "_BarSpineFloor", "_BarEdgeBlend",
            "_MacroScale", "_MacroStrength", "_WetBandMetres", "_WetStrength",
        };
        private static readonly string[] IncludeFloat4s =
            { "_IslandCenter", "_WeatherFacing", "_BarFrom", "_BarTo", "_MacroTint", "_WetTint" };
        private static readonly string[] IncludeSplats =
            { "_SplatA", "_SplatB", "_SplatC", "_SplatD", "_SplatE", "_SplatF" };

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

        /// <summary>
        /// W4-4b: the sea draws its floor with the ground's own albedo from the ONE include, never a copy.
        /// Desktop only: a mobile build never declares the floor and keeps the painted bed (2A stays unbuilt as
        /// the mobile fallback, ruling 7). The sea declares each value the include reads by the terrain's own
        /// name and type, outside its Properties, so the region's ground writes one block both read and no
        /// preset can carry a value of it; and the floor reads the ground at the terrain's own coordinates,
        /// statement for statement, from the same height read.
        /// </summary>
        [Test]
        public void WaterAndTerrain_ShareOneSplatInclude()
        {
            string water = Code(WaterPath);
            string terrain = Code(TerrainPath);
            Assert.AreEqual(1, Count(water, Regex.Escape(IncludeLine)), "the sea includes the ground's albedo once");
            CollectionAssert.AreEquivalent(IncludeFloats.Concat(IncludeFloat4s).Concat(IncludeSplats).ToArray(),
                Regex.Matches(Code(IncludePath), @"(?<![A-Za-z0-9_])_[A-Z][A-Za-z0-9_]*").Cast<Match>()
                    .Select(m => m.Value).Distinct().ToArray(),
                "the include reads the thirty values and the six splats named here, and nothing else");

            // The floor's block: desktop only, its declarations, its two sampling macros, then the include.
            StringAssert.Contains("#if!defined(SHADER_API_MOBILE)#defineHH_SHALLOWS_FLOOR1#endif", Squash(water),
                "a mobile build never defines the floor");
            int open = water.IndexOf("#if defined(HH_SHALLOWS_FLOOR)", System.StringComparison.Ordinal);
            Assert.Greater(open, -1, "the floor's block");
            string block = water.Substring(open, water.IndexOf("#endif", open, System.StringComparison.Ordinal) - open);
            string squashed = Squash(block);
            int at = squashed.IndexOf(Squash(IncludeLine), System.StringComparison.Ordinal);
            Assert.Greater(at, -1, "the include is inside the floor's block");
            // The splats through URP's linear clamp at level 0 (they import unmipped, TerrainSplatAssets.cs
            // :203, so level 0 is the texture); the kit through URP's point repeat with explicit gradients. Those
            // are the ground's own sampler states (the include's header), in two slots where the textures' own
            // seven would pass the sixteen a D3D11 stage allows.
            foreach (string macro in new[]
                     {
                         "#defineTSA_SAMPLE_SPLAT(tex,smp,uv)SAMPLE_TEXTURE2D_LOD(tex,sampler_LinearClamp,uv,0)",
                         "#defineTSA_SAMPLE_KIT(uv,slice,duvx,duvy)SAMPLE_TEXTURE2D_ARRAY_GRAD(_DetailArr256," +
                         "sampler_PointRepeat,uv,slice,duvx,duvy)",
                     })
            {
                int i = squashed.IndexOf(macro, System.StringComparison.Ordinal);
                Assert.IsTrue(i >= 0 && i < at, "'" + macro + "' is defined before the include reads it");
            }
            foreach (string name in SharedFunctions)
                Assert.AreEqual(0, Definitions(water, name), name + " has no second body in the sea");

            string properties = Body(water, "Properties");
            StringAssert.Contains("_SeabedTex", properties, "the sea's Properties block");
            foreach (string name in IncludeFloats.Concat(new[] { "_DetailLoaded", "_PixelsPerMetre" }))
                AssertDeclaredAsTheTerrainDoes(name, "float", terrain, water, block, properties);
            foreach (string name in IncludeFloat4s)
                AssertDeclaredAsTheTerrainDoes(name, "float4", terrain, water, block, properties);
            foreach (string name in IncludeSplats)
                AssertDeclaredAsTheTerrainDoes(name, "TEXTURE2D", terrain, water, block, properties);
            AssertDeclaredAsTheTerrainDoes("_DetailArr256", "TEXTURE2D_ARRAY", terrain, water, block, properties);

            // The floor reads the ground where the terrain does: the terrain's own statements at the floor's
            // point, then the shared functions in the terrain's order.
            string helper = Squash(Body(water, FloorHelper));
            string ground = Squash(Body(terrain, TerrainFrag));
            foreach (string statement in new[]
                     {
                         "floatppm=max(_PixelsPerMetre,1.0);",
                         "float2wpx=floor(wp*ppm)/ppm;",
                         "float2uv=(wp-_HeightWorldMin.xy)/max(_HeightWorldSize.xy,float2(1e-3,1e-3));",
                         "floatw[HH_MAT_COUNT],intensity[HH_MAT_COUNT];",
                         "TSA_GroundWeights(wp,wpx,uv,e,w,intensity);",
                     })
            {
                StringAssert.Contains(statement, helper, "the floor: " + statement);
                StringAssert.Contains(statement, ground, "the ground: " + statement);
            }
            int last = -1;
            foreach (string call in new[]
                     {
                         "TSA_Hem(e)", "TSA_GroundWeights(", "TSA_KitAlbedo(wp,w,intensity,dwx,dwy)",
                         "TSA_Macro(ground,wp)", "TSA_Wet(ground,e,level)",
                     })
            {
                int i = helper.IndexOf(call, System.StringComparison.Ordinal);
                Assert.Greater(i, last, call + ", in the terrain's order");
                last = i;
            }

            // One height read, the terrain's: the same map at the same uv. The committed height maps import
            // unmipped (PaintedHeightPng.cs :102), so the floor's explicit level 0 is the terrain's texel.
            StringAssert.Contains("floate=SeabedElevationLod(wp);", helper);
            StringAssert.Contains(
                "float2uv=(worldXY-_HeightWorldMin.xy)/max(_HeightWorldSize.xy,float2(1e-3,1e-3));" +
                "floatr=SAMPLE_TEXTURE2D_LOD(_HeightTex,sampler_HeightTex,uv,0).r;" +
                "returnlerp(_HeightMin,_HeightMax,r);",
                Squash(Body(water, "float SeabedElevationLod(float2 worldXY)")));
            StringAssert.Contains(
                "floatr=SAMPLE_TEXTURE2D(_HeightTex,sampler_HeightTex,uv).r;floate=lerp(_HeightMin,_HeightMax,r);",
                ground);
        }

        /// <summary>
        /// W4-4b, the grade's fault guarded: the floor changes the BED and nothing else of the pixel, before the
        /// composite, so it is in the colour the palette grade takes and is graded with the water as the painted
        /// bed is. Nothing between the composite and the grade returns or discards; the frag's one return
        /// follows the grade. Both passes draw it: the displaced pass's fragment is this frag behind its hull
        /// guard. Bar sources: b8710c59's water frag :5048 (the composite) and :6025 (the grade).
        /// </summary>
        [Test]
        public void TheFloor_GoesThroughThePaletteGrade()
        {
            string water = Code(WaterPath);
            string frag = Body(water, WaterFrag);
            string squashed = Squash(frag);
            Assert.AreEqual(1, Count(squashed, Regex.Escape(FloorCall)), "the floor is drawn once, into the bed");
            Assert.AreEqual(1, Count(squashed, Regex.Escape(BedLerp)), "one composite");
            Assert.AreEqual(1, Count(squashed, Regex.Escape(Grade)), "one grade");
            int call = squashed.IndexOf(FloorCall, System.StringComparison.Ordinal);
            int lerp = squashed.IndexOf(BedLerp, System.StringComparison.Ordinal);
            Assert.AreEqual(call + FloorCall.Length + "#endif".Length, lerp,
                "the bed the floor drew goes straight into the composite");
            Assert.Less(lerp, squashed.IndexOf(Grade, System.StringComparison.Ordinal), "the composite, then the grade");

            int from = frag.IndexOf("col.rgb = lerp(col.rgb, bed.rgb,", System.StringComparison.Ordinal);
            int to = frag.IndexOf("col.rgb = PaletteGrade(col.rgb, dayNightLuma);", System.StringComparison.Ordinal);
            Assert.IsTrue(from > 0 && to > from, "the composite and the grade, as written");
            string between = frag.Substring(from, to - from);
            Assert.AreEqual(0, Count(between, @"\breturn\b") + Count(between, @"\bdiscard\b"),
                "every floor pixel reaches the grade");
            Assert.AreEqual(1, Count(frag, @"\breturn\b"), "the frag returns once");
            Assert.Greater(Regex.Match(frag, @"\breturn\b").Index, to, "after the grade");

            // The helper hands back a bed: the bake's untouched, or the ground laid over it.
            string helper = Body(water, FloorHelper);
            Assert.AreEqual(0, Count(helper, @"\bcol\b"), "the floor never touches the pixel's colour");
            Assert.AreEqual(2, Count(helper, @"\breturn\b"));
            StringAssert.Contains("if(hem<=0.0)returnbed;", Squash(helper));
            StringAssert.EndsWith(
                "floata=lerp(bed.a,1.0,hem);returnhalf4(lerp(bed.rgb,ground,saturate(hem/max(a,1e-4))),a);",
                Squash(helper), "the ground over the bake, the over operator in straight alpha");

            StringAssert.EndsWith("returnfrag(IN);", Squash(Body(water, "half4 fragDisplaced (Varyings IN) : SV_Target")),
                "the displaced pass draws this frag");
        }

        /// <summary>
        /// W4-4b, the absorption's fault guarded: the floor is absorbed per channel by the mood's own depths.
        /// It sits INSIDE the painted bed's column, which is unchanged: the bake's sample, the transmission at
        /// the fringe depth (one exponential per channel, so red goes first and the floor darkens and blues with
        /// depth), its bands, the floor, then the composite weighing each channel by its own transmission. A
        /// murky or stormy mood hides the floor where it hid the painted bed, and the gate skips the floor
        /// exactly where the composite weighs the bed 0 in every channel. Bar sources: b8710c59's water frag
        /// :5044-:5048 and AbsorptionTransmission :1761-:1764.
        /// </summary>
        [Test]
        public void TheFloor_IsAbsorbedPerChannel()
        {
            string water = Code(WaterPath);
            string frag = Squash(Body(water, WaterFrag));
            StringAssert.Contains(
                BedSample + BedColumn + BedBand + "#ifdefined(HH_SHALLOWS_FLOOR)" + FloorGate + FloorCall + "#endif" +
                BedLerp, frag, "the floor inside the painted bed's column, the column unchanged");
            StringAssert.Contains("float3absSigma=AbsorptionSigma();", frag);
            StringAssert.Contains(
                "float3AbsorptionTransmission(float3sigma,floatdepth){returnexp(-sigma*(ABSORPTION_PATH*max(depth,0.0)));}",
                Squash(water), "one transmission per channel");
        }

        /// <summary>
        /// W4-4b, the caustics' fault guarded: the caustics light the floor. They are ADDED to the pixel after
        /// the composite, at the pixel's own depth and through the column's own banded transmission, so a floor
        /// pixel takes the light the painted bed took; and between the composite and the add nothing reads the
        /// floor or the bed, so the floor changes nothing of the caustics (C2a's dials stay). Bar sources:
        /// b8710c59's water frag :5048 (the composite), :5388 (the gate's depth), :5458-:5466 (the
        /// transmission), :5469 (the add) and :6025 (the grade).
        /// </summary>
        [Test]
        public void TheCaustics_LightTheSplatFloor()
        {
            string frag = Body(Code(WaterPath), WaterFrag);
            string squashed = Squash(frag);
            int call = squashed.IndexOf(FloorCall, System.StringComparison.Ordinal);
            int lerp = squashed.IndexOf(BedLerp, System.StringComparison.Ordinal);
            Assert.IsTrue(call > 0 && call < lerp, "the floor is in the composite the caustics are added onto");
            Assert.AreEqual(1, Count(squashed, Regex.Escape(CausticAdd)), "the caustics are added once");
            int add = squashed.IndexOf(CausticAdd, System.StringComparison.Ordinal);
            Assert.IsTrue(lerp < add && add < squashed.IndexOf(Grade, System.StringComparison.Ordinal),
                "added onto the composited floor, before the grade");

            string light = squashed.Substring(lerp + BedLerp.Length, add - lerp - BedLerp.Length);
            foreach (string statement in new[]
                     {
                         "floatcausticDepth=depth-_CausticShallowBias;",
                         "float3causticTransmission=float3(1.0,1.0,1.0);",
                         "float3causticT=AbsorptionTransmission(causticSigma,depth);",
                         "causticT=AbsorptionBand(causticT,_AbsorptionBands);",
                         "causticTransmission=lerp(float3(1.0,1.0,1.0),causticT,saturate(_CausticClarity));",
                     })
                StringAssert.Contains(statement, light, "the caustics at the pixel's own depth: " + statement);
            foreach (string floor in new[] { "ShallowsFloor", "_Splat", "TSA_", "_DetailArr256", "_SeabedTex", "floorDx" })
                StringAssert.DoesNotContain(floor, light, "the caustics read nothing of the floor's: " + floor);
            int from = frag.IndexOf("col.rgb = lerp(col.rgb, bed.rgb,", System.StringComparison.Ordinal);
            int to = frag.IndexOf("col.rgb += _CausticColor.rgb", System.StringComparison.Ordinal);
            Assert.AreEqual(2, Count(frag.Substring(from, to - from), @"\bbed\b"),
                "the composite's own two reads of the bed, and none after it");
        }

        /// <summary>
        /// W4-4b: the floor's wobble lands on the ground's own pixel grid. The view bends by the shared wave
        /// field's slope (the one sample per pixel the swell draws and the hulls ride, now taken ahead of the
        /// composite) times the column's depth and the bend (GameConfig.ShallowsFloorRefraction), and the offset
        /// is rounded to whole ground pixels, so the floor moves by whole texels of its own grid; the kit reads
        /// at point, so a whole-texel step never resamples between texels. Bar sources: this PR's helper, and
        /// b8710c59's water frag :5099 (the sample, then below the composite; its call unchanged).
        /// </summary>
        [Test]
        public void TheFloor_BendsByWholeGroundPixels()
        {
            string water = Code(WaterPath);
            StringAssert.StartsWith(
                "floatppm=max(_PixelsPerMetre,1.0);" +
                "float2wp=worldXY+round(slope*depth*_ShallowsFloorRefraction*ppm)/ppm;" +
                "floate=SeabedElevationLod(wp);",
                Squash(Body(water, FloorHelper)), "the bend in whole ground pixels, before the floor's first read");
            string frag = Squash(Body(water, WaterFrag));
            const string sample = "WaveFieldSample(Pixelize(worldXY),waveFreqScale,waveFetchFactors," +
                                  "waveHeight,waveSlope,waveCrest,wavePrimCos);";
            Assert.AreEqual(1, Count(frag, Regex.Escape(sample)), "the field's one sample per pixel");
            Assert.Less(frag.IndexOf(sample, System.StringComparison.Ordinal),
                frag.IndexOf(FloorCall, System.StringComparison.Ordinal), "taken before the floor bends by its slope");
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

        /// <summary>The sea declares <paramref name="name"/> once, inside its floor's block, by the type the
        /// terrain declares it with, and its Properties block never names it.</summary>
        private static void AssertDeclaredAsTheTerrainDoes(string name, string type, string terrain, string water,
                                                           string block, string properties)
        {
            CollectionAssert.AreEqual(new[] { type }, Declarations(terrain, name), name + ": the terrain's " + type);
            CollectionAssert.AreEqual(new[] { type }, Declarations(water, name), name + ": declared once in the sea");
            CollectionAssert.AreEqual(new[] { type }, Declarations(block, name), name + ": in the floor's block");
            Assert.AreEqual(0, Count(properties, @"(?<![A-Za-z0-9_])" + Regex.Escape(name) + @"(?![A-Za-z0-9_])"),
                name + " is no property of the sea's, so no preset carries it");
        }

        /// <summary>The type of each declaration of <paramref name="name"/>: a scalar or vector statement (a
        /// comma list declares each of its names), or a texture macro.</summary>
        private static string[] Declarations(string source, string name)
        {
            var types = new List<string>();
            foreach (Match m in Regex.Matches(source,
                         @"(?<![A-Za-z0-9_])(float4|float3|float2|float|half4|half3|half)\s+(_[A-Za-z0-9_]+(?:\s*,\s*_[A-Za-z0-9_]+)*)\s*;"))
                foreach (string declared in m.Groups[2].Value.Split(','))
                    if (declared.Trim() == name)
                        types.Add(m.Groups[1].Value);
            foreach (Match m in Regex.Matches(source,
                         @"(?<![A-Za-z0-9_])(TEXTURE2D_ARRAY|TEXTURE2D)\(\s*" + Regex.Escape(name) + @"\s*\)"))
                types.Add(m.Groups[1].Value);
            return types.ToArray();
        }

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
