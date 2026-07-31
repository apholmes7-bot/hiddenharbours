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

        /// <summary>Canonical material order 0..9 — the shader header's list and the splat channel
        /// packing (A.rgba, B.rgba, C.rg) both follow it.</summary>
        private static readonly string[] CanonicalOrder =
            { "Grass", "Marram", "Sand", "Shingle", "Ripple", "Shelf", "Silt", "Dirt", "Marsh", "Sedge" };

        private const string SplatShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursTerrainSplat.shader";

        private static float[] ParseShaderTable(string source, string tableName)
        {
            var m = Regex.Match(source, tableName + @"\[10\]\s*=\s*\{([^}]*)\}");
            Assert.IsTrue(m.Success, $"Could not find 'static const float {tableName}[10]' in the shader.");
            return m.Groups[1].Value.Split(',').Select(s => float.Parse(s.Trim())).ToArray();
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
            foreach (string name in TerrainTexArrayBuilder.Order256.Concat(TerrainTexArrayBuilder.Order512))
            foreach (string step in TerrainTexArrayBuilder.LadderSteps)
            {
                string path = $"{TerrainTexArrayBuilder.TexDir}/{name}{step}.png";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.IsNotNull(tex, $"Kit texture missing: '{path}' — the array builder would skip the pack.");
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
