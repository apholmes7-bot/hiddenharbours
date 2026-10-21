using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>ONE HEIGHT SOURCE, AT SIXTEEN BITS</b> (terrain pass 9 PR 4; ADR 0046 §8; the owner's decision 11:
    /// "a 16-bit height map, −4 to +7 m"). Subjects: the terrain paint tool's two height writers — a map's
    /// creation or export (<see cref="TerrainPaintTool.BakeAnalyticCoast"/> → <c>CreatePaintedMapAsset</c>) and
    /// a stroke's commit (<c>CommitTexture</c>) — which both write through
    /// <see cref="PaintedHeightPng.WriteAndImport"/>; and the sim's read of what they wrote,
    /// <see cref="PaintedHeightMap.ReadNormalizedR"/>.
    ///
    /// <para><b>1. A height the tool bakes comes back within one R16 step.</b> A ramp, asymmetric in both
    /// axes (so a flipped or transposed bake fails too), baked over −4 … +7 m and read back through the sim's
    /// own decode at every texel centre. The bar is one sixteen-bit step over the range,
    /// <c>11 / 65535</c> = 0.17 mm; rounding to a code costs at most half of it. <b>On the base</b> the tool
    /// wrote <c>R8</c>, one step 4.3 cm: the worst texel of this ramp comes back 0.0216 m out, 128× the
    /// bar (a counterfactual computed outside Unity, the base's encode and decode over the same ramp).</para>
    ///
    /// <para><b>2. An 8-bit map widens on its first write without moving a texel.</b> The committed maps
    /// stay 8-bit files until someone paints them; the first stroke rewrites the whole file through the
    /// tool's one write. An 8-bit PNG, imported as the committed maps are, is read the way the tool reads
    /// it (<c>GetPixels</c>) and written back the way a stroke writes it. The file must come back sixteen
    /// bits deep, each code k must be stored as exactly 257·k (65535 = 255 × 257), and every texel must
    /// decode to the metres it decoded to before — and to <c>lerp(min, max, k / 255)</c>, the expectation
    /// this test computes from the code it wrote, not from the code under test. <b>On the base</b>
    /// <c>PaintedHeightPng</c> does not exist; the base's stroke (<c>CommitTexture</c>, base :1012–1024)
    /// encoded an <c>R8</c> texture, so the file stays eight bits deep and the bit-depth and 257·k
    /// assertions fail.</para>
    /// </summary>
    public class PaintedHeightMapR16Tests
    {
        private const string TempMapPath = "Assets/TempR16HeightMap.asset";
        private const string TempMapPngPath = "Assets/TempR16HeightMap_HeightTex.png";
        private const string TempWidenPngPath = "Assets/TempR16Widen_HeightTex.png";

        // The range PR 4 carries (decision 11), and one sixteen-bit step over it.
        private const float RangeMin = -4f, RangeMax = 7f;
        private const float OneR16Step = (RangeMax - RangeMin) / 65535f;

        private PaintedHeightMap _inMemoryMap;

        [TearDown]
        public void TearDown()
        {
            if (_inMemoryMap != null) Object.DestroyImmediate(_inMemoryMap);
            _inMemoryMap = null;
            AssetDatabase.DeleteAsset(TempMapPath);
            AssetDatabase.DeleteAsset(TempMapPngPath);
            AssetDatabase.DeleteAsset(TempWidenPngPath);
            GameServices.Reset();
        }

        /// <summary>A plain ramp in BOTH axes, inside −4 … +7 m over the fixture's 80 × 60 m
        /// (−3.38 … +4.97 m), so the bake keeps the map's own range.</summary>
        private sealed class RampTerrain : ITidalTerrain
        {
            public float ElevationAt(Vector2 p) => -3.5f + 0.0731f * (p.x + 40f) + 0.0457f * (p.y + 30f);
        }

        // ---------------------------------------------------------------------------------------------------
        // 1. The bake
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void ABakedHeight_ComesBackWithinOneSixteenBitStep()
        {
            var map = ScriptableObject.CreateInstance<PaintedHeightMap>();
            var so = new SerializedObject(map);
            so.FindProperty("_minElevation").floatValue = RangeMin;
            so.FindProperty("_maxElevation").floatValue = RangeMax;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(map, TempMapPath);
            map = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(TempMapPath);

            var ramp = new RampTerrain();
            var texels = new Vector2Int(40, 30);
            var size = new Vector2(80f, 60f);
            PaintedHeightMap baked = TerrainPaintTool.BakeAnalyticCoast(
                map, new ITidalTerrain[] { ramp }, Vector2.zero, size, texels);
            Assert.IsNotNull(baked, "the bake returned no map.");
            baked.Rebuild();   // the bake overwrites the map in place; a live map re-decodes on Rebuild
            Assert.AreEqual(RangeMin, baked.MinElevation, "the bake moved the range's minimum.");
            Assert.AreEqual(RangeMax, baked.MaxElevation, "the bake moved the range's maximum.");

            PaintedHeightField field = baked.Field;
            Assert.IsNotNull(field, "the baked map decoded no field.");
            Assert.AreEqual(texels.x, field.Width);
            Assert.AreEqual(texels.y, field.Height);

            int bad = 0;
            float worst = 0f;
            Vector2Int worstAt = default;
            for (int y = 0; y < field.Height; y++)
            {
                for (int x = 0; x < field.Width; x++)
                {
                    Vector2 p = field.TexelToWorld(x, y);
                    float err = Mathf.Abs(field.ElevationAt(p) - ramp.ElevationAt(p));
                    if (!(err <= OneR16Step)) bad++;
                    if (float.IsNaN(err) || err > worst) { worst = err; worstAt = new Vector2Int(x, y); }
                }
            }

            Texture2D tex = baked.HeightTexture;
            string how = $"The map loads as {(tex != null ? tex.format.ToString() : "no texture")} with the " +
                         $"editor on {EditorUserBuildSettings.activeBuildTarget}. Worst texel {worstAt}: " +
                         $"{worst:0.000000} m out; the bar is one R16 step over {RangeMin} … {RangeMax} m, " +
                         $"{OneR16Step:0.000000} m (an R8 map's worst over this ramp is 0.021557 m).";
            Assert.AreEqual(0, bad,
                $"{bad} of {field.Width * field.Height} baked texels came back more than one sixteen-bit step " +
                "from the height they were baked from — the map is not being written, imported or read at " +
                "sixteen bits (ADR 0046 §8). " + how);
            Debug.Log("[PaintedHeightMapR16] the bake: " + how);
        }

        // ---------------------------------------------------------------------------------------------------
        // 2. Widening
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void AnEightBitMap_WidensOnItsFirstWrite_WithoutMovingATexel()
        {
            const int w = 16, h = 16;           // 256 texels: code k at texel k, in Unity's order (bottom row first)
            const float lo = -4f, hi = 6f;      // St Peters' committed range

            // An 8-bit greyscale PNG holding every code once, imported the way the committed maps are.
            var codes8 = new byte[w * h];
            for (int i = 0; i < codes8.Length; i++) codes8[i] = (byte)i;
            var src = new Texture2D(w, h, TextureFormat.R8, false, true);
            try
            {
                src.SetPixelData(codes8, 0);
                src.Apply(false, false);
                File.WriteAllBytes(TempWidenPngPath, src.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(src);
            }
            AssertPngDepth(TempWidenPngPath, 8, "the fixture's 8-bit PNG");
            ImportAsTheCommittedMapsAre(TempWidenPngPath);

            var tex8 = AssetDatabase.LoadAssetAtPath<Texture2D>(TempWidenPngPath);
            Assert.IsNotNull(tex8, "the 8-bit fixture did not import.");
            Assert.IsTrue(tex8.isReadable, "the 8-bit fixture imported unreadable.");
            Assert.AreNotEqual(TextureFormat.R16, tex8.format,
                "premise: an 8-bit PNG imported like the committed maps loads at eight bits.");
            Color[] px8 = tex8.GetPixels();     // what the tool's stroke reads (and edits) before it commits
            Assert.AreEqual(w * h, px8.Length);
            for (int k = 0; k < px8.Length; k++)
                Assert.AreEqual(k / 255f, px8[k].r, 1e-6f, $"premise: texel {k} holds code {k}.");

            // The metres the map decodes to BEFORE the write, through the production decode.
            _inMemoryMap = ScriptableObject.CreateInstance<PaintedHeightMap>();
            Bind(_inMemoryMap, tex8, new Vector2(w, h), lo, hi);
            var before = new float[w * h];
            for (int k = 0; k < before.Length; k++)
                before[k] = _inMemoryMap.Field.ElevationAtTexel(k % w, k / w);

            // THE FIRST WRITE: what a stroke's commit does with the pixels it read.
            Texture2D tex16 = PaintedHeightPng.WriteAndImport(TempWidenPngPath, px8, w, h);

            AssertPngDepth(TempWidenPngPath, 16, "the map after its first write");
            Assert.IsNotNull(tex16, "the written map did not load.");
            Assert.AreEqual(TextureFormat.R16, tex16.format,
                $"the written map loads as {tex16.format} with the editor on " +
                $"{EditorUserBuildSettings.activeBuildTarget}: the importer narrowed a sixteen-bit file.");
            Assert.IsTrue(tex16.isReadable, "the written map imported unreadable.");

            var raw = tex16.GetPixelData<ushort>(0);
            Assert.AreEqual(w * h, raw.Length, "the written map's texel count.");
            int wrongCodes = 0, firstWrong = -1;
            for (int k = 0; k < raw.Length; k++)
                if (raw[k] != 257 * k) { if (firstWrong < 0) firstWrong = k; wrongCodes++; }
            Assert.AreEqual(0, wrongCodes,
                $"{wrongCodes} texels were not widened to 257·k — the first, texel {firstWrong}, holds " +
                $"{(firstWrong < 0 ? 0 : raw[firstWrong])} (want {257 * firstWrong}).");

            float[] r01 = PaintedHeightMap.ReadNormalizedR(tex16);
            Assert.IsNotNull(r01, "the sim could not read the written map.");
            for (int k = 0; k < r01.Length; k++)
                Assert.AreEqual(k / 255f, r01[k],
                    $"texel {k}: the sim reads {r01[k]:R}, not the {(k / 255f):R} the 8-bit map held.");

            // The metres AFTER the write, through the same production decode.
            Bind(_inMemoryMap, tex16, new Vector2(w, h), lo, hi);
            int moved = 0;
            float worst = 0f;
            for (int k = 0; k < before.Length; k++)
            {
                float after = _inMemoryMap.Field.ElevationAtTexel(k % w, k / w);
                float want = Mathf.Lerp(lo, hi, k / 255f);   // this test's own expectation
                float err = Mathf.Max(Mathf.Abs(after - before[k]), Mathf.Abs(after - want));
                if (!(err <= 1e-5f)) moved++;
                if (float.IsNaN(err) || err > worst) worst = err;
            }
            Assert.AreEqual(0, moved,
                $"{moved} texels decode to different metres after the map's first write (worst {worst} m).");
            Debug.Log($"[PaintedHeightMapR16] widening: all {w * h} codes stored as 257·k in a 16-bit file, " +
                      $"read back as k / 255 exactly, and no texel moved by more than {worst:0.0e+00} m.");
        }

        // ---------------------------------------------------------------------------------------------------

        private static void Bind(PaintedHeightMap map, Texture2D tex, Vector2 size, float lo, float hi)
        {
            var so = new SerializedObject(map);
            so.FindProperty("_heightTexture").objectReferenceValue = tex;
            so.FindProperty("_worldCenter").vector2Value = Vector2.zero;
            so.FindProperty("_worldSize").vector2Value = size;
            so.FindProperty("_minElevation").floatValue = lo;
            so.FindProperty("_maxElevation").floatValue = hi;
            so.ApplyModifiedPropertiesWithoutUndo();
            map.Rebuild();
        }

        /// <summary>The import settings the committed height maps carry: a linear, readable, unmipped,
        /// clamped, uncompressed data texture on the Default platform, and no Standalone override.</summary>
        private static void ImportAsTheCommittedMapsAre(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.IsNotNull(importer, $"{path} has no TextureImporter.");
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.ClearPlatformTextureSettings("Standalone");
            importer.SaveAndReimport();
        }

        /// <summary>The PNG's own bit depth (IHDR byte 24) and colour type (byte 25, 0 = greyscale).</summary>
        private static void AssertPngDepth(string path, int bitDepth, string what)
        {
            byte[] png = File.ReadAllBytes(path);
            Assert.GreaterOrEqual(png.Length, 29, $"{what}: '{path}' is too short to be a PNG.");
            Assert.AreEqual(0x89, png[0], $"{what}: '{path}' is not a PNG.");
            Assert.AreEqual(bitDepth, png[24], $"{what}: '{path}' is {png[24]} bits deep, not {bitDepth}.");
            Assert.AreEqual(0, png[25], $"{what}: '{path}' is colour type {png[25]}, not greyscale (0).");
        }
    }
}
