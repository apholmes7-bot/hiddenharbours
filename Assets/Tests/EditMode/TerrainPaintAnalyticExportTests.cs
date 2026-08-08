using System.Collections.Generic;
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
    /// ⭐ <b>THE TERRAIN PAINT TOOL'S OPEN-SCENE ANALYTIC EXPORT</b> — the seeder that lets Nine Mile
    /// Creek's blank 1520 × 1120 canvas be filled from its OWN coast instead of from St Peters'.
    ///
    /// <para>The load-bearing test in this file is
    /// <see cref="TheExportedMapCarriesTheAnalyticMainlandCoast"/>. Nine Mile Creek's bar crest is
    /// SEAM-PINNED against St Peters' (#453/#462): the two regions' halves of one crossing must bare at
    /// the same tide and to the same width, which only holds while the crest is 0.88 m on both sides. A
    /// freehand brush cannot hit that, and an export that samples the wrong terrain, the wrong rect, or
    /// the right rect upside down would hand the owner a crossing that reads correct on the map and
    /// wrong under his boots. So the export is measured against the ANALYTIC plan
    /// (<see cref="NineMileCreekMainland"/>'s own constants, through the same
    /// <c>ConfigureTerrain</c> call the builder makes) rather than against numbers restated here.</para>
    ///
    /// <para><b>The orientation trap has its own test.</b> <c>EncodeToPNG</c> writes rows in the opposite
    /// order to <c>Texture2D.SetPixels</c>, so a bake that is wrong way up round-trips CLEANLY through
    /// any y-symmetric fixture and fails only in the game. Every fixture here is asymmetric in BOTH
    /// axes, and <see cref="TheExportedMapIsNeitherUpsideDownNorTransposed"/> proves the asymmetry
    /// before it relies on it.</para>
    ///
    /// <para>Nothing here touches the owner's <c>Data/Terrain/PaintedSeabed.asset</c> or the committed
    /// <c>StPetersSeabed</c>: the round-trip tests mint their own temp map at the Assets root and delete
    /// it again (the <c>StPetersStarterSplatTests</c> importer-probe convention).</para>
    /// </summary>
    public class TerrainPaintAnalyticExportTests
    {
        // Temp assets, at the Assets root, deleted in TearDown. The PNG name is the tool's own
        // "<stem>_HeightTex.png" sibling convention — asserted below rather than assumed.
        private const string TempMapPath = "Assets/TempAnalyticExportMap.asset";
        private const string TempPngPath = "Assets/TempAnalyticExportMap_HeightTex.png";

        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            AssetDatabase.DeleteAsset(TempMapPath);
            AssetDatabase.DeleteAsset(TempPngPath);
            GameServices.Reset();
        }

        // =============================================================================================
        //  fixtures
        // =============================================================================================

        /// <summary>A terrain that is a plain ramp in BOTH axes — so a flipped, mirrored or transposed
        /// grid cannot round-trip past it. Pure; no scene.</summary>
        private sealed class BiasedRampTerrain : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => 0.1f * worldPos.x + 0.01f * worldPos.y;
        }

        private sealed class ConstantTerrain : ITidalTerrain
        {
            private readonly float _elevation;
            public ConstantTerrain(float elevation) => _elevation = elevation;
            public float ElevationAt(Vector2 worldPos) => _elevation;
        }

        /// <summary>The mainland terrain the SCENE carries — built through the builder's own configure
        /// call, so a test can never assert a coast Nine Mile Creek does not have.</summary>
        private MainlandTidalTerrain NineMileCreekTerrain()
        {
            var go = new GameObject("NineMileCreekMainland_ExportTest");
            _spawned.Add(go);
            var terrain = go.AddComponent<MainlandTidalTerrain>();
            NineMileCreekMainland.ConfigureTerrain(terrain);
            return terrain;
        }

        /// <summary>An empty painted map asset on disk, at the range Nine Mile Creek's blank canvas
        /// publishes (−4 … 6 m) — the range the export has to WIDEN to reach a −6 m bay floor.</summary>
        private static PaintedHeightMap TempMap(float minElevation = -4f, float maxElevation = 6f)
        {
            var map = ScriptableObject.CreateInstance<PaintedHeightMap>();
            var so = new SerializedObject(map);
            so.FindProperty("_minElevation").floatValue = minElevation;
            so.FindProperty("_maxElevation").floatValue = maxElevation;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(map, TempMapPath);
            return AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(TempMapPath);
        }

        /// <summary>The tolerance the R8 encoding itself imposes: one 8-bit step across the map's own
        /// range, plus a hair for the bilinear read. Derived from the map, never a literal — a wider
        /// range is a coarser map, and a test with a fixed tolerance would quietly stop measuring.</summary>
        private static float R8Tolerance(PaintedHeightMap map) =>
            1.5f * (map.MaxElevation - map.MinElevation) / 255f + 0.01f;

        // =============================================================================================
        //  1. composition — several analytic terrains in one scene
        // =============================================================================================

        [Test]
        public void ComposeElevation_TakesTheHighestTerrain()
        {
            var terrains = new ITidalTerrain[] { new ConstantTerrain(-6f), new ConstantTerrain(2.5f),
                                                 new ConstantTerrain(-1f) };
            Assert.AreEqual(2.5f, TerrainPaintTool.ComposeElevation(terrains, Vector2.zero), 1e-4f,
                "two analytic terrains in one scene compose the way their own zones do — highest wins");
        }

        [Test]
        public void ComposeElevation_SkipsNullsAndReportsNothingAsNaN()
        {
            var terrains = new ITidalTerrain[] { null, new ConstantTerrain(1.25f) };
            Assert.AreEqual(1.25f, TerrainPaintTool.ComposeElevation(terrains, Vector2.zero), 1e-4f);

            Assert.IsNaN(TerrainPaintTool.ComposeElevation(new ITidalTerrain[0], Vector2.zero),
                "nothing to sample is NaN, not 0 — a quiet 0 would bake a sea-level plateau over " +
                "the whole region and look like a successful export");
        }

        // =============================================================================================
        //  2. the texel walk — the same one BakeStPetersSeabed makes
        // =============================================================================================

        [Test]
        public void SampleAnalyticElevations_PutsTexelZeroAtTheWorldMinCorner()
        {
            var terrain = new BiasedRampTerrain();
            Vector2 center = new Vector2(100f, -50f), size = new Vector2(40f, 20f);
            var texels = new Vector2Int(8, 4);

            float[] elev = TerrainPaintTool.SampleAnalyticElevations(
                new[] { terrain }, center, size, texels);

            Assert.AreEqual(texels.x * texels.y, elev.Length);

            Vector2 min = center - size * 0.5f;
            for (int y = 0; y < texels.y; y++)
            for (int x = 0; x < texels.x; x++)
            {
                var expectedAt = new Vector2(min.x + (x + 0.5f) / texels.x * size.x,
                                             min.y + (y + 0.5f) / texels.y * size.y);
                Assert.AreEqual(terrain.ElevationAt(expectedAt), elev[y * texels.x + x], 1e-4f,
                    $"texel ({x},{y}) samples its own centre, with y = 0 the world-MIN row");
            }
        }

        [Test]
        public void SampleAnalyticElevations_Cancelling_WritesNothing()
        {
            float[] elev = TerrainPaintTool.SampleAnalyticElevations(
                new ITidalTerrain[] { new ConstantTerrain(1f) },
                Vector2.zero, new Vector2(10f, 10f), new Vector2Int(16, 16),
                onProgress: _ => false);

            Assert.IsNull(elev, "a cancelled walk returns nothing at all — a half-sampled coast written " +
                                "over the owner's map would be worse than no export");
        }

        // =============================================================================================
        //  3. the encode range — the clip that would be silent
        // =============================================================================================

        [Test]
        public void ResolveEncodeRange_WidensToReachTheCoastItSampled()
        {
            // Nine Mile Creek's shape exactly: a −6 m bay floor sampled into a map that publishes −4 m.
            var sampled = new[] { -6f, -1.6f, 0.88f, 6f };
            TerrainPaintTool.ResolveEncodeRange(sampled, -4f, 6f, out float min, out float max);

            Assert.AreEqual(-6f, min, 1e-4f,
                "the bay floor must be REACHABLE — encoding clamps, so a −4 m range would write the " +
                "−6 m bay as −4 m with no error anywhere");
            Assert.AreEqual(6f, max, 1e-4f);
        }

        [Test]
        public void ResolveEncodeRange_KeepsTheHeadroomTheMapAlreadyPublished()
        {
            // A map the owner already painted an 8 m cliff into: a re-export must not narrow it back.
            var sampled = new[] { -6f, 6f };
            TerrainPaintTool.ResolveEncodeRange(sampled, -4f, 8f, out float min, out float max);

            Assert.AreEqual(-6f, min, 1e-4f);
            Assert.AreEqual(8f, max, 1e-4f,
                "re-exporting must not narrow the range the owner's brush presets paint into");
        }

        [Test]
        public void ResolveEncodeRange_ADegenerateRangeIsOpened()
        {
            TerrainPaintTool.ResolveEncodeRange(new[] { 3f, 3f }, 3f, 3f, out float min, out float max);
            Assert.That(max - min, Is.GreaterThanOrEqualTo(TerrainPaintTool.MinimumEncodeSpan),
                "a zero span makes every texel decode to the same height — a flat map that looks " +
                "exactly like a successful bake");
        }

        [Test]
        public void ResolveEncodeRange_ABackwardsMapRangeStillWidensOutwards()
        {
            TerrainPaintTool.ResolveEncodeRange(new[] { 0f }, 6f, -4f, out float min, out float max);
            Assert.AreEqual(-4f, min, 1e-4f);
            Assert.AreEqual(6f, max, 1e-4f);
        }

        [Test]
        public void EncodeElevationPixels_RoundTripsThroughTheSharedDecode()
        {
            var elevations = new[] { -6f, -1.6f, 0f, 0.88f, 6f };
            var pixels = TerrainPaintTool.EncodeElevationPixels(elevations, -6f, 6f);

            Assert.AreEqual(elevations.Length, pixels.Length);
            for (int i = 0; i < elevations.Length; i++)
            {
                Assert.AreEqual(pixels[i].r, pixels[i].g, 1e-6f, "G = B = R so an 8-bit grayscale PNG round-trips");
                Assert.AreEqual(pixels[i].r, pixels[i].b, 1e-6f);
                Assert.AreEqual(elevations[i],
                    PaintedHeightField.DecodeElevation(pixels[i].r, -6f, 6f), 1e-3f,
                    "the export encodes through the one shared field encoder, not its own arithmetic");
            }
        }

        // =============================================================================================
        //  4. the destination — never a name this tool knows
        // =============================================================================================

        [Test]
        public void ResolveMapWritePaths_TargetsTheAssignedAssetAndItsOwnSiblingPng()
        {
            var map = TempMap();
            Assert.IsTrue(TerrainPaintTool.ResolveMapWritePaths(map, out string assetPath, out string pngPath));
            Assert.AreEqual(TempMapPath, assetPath,
                "the export writes into the ASSIGNED map — the St Peters seed's name never enters it");
            Assert.AreEqual(TempPngPath, pngPath);
            Assert.That(assetPath, Does.Not.Contain(TerrainPaintTool.StPetersSeabedName));
        }

        [Test]
        public void ResolveMapWritePaths_AnUnsavedMapHasNowhereToWrite()
        {
            var loose = ScriptableObject.CreateInstance<PaintedHeightMap>();
            try
            {
                Assert.IsFalse(TerrainPaintTool.ResolveMapWritePaths(loose, out _, out _),
                    "an in-memory map has no file behind it — refuse rather than invent one");
                Assert.IsFalse(TerrainPaintTool.ResolveMapWritePaths(null, out _, out _));
            }
            finally { Object.DestroyImmediate(loose); }
        }

        [Test]
        public void BakeAnalyticCoast_RefusesTheThreeMissingInputs()
        {
            var map = TempMap();
            var terrains = new ITidalTerrain[] { new ConstantTerrain(0f) };
            var texels = new Vector2Int(8, 8);
            var size = new Vector2(8f, 8f);

            Assert.IsNull(TerrainPaintTool.BakeAnalyticCoast(null, terrains, Vector2.zero, size, texels),
                "no map assigned");
            Assert.IsNull(TerrainPaintTool.BakeAnalyticCoast(map, new ITidalTerrain[0], Vector2.zero, size, texels),
                "no analytic terrain in the open scene");
            Assert.IsNull(TerrainPaintTool.BakeAnalyticCoast(map, terrains, Vector2.zero, size, Vector2Int.zero),
                "no usable extent");
        }

        // =============================================================================================
        //  5. ⭐ the round trip — the map the owner will actually paint on
        // =============================================================================================

        [Test]
        public void TheExportedMapCarriesTheAnalyticMainlandCoast()
        {
            var terrain = NineMileCreekTerrain();
            var map = TempMap();

            Vector2 center = NineMileCreekMainland.RegionWorldCenter;
            Vector2 size = NineMileCreekMainland.RegionWorldSize;
            var texels = new Vector2Int(
                Mathf.CeilToInt(size.x * NineMileCreekMainland.SeabedPixelsPerMetre),
                Mathf.CeilToInt(size.y * NineMileCreekMainland.SeabedPixelsPerMetre));

            var baked = TerrainPaintTool.BakeAnalyticCoast(
                map, new ITidalTerrain[] { terrain }, center, size, texels);

            Assert.IsNotNull(baked, "the bake produced a map");
            Assert.AreEqual(texels.x, baked.HeightTexture.width,
                "1520 × 1120 at 2 px/m — an importer size cap here would rescale every elevation");
            Assert.AreEqual(texels.y, baked.HeightTexture.height);
            Assert.AreEqual(center, baked.WorldCenter);
            Assert.AreEqual(size, baked.WorldSize);
            Assert.That(baked.MinElevation, Is.LessThanOrEqualTo(NineMileCreekMainland.BayFloorElevation),
                "the −6 m bay floor is inside the encoded range, not clipped to the canvas's −4 m");

            baked.Rebuild();
            var field = baked.Field;
            Assert.IsNotNull(field, "the written PNG imported CPU-readable so the sim can decode it");
            float tol = R8Tolerance(baked);

            void Matches(Vector2 p, string what)
            {
                Assert.AreEqual(terrain.ElevationAt(p), field.ElevationAt(p), tol,
                    $"{what} at {p} must come off the analytic plan, not off a brush");
            }

            // ⭐ THE SEAM-PINNED BAR CREST — the crossing's half that has to agree with St Peters'.
            Vector2 crest = Vector2.Lerp(NineMileCreekMainland.BarFrom, NineMileCreekMainland.BarTo, 0.75f);
            Matches(crest, "the tidal bar's crest");
            Assert.AreEqual(NineMileCreekMainland.BarCrestElevation, field.ElevationAt(crest), tol,
                "and it is the RULED 0.88 m — the number the two halves of the crossing share");

            // The gut: the boat passage cut back through the bar.
            Vector2 gut = Vector2.Lerp(NineMileCreekMainland.BarFrom, NineMileCreekMainland.BarTo,
                                       NineMileCreekMainland.GutAlong);
            Matches(gut, "the gut through the bar");
            Assert.That(field.ElevationAt(gut), Is.LessThan(NineMileCreekMainland.BarCrestElevation),
                "the gut is CUT through the crest — a map that lost it walls the crossing off to hulls");

            // The quay fill, the harbour shoal that gates the berth, the open bay, the fields.
            Matches(NineMileCreekMainland.WestWallFill.Center, "the west wall's deck");
            Matches(NineMileCreekMainland.NorthWallFill.Center, "the north wall's deck");
            Matches(NineMileCreekMainland.HarbourShoalFill.Center, "the harbour shoal");
            Matches(new Vector2(330f, 200f), "the open bay");
            Matches(new Vector2(-300f, 0f), "the fields inland");

            Assert.AreEqual(NineMileCreekMainland.BasinBedElevation,
                field.ElevationAt(NineMileCreekMainland.HarbourShoalFill.Center), tol,
                "the ruled −1.6 m gate — the one number every hull here is measured against");
            Assert.AreEqual(NineMileCreekMainland.BayFloorElevation,
                field.ElevationAt(new Vector2(330f, 200f)), tol,
                "the open bay keeps its −6 m floor rather than saturating at the canvas's −4 m");
        }

        [Test]
        public void TheExportedMapIsNeitherUpsideDownNorTransposed()
        {
            // ⚠ EncodeToPNG writes rows in the opposite order to SetPixels, so a bake that is wrong way
            // up round-trips CLEANLY through a symmetric fixture. This one is asymmetric in BOTH axes,
            // and says so before it relies on it.
            var go = new GameObject("AsymmetricCoast_ExportTest");
            _spawned.Add(go);
            var terrain = go.AddComponent<RectTidalTerrain>();
            // Off-centre in x AND y, and far enough off the diagonal that its own transpose falls
            // outside it — a fat zone straddling the diagonal would swallow the transpose test.
            Vector2 zone = new Vector2(20f, -15f);
            terrain.Configure(-3f, new[]
            {
                new RectTidalTerrain.LandZone(zone, new Vector2(6f, 5f), 5f, 3f),
            });

            // A NON-square rect and grid, so an x-outer index or a swapped axis cannot come out square
            // and go unnoticed.
            var center = new Vector2(0f, 0f);
            var size = new Vector2(80f, 60f);
            var texels = new Vector2Int(80, 60);

            Vector2 flippedY = new Vector2(zone.x, -zone.y);      // what an upside-down bake would put here
            Vector2 transposed = new Vector2(zone.y, zone.x);     // what a transposed bake would put here

            Assert.That(Mathf.Abs(terrain.ElevationAt(zone) - terrain.ElevationAt(flippedY)),
                Is.GreaterThan(4f), "the fixture IS asymmetric in y — otherwise this test proves nothing");
            Assert.That(Mathf.Abs(terrain.ElevationAt(zone) - terrain.ElevationAt(transposed)),
                Is.GreaterThan(4f), "and in x");

            var map = TempMap(-3f, 5f);
            var baked = TerrainPaintTool.BakeAnalyticCoast(
                map, new ITidalTerrain[] { terrain }, center, size, texels);
            Assert.IsNotNull(baked);

            baked.Rebuild();
            var field = baked.Field;
            float tol = R8Tolerance(baked);

            Assert.AreEqual(terrain.ElevationAt(zone), field.ElevationAt(zone), tol,
                "the land zone is where the terrain puts it");
            Assert.AreEqual(terrain.ElevationAt(flippedY), field.ElevationAt(flippedY), tol,
                "and NOT at its y-mirror — an EncodeToPNG row flip lands here");
            Assert.AreEqual(terrain.ElevationAt(transposed), field.ElevationAt(transposed), tol,
                "and NOT at its transpose — a non-square grid indexed x-outer lands here");
        }

        [Test]
        public void ExportingIntoAMapTwiceIsIdempotent()
        {
            // The owner will re-export after every terrain tweak; the second click must not drift the
            // range (which would re-quantise the whole map) or mint a "TempAnalyticExportMap 1.asset".
            var go = new GameObject("IdempotentCoast_ExportTest");
            _spawned.Add(go);
            var terrain = go.AddComponent<RectTidalTerrain>();
            terrain.Configure(-3f, new[]
            {
                new RectTidalTerrain.LandZone(new Vector2(20f, -15f), new Vector2(6f, 5f), 5f, 3f),
            });

            var map = TempMap();
            var texels = new Vector2Int(40, 30);
            var size = new Vector2(80f, 60f);

            var first = TerrainPaintTool.BakeAnalyticCoast(
                map, new ITidalTerrain[] { terrain }, Vector2.zero, size, texels);
            Assert.IsNotNull(first);
            float min1 = first.MinElevation, max1 = first.MaxElevation;

            var second = TerrainPaintTool.BakeAnalyticCoast(
                first, new ITidalTerrain[] { terrain }, Vector2.zero, size, texels);
            Assert.IsNotNull(second);

            Assert.AreEqual(TempMapPath, AssetDatabase.GetAssetPath(second),
                "the re-export updates the map IN PLACE — no ' 1.asset' duplicate orphaning the PNG");
            Assert.AreEqual(min1, second.MinElevation, 1e-4f, "the range settles rather than creeping");
            Assert.AreEqual(max1, second.MaxElevation, 1e-4f);
        }
    }
}
