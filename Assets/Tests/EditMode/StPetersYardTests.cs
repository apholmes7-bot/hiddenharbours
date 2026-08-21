using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>ST PETERS' YARDS — the table, and the promise it makes to everything that was there first.</b>
    ///
    /// <para>The lawn ruling (2026-08-20) turns one authored polygon into three derived things: the mown
    /// ground, the wild grass that stops at its edge, and the fence that stands on it. These tests hold
    /// the four claims that make the arrangement safe to land before any of the art exists:</para>
    ///
    /// <list type="number">
    /// <item><b>⭐ NOTHING OUTSIDE A YARD MOVED.</b> The meadow is unchanged, site for site, everywhere
    /// except inside an authored polygon — asserted against a legacy predicate rebuilt from the same
    /// constants, so it cannot be satisfied by the new code agreeing with itself.</item>
    /// <item><b>Every yard holds its own building</b>, so a lawn reaches the walls of the house standing
    /// on it.</item>
    /// <item><b>No two yards claim the same ground</b>, and no yard swallows a neighbour's building —
    /// the village is tight enough that both are real risks and neither is visible from outside.</item>
    /// <item><b>Every yard belongs to a building that exists</b>, read from the island's own site list.
    /// </item>
    /// </list>
    /// </summary>
    public class StPetersYardTests
    {
        GameObject _go;
        TidalTerrain _terrain;

        [SetUp]
        public void SetUp()
        {
            StPetersYards.InvalidateCache();
            _go = new GameObject("TidalTerrain_Yards");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            StPetersYards.InvalidateCache();
        }

        // =============================================================================================
        //  1. the promise: nothing outside a yard moved
        // =============================================================================================

        /// <summary>
        /// The meadow gate as it was BEFORE yards existed, rebuilt from the very constants the live one
        /// reads. Not a copy of a remembered implementation: every clause below is the public constant
        /// the shipped predicate uses, so the two can only disagree about the yards.
        /// </summary>
        bool LegacyIsPlantableMeadow(Vector2 p)
        {
            if (_terrain.ElevationAt(p) < StPetersShoreMap.GrassFloorElevation) return false;

            // Per-site radius, not the global floor: the keepout went per site with the cannery (#599,
            // 9.06 m against a 7 m constant), and that radius is a published fact of the keepout list
            // that StPetersCanneryTests pins on its own. This predicate exists to catch a YARD leaking;
            // reading the same radius the live gate reads is what keeps the two disagreeing only about
            // the yards — the global constant here would re-assert a rule the island no longer has.
            for (int i = 0; i < StPetersGrass.BuildingKeepouts.Length; i++)
                if (Vector2.Distance(p, StPetersGrass.BuildingKeepouts[i].Position) <
                    StPetersGrass.BuildingKeepouts[i].RadiusMetres) return false;

            if (Vector2.Distance(p, StPetersBuilder.DockZonePos) < StPetersWoods.DockClearance)
                return false;
            if (StPetersShoreMap.DistanceToSegment(
                    p, StPetersBuilder.BerthFrom, StPetersBuilder.BerthTo) < StPetersWoods.DockClearance)
                return false;

            if (StPetersGrass.DistanceToWalkedPath(p) < StPetersGrass.PathBareHalfWidthMetres) return false;

            return true;
        }

        [Test]
        public void OutsideEveryYard_TheMeadowIsExactlyWhatItWas()
        {
            int sampled = 0, insideYards = 0, disagreed = 0;
            var firstDisagreement = Vector2.zero;

            // A grid over the whole island at a grain far finer than a yard, so a polygon that leaked
            // even a metre outside its own edge is caught.
            for (float x = StPetersBuilder.IslandCenter.x - StPetersBuilder.IslandRadius;
                 x <= StPetersBuilder.IslandCenter.x + StPetersBuilder.IslandRadius; x += 1.0f)
            for (float y = StPetersBuilder.IslandCenter.y - StPetersBuilder.IslandRadiusY;
                 y <= StPetersBuilder.IslandCenter.y + StPetersBuilder.IslandRadiusY; y += 1.0f)
            {
                var p = new Vector2(x, y);
                if (StPetersYards.IsInsideAYard(p)) { insideYards++; continue; }

                sampled++;
                if (StPetersGrass.IsPlantableMeadow(_terrain, p) == LegacyIsPlantableMeadow(p)) continue;
                if (disagreed == 0) firstDisagreement = p;
                disagreed++;
            }

            Assert.Greater(sampled, 10000, "the sweep has to actually cover the island.");
            Assert.Greater(insideYards, 0, "…and it has to actually reach the yards, or it proves nothing.");
            Assert.AreEqual(0, disagreed,
                $"the meadow changed at {disagreed} site(s) OUTSIDE every authored yard — first at " +
                $"{firstDisagreement}. Yards ADD to the clearance discs; they replace nothing.");

            Debug.Log($"[StPetersYardTests] {sampled:N0} sites outside the yards, all unchanged; " +
                      $"{insideYards:N0} sites now inside a mow line.");
        }

        [Test]
        public void InsideEveryYard_TheWildGrassIsGone()
        {
            foreach (Yard yard in StPetersYards.Yards)
            {
                Vector2 c = yard.Polygon.Centre;
                Assert.IsFalse(StPetersGrass.IsPlantableMeadow(_terrain, c),
                    $"'{yard.Name}': the middle of a mown lawn still grows wild tufts.");
            }
        }

        // =============================================================================================
        //  2. every yard holds its own building
        // =============================================================================================

        [Test]
        public void EveryYard_HoldsItsOwnBuildingsFootprint()
        {
            foreach (Yard yard in StPetersYards.Yards)
            {
                float margin = yard.Polygon.DistanceToEdge(yard.Owner) - yard.OwnerRadiusMetres;
                Assert.IsTrue(yard.Polygon.ContainsCircle(yard.Owner, yard.OwnerRadiusMetres),
                    $"'{yard.Name}' does not hold its own building: the mow line passes " +
                    $"{-margin:0.00} m INSIDE the walls, so the lawn would stop short of the house.");
                Debug.Log($"[StPetersYardTests] {yard.Name}: {yard.AreaMetresSquared:0} m2, " +
                          $"{margin:0.00} m of lawn past the walls, fence {yard.Fence}.");
            }
        }

        [Test]
        public void TheFootprintRadius_StaysBelowTheClearanceItIsNot()
        {
            Assert.Less(StPetersYards.DwellingFootprintRadiusMetres,
                        StPetersGrass.BuildingClearanceMetres,
                        "the yard holds a building's WALLS; the clearance disc holds its walls plus the " +
                        "margin that keeps the meadow off them. If a bigger shell ever raises the " +
                        "clearance, this is the number beside it that has to be looked at.");
        }

        [Test]
        public void TheCamperFootprint_AgreesWithItsSidecar()
        {
            float sidecar = StPetersCamperLot.FootprintRadiusMetres;
            if (sidecar <= 0f)
            {
                Assert.Inconclusive(
                    "the camper sidecar did not read, so the pinned footprint could not be checked " +
                    "against it. That is exactly why the yard's SHAPE takes the constant rather than " +
                    "this call — a zero here would otherwise reshape the lot in silence.");
                return;
            }
            Assert.AreEqual(sidecar, StPetersYards.CamperFootprintRadiusMetres, 0.05f,
                "the pinned camper footprint has drifted from the sidecar it was measured off.");
        }

        // =============================================================================================
        //  3. no yard fights its neighbours
        // =============================================================================================

        [Test]
        public void NoTwoYards_ClaimTheSameGround()
        {
            IReadOnlyList<Yard> yards = StPetersYards.Yards;
            for (int i = 0; i < yards.Count; i++)
                for (int j = i + 1; j < yards.Count; j++)
                    Assert.IsFalse(YardPlan.Overlap(yards[i], yards[j]),
                        $"'{yards[i].Name}' and '{yards[j].Name}' overlap — two lawns claiming one " +
                        "strip, and two fences a metre apart. Their owners stand " +
                        $"{Vector2.Distance(yards[i].Owner, yards[j].Owner):0.0} m apart, so one of " +
                        "them has to give ground.");
        }

        [Test]
        public void NoYard_SwallowsSomebodyElsesBuilding()
        {
            foreach (Yard yard in StPetersYards.Yards)
            foreach (Vector2 site in StPetersGrass.BuildingSites)
            {
                if (Vector2.Distance(site, yard.Owner) < 0.01f) continue;
                Assert.IsFalse(yard.Contains(site),
                    $"'{yard.Name}' has grown over the building at {site} — a yard that mows a " +
                    "neighbour's dooryard is a boundary nobody could have drawn.");
            }
        }

        [Test]
        public void GinnysSheds_StayOutsideHerMownYard()
        {
            Yard hers = StPetersYards.Require(StPetersYards.GinnyPlotYard);
            foreach (var shed in StPetersGinnyPlot.Sheds)
                Assert.IsFalse(hers.Contains(shed.Position),
                    $"her '{shed.Key}' has ended up inside the mown yard. It is meant to stand on the " +
                    "rough — a let-go property whose sheds sit on a lawn reads as tended, which is the " +
                    "opposite of what her plot says.");
        }

        // =============================================================================================
        //  4. the table is about real buildings
        // =============================================================================================

        [Test]
        public void EveryYard_BelongsToABuildingTheIslandActuallyHas()
        {
            foreach (Yard yard in StPetersYards.Yards)
            {
                bool found = false;
                foreach (Vector2 site in StPetersGrass.BuildingSites)
                    if (Vector2.Distance(site, yard.Owner) < 0.01f) { found = true; break; }

                Assert.IsTrue(found,
                    $"'{yard.Name}' is a yard for a building at {yard.Owner} that is not on the " +
                    "island's site list. Either the building moved and the yard did not follow, or the " +
                    "yard is for something that was never placed.");
            }
        }

        [Test]
        public void EveryYard_HasAUniqueName()
        {
            var seen = new HashSet<string>();
            foreach (Yard yard in StPetersYards.Yards)
                Assert.IsTrue(seen.Add(yard.Name), $"two yards are both called '{yard.Name}'.");
        }

        // =============================================================================================
        //  5. the fence line
        // =============================================================================================

        [Test]
        public void AFencedYard_ProducesARunWithItsGateCutOutOfIt()
        {
            foreach (Yard yard in StPetersYards.Yards)
            {
                List<Vector2[]> runs = yard.FenceRuns();

                if (yard.Fence == YardFence.None)
                {
                    Assert.AreEqual(0, runs.Count,
                        $"'{yard.Name}' is unfenced, so it must ask for no fence at all — the mow line " +
                        "carries the whole statement.");
                    continue;
                }

                Assert.Greater(runs.Count, 0, $"'{yard.Name}' is fenced but asks for no runs.");

                float built = 0f;
                foreach (var run in runs)
                    for (int i = 0; i + 1 < run.Length; i++) built += Vector2.Distance(run[i], run[i + 1]);

                float perimeter = yard.Polygon.Perimeter;
                float missing = perimeter - built;
                Assert.AreEqual(YardPlan.GateWidthMetres * yard.Gates.Length, missing, 0.35f,
                    $"'{yard.Name}': the fence is {missing:0.00} m short of its boundary, which should " +
                    $"be exactly its {yard.Gates.Length} gate(s). A gate you can see and cannot walk " +
                    "through is the failure this measures.");
            }
        }

        [Test]
        public void EveryGate_SitsOnItsOwnBoundary()
        {
            foreach (Yard yard in StPetersYards.Yards)
            foreach (Vector2 gate in yard.Gates)
                Assert.Less(yard.Polygon.DistanceToEdge(gate), 0.01f,
                    $"'{yard.Name}' has a gate at {gate} that is not on its fence line.");
        }

        [Test]
        public void EveryGate_FacesTheWayYouArriveFrom()
        {
            Vector2 green = StPetersBuilder.VillageGreen;
            foreach (Yard yard in StPetersYards.Yards)
            {
                if (yard.Name == StPetersYards.CamperLotYard) continue;   // faces the cottage, not the green

                foreach (Vector2 gate in yard.Gates)
                    Assert.Less(Vector2.Distance(gate, green), Vector2.Distance(yard.Owner, green),
                        $"'{yard.Name}' opens on the far side from the green — you would walk round " +
                        "the house to get in the gate.");
            }
        }
    }
}
