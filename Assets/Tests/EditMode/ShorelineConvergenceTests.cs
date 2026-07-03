#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// SHORELINE CONVERGENCE (ADR 0012 recommendation 4): Coddle Cove and Port Greywick now run the SAME
    /// tide-driven water model as St Peters — an analytic seabed (<see cref="RectTidalTerrain"/>) whose one
    /// height drives the water render, the on-foot walkability and the boat grounding (P1: what you see is
    /// what you can sail/walk). These tests drive the terrains from the BUILDERS' authored constants (the
    /// single source of truth each scene is built from — the StPetersTerrainTests convention) against the
    /// LIVE tide swing (the persistent core's St Peters profile, mean 0 ± 3.5 m), asserting:
    /// the land/planks the player and vendors use stay EXPOSED at the highest water, the water the boat
    /// parks in stays FLOATABLE at the lowest, and each region has a genuinely INTERTIDAL band — the
    /// converged, visibly moving shoreline. Plus the cove logic-tree wiring (terrain + WaterSurface Sea
    /// under the --LOGIC-- root, terrain enabling BEFORE the sea).
    /// </summary>
    public class ShorelineConvergenceTests
    {
        // The LIVE tide both regions run under (the persistent core's, authored by the START scene =
        // St Peters; PersistentCoreBuilder: "nothing re-points it on a region hop yet"). Assert at the
        // EXTREMES of the swing — the strictest case.
        const float HighWater = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;   // +3.5
        const float LowWater  = StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude;   // -3.5

        const float DoryDraught = 0.3f;   // the start boat's draught (GreyboxBuilder authors it)

        static RectTidalTerrain MakeCove(out GameObject go)
        {
            go = new GameObject("CoveTerrain_Test");
            var t = go.AddComponent<RectTidalTerrain>();
            GreyboxBuilder.ConfigureCoveTerrain(t);
            return t;
        }

        static RectTidalTerrain MakeGreywick(out GameObject go)
        {
            go = new GameObject("GreywickTerrain_Test");
            var t = go.AddComponent<RectTidalTerrain>();
            GreywickBuilder.ConfigureGreywickTerrain(t);
            return t;
        }

        [TearDown]
        public void TearDown() => GameServices.Reset();

        // =====================================================================================
        //  CODDLE COVE
        // =====================================================================================

        [Test]
        public void Cove_LandAndDockPlanks_AlwaysExposed_EvenAtHighWater()
        {
            var t = MakeCove(out var go);
            try
            {
                // The fence-interior land the player roams + the dock planks (disembark) + the dock head.
                foreach (var p in new[]
                {
                    new Vector2(0f, 0f), new Vector2(-9f, 8f), new Vector2(9f, -4.5f),   // fence interior
                    (Vector2)GreyboxBuilder.CoveDisembarkPos,                             // the planks
                    (Vector2)GreyboxBuilder.CoveDockZonePos,                              // the dock head
                })
                {
                    float e = t.ElevationAt(p);
                    Assert.IsTrue(TidalExposure.IsExposed(HighWater, e),
                        $"the cove ground/planks at {p} must stay walkable at the highest water " +
                        "(the on-foot tide gate is LIVE here now that a terrain registers)");
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Cove_ArrivalAndFishingWater_StillFloat_AtLowWater()
        {
            var t = MakeCove(out var go);
            try
            {
                // The return-from-Greywick arrival parks the boat here; it must float at dead low.
                float arrivalDepth = TidalExposure.WaterDepth(
                    LowWater, t.ElevationAt(GreyboxBuilder.CoveArrivalPos));
                Assert.Greater(arrivalDepth, DoryDraught,
                    "the boat parked at the cove arrival must still float at the lowest water");

                // The west passage band + the fishing spot keep water too.
                Assert.Greater(TidalExposure.WaterDepth(
                        LowWater, t.ElevationAt(GreyboxBuilder.ToGreywickPassagePos)), DoryDraught,
                    "the Cove→Greywick passage stays sailable at low water");
                Assert.Greater(TidalExposure.WaterDepth(LowWater, t.ElevationAt(new Vector2(5f, -10f))), 0f,
                    "the fishing spot beside the dock keeps water at dead low");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Cove_SouthBeach_IsIntertidal_TheShorelineVisiblyMoves()
        {
            // THE CONVERGENCE ASSERTION: the cove now has ground that covers at high water and bares at
            // low — a shoreline that advances/retreats with the tide (P1), where the old cove had a fixed
            // no-tide edge. A beach point south of the fence line, clear of the dock spur.
            var t = MakeCove(out var go);
            try
            {
                float e = t.ElevationAt(new Vector2(5f, -7f));
                Assert.IsFalse(TidalExposure.IsExposed(HighWater, e),
                    "at high water the south beach is covered — the water reaches toward the fence");
                Assert.IsTrue(TidalExposure.IsExposed(LowWater, e),
                    "at low water the same beach bares — the waterline has visibly retreated");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Cove_DeepFloor_NeverBares()
        {
            var t = MakeCove(out var go);
            try
            {
                float e = t.ElevationAt(new Vector2(0f, -20f));   // open water, south of everything
                Assert.IsFalse(TidalExposure.IsExposed(LowWater, e),
                    "the cove's open-water floor never bares (it is below the lowest water)");
                Assert.AreEqual(GreyboxBuilder.CoveDeepElevation, e, 1e-4f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // =====================================================================================
        //  PORT GREYWICK
        // =====================================================================================

        [Test]
        public void Greywick_TownLandAndWharfDeck_AlwaysExposed_EvenAtHighWater()
        {
            var t = MakeGreywick(out var go);
            try
            {
                // The vendor buildings, the quay the player roams, the wharf deck (disembark) + head.
                foreach (var p in new[]
                {
                    new Vector2(-8f, 3f),  new Vector2(-8f, -3f),   // shipwright / fish stall
                    new Vector2(-12f, 9f), new Vector2(-12f, -9f),  // harbourmaster / general store
                    (Vector2)GreywickBuilder.DisembarkPos,          // the wharf deck planks
                    (Vector2)GreywickBuilder.DockZonePos,           // the wharf head
                })
                {
                    float e = t.ElevationAt(p);
                    Assert.IsTrue(TidalExposure.IsExposed(HighWater, e),
                        $"Greywick's land/deck at {p} must stay walkable at the highest water");
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Greywick_DredgedHarbour_StaysDeep_AtLowWater()
        {
            var t = MakeGreywick(out var go);
            try
            {
                // Canon: Greywick is the DEEP, DREDGED harbour (IsDeepHarbour). The arrival berth and the
                // return passage carry a real under-keel margin even at dead low — no tide-gating here.
                float arrivalDepth = TidalExposure.WaterDepth(
                    LowWater, t.ElevationAt(GreywickBuilder.ArrivalPos));
                Assert.Greater(arrivalDepth, 2f,
                    "the dredged berth off the wharf head keeps a deep-harbour margin at low water");
                Assert.Greater(TidalExposure.WaterDepth(
                        LowWater, t.ElevationAt(GreywickBuilder.ToCovePassagePos)), 2f,
                    "the return passage east stays deep");
                Assert.AreEqual(GreywickBuilder.GreywickDeepElevation,
                    t.ElevationAt(new Vector2(30f, 0f)), 1e-4f, "open harbour = the dredged floor");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Greywick_QuayEdge_IsIntertidal_TheTideReadsAgainstTheQuay()
        {
            // The converged tide in Greywick: a MODEST intertidal band on the steep quay edge (the sand
            // strip east of the fence line) — the water visibly rises/falls against the quay, while the
            // dredged harbour itself never gates a boat. A point on the quay-edge falloff, clear of the wharf.
            var t = MakeGreywick(out var go);
            try
            {
                float e = t.ElevationAt(new Vector2(-2.5f, 8f));
                Assert.IsFalse(TidalExposure.IsExposed(HighWater, e),
                    "at high water the quay-edge sand is covered");
                Assert.IsTrue(TidalExposure.IsExposed(LowWater, e),
                    "at low water the same sand bares — the waterline visibly falls against the quay");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void BothTerrains_AreDeterministic_NoRng()
        {
            var cove = MakeCove(out var coveGo);
            var gw = MakeGreywick(out var gwGo);
            try
            {
                var p = new Vector2(3.1f, -6.4f);
                float c0 = cove.ElevationAt(p);
                float g0 = gw.ElevationAt(p);
                for (int i = 0; i < 8; i++)
                {
                    Assert.AreEqual(c0, cove.ElevationAt(p), 1e-6f, "cove: pure authored geometry");
                    Assert.AreEqual(g0, gw.ElevationAt(p), 1e-6f, "Greywick: pure authored geometry");
                }
            }
            finally
            {
                Object.DestroyImmediate(coveGo);
                Object.DestroyImmediate(gwGo);
            }
        }

        // =====================================================================================
        //  THE COVE LOGIC TREE — the converged water is actually WIRED into the scene
        // =====================================================================================

        public class CoveLogicTreeWiring
        {
            Scene _scene;
            GreyboxBuilder.DataRefs _data;
            readonly HashSet<GameObject> _preExisting = new();

            [SetUp]
            public void SetUp()
            {
                // The CoveLogicRefreshTests convention: operate on the active scene, remember what was
                // already there so TearDown removes only what this test introduced.
                _scene = EditorSceneManager.GetActiveScene();
                _preExisting.Clear();
                foreach (var go in _scene.GetRootGameObjects())
                    if (go != null) _preExisting.Add(go);
                _data = GreyboxBuilder.PrepareData();
            }

            [TearDown]
            public void TearDown()
            {
                if (_scene.IsValid())
                    foreach (var go in _scene.GetRootGameObjects().ToArray())
                        if (go != null && !_preExisting.Contains(go))
                            Object.DestroyImmediate(go);
                GameServices.Reset();
            }

            GameObject TheLogicRoot() =>
                _scene.GetRootGameObjects().First(go => go.GetComponent<RegionLogicRoot>() != null);

            [Test]
            public void RebuildLogicSubtree_WiresTheConvergedWaterModel()
            {
                GreyboxBuilder.RebuildLogicSubtree(_scene, _data);
                var root = TheLogicRoot();

                // The one-height source is in the tree and authored to the cove constants.
                var terrain = root.GetComponentInChildren<RectTidalTerrain>();
                Assert.IsNotNull(terrain, "the cove logic tree must carry the RectTidalTerrain height source");
                Assert.AreEqual(GreyboxBuilder.CoveLandElevation,
                    terrain.ElevationAt(Vector2.zero), 1e-4f, "authored land plateau");
                Assert.AreEqual(GreyboxBuilder.CoveDeepElevation,
                    terrain.ElevationAt(new Vector2(0f, -20f)), 1e-4f, "authored deep floor");

                // The Sea carries the WaterSurface (the shader bridge) over the authored bake rect.
                var surface = root.GetComponentInChildren<HiddenHarbours.Art.WaterSurface>();
                Assert.IsNotNull(surface, "the cove Sea must carry a WaterSurface (the tide-driven shader bridge)");
                var so = new UnityEditor.SerializedObject(surface);
                Assert.AreEqual(GreyboxBuilder.CoveSeaCenter, so.FindProperty("_heightWorldCenter").vector2Value,
                    "the height bake covers the cove's water rectangle");
                Assert.AreEqual(GreyboxBuilder.CoveSeaSize, so.FindProperty("_heightWorldSize").vector2Value);
                Assert.AreEqual(GreyboxBuilder.CoveHeightResolution,
                    so.FindProperty("_heightResolution").intValue, "the ADR 0012 smoothed-shore bake resolution");
                Assert.AreEqual(GreyboxBuilder.CoveHeightMin, so.FindProperty("_heightMin").floatValue, 1e-4f);
                Assert.AreEqual(GreyboxBuilder.CoveHeightMax, so.FindProperty("_heightMax").floatValue, 1e-4f);

                // Sorting: above the owner's painted ground (-20), below decor/buildings/player.
                var seaSr = surface.GetComponent<SpriteRenderer>();
                Assert.IsNotNull(seaSr);
                Assert.Greater(seaSr.sortingOrder, -20,
                    "the Sea must render ABOVE the painted ground tilemaps so flooded ground is covered");
                Assert.Less(seaSr.sortingOrder, 0, "and below decor/buildings/the player");

                // Enable order: the terrain child precedes the Sea child, so on a region toggle-on the
                // terrain's OnEnable registers into GameServices BEFORE the WaterSurface's OnEnable bakes.
                int terrainIndex = terrain.transform.GetSiblingIndex();
                int seaIndex = surface.transform.GetSiblingIndex();
                Assert.Less(terrainIndex, seaIndex,
                    "TidalTerrain must be created before the Sea (terrain registers before the water bakes)");
            }
        }
    }
}
#endif
