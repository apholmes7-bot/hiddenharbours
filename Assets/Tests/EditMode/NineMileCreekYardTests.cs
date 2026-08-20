using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>NINE MILE CREEK'S YARDS.</b> The same four claims <see cref="StPetersYardTests"/> holds for the
    /// island, on the mainland's nine town lots — plus the one this region adds: every yard faces the
    /// road, and it faces it through the SAME call the crushed-shell town walks are derived through, so a
    /// front boundary and the walk up to the door can never disagree about which way a house is turned.
    /// </summary>
    public class NineMileCreekYardTests
    {
        GameObject _go;
        MainlandTidalTerrain _terrain;

        [SetUp]
        public void SetUp()
        {
            NineMileCreekYards.InvalidateCache();
            NineMileCreekFields.InvalidateRoadCache();
            _go = new GameObject("MainlandTidalTerrain_Yards");
            _terrain = _go.AddComponent<MainlandTidalTerrain>();
            NineMileCreekMainland.ConfigureTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            NineMileCreekYards.InvalidateCache();
            NineMileCreekFields.InvalidateRoadCache();
        }

        // =============================================================================================
        //  1. the promise: nothing outside a yard moved
        // =============================================================================================

        /// <summary>The meadow gate as it was before yards existed, rebuilt from the same public
        /// constants the shipped one reads — see <see cref="StPetersYardTests"/> for why it is written
        /// out rather than remembered.</summary>
        bool LegacyIsGrassGround(Vector2 p)
        {
            if (!NineMileCreekFields.IsFieldGround(_terrain, p, NineMileCreekFields.GrassFloorElevation))
                return false;
            if (NineMileCreekFields.OnATownLot(p, 0f)) return false;
            if (NineMileCreekFields.OnAnyCarriageway(p, 0f)) return false;
            if (NineMileCreekFields.OnAnyPad(p, 0f)) return false;
            return true;
        }

        [Test]
        public void OutsideEveryYard_TheMeadowIsExactlyWhatItWas()
        {
            // The town and a wide margin round it — every yard in the table plus 40 m of field on all
            // sides. Sweeping the whole region would cost a coast walk per sample for ground no yard is
            // within two hundred metres of.
            const float MinX = -250f, MaxX = -80f, MinY = 20f, MaxY = 240f;

            int sampled = 0, insideYards = 0, disagreed = 0;
            var firstDisagreement = Vector2.zero;

            for (float x = MinX; x <= MaxX; x += 1.0f)
            for (float y = MinY; y <= MaxY; y += 1.0f)
            {
                var p = new Vector2(x, y);
                if (NineMileCreekYards.IsInsideAYard(p)) { insideYards++; continue; }

                sampled++;
                if (NineMileCreekFields.IsGrassGround(_terrain, p) == LegacyIsGrassGround(p)) continue;
                if (disagreed == 0) firstDisagreement = p;
                disagreed++;
            }

            Assert.Greater(sampled, 10000, "the sweep has to actually cover the town.");
            Assert.Greater(insideYards, 0, "…and reach the yards, or it proves nothing.");
            Assert.AreEqual(0, disagreed,
                $"the meadow changed at {disagreed} site(s) OUTSIDE every authored yard — first at " +
                $"{firstDisagreement}. Yards ADD to the town-lot discs; they replace nothing.");

            Debug.Log($"[NineMileCreekYardTests] {sampled:N0} sites outside the yards, all unchanged; " +
                      $"{insideYards:N0} sites now inside a mow line.");
        }

        [Test]
        public void InsideEveryYard_NeitherGrassNorHedgeIsPlanted()
        {
            foreach (Yard yard in NineMileCreekYards.Yards)
            {
                Vector2 c = yard.Polygon.Centre;
                Assert.IsFalse(NineMileCreekFields.IsGrassGround(_terrain, c),
                    $"'{yard.Name}': the middle of a mown lawn still grows wild tufts.");
                Assert.IsFalse(NineMileCreekFields.IsWoodyGround(_terrain, c),
                    $"'{yard.Name}': a hedgerow is being planted through somebody's dooryard.");
            }
        }

        // =============================================================================================
        //  2 & 3. the yards hold their buildings and do not fight each other
        // =============================================================================================

        [Test]
        public void EveryYard_HoldsItsOwnLotsReservedGround()
        {
            foreach (Yard yard in NineMileCreekYards.Yards)
            {
                float margin = yard.Polygon.DistanceToEdge(yard.Owner) - yard.OwnerRadiusMetres;
                Assert.IsTrue(yard.Polygon.ContainsCircle(yard.Owner, yard.OwnerRadiusMetres),
                    $"'{yard.Name}' does not hold the ground its lot reserves: the mow line passes " +
                    $"{-margin:0.00} m inside it.");
                Debug.Log($"[NineMileCreekYardTests] {yard.Name}: {yard.AreaMetresSquared:0} m2, " +
                          $"{margin:0.00} m past the lot, fence {yard.Fence}.");
            }
        }

        [Test]
        public void NoTwoYards_ClaimTheSameGround()
        {
            IReadOnlyList<Yard> yards = NineMileCreekYards.Yards;
            for (int i = 0; i < yards.Count; i++)
                for (int j = i + 1; j < yards.Count; j++)
                    Assert.IsFalse(YardPlan.Overlap(yards[i], yards[j]),
                        $"'{yards[i].Name}' and '{yards[j].Name}' overlap; their lots stand " +
                        $"{Vector2.Distance(yards[i].Owner, yards[j].Owner):0.0} m apart.");
        }

        [Test]
        public void NoYard_SwallowsAnotherTownLot()
        {
            foreach (Yard yard in NineMileCreekYards.Yards)
            foreach (Vector3 lot in NineMileCreekMainland.TownLots)
            {
                var site = new Vector2(lot.x, lot.y);
                if (Vector2.Distance(site, yard.Owner) < 0.01f) continue;
                Assert.IsFalse(yard.Contains(site),
                    $"'{yard.Name}' has grown over the lot at {site}.");
            }
        }

        [Test]
        public void EveryTownLot_HasExactlyOneYard()
        {
            IReadOnlyList<Yard> yards = NineMileCreekYards.Yards;
            Assert.AreEqual(NineMileCreekMainland.TownLots.Length, yards.Count,
                "one yard per town lot: a lot that quietly loses its row grows a meadow through its " +
                "dooryard and nothing says so.");

            foreach (Vector3 lot in NineMileCreekMainland.TownLots)
            {
                var site = new Vector2(lot.x, lot.y);
                Assert.IsTrue(YardPlan.HasYardFor(yards, site), $"the lot at {site} has no yard.");
            }
        }

        // =============================================================================================
        //  4. every yard faces its road — through the road pass's own call
        // =============================================================================================

        [Test]
        public void EveryYard_TurnsItsGateTowardTheRoad()
        {
            foreach (Yard yard in NineMileCreekYards.Yards)
            {
                Vector2 road = NineMileCreekRoads.NearestPointOnAnyRoad(yard.Owner);
                foreach (Vector2 gate in yard.Gates)
                    Assert.Less(Vector2.Distance(gate, road), Vector2.Distance(yard.Owner, road),
                        $"'{yard.Name}' opens away from the road it stands on — the shell walk arrives " +
                        "at a fence.");
            }
        }

        [Test]
        public void NoYard_IsBuiltAcrossTheCarriageway()
        {
            foreach (Yard yard in NineMileCreekYards.Yards)
            foreach (Vector2 corner in yard.Polygon.Vertices)
                Assert.IsFalse(NineMileCreekFields.OnAnyCarriageway(corner, 0f),
                    $"'{yard.Name}' has a corner at {corner} standing in the road.");
        }

        [Test]
        public void TheWharfYard_IsDeliberatelyAbsent()
        {
            // This is a PROPOSAL pinned as a test so it cannot drift into being an accident: the spit is
            // made ground where nothing grows, its buildings' ground is already reserved, and what it
            // actually wants is a blocking edge along the quay rather than a lawn. If a later pass adds
            // wharf yards on purpose, this test is the place that says the reasoning was revisited.
            foreach (Yard yard in NineMileCreekYards.Yards)
                Assert.IsFalse(NineMileCreekMainland.IsWorkingSiteInTheWay(yard.Owner),
                    $"'{yard.Name}' is a yard on a WORKING site. The wharf wants a PropertyBoundary " +
                    "along the quay, not a mown lawn — see NineMileCreekYards' remarks.");
        }

        [Test]
        public void EveryYard_HasAUniqueName()
        {
            var seen = new HashSet<string>();
            foreach (Yard yard in NineMileCreekYards.Yards)
                Assert.IsTrue(seen.Add(yard.Name), $"two yards are both called '{yard.Name}'.");
        }

        [Test]
        public void AFencedYard_ProducesARunWithItsGateCutOutOfIt()
        {
            foreach (Yard yard in NineMileCreekYards.Yards)
            {
                List<Vector2[]> runs = yard.FenceRuns();

                if (yard.Fence == YardFence.None)
                {
                    Assert.AreEqual(0, runs.Count, $"'{yard.Name}' is unfenced and must ask for no runs.");
                    continue;
                }

                Assert.Greater(runs.Count, 0, $"'{yard.Name}' is fenced but asks for no runs.");

                float built = 0f;
                foreach (var run in runs)
                    for (int i = 0; i + 1 < run.Length; i++) built += Vector2.Distance(run[i], run[i + 1]);

                float missing = yard.Polygon.Perimeter - built;
                Assert.AreEqual(YardPlan.GateWidthMetres * yard.Gates.Length, missing, 0.35f,
                    $"'{yard.Name}': the fence is {missing:0.00} m short of its boundary, which should " +
                    $"be exactly its {yard.Gates.Length} gate(s).");
            }
        }

        [Test]
        public void TheFenceBill_IsReported()
        {
            float metres = YardPlan.TotalFenceMetres(NineMileCreekYards.Yards);
            Assert.Greater(metres, 0f, "some of these lots are fenced, so the bill cannot be zero.");
            Debug.Log($"[NineMileCreekYardTests] {metres:0} m of fence across " +
                      $"{NineMileCreekYards.Yards.Count} yards.");
        }
    }
}
