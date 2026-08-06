using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ADR 0028's drift guard: the terrain splat shader is a second IMPLEMENTATION of the band look,
    /// but <see cref="StPetersShoreMap"/> stays the single source of truth — the builder pushes the
    /// CPU constants into the surface at build time, and THIS pins the shipped material/shader
    /// DEFAULTS to the same numbers, so an edit to either side that forgets the other fails red
    /// instead of silently splitting the picture from the classifier.
    ///
    /// <para>Also pins the sorting contract the tide reveal depends on: the ground quad must sit
    /// below the retained tile layers AND below the Sea plane at −5 (ADR 0012 — the water clips
    /// itself transparent over dry ground; anything at or above it would cover the reveal).</para>
    /// </summary>
    public class TerrainSplatBandPinTests
    {
        private const string SplatMaterialPath = "Assets/_Project/Art/Materials/TerrainSplat.mat";
        private const int SeaSortingOrder = -5;   // the builder's Sea plane slot (StPetersBuilder)

        private static Material LoadMat()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SplatMaterialPath);
            Assert.IsNotNull(mat, $"'{SplatMaterialPath}' missing — the pin guards nothing.");
            return mat;
        }

        [Test]
        public void MaterialDefaults_MatchTheCpuClassifierConstants()
        {
            var mat = LoadMat();

            // The band ladder (StPetersShoreMap §floors).
            Assert.AreEqual(StPetersShoreMap.PaintFloorElevation, mat.GetFloat("_FloorPaint"), 1e-4f,
                "_FloorPaint drifted from StPetersShoreMap.PaintFloorElevation.");
            Assert.AreEqual(StPetersShoreMap.RippleFloorElevation, mat.GetFloat("_FloorRipple"), 1e-4f,
                "_FloorRipple drifted from StPetersShoreMap.RippleFloorElevation.");
            Assert.AreEqual(StPetersShoreMap.SandFloorElevation, mat.GetFloat("_FloorSand"), 1e-4f,
                "_FloorSand drifted from StPetersShoreMap.SandFloorElevation.");
            Assert.AreEqual(StPetersShoreMap.MarramFloorElevation, mat.GetFloat("_FloorMarram"), 1e-4f,
                "_FloorMarram drifted from StPetersShoreMap.MarramFloorElevation.");
            Assert.AreEqual(StPetersShoreMap.GrassFloorElevation, mat.GetFloat("_FloorGrass"), 1e-4f,
                "_FloorGrass drifted from StPetersShoreMap.GrassFloorElevation.");
            Assert.AreEqual(StPetersShoreMap.ShingleFloorElevation, mat.GetFloat("_FloorShingle"), 1e-4f,
                "_FloorShingle drifted from StPetersShoreMap.ShingleFloorElevation.");

            // The meander (same parameters as the CPU wiggle; the lattice hash is look-only free).
            Assert.AreEqual(StPetersShoreMap.BandWiggleMetres, mat.GetFloat("_BandWiggleMetres"), 1e-4f);
            Assert.AreEqual(StPetersShoreMap.BandWiggleScale, mat.GetFloat("_BandWiggleScale"), 1e-4f);
            Assert.AreEqual(StPetersShoreMap.BandDetailMetres, mat.GetFloat("_BandDetailMetres"), 1e-4f);
            Assert.AreEqual(StPetersShoreMap.BandDetailScale, mat.GetFloat("_BandDetailScale"), 1e-4f);

            // The sector + the bar's wiggle-exempt vocabulary.
            Assert.AreEqual(StPetersShoreMap.SectorFeather, mat.GetFloat("_SectorFeather"), 1e-4f);
            Assert.AreEqual(StPetersShoreMap.BarSpineHalfWidth, mat.GetFloat("_BarSpineHalfWidth"), 1e-4f);
            Assert.AreEqual(StPetersShoreMap.BarSpineFloorElevation, mat.GetFloat("_BarSpineFloor"), 1e-4f);
        }

        // =========================================================================================
        //  ADR 0028 PR 2: the material/slice tables live in THREE places — the shader's static
        //  arrays, TerrainTexArrayBuilder's pack order, and the kit manifest's offset flags. These
        //  pins parse the shader source (batch-safe, no compile needed) and hold all three together.
        // =========================================================================================

        /// <summary>Canonical material order 0..17 — the shader header's list and the splat channel
        /// packing (A.rgba, B.rgba, C.rgba, D.rgba, E.rg) both follow it. Written out as a LITERAL on
        /// purpose: deriving it from the code under test would pin nothing.</summary>
        private static readonly string[] CanonicalOrder =
        {
            "Grass", "Marram", "Sand", "Shingle", "Ripple", "Shelf", "Silt",
            "Dirt", "Marsh", "Sedge", "Foreshore", "Talus", "Ledge", "Rockweed",
            "Musselbed", "Oysterreef", "Eelgrass", "Irishmoss",
        };

        /// <summary>The order as SHIPPED before kit v3 — indices 0..13 can never move, because
        /// committed splat PNGs and the shader's unpack agree on what each index means. A reorder
        /// would repaint the ground silently rather than fail.
        ///
        /// <para>Grown from 10 to 14 when the v3 beds appended: the v2 families are shipped now too,
        /// with paint in StPetersSplatC/D behind them, so freezing only the v1 prefix would have let
        /// foreshore..rockweed be reordered under committed pixels.</para></summary>
        private static readonly string[] FrozenPrefixShipped =
        {
            "Grass", "Marram", "Sand", "Shingle", "Ripple", "Shelf", "Silt", "Dirt", "Marsh", "Sedge",
            "Foreshore", "Talus", "Ledge", "Rockweed",
        };

        /// <summary>MAT_ARRAY/MAT_SLICE/MAT_METRES/MAT_OFFSET at indices 0..13, as shipped before v3.</summary>
        private static readonly float[] FrozenArrayShipped  = { 0, 0, 0, 1, 1, 0, 1, 0, 0, 0, 1, 1, 0, 0 };
        private static readonly float[] FrozenSliceShipped  = { 0, 3, 6, 0, 3, 9, 6, 12, 15, 18, 9, 12, 21, 24 };
        private static readonly float[] FrozenMetresShipped = { 8, 8, 8, 16, 16, 8, 16, 8, 8, 8, 16, 16, 8, 8 };
        private static readonly float[] FrozenOffsetShipped = { 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 0, 0 };

        private const string SplatShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursTerrainSplat.shader";

        /// <summary>Parse one shader table, taking its DECLARED length from the source rather than
        /// assuming one — then hold that length to the C# material count, so growing the table on
        /// one side only fails here instead of reading past the end on the GPU.</summary>
        private static float[] ParseShaderTable(string source, string tableName)
        {
            var m = Regex.Match(source, tableName + @"\[(\d+)\]\s*=\s*\{([^}]*)\}");
            Assert.IsTrue(m.Success, $"Could not find 'static const float {tableName}[N]' in the shader.");

            int declared = int.Parse(m.Groups[1].Value);
            Assert.AreEqual(TerrainSplatBrush.MaterialCount, declared,
                $"{tableName} is declared [{declared}] but TerrainSplatBrush.MaterialCount is " +
                $"{TerrainSplatBrush.MaterialCount} — the shader and the brush disagree on how many " +
                "materials exist.");

            float[] values = m.Groups[2].Value.Split(',').Select(s => float.Parse(s.Trim())).ToArray();
            Assert.AreEqual(declared, values.Length,
                $"{tableName} declares [{declared}] but lists {values.Length} entries.");
            return values;
        }

        [Test]
        public void CanonicalOrder_IsAppendOnly_TheShippedPrefixNeverMoves()
        {
            // The handoff's hard rule, asserted rather than eyeballed: kit v3 APPENDS.
            int n = FrozenPrefixShipped.Length;
            CollectionAssert.AreEqual(FrozenPrefixShipped, CanonicalOrder.Take(n).ToArray(),
                $"Canonical material indices 0..{n - 1} changed. Every committed splat PNG encodes the " +
                "old meaning per channel — a reorder repaints St Peters silently. APPEND instead.");

            string src = File.ReadAllText(SplatShaderPath);
            CollectionAssert.AreEqual(FrozenArrayShipped,
                ParseShaderTable(src, "MAT_ARRAY").Take(n).ToArray(), $"MAT_ARRAY[0..{n - 1}] moved.");
            CollectionAssert.AreEqual(FrozenSliceShipped,
                ParseShaderTable(src, "MAT_SLICE").Take(n).ToArray(), $"MAT_SLICE[0..{n - 1}] moved.");
            CollectionAssert.AreEqual(FrozenMetresShipped,
                ParseShaderTable(src, "MAT_METRES").Take(n).ToArray(), $"MAT_METRES[0..{n - 1}] moved.");
            CollectionAssert.AreEqual(FrozenOffsetShipped,
                ParseShaderTable(src, "MAT_OFFSET").Take(n).ToArray(), $"MAT_OFFSET[0..{n - 1}] moved.");
        }

        // ⭐ The four v3 beds need no pin of their own here. Every table test below is generic over
        // CanonicalOrder: ShaderMaterialTables_MatchTheArrayBuilderPackOrder derives their array,
        // slice and tile metres from the pack order; ShaderOffsetFlags_MatchTheKitManifest reads
        // their chunkOffset straight out of materials.json (so the mussel/eelgrass "never offset"
        // ruling is checked against the kit's own word, not a copy of it); and
        // KitTextures_ExistAtTheSizesThePackOrderExpects proves the twelve new PNGs imported at the
        // sizes claimed. Adding bed-specific literals here would restate all three less well.

        [Test]
        public void BrushMaterialNames_MatchTheCanonicalOrder()
        {
            // The brush names what the owner clicks in the picker; the shader unpacks by index.
            // If these two lists disagree, the picker paints a material other than the one it says.
            CollectionAssert.AreEqual(CanonicalOrder, TerrainSplatBrush.MaterialNames,
                "TerrainSplatBrush.MaterialNames drifted from the canonical order the shader unpacks.");
            Assert.AreEqual(CanonicalOrder.Length, TerrainSplatBrush.MaterialCount,
                "MaterialCount disagrees with MaterialNames.Length.");
        }

        [Test]
        public void SplatMapCount_CoversEveryMaterialChannel()
        {
            // Five RGBA maps = 20 channels for 18 materials. The moment a 21st material is wanted
            // this fails, which is the point: a sixth map is a deliberate decision, not a surprise.
            // (It fired for real on kit v3 — 14 + 4 beds did not fit four maps, and this is where
            // that was found rather than in a silently-unpainted eelgrass meadow.)
            Assert.LessOrEqual(TerrainSplatBrush.MaterialCount, TerrainSplatBrush.TextureCount * 4,
                $"{TerrainSplatBrush.MaterialCount} materials do not fit " +
                $"{TerrainSplatBrush.TextureCount} RGBA splat maps — add a map (and its shader " +
                "sampler, Properties entry, ConfigureSplat argument and asset path) first.");
            Assert.AreEqual(TerrainSplatBrush.TextureCount, TerrainSplatBrush.TextureSuffixes.Length,
                "TextureSuffixes must name exactly TextureCount maps.");
        }

        [Test]
        public void ShaderMaterialTables_MatchTheArrayBuilderPackOrder()
        {
            string src = File.ReadAllText(SplatShaderPath);
            float[] arr = ParseShaderTable(src, "MAT_ARRAY");
            float[] slice = ParseShaderTable(src, "MAT_SLICE");
            float[] metres = ParseShaderTable(src, "MAT_METRES");

            for (int i = 0; i < CanonicalOrder.Length; i++)
            {
                string name = CanonicalOrder[i];
                int i512 = System.Array.IndexOf(TerrainTexArrayBuilder.Order512, name);
                int i256 = System.Array.IndexOf(TerrainTexArrayBuilder.Order256, name);
                Assert.IsTrue(i512 >= 0 || i256 >= 0,
                    $"'{name}' is in neither pack order — the builder cannot supply the shader's slice.");

                float expArr = i512 >= 0 ? 1f : 0f;
                float expSlice = (i512 >= 0 ? i512 : i256) * TerrainTexArrayBuilder.LadderSteps.Length;
                float expMetres = i512 >= 0 ? 16f : 8f;   // the kit: 512 px = 16 m, 256 px = 8 m at 32 px/m

                Assert.AreEqual(expArr, arr[i], $"MAT_ARRAY[{i}] ({name}) disagrees with the pack order.");
                Assert.AreEqual(expSlice, slice[i], $"MAT_SLICE[{i}] ({name}) disagrees with the pack order.");
                Assert.AreEqual(expMetres, metres[i], $"MAT_METRES[{i}] ({name}) disagrees with the kit sizing.");
            }
        }

        [Test]
        public void ShaderOffsetFlags_MatchTheKitManifest()
        {
            // The manifest is the kit's word on which materials tolerate hashed UV offsets
            // (README §4: never the directional ripple/marram — an offset slices a ripple train).
            string manifestPath = Path.Combine(Application.dataPath, "../docs/art/rigs/terrain/materials.json");
            Assert.IsTrue(File.Exists(manifestPath), "Kit manifest missing at docs/art/rigs/terrain/materials.json.");
            string manifest = File.ReadAllText(manifestPath);

            var allowed = new Dictionary<string, bool>();
            foreach (Match m in Regex.Matches(manifest,
                @"""key"":\s*""(\w+)"".*?""chunkOffset"":\s*(true|false)"))
                allowed[m.Groups[1].Value] = m.Groups[2].Value == "true";

            float[] off = ParseShaderTable(File.ReadAllText(SplatShaderPath), "MAT_OFFSET");
            for (int i = 0; i < CanonicalOrder.Length; i++)
            {
                string key = CanonicalOrder[i].ToLowerInvariant();
                Assert.IsTrue(allowed.ContainsKey(key), $"Manifest has no entry for '{key}'.");
                Assert.AreEqual(allowed[key] ? 1f : 0f, off[i],
                    $"MAT_OFFSET[{i}] ({key}) disagrees with the kit manifest's chunkOffset flag.");
            }
        }

        [Test]
        public void KitTextures_ExistAtTheSizesThePackOrderExpects()
        {
            foreach (var (order, size) in new[]
                     { (TerrainTexArrayBuilder.Order256, 256), (TerrainTexArrayBuilder.Order512, 512) })
            foreach (string name in order)
            foreach (string step in TerrainTexArrayBuilder.LadderSteps)
            {
                string path = $"{TerrainTexArrayBuilder.TexDir}/{name}{step}.png";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.IsNotNull(tex, $"Kit texture missing: '{path}' — the array builder would skip the pack.");

                // BuildArray hard-rejects any other size and then builds NOTHING ("never half a
                // kit"), so a wrong-sized import costs the whole ground, not one material.
                Assert.AreEqual(size, tex.width, $"'{path}' is {tex.width}px wide, not {size}px.");
                Assert.AreEqual(size, tex.height, $"'{path}' is {tex.height}px tall, not {size}px.");
            }
        }

        [Test]
        public void KitTextures_CarryTheLoadBearingImportSettings()
        {
            // These four are not cosmetic. isReadable off makes the array pack fail; sRGB off
            // gamma-warps every albedo; a compressed or filtered import is DXT blocking and
            // blur on a kit whose whole contract is "Repeat + Point, exactly periodic".
            foreach (string name in TerrainTexArrayBuilder.Order256.Concat(TerrainTexArrayBuilder.Order512))
            foreach (string step in TerrainTexArrayBuilder.LadderSteps)
            {
                string path = $"{TerrainTexArrayBuilder.TexDir}/{name}{step}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, $"No TextureImporter for '{path}'.");

                Assert.IsTrue(importer.isReadable,
                    $"'{path}' is not readable — TerrainTexArrayBuilder.BuildArray reads its pixels.");
                Assert.IsTrue(importer.sRGBTexture,
                    $"'{path}' is not sRGB — the kit is albedo (note this is the OPPOSITE of the " +
                    "splat weight maps, which are linear data).");
                Assert.AreEqual(FilterMode.Point, importer.filterMode,
                    $"'{path}' is filtered — the kit contract is Point (kit README §7).");
                Assert.AreEqual(TextureWrapMode.Repeat, importer.wrapMode,
                    $"'{path}' does not Repeat — every kit tile is exactly periodic.");
                Assert.AreEqual(TextureImporterCompression.Uncompressed,
                    importer.textureCompression,
                    $"'{path}' is compressed — DXT blocking is visible on the flats (README §7).");
                Assert.GreaterOrEqual(importer.maxTextureSize, Mathf.Max(1, LoadedSize(path)),
                    $"'{path}' has a max size below its native resolution — Unity would import it " +
                    "DOWNSCALED and the array pack would reject it.");
            }
        }

        private static int LoadedSize(string path)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return tex != null ? Mathf.Max(tex.width, tex.height) : 0;
        }

        [Test]
        public void EdgeStrips_AreImportedAndCarryTheirDecalSettings()
        {
            // Kit v2's edge strips are imported but NOT wired (docs/design/terrain-edge-strips.md).
            // Pin the settings anyway: they are what makes the strips usable when the feature lands,
            // and an unwired asset is exactly the kind that drifts unnoticed.
            foreach (string name in new[] { "Turf", "Scarp", "Wrack", "Weedline" })
            foreach (string step in TerrainTexArrayBuilder.LadderSteps)
            {
                string path = $"{TerrainTexArrayBuilder.TexDir}/Edges/{name}{step}.png";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.IsNotNull(tex, $"Edge strip missing: '{path}'.");
                Assert.AreEqual(256, tex.width, $"'{path}' is not 256 wide (8 m along shore).");
                Assert.AreEqual(128, tex.height, $"'{path}' is not 128 tall (4 m across the boundary).");

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, $"No TextureImporter for '{path}'.");
                Assert.IsTrue(importer.alphaIsTransparency,
                    $"'{path}' must carry straight alpha — a strip IS its alpha falloff.");
                Assert.AreEqual(TextureWrapMode.Repeat, importer.wrapModeU,
                    $"'{path}' must repeat along the shore (s).");
                Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapModeV,
                    $"'{path}' must CLAMP across the boundary (t) — repeating t wraps the seaward " +
                    "edge of the strip back onto its landward edge.");
            }
        }

        [Test]
        public void GroundSortsBelowTheTileBandAndTheSea()
        {
            Assert.Less(TerrainSplatSurface.DefaultSortingOrder, StPetersShorePainter.GroundSortingOrder,
                "The splat ground must render UNDER the retained tile layers.");
            Assert.Less(TerrainSplatSurface.DefaultSortingOrder, SeaSortingOrder,
                "The splat ground must render UNDER the Sea plane or the ADR 0012 tide reveal breaks.");
        }

        [Test]
        public void MaterialUsesTheTerrainSplatShader()
        {
            // By ASSET, not by Shader.name — the name is empty in batch mode (the water sweep's
            // path-based discovery exists for the same reason).
            var mat = LoadMat();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/_Project/Art/Shaders/HiddenHarboursTerrainSplat.shader");
            Assert.IsNotNull(shader, "HiddenHarboursTerrainSplat.shader missing.");
            Assert.AreEqual(shader, mat.shader,
                "TerrainSplat.mat is not on HiddenHarbours/TerrainSplat — the defaults pinned above " +
                "would be someone else's defaults.");
        }
    }
}
