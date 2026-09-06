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
    /// SHORELINE CONVERGENCE (ADR 0012 recommendation 4): Coddle Cove and Nine Mile Creek now run the SAME
    /// tide-driven water model as St Peters — an analytic seabed (<see cref="RectTidalTerrain"/>) whose one
    /// height drives the water render, the on-foot walkability and the boat grounding (P1: what you see is
    /// what you can sail/walk). These tests drive the terrains from the BUILDERS' authored constants (the
    /// single source of truth each scene is built from — the StPetersTerrainTests convention) against the
    /// LIVE tide swing (the persistent core's St Peters profile, mean 0 ± 2.2 m), asserting:
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
        const float HighWater = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;   // +2.2
        const float LowWater  = StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude;   // -2.2

        const float DoryDraught = 0.3f;   // the start boat's draught (GreyboxBuilder authors it)

        static RectTidalTerrain MakeCove(out GameObject go)
        {
            go = new GameObject("CoveTerrain_Test");
            var t = go.AddComponent<RectTidalTerrain>();
            GreyboxBuilder.ConfigureCoveTerrain(t);
            return t;
        }

        static MainlandTidalTerrain MakeNineMileCreek(out GameObject go)
        {
            go = new GameObject("NineMileCreekTerrain_Test");
            var t = go.AddComponent<MainlandTidalTerrain>();
            NineMileCreekBuilder.ConfigureNineMileCreekTerrain(t);
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
                // The return-from-Nine Mile Creek arrival parks the boat here; it must float at dead low.
                float arrivalDepth = TidalExposure.WaterDepth(
                    LowWater, t.ElevationAt(GreyboxBuilder.CoveArrivalPos));
                Assert.Greater(arrivalDepth, DoryDraught,
                    "the boat parked at the cove arrival must still float at the lowest water");

                // The west passage band + the fishing spot keep water too.
                Assert.Greater(TidalExposure.WaterDepth(
                        LowWater, t.ElevationAt(GreyboxBuilder.ToNineMileCreekPassagePos)), DoryDraught,
                    "the Cove→Nine Mile Creek passage stays sailable at low water");
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
                // ⚠ The probe moved half a metre seaward (−7 → −7.5) with the 2026-08-01 amplitude
                // ruling. The cove's beach is STEEP here — 2.48 m at y = −7, −0.48 m at y = −8 — so the
                // intertidal band is a metre wide and a smaller swing moves it. At −7 the ground is
                // 2.48 m, which the old ±3.5 m swing covered and the new ±2.2 m one does not; at −7.5 it
                // is ~1.0 m, comfortably inside the new swing. The beach is as intertidal as it ever was;
                // the probe was simply standing on the part of it the tide no longer reaches.
                float e = t.ElevationAt(new Vector2(5f, -7.5f));
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
        //  NINE MILE CREEK
        // =====================================================================================

        [Test]
        public void NineMileCreek_TheQuayAndTheTown_AlwaysExposed_EvenAtHighWater()
        {
            var t = MakeNineMileCreek(out var go);
            try
            {
                // Everywhere the player stands and everywhere a vendor does — derived from the plan, so
                // moving a lot re-checks it rather than leaving a stale literal behind.
                var dry = new List<Vector2>
                {
                    (Vector2)NineMileCreekBuilder.DisembarkPos,   // the quay you step onto
                    (Vector2)NineMileCreekBuilder.FishBuyerPos,   // the till on the spit
                    (Vector2)NineMileCreekBuilder.DoryYardPos,    // the hard the dory is sold off
                    NineMileCreekWharf.DeckFootprint().center,
                    NineMileCreekWharf.ApronFootprint().center,
                    (Vector2)NineMileCreekMainland.WinchPos,
                    (Vector2)NineMileCreekMainland.UnloadApronPos,
                };
                dry.AddRange(NineMileCreekMainland.TownLots.Select(v => (Vector2)v));
                dry.AddRange(NineMileCreekMainland.ShantyRow.Select(v => (Vector2)v));

                foreach (var p in dry)
                {
                    float e = t.ElevationAt(p);
                    Assert.IsTrue(TidalExposure.IsExposed(HighWater, e),
                        $"Nine Mile Creek's ground at {p} stands at {e:0.00} m and must stay walkable at " +
                        $"the highest water ({HighWater:0.00} m)");
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void NineMileCreek_TheBerthIsGATED_NotDredged_TheShoalIsTheLaddersMiddleRung()
        {
            // ⚠ THIS TEST'S CLAIM HAS NOW BEEN INVERTED TWICE, and both turns were owner rulings.
            //
            // It first read "the DREDGED berth keeps a deep-harbour margin at low water — no tide-gating
            // here", because the region was standing in for Port Greywick with a flat −6 m floor. The
            // ruled ladder is three HARBOURS — St Peters' dock ~0.6 m, Nine Mile Creek ~1.6 m, Greywick
            // 6 m dredged — and this is the middle rung, so it became "the berth is a SHOAL, the shoal IS
            // the gate, and the fleet dries out under itself at spring low".
            //
            // ⭐⭐ 2026-09-04, the owner again: *"the bullpen should always have water at low tide so all
            // the lobster boats can park on the wall."* The berth trench (NineMileCreekMainland §8b¾) cuts
            // the berth line to the depth the fleet lies in. THE LADDER IS UNCHANGED, and that is the
            // point of what is asserted below instead: what admits a hull to this harbour was never the
            // berth but the FAIRWAY that leads to it, and the trench is cut for the same deepest resident
            // the fairway is. Nothing new gets in; the fleet that was already here simply stops sitting
            // on the mud. The drying that IS the region's teeth is asserted on the flats, in
            // NineMileCreekChannelTests and NineMileCreekWharfTests.
            var t = MakeNineMileCreek(out var go);
            try
            {
                float bed = t.ElevationAt(NineMileCreekBuilder.ArrivalPos);
                Assert.GreaterOrEqual(
                    NineMileCreekMainland.SpringLowWater - bed,
                    NineMileCreekMainland.BerthDepthNeededMetres - 1e-3f,
                    "the berth must hold the water the deepest resident needs to lie in it at dead low " +
                    "spring — the 2026-09-04 ruling");
                Assert.Greater(bed, NineMileCreekMainland.BayFloorElevation,
                    "…and it must not have been cut to the open bay's own floor: the trench is a berth " +
                    "pocket, not a dredged harbour");

                Assert.Greater(TidalExposure.WaterDepth(HighWater, bed), 1.3f,
                    "a lobster boat must float here on the flood, or the region's ceiling hull has no home");
                Assert.LessOrEqual(
                    TidalExposure.WaterDepth(LowWater, t.ElevationAt(new Vector2(120f, 56f))), 0f,
                    "…and the harbour FLATS must still bare at spring low. If nothing dries, this region " +
                    "has quietly become the dredged harbour it stopped being — the berths are cut now, " +
                    "the basin either side of the lane is not");

                Assert.AreEqual(NineMileCreekBuilder.NineMileCreekDeepElevation,
                    t.ElevationAt(NineMileCreekBuilder.ToCovePassagePos), 1e-3f,
                    "…while the open bay you sail home across is the deep floor, and never gates anyone");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void NineMileCreek_TheBarIsIntertidal_TheCrossingIsTheShorelineThatMoves()
        {
            // THE CONVERGENCE ASSERTION for this region. The old one probed a 3 m sand strip beside a
            // quay; the mainland's moving shoreline is the CROSSING — 305 m of bar that covers at high
            // water and bares at low, which is the whole reason the region exists.
            var t = MakeNineMileCreek(out var go);
            try
            {
                Vector2 midBar = Vector2.Lerp(NineMileCreekMainland.BarFrom, NineMileCreekMainland.BarTo, 0.7f);
                float e = t.ElevationAt(midBar);

                Assert.IsFalse(TidalExposure.IsExposed(HighWater, e),
                    $"at high water the bar at {midBar} ({e:0.00} m) is covered — you need a boat");
                Assert.IsTrue(TidalExposure.IsExposed(LowWater, e),
                    "at low water the same ground bares — you can walk to the island, if you watch the water");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void BothTerrains_AreDeterministic_NoRng()
        {
            var cove = MakeCove(out var coveGo);
            var gw = MakeNineMileCreek(out var gwGo);
            try
            {
                var p = new Vector2(63.1f, 86.4f);   // in the creek's basin, and in the cove's open water
                float c0 = cove.ElevationAt(p);
                float g0 = gw.ElevationAt(p);
                for (int i = 0; i < 8; i++)
                {
                    Assert.AreEqual(c0, cove.ElevationAt(p), 1e-6f, "cove: pure authored geometry");
                    Assert.AreEqual(g0, gw.ElevationAt(p), 1e-6f, "Nine Mile Creek: pure authored geometry");
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
