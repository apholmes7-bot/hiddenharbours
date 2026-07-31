using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The St Peters starter splat (owner request 2026-07-30): the stroke PLANS are pure functions
    /// of the builder's constants, so where the paths, silt, marsh and sedge land is pinned here
    /// headlessly — and the splat IMPORT is pinned LINEAR, because an sRGB import would gamma-warp
    /// every painted weight the shader reads (0.5 "base" would arrive as ~0.21).
    /// </summary>
    public class StPetersStarterSplatTests
    {
        // ============================ THE STROKE PLANS ============================

        [Test]
        public void MaterialIndices_MatchTheCanonicalOrder()
        {
            Assert.AreEqual("Silt", TerrainSplatBrush.MaterialNames[StPetersStarterSplat.Silt]);
            Assert.AreEqual("Dirt", TerrainSplatBrush.MaterialNames[StPetersStarterSplat.Dirt]);
            Assert.AreEqual("Marsh", TerrainSplatBrush.MaterialNames[StPetersStarterSplat.Marsh]);
            Assert.AreEqual("Sedge", TerrainSplatBrush.MaterialNames[StPetersStarterSplat.Sedge]);
        }

        [Test]
        public void SlipPath_RunsFromTheVillageGreenToTheSlip()
        {
            Vector2[] path = StPetersStarterSplat.VillageToSlipPath();
            Assert.GreaterOrEqual(path.Length, 4, "the ask is a gentle curve — 2-3 bends, not a straight line");
            Assert.AreEqual(StPetersBuilder.VillageGreen, path[0]);
            Assert.AreEqual(new Vector2(StPetersBuilder.BerthTo.x, StPetersBuilder.BerthTo.y),
                            path[path.Length - 1], "the path must end at the slip's shoreline head");
        }

        [Test]
        public void BarHeadPath_RunsFromTheVillageGreenToTheBarHead()
        {
            Vector2[] path = StPetersStarterSplat.VillageToBarHeadPath();
            Assert.GreaterOrEqual(path.Length, 3);
            Assert.AreEqual(StPetersBuilder.VillageGreen, path[0]);
            Assert.AreEqual(StPetersBuilder.SandbarFrom, path[path.Length - 1]);
        }

        [Test]
        public void BentPath_BendsStayWithinTheAmplitude_AndAreDeterministic()
        {
            Vector2 from = new Vector2(0f, 0f), to = new Vector2(100f, 0f);
            Vector2[] p1 = StPetersStarterSplat.BentPath(from, to, 3, 8f, 41);
            Vector2[] p2 = StPetersStarterSplat.BentPath(from, to, 3, 8f, 41);
            CollectionAssert.AreEqual(p1, p2, "hash-jittered bends must be deterministic (rule 5)");

            for (int i = 1; i < p1.Length - 1; i++)
            {
                float t = i / (float)(p1.Length - 1);
                Vector2 onLine = Vector2.Lerp(from, to, t);
                Assert.LessOrEqual(Vector2.Distance(p1[i], onLine), 8f + 1e-3f,
                    $"bend {i} strayed past the amplitude");
            }
        }

        [Test]
        public void SiltBlobs_HugTheChannelEdges_OnTheFlats()
        {
            Vector2 crossing = StPetersStarterSplat.ChannelCrossing();
            Assert.AreEqual(
                Vector2.Lerp(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo,
                             StPetersBuilder.ChannelAlong),
                crossing, "the crossing must be the terrain's own channel lerp");

            Vector2 barDir = (StPetersBuilder.SandbarTo - StPetersBuilder.SandbarFrom).normalized;
            Vector2 perp = new Vector2(-barDir.y, barDir.x);

            StPetersStarterSplat.Blob[] blobs = StPetersStarterSplat.SiltBlobs();
            Assert.AreEqual(6, blobs.Length, "three blobs per side of the channel");

            foreach (var blob in blobs)
            {
                float along = Vector2.Dot(blob.Center - crossing, barDir);
                float across = Vector2.Dot(blob.Center - crossing, perp);

                Assert.GreaterOrEqual(Mathf.Abs(along) - blob.Radius, StPetersBuilder.ChannelHalfWidth - 1e-3f,
                    "a silt blob reached into the boat channel — it must HUG the edge, not sit in the gut");
                Assert.LessOrEqual(Mathf.Abs(along), StPetersBuilder.ChannelHalfWidth + 20f,
                    "a silt blob drifted far from the channel it is supposed to flank");
                Assert.LessOrEqual(Mathf.Abs(across), StPetersBuilder.SandbarHalfWidth,
                    "a silt blob left the bar's flats");
                Assert.That(blob.Radius, Is.InRange(StPetersStarterSplat.SiltRadiusMin,
                                                    StPetersStarterSplat.SiltRadiusMax));
                Assert.That(blob.Intensity, Is.InRange(StPetersStarterSplat.SiltIntensityMin,
                                                       StPetersStarterSplat.SiltIntensityMax));
            }

            CollectionAssert.AreEqual(blobs, StPetersStarterSplat.SiltBlobs(),
                "the blob plan must be deterministic (rule 5)");
        }

        [Test]
        public void MarshPocket_SitsNorthWest_InTheUpperSandBand()
        {
            var go = new GameObject("TidalTerrain_StarterSplatTest");
            try
            {
                var terrain = go.AddComponent<TidalTerrain>();
                StPetersBuilder.ConfigureTidalTerrain(terrain);

                Vector2 pocket = StPetersStarterSplat.FindMarshPocket(terrain.ElevationAtZones);
                Assert.AreNotEqual(StPetersBuilder.IslandCenter, pocket, "no pocket found at all");
                Assert.Less(pocket.x, StPetersBuilder.IslandCenter.x, "the pocket must lie WEST of the centre");
                Assert.Greater(pocket.y, 0f, "the pocket must lie NORTH — the sheltered side");

                float elev = terrain.ElevationAtZones(pocket);
                Assert.LessOrEqual(elev, StPetersStarterSplat.MarshPocketElevation + 1e-3f,
                    "the pocket must sit at/below the marsh elevation (the first crossing)");
                Assert.GreaterOrEqual(elev, StPetersShoreMap.SandFloorElevation,
                    "the pocket fell below the sand floor — that is flats, not a marsh hollow");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SedgeFringe_RingsThePocketJustOutsideItsRim()
        {
            Vector2 centre = new Vector2(3f, 67f);
            Vector2[] ring = StPetersStarterSplat.SedgeFringe(centre);
            Assert.AreEqual(StPetersStarterSplat.SedgeFringeCount, ring.Length);
            foreach (Vector2 p in ring)
                Assert.AreEqual(StPetersStarterSplat.MarshRadiusMetres + 2f,
                                Vector2.Distance(p, centre), 1e-3f);
        }

        // ============================ THE LINEAR-IMPORT TRAP ============================

        [Test]
        public void ConfigureImporter_PinsLinearReadableUncompressed()
        {
            // Prove the importer the commit path applies actually lands the DATA settings — on a
            // throwaway PNG, so this guards the behaviour even before any splat is committed.
            const string probePath = "Assets/TempSplatImporterProbe.png";
            try
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
                File.WriteAllBytes(probePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(probePath, ImportAssetOptions.ForceSynchronousImport);

                TerrainSplatAssets.ConfigureImporter(probePath);

                var importer = (TextureImporter)AssetImporter.GetAtPath(probePath);
                Assert.IsNotNull(importer);
                Assert.IsFalse(importer.sRGBTexture,
                    "SPLAT MAPS MUST IMPORT LINEAR — sRGB would gamma-warp every painted weight");
                Assert.IsTrue(importer.isReadable, "the brush edits the pixels in place");
                Assert.IsFalse(importer.mipmapEnabled);
                Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
                Assert.AreEqual(FilterMode.Bilinear, importer.filterMode);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
            }
            finally
            {
                AssetDatabase.DeleteAsset(probePath);
            }
        }

        [Test]
        public void CommittedSplatMaps_ImportLinear()
        {
            // Pins the COMMITTED assets once the starter paint (or any brush commit) has produced
            // them. Absent maps are not a failure — the menu creates them on first run.
            bool anyExists = false;
            for (int i = 0; i < TerrainSplatBrush.TextureCount; i++)
            {
                string path = TerrainSplatAssets.PathOf(i);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                anyExists = true;
                Assert.IsFalse(importer.sRGBTexture,
                    $"'{path}' imports sRGB — the shader would read gamma-warped weights. Re-commit " +
                    "through the tool (TerrainSplatAssets.ConfigureImporter).");
                Assert.IsTrue(importer.isReadable, $"'{path}' must stay CPU-readable for the brush");
                Assert.IsFalse(importer.mipmapEnabled, $"'{path}' must not carry mips");
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression,
                    $"'{path}' must stay uncompressed — block compression mangles painted weights");
            }
            if (!anyExists)
                Assert.Ignore("No splat maps committed yet — run Hidden Harbours ▸ Tools ▸ " +
                              "Paint St Peters Starter Splat (or paint with the Material brush) first.");
        }
    }
}
