using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The BUILDER's wiring of Nine Mile Creek's painted ground (ADR 0028).
    ///
    /// <para>⭐ <b>Scene-wired is not builder-wired.</b> A splat surface dragged into the scene and tuned
    /// by hand survives exactly until the next time somebody runs the Build menu. Everything the owner's
    /// one Build click has to reproduce is pushed from <see cref="NineMileCreekBuilder"/>, and this file
    /// asserts it arrives — because a band floor left at the component's serialized default draws a coast
    /// metres out of position with nothing erroring, and the failure looks like an art bug.</para>
    ///
    /// <para>The asset-I/O half of <c>BuildSplatGround</c> (minting a height map, packing the detail
    /// arrays, loading five PNGs) is deliberately NOT run here — a test that writes to the project leaves
    /// churn in everyone's working tree. What is pinned instead is the PATHS it resolves, which is where
    /// that half actually goes wrong: two spellings of the same filename wire null in silence.</para>
    /// </summary>
    public class NineMileCreekSplatGroundTests
    {
        // ============================ THE NUMBERS THE BUILDER PUSHES ============================

        private static SerializedObject Configured(out GameObject go)
        {
            go = new GameObject("NMCSplatWiringTest");
            var splat = go.AddComponent<TerrainSplatSurface>();
            NineMileCreekBuilder.ConfigureSplatSurface(splat);
            return new SerializedObject(splat);
        }

        [Test]
        public void TheBuilderPushesTheBandLadder_NotTheComponentDefaults()
        {
            var so = Configured(out GameObject go);
            try
            {
                Assert.AreEqual(NineMileCreekShoreMap.PaintFloorElevation,
                    so.FindProperty("_floorPaint").floatValue, 1e-5f);
                Assert.AreEqual(NineMileCreekShoreMap.RippleFloorElevation,
                    so.FindProperty("_floorRipple").floatValue, 1e-5f);
                Assert.AreEqual(NineMileCreekShoreMap.SandFloorElevation,
                    so.FindProperty("_floorSand").floatValue, 1e-5f);
                Assert.AreEqual(NineMileCreekShoreMap.MarramFloorElevation,
                    so.FindProperty("_floorMarram").floatValue, 1e-5f);
                Assert.AreEqual(NineMileCreekShoreMap.GrassFloorElevation,
                    so.FindProperty("_floorGrass").floatValue, 1e-5f);
                Assert.AreEqual(NineMileCreekShoreMap.ShingleFloorElevation,
                    so.FindProperty("_floorShingle").floatValue, 1e-5f);

                Assert.AreEqual(NineMileCreekShoreMap.BandWiggleMetres,
                    so.FindProperty("_bandWiggleMetres").floatValue, 1e-5f);
                Assert.AreEqual(NineMileCreekShoreMap.BandWiggleScale,
                    so.FindProperty("_bandWiggleScale").floatValue, 1e-5f);
                Assert.AreEqual(NineMileCreekShoreMap.BandDetailMetres,
                    so.FindProperty("_bandDetailMetres").floatValue, 1e-5f);
                Assert.AreEqual(NineMileCreekShoreMap.BandDetailScale,
                    so.FindProperty("_bandDetailScale").floatValue, 1e-5f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheBuilderNeutralisesTheWeatherSector()
        {
            // ⭐ The shader carries ONE island-shaped weather/sheltered split and cannot read a coast run.
            // Feeding it a bearing would be wrong somewhere along 596 m of coastline, so the whole region
            // is pushed onto the sheltered ladder and this coast's rock arrives as PAINT instead.
            var so = Configured(out GameObject go);
            try
            {
                Vector2 anchor = so.FindProperty("_islandCenter").vector2Value;
                Assert.AreEqual(NineMileCreekShoreMap.SectorAnchor.x, anchor.x, 1e-3f);
                Assert.AreEqual(NineMileCreekShoreMap.SectorAnchor.y, anchor.y, 1e-3f);

                Vector2 facing = so.FindProperty("_weatherFacing").vector2Value;
                Assert.AreEqual(NineMileCreekShoreMap.SectorFacing.x, facing.x, 1e-5f);
                Assert.AreEqual(NineMileCreekShoreMap.SectorFacing.y, facing.y, 1e-5f);

                Assert.AreEqual(NineMileCreekShoreMap.SectorAspect,
                    so.FindProperty("_islandAspect").floatValue, 1e-5f,
                    "a mainland is not an ellipse — there is no aspect to normalise");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheBuilderPushesThisRegionsHalfOfTheCrossing_WithTheSharedSpine()
        {
            var so = Configured(out GameObject go);
            try
            {
                // The GEOMETRY is this region's own…
                Assert.AreEqual(NineMileCreekMainland.BarFrom, so.FindProperty("_barFrom").vector2Value);
                Assert.AreEqual(NineMileCreekMainland.BarTo, so.FindProperty("_barTo").vector2Value);
                Assert.AreEqual(NineMileCreekMainland.BarHalfWidth,
                    so.FindProperty("_barHalfWidth").floatValue, 1e-5f);

                // …and the SPINE is the island's, because it is one bar spanning the seam and
                // NineMileCreekMainland §6 keeps the two halves mirror images on purpose. A crossing whose
                // walking line changed width halfway would be the crossing telling a lie about itself.
                Assert.AreEqual(StPetersShoreMap.BarSpineHalfWidth,
                    so.FindProperty("_barSpineHalfWidth").floatValue, 1e-5f);
                Assert.AreEqual(StPetersShoreMap.BarSpineFloorElevation,
                    so.FindProperty("_barSpineFloor").floatValue, 1e-5f);

                Assert.Greater(so.FindProperty("_barHalfWidth").floatValue, 0f,
                    "a zero half-width switches the bar off in the shader — the cobble line disappears");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ============================ THE QUAD'S EXTENT ============================

        [Test]
        public void TheGroundQuadCoversTheWholeRegion_WhichNeedsConfigureBeforeTheFirstEnable()
        {
            // ⭐⭐ THE BUG THE FIRST GPU CAPTURE OF THIS PASS FOUND, in executable form.
            //
            // TerrainSplatSurface is [ExecuteAlways], so AddComponent fires OnEnable IMMEDIATELY and the
            // quad's mesh is built from whatever extent the component is carrying AT THAT INSTANT — on a
            // fresh component, its serialized default. Configure() afterwards updates the fields, but
            // EnsureBuilt() early-returns once the mesh exists. The result is a default-sized quad in the
            // middle of a 760 × 560 m region: a small rectangle of painted ground with black around it.
            //
            // St Peters survives the same ordering only because its builder SAVES THE SCENE — on the next
            // load OnEnable runs again with the correct serialized extent. That makes the wiring depend on
            // a save/reload round-trip nobody wrote down, and it is invisible until something looks at the
            // scene without reloading it.

            // (a) The naive order really is broken — otherwise the guard below proves nothing.
            var naiveGo = new GameObject("NMCSplatNaiveOrder");
            try
            {
                var naive = naiveGo.AddComponent<TerrainSplatSurface>();          // OnEnable builds here…
                naive.Configure(NineMileCreekMainland.RegionWorldCenter,          // …and this is too late
                                NineMileCreekMainland.RegionWorldSize, null,
                                TerrainSplatSurface.DefaultSortingOrder);
                Assert.That(QuadWidth(naiveGo),
                    Is.Not.EqualTo(NineMileCreekMainland.RegionWorldSize.x).Within(0.5f),
                    "AddComponent-then-Configure now yields the right extent — the trap this test " +
                    "documents has been fixed upstream, so the builder's SetActive dance can go too");
            }
            finally { Object.DestroyImmediate(naiveGo); }

            // (b) The builder's order — inactive, configure, activate — gets the region's real extent.
            var go = new GameObject("NMCSplatBuilderOrder");
            try
            {
                go.SetActive(false);
                var splat = go.AddComponent<TerrainSplatSurface>();
                splat.Configure(NineMileCreekMainland.RegionWorldCenter,
                                NineMileCreekMainland.RegionWorldSize, null,
                                TerrainSplatSurface.DefaultSortingOrder);
                NineMileCreekBuilder.ConfigureSplatSurface(splat);
                go.SetActive(true);

                Assert.AreEqual(NineMileCreekMainland.RegionWorldSize.x, QuadWidth(go), 0.5f,
                    "the ground quad is not the region's width — configure the component BEFORE its " +
                    "first OnEnable (create the GameObject inactive)");
                Assert.AreEqual(NineMileCreekMainland.RegionWorldSize.y, QuadHeight(go), 0.5f,
                    "the ground quad is not the region's height");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>The generated quad child's world-metre width (it is built with HideFlags.DontSave, so
        /// it is a child MeshFilter rather than anything the scene serializes).</summary>
        private static float QuadWidth(GameObject host)
        {
            var mf = host.GetComponentInChildren<MeshFilter>(true);
            return mf == null || mf.sharedMesh == null ? -1f : mf.sharedMesh.bounds.size.x;
        }

        /// <inheritdoc cref="QuadWidth"/>
        private static float QuadHeight(GameObject host)
        {
            var mf = host.GetComponentInChildren<MeshFilter>(true);
            return mf == null || mf.sharedMesh == null ? -1f : mf.sharedMesh.bounds.size.y;
        }

        // ============================ THE PATHS ============================

        [Test]
        public void TheGroundSortsBelowTheSeaPlane()
        {
            // The whole ADR 0012 tide reveal depends on this: the sea clips itself transparent over dry
            // ground, so the painting has to be UNDER it. −5 is the Sea plane's order in this scene.
            Assert.Less(TerrainSplatSurface.DefaultSortingOrder, -5,
                "the splat ground must sort below the Sea plane or the tide reveal inverts");
        }

        [Test]
        public void TheSeabedTheBuilderLoadsIsTheOneTheBakeWrites()
        {
            // ⚠ Two spellings of one filename wire NULL in silence, and a null height map makes the quad
            // clip itself empty everywhere — which looks exactly like "the region is not painted".
            string expected = "Assets/_Project/Data/Terrain/" +
                              TerrainPaintTool.NineMileCreekSeabedName + ".asset";
            var map = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(expected);
            Assert.IsNotNull(map,
                $"no committed height map at {expected} — the splat quad has nothing to clip against and " +
                "Nine Mile Creek's ground renders empty. Run 'TerrainPaintTool." +
                "RebakeNineMileCreekSeabedFromCommandLine' (or rebuild the region).");

            Assert.IsNotNull(map.HeightTexture, "the height map carries no texture");
            Assert.AreEqual(TerrainPaintTool.NineMileCreekMinElevation, map.MinElevation, 1e-3f,
                "the map's encode range no longer matches the region's bay floor — every elevation in it " +
                "decodes wrong");
            Assert.AreEqual(TerrainPaintTool.NineMileCreekMaxElevation, map.MaxElevation, 1e-3f);
        }

        [Test]
        public void TheSeabedMapCoversTheRegionAtItsPublishedGrid()
        {
            var region = NineMileCreekStarterSplat.NineMileCreekRegion();
            Assert.IsNotNull(region, "region.nine_mile_creek not found");

            var map = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(
                "Assets/_Project/Data/Terrain/" + TerrainPaintTool.NineMileCreekSeabedName + ".asset");
            if (map == null || map.HeightTexture == null)
                Assert.Ignore("no committed height map yet — TheSeabedTheBuilderLoadsIsTheOneTheBakeWrites " +
                              "is the test that reports that.");

            // ⚠ A texture importer's size cap silently DOWNSCALES anything over its Max Size, which would
            // leave every world→texel mapping in the map a lie with the bake reporting success.
            Assert.AreEqual(region.SeabedTexels.x, map.HeightTexture.width,
                "the committed height texture is not the width the region publishes — the importer's " +
                "size cap has rescaled it, or the region's extent changed without a re-bake");
            Assert.AreEqual(region.SeabedTexels.y, map.HeightTexture.height);
        }

        [Test]
        public void TheSplatMapsTheBuilderLoadsAreThisRegionsStem()
        {
            // ⭐ #522's stem overloads exist for exactly this: the default resolves to St Peters, so a
            // builder that forgot its own stem would wire the ISLAND'S maps onto the mainland's quad —
            // five textures on the wrong world frame, at the wrong resolution, and no error anywhere.
            for (int i = 0; i < TerrainSplatBrush.TextureCount; i++)
            {
                string path = TerrainSplatAssets.PathOf(i, NineMileCreekStarterSplat.SplatBaseName);
                StringAssert.Contains("NineMileCreekSplat", path);
                Assert.AreNotEqual(TerrainSplatAssets.PathOf(i), path,
                    "the mainland is resolving the island's splat path");
            }
        }

        [Test]
        public void TheCommittedSplatMapsMatchTheHeightMapsGrid()
        {
            // A splat map addresses the SAME world rect as the height map at the same texel grid — the
            // shader samples both with one UV. A mismatch slides the whole painting against the ground.
            var map = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(
                "Assets/_Project/Data/Terrain/" + TerrainPaintTool.NineMileCreekSeabedName + ".asset");
            var splatA = AssetDatabase.LoadAssetAtPath<Texture2D>(
                TerrainSplatAssets.PathOf(0, NineMileCreekStarterSplat.SplatBaseName));
            if (map == null || map.HeightTexture == null || splatA == null)
                Assert.Ignore("height map or splat maps not committed yet.");

            Assert.AreEqual(map.HeightTexture.width, splatA.width,
                "the splat maps and the height map are on different grids — the painting slides against " +
                "the ground it is meant to describe");
            Assert.AreEqual(map.HeightTexture.height, splatA.height);
        }
    }
}
