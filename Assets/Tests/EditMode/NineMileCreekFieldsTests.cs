using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE FIELDS OF NINE MILE CREEK — is this a farmed coast, and does it stay one?</b>
    ///
    /// <para>The load-bearing test in this file is <see cref="TheHinterlandIsFieldsAndNotForest"/>. The
    /// mainland doc's first photograph is the constraint the whole region hangs on — <i>the land behind
    /// the wharf is FIELDS, NOT FOREST</i> — and it is exactly the kind of constraint that erodes: one
    /// re-tune of a density constant and a farmed coast quietly becomes a wood, with nothing failing and
    /// nobody noticing until the region no longer looks like the photograph. So the anti-stand rule is
    /// measured rather than intended: no circle the size of a small wood may contain more trees than a
    /// hedgerow does.</para>
    ///
    /// <para>The terrain is built through the SAME configure call the builder uses, so a test can never
    /// assert a hinterland the scene does not carry. Nothing here loads an asset: the planting is a pure
    /// function of the published plan plus that terrain, which is why it can be asserted headlessly.</para>
    /// </summary>
    public class NineMileCreekFieldsTests
    {
        private GameObject _terrainGo;
        private MainlandTidalTerrain _terrain;

        private List<NineMileCreekFields.ShrubSite> _shrubs;
        private List<NineMileCreekFields.TreeSite> _trees;
        private List<NineMileCreekFields.MarshSite> _marsh;
        private List<StPetersGrass.GrassTuftSite> _grass;

        /// <summary>Every species the tree kit can bake, so the gradient is exercised at full width
        /// whatever a given machine has on disk. The scatter takes an "available" set precisely so a
        /// partially-baked kit degrades instead of leaving bare ground; a test that passed the machine's
        /// real set would assert a different thing on every machine.</summary>
        private static readonly string[] AllTreeSpecies =
        {
            "BlackSpruce", "RedSpruce", "BalsamFir", "WhiteCedar",
            "RedMaple", "RedOak", "WhiteBirch", "TremblingAspen", "WhitePine",
        };

        /// <summary>
        /// ⚠ ONE-TIME, not per-test, and that is a real cost decision rather than tidiness: the grass
        /// walk alone visits eighty thousand candidate sites and reads the authored terrain at every one
        /// of them. Re-running all four scatters for each of two dozen tests would put a minute of
        /// arithmetic into a suite that already runs six and a half thousand cases. The scatters are pure,
        /// so sharing their output across the class cannot make one test's result depend on another's.
        /// </summary>
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _terrainGo = new GameObject("NineMileCreekMainland_FieldTest");
            _terrain = _terrainGo.AddComponent<MainlandTidalTerrain>();
            NineMileCreekMainland.ConfigureTerrain(_terrain);

            NineMileCreekFields.InvalidateRoadCache();
            // ⚠ The wood-lot table too, since #554: the farmed gate refuses ground a lot has taken, and
            // the shrub habitat field now names a lot's ground `woods`. A sibling fixture's sabotage arm
            // must not be able to reach in here through a memo.
            NineMileCreekWoodLots.InvalidateCache();

            _shrubs = NineMileCreekFields.ScatterHedges(_terrain, 4);
            _trees = NineMileCreekFields.ScatterTrees(_terrain, AllTreeSpecies, _ => 4);
            _marsh = NineMileCreekFields.ScatterMarsh(_terrain);
            _grass = NineMileCreekFields.ScatterGrass(_terrain);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (_terrainGo != null) Object.DestroyImmediate(_terrainGo);
            GameServices.Reset();
        }

        // =============================================================================================
        //  1. THE LAW — fields, not forest
        // =============================================================================================

        [Test]
        public void TheHinterlandIsFieldsAndNotForest()
        {
            // ⭐ NO STANDS. A wood is a place where trees are close together; a farmed coast's trees are
            // in a line along a hedge or standing alone in a field. Measured as a neighbour count inside
            // a small-wood-sized circle, so the rule survives any re-tune of the density constants: if a
            // stand field is ever added, this is what fails.
            const float woodRadius = 24f;
            const int standIsThisMany = 9;

            for (int i = 0; i < _trees.Count; i++)
            {
                int near = 0;
                for (int j = 0; j < _trees.Count; j++)
                {
                    if (i == j) continue;
                    if (Vector2.Distance(_trees[i].Position, _trees[j].Position) <= woodRadius) near++;
                }

                Assert.That(near, Is.LessThan(standIsThisMany),
                    $"a tree at {_trees[i].Position} has {near} neighbours inside {woodRadius} m — that " +
                    "is a STAND, and this region's canon is that the land behind the wharf is FIELDS, " +
                    "not forest (mainland doc §1, photograph 1)");
            }
        }

        [Test]
        public void MostOfTheTreesStandInAHedgeRatherThanInTheOpen()
        {
            Assert.That(_trees.Count, Is.GreaterThan(0), "the region grew no trees at all");
            Assert.That(_trees.Count, Is.LessThan(600),
                $"{_trees.Count} trees is a wood however they are spread — a farmed coast carries " +
                "hedgerow trees and the odd field tree, not a canopy");

            int inHedge = 0;
            foreach (var t in _trees) if (t.InHedge) inHedge++;

            // The commonest tree in farmland anywhere is a hedgerow tree — a hedge left long enough grows
            // them out of itself. If the field trees ever outnumber them, the region has stopped reading
            // as enclosed farmland and started reading as parkland.
            Assert.That(inHedge * 2, Is.GreaterThan(_trees.Count),
                $"only {inHedge} of {_trees.Count} trees stand in a hedgerow — on enclosed farmland most " +
                "of them should");
        }

        /// <summary>
        /// ⭐⭐ <b>THE INVERSE OF A RETIRED GUARD, AND THE SWAP IS DELIBERATE.</b>
        ///
        /// <para>This used to be <c>NoWoodsHabitatIsEverAskedFor</c>: the shrub kit's <c>woods</c> habitat
        /// is an UNDERSTOREY, it belongs INSIDE a stand, and until #554 this coast genuinely had none — so
        /// the test pinned that the habitat was never requested at all. #554 gave the region three bounded
        /// wood lots and flagged the gap rather than absorbing it; this pass fills it, and the old test's
        /// premise ("this coast has no stands") is simply no longer true. A test whose premise has expired
        /// must be replaced by the one that still has a job, not deleted.</para>
        ///
        /// <para><b>The job that survives is the BOUND.</b> The reason a hedgerow may not ask for
        /// <c>woods</c> was never "there are no woods here" — it was "a hedge is not a forest floor", and
        /// that is as true now as it was. So the claim tightens rather than relaxing: the woods habitat is
        /// asked <b>only inside a declared stand</b>, and nowhere else in this region — not in the meadow,
        /// not in a hedgerow, and not in a cut lane's tread (which the understorey's own test proves is
        /// never planted at all). If a re-tune ever lets it leak back out into the fields, this fails with
        /// the ground it leaked onto.</para>
        /// </summary>
        [Test]
        public void TheWoodsHabitatIsAskedOnlyInsideAStand()
        {
            // (1) The half that is word-for-word the old guard: no HEDGE shrub asks for it. A hedgerow is
            // `edge` — rose and raspberry, which is what actually grows in a Maritime hedge.
            foreach (var shrub in _shrubs)
                Assert.That(shrub.Habitat, Is.Not.EqualTo(StPetersShrubs.WoodsHabitat),
                    $"a hedge shrub on '{shrub.Line}' at {shrub.Position} asked for the woods " +
                    "understorey — a hedge is not a forest floor, whatever else this region has grown");

            // (2) …and the half that is new: the habitat FIELD itself, walked over the whole region rather
            // than sampled where something happens to be planted. This is what makes the bound a property
            // of the RULE instead of a property of where the hedges happen to fall.
            //
            // ⚠ Yes, this is structurally true of today's ShrubHabitatAt — it names `woods` off the same
            // InALot call this walk asks. That is what a guard looks like BEFORE the thing it guards
            // against happens, and it is the shape the test it replaces had too. What it costs is one
            // grid walk; what it buys is that the day someone answers "the woods should feel bigger" with
            // a noise threshold — which this region's own docs call "exactly how a farmed coast turns
            // into a wood by accident" — the leak fails here, in the fields' own fixture, naming the
            // ground it leaked onto.
            const float step = 6f;
            Rect region = NineMileCreekRoads.RegionRect();

            int inLots = 0;
            var strays = new List<Vector2>();
            for (float x = region.xMin; x < region.xMax; x += step)
            for (float y = region.yMin; y < region.yMax; y += step)
            {
                var p = new Vector2(x, y);
                if (NineMileCreekFields.ShrubHabitatAt(p) != StPetersShrubs.WoodsHabitat) continue;
                if (NineMileCreekWoodLots.InALot(p)) inLots++;
                else strays.Add(p);
            }

            string first = strays.Count > 0 ? strays[0].ToString() : "none";
            Assert.That(strays, Is.Empty,
                $"{strays.Count} patch(es) of this region call themselves woods understorey while standing " +
                $"outside every declared wood lot (first at {first}). The kit's woods habitat belongs " +
                "INSIDE a stand; a farmed coast that grows it out in the open field has stopped being a " +
                "farmed coast");

            Assert.That(inLots, Is.GreaterThan(0),
                "no ground in this region resolves to the woods habitat at all, so the assertion above is " +
                "vacuous — either the wood lots have vanished from the table or ShrubHabitatAt has stopped " +
                "asking about them, and in both cases the lots are back to a meadow floor");
        }

        // =============================================================================================
        //  2. NOTHING ON THE MADE GROUND
        // =============================================================================================

        [Test]
        public void NothingGrowsOnTheWharfsMadeGround()
        {
            // ⭐ The directive's own words, and the wharf plan's: the spit, both decks, the breakwater and
            // the harbour shoal are BUILT ground and the plan keeps them clear. A hedgerow across a
            // working yard is exactly as wrong as a spruce on the quay.
            foreach (var s in _shrubs)
                Assert.IsFalse(NineMileCreekShoreMap.IsMadeGround(s.Position),
                    $"a hedge shrub stands on the wharf's made ground at {s.Position}");
            foreach (var t in _trees)
                Assert.IsFalse(NineMileCreekShoreMap.IsMadeGround(t.Position),
                    $"a tree stands on the wharf's made ground at {t.Position}");
            foreach (var m in _marsh)
                Assert.IsFalse(NineMileCreekShoreMap.IsMadeGround(m.Position),
                    $"a marsh plant stands on the wharf's made ground at {m.Position}");
            foreach (var g in _grass)
                Assert.IsFalse(NineMileCreekShoreMap.IsMadeGround(g.Position),
                    $"grass grows on the wharf's made ground at {g.Position}");
        }

        [Test]
        public void NothingWoodyStandsInARoadsCarriageway()
        {
            foreach (var s in _shrubs)
                Assert.IsFalse(NineMileCreekFields.OnAnyCarriageway(s.Position, 0f),
                    $"a hedge shrub stands in a carriageway at {s.Position} — a hedge is beside a road, " +
                    "not in it");
            foreach (var t in _trees)
                Assert.IsFalse(NineMileCreekFields.OnAnyCarriageway(t.Position, 0f),
                    $"a tree stands in a carriageway at {t.Position}");
        }

        [Test]
        public void TheMeadowGrowsToTheRoadEdgeAndStops()
        {
            // Grass is allowed what the hedge is not: it grows right up to the gravel. What it may never
            // do is grow ON it — a road the ground paints as gravel and the meadow grows over is not a
            // road (StPetersGrass's own sentence about its walked paths).
            foreach (var g in _grass)
                Assert.IsFalse(NineMileCreekFields.OnAnyCarriageway(g.Position, 0f),
                    $"grass grows on a carriageway at {g.Position}");
        }

        [Test]
        public void TheLandingIsOpenGround()
        {
            // You step off 610 m of wet cobble; the first thing you see should be the coast and the road,
            // not a hedge. The same ruling St Peters makes about its own end of the crossing.
            Vector2 landing = NineMileCreekMainland.BarRoad[0];

            foreach (var s in _shrubs)
                Assert.That(Vector2.Distance(s.Position, landing),
                    Is.GreaterThanOrEqualTo(NineMileCreekFields.LandingClearanceMetres),
                    $"a hedge shrub at {s.Position} stands in the arrival");
            foreach (var t in _trees)
                Assert.That(Vector2.Distance(t.Position, landing),
                    Is.GreaterThanOrEqualTo(NineMileCreekFields.LandingClearanceMetres),
                    $"a tree at {t.Position} stands in the arrival");
        }

        [Test]
        public void NothingWoodyStandsOnATownLotOrAWorkingSite()
        {
            foreach (var s in _shrubs)
            {
                Assert.IsFalse(NineMileCreekFields.OnATownLot(s.Position, 0f),
                    $"a hedge shrub stands on a town lot at {s.Position}");
                Assert.IsFalse(NineMileCreekFields.OnAWorkingSite(s.Position),
                    $"a hedge shrub stands on one of the wharf plan's working sites at {s.Position}");
            }
            foreach (var t in _trees)
            {
                Assert.IsFalse(NineMileCreekFields.OnATownLot(t.Position, 0f),
                    $"a tree stands on a town lot at {t.Position}");
                Assert.IsFalse(NineMileCreekFields.OnAWorkingSite(t.Position),
                    $"a tree stands on one of the wharf plan's working sites at {t.Position}");
            }
        }

        [Test]
        public void EverythingStandsOnGroundThatStaysDry()
        {
            float high = NineMileCreekMainland.SpringHighWater;

            foreach (var s in _shrubs)
                Assert.That(_terrain.ElevationAt(s.Position), Is.GreaterThan(high),
                    $"a hedge shrub at {s.Position} is under spring high water");
            foreach (var t in _trees)
                Assert.That(_terrain.ElevationAt(t.Position), Is.GreaterThan(high),
                    $"a tree at {t.Position} is under spring high water");
            foreach (var g in _grass)
                Assert.That(_terrain.ElevationAt(g.Position), Is.GreaterThan(high),
                    $"grass at {g.Position} is under spring high water");
        }

        // =============================================================================================
        //  3. THE HEDGEROWS — on the boundaries and the two GRAVEL roads
        // =============================================================================================

        [Test]
        public void EveryHedgeShrubRidesAFieldBoundaryOrARoad()
        {
            // ⭐ THE "DERIVE, NEVER EYEBALL" TEST. Every shrub has to be explicable by the line that owns
            // it — on a boundary latitude within the hedge's own wander, or at a road's published offset.
            float wander = NineMileCreekFields.HedgeWanderMetres + 1e-3f;

            foreach (var s in _shrubs)
            {
                if (s.Line.StartsWith("FieldBoundary_"))
                {
                    float best = float.MaxValue;
                    foreach (float y in NineMileCreekFields.FieldBoundaries())
                        best = Mathf.Min(best, Mathf.Abs(s.Position.y - y));
                    Assert.That(best, Is.LessThanOrEqualTo(wander),
                        $"a boundary hedge shrub at {s.Position} is {best:0.00} m off the nearest field " +
                        $"boundary, further than the {NineMileCreekFields.HedgeWanderMetres} m a hedge " +
                        "is allowed to wander");
                    continue;
                }

                Assert.IsTrue(s.Line.StartsWith("RoadHedge_"),
                    $"a hedge shrub at {s.Position} belongs to '{s.Line}', which is neither a field " +
                    "boundary nor a road");
            }
        }

        [Test]
        public void OnlyTheGravelRoadsAreHedged()
        {
            // ⚠ The bar road is a red dirt track along an open clifftop and the photographs show it
            // running through bare field; hedging it would turn the region's most exposed road into a
            // lane. The gully path is nineteen metres of foot tread.
            foreach (var s in _shrubs)
            {
                Assert.That(s.Line, Does.Not.Contain(NineMileCreekRoads.BarRoadName),
                    $"the bar road has been hedged at {s.Position}");
                Assert.That(s.Line, Does.Not.Contain(NineMileCreekRoads.GullyPathName),
                    $"the gully path has been hedged at {s.Position}");
            }

            var lines = new HashSet<string>();
            foreach (var s in _shrubs) lines.Add(s.Line);

            Assert.IsTrue(ContainsLineFor(lines, NineMileCreekRoads.WharfRoadName),
                "Wharf Road carries no hedge at all");
            Assert.IsTrue(ContainsLineFor(lines, NineMileCreekRoads.ThroughRoadName),
                "the through-road carries no hedge at all");
        }

        [Test]
        public void TheHedgeAndThePoleLineDoNotShareAVerge()
        {
            // ⭐ NineMileCreekMainland §12 published the pole route in Phase A — along Wharf Road, 5 m to
            // its NORTH — and the dressing has walked it since. A hedge at the same offset would grow a
            // bush round the foot of every pole and run a wire span through a hedgerow. So Wharf Road is
            // hedged on its south side only, and this measures it rather than trusting the rule.
            foreach (var pole in NineMileCreekDressing.Poles())
            foreach (var shrub in _shrubs)
                Assert.That(Vector2.Distance(shrub.Position, pole.Position),
                    Is.GreaterThan(PoleClearanceMetres),
                    $"a hedge shrub at {shrub.Position} stands {Vector2.Distance(shrub.Position, pole.Position):0.00} m " +
                    $"from a utility pole at {pole.Position} — a hedge and a pole line do not share a verge");
        }

        /// <summary>How much room a pole needs round its foot: enough to stand a ladder against.</summary>
        private const float PoleClearanceMetres = 2f;

        [Test]
        public void TheHedgeIsGappyRatherThanAWall()
        {
            // A real hedgerow has gates and gaps a tractor goes through. A solid one reads as a wall the
            // player will try to walk round, which on a 96 m field boundary is a long walk.
            Assert.That(NineMileCreekFields.HedgeDensity, Is.LessThan(0.85f));
            Assert.That(_shrubs.Count, Is.GreaterThan(0), "the region grew no hedgerows at all");
        }

        [Test]
        public void TheFieldBoundariesAreALatticeAnchoredOnTheRegion()
        {
            var boundaries = NineMileCreekFields.FieldBoundaries();
            Assert.That(boundaries.Count, Is.GreaterThan(2),
                "a region this tall should carry several field strips");
            Assert.That(boundaries, Contains.Item(0f),
                "the lattice is anchored on the region's own centre line, so index 0 is y = 0");

            for (int i = 1; i < boundaries.Count; i++)
                Assert.That(boundaries[i] - boundaries[i - 1],
                    Is.EqualTo(NineMileCreekFields.FieldStripMetres).Within(1e-3f),
                    "the strips are one width apart — a lattice, not a table");

            float half = NineMileCreekMainland.RegionWorldSize.y * 0.5f;
            foreach (float y in boundaries)
                Assert.That(Mathf.Abs(y), Is.LessThanOrEqualTo(half),
                    "a field boundary runs off the edge of the region");
        }

        // =============================================================================================
        //  4. THE MEADOW — and its budget (rule 7)
        // =============================================================================================

        [Test]
        public void TheGrassFieldIsBoundedToTheLandRatherThanToTheRegion()
        {
            var layout = NineMileCreekFields.FieldLayout();
            Rect region = NineMileCreekRoads.RegionRect();

            float width = layout.CellsX * layout.CellSize;
            Assert.That(width, Is.LessThan(region.width),
                "the grass grid covers the whole region rectangle — more than a third of which is open " +
                "bay that can never grow a blade, and a field costs a byte per CELL whether or not " +
                "anything grows in it");

            // …and it must still reach the coast, or the shore's own green fringe goes unplanted.
            float east = layout.OriginX + width;
            Assert.That(east, Is.GreaterThanOrEqualTo(NineMileCreekFields.GrassEastEdge() - layout.CellSize),
                "the grass grid stops short of the coastline");
        }

        [Test]
        public void TheGrassFieldFitsItsPayloadBudget()
        {
            // ⭐ RULE 7 AS A NUMBER. The field is one byte per candidate site and it is written into a
            // COMMITTED scene, so the budget is the scene's. St Peters' island bakes ~93 KB; this region
            // is roughly seven times the plantable area and must not cost seven times the bytes — which
            // is what the coarser grid and the single slot buy.
            var layout = NineMileCreekFields.FieldLayout();
            int payload = layout.CellsX * layout.CellsY * layout.Slots;

            Assert.That(payload, Is.LessThan(GrassPayloadBudgetBytes),
                $"the grass field is {payload:N0} bytes of scene, over the {GrassPayloadBudgetBytes:N0} " +
                "byte budget. The levers are GrassStepMetres and GrassSlotsPerCell, in that order.");

            Assert.That(_grass.Count, Is.LessThan(GrassTuftBudget),
                $"{_grass.Count:N0} tufts is more geometry than the batched field path was sized for — " +
                "the chunk meshes are derived at load and every tuft is four vertices");
            Assert.That(_grass.Count, Is.GreaterThan(2000),
                "the fields grew almost no grass — something is gating the whole hinterland out");
        }

        /// <summary>The scene-payload ceiling, in bytes. A shade over St Peters' own field, which is the
        /// only calibrated number this project has for what a committed meadow may cost.</summary>
        private const int GrassPayloadBudgetBytes = 100_000;

        /// <summary>
        /// The derived-geometry ceiling. St Peters ratified ~27,000 tufts; this allows half as many again
        /// for a region seven times the size, which is what "sparser, over more ground" means as a number.
        ///
        /// <para>⚠ <b>THIS NUMBER HAS ALREADY EARNED ITS KEEP.</b> The first draft's 1.8 m grid measured
        /// 42,819 tufts here — 7% over — because the plantable fraction turned out to be 86% of the grid
        /// rather than the ~50% it was sized against. The grain moved to 2.0 m and the ceiling did NOT
        /// move, which is the whole point of writing a budget down: it is the argument, and the density
        /// constants are what give way to it.</para>
        /// </summary>
        private const int GrassTuftBudget = 40_000;

        [Test]
        public void TheMeadowIsCoarserThanTheIslands()
        {
            // Both halves of this are on purpose — see NineMileCreekFields.GrassStepMetres. St Peters is a
            // reverting island nobody has cut in a generation; this is a worked field.
            Assert.That(NineMileCreekFields.GrassStepMetres, Is.GreaterThan(StPetersGrass.GrassStep),
                "a farmed field is not a hayfield");
            Assert.That(NineMileCreekFields.GrassSlotsPerCell,
                Is.LessThan(StPetersGrass.MaxTuftsPerCell),
                "a second slot doubles both the payload and the derived geometry to buy a clumpiness " +
                "that is wrong on worked ground");
        }

        [Test]
        public void EveryGrassHabitatIsOneTheFieldCanName()
        {
            // The field stores a habitat as a nibble index into StPetersGrassField.HabitatIds. A habitat
            // the vocabulary cannot name is stored as 0 — "nothing grows here" — so the tuft silently
            // vanishes at load. This is that failure, caught at bake.
            var seen = new HashSet<string>();
            foreach (var g in _grass) seen.Add(g.Habitat);

            foreach (string habitat in seen)
                Assert.That(StPetersGrassField.HabitatIdOf(habitat), Is.GreaterThan(0),
                    $"the fields grow '{habitat}', which the grass field's vocabulary cannot name — " +
                    "every tuft tagged with it would be dropped at load with nothing failing");

            Assert.That(seen.Count, Is.GreaterThan(2),
                $"the whole hinterland resolved to {seen.Count} habitat(s) — the habitat map is not " +
                "varying, so the fields, the verges and the shore all draw the same blade");
        }

        [Test]
        public void NoTuftAsksForTheBroadClumps()
        {
            // The library's wide art is the island's cheapest coverage, and coverage is exactly what a
            // worked field is not supposed to have.
            foreach (var g in _grass)
                Assert.IsFalse(g.Broad, $"a tuft at {g.Position} asked for a broad clump");
        }

        // =============================================================================================
        //  5. THE MARSH
        // =============================================================================================

        [Test]
        public void BothPondsGetTheirMarsh()
        {
            int barachois = 0, pool = 0;
            foreach (var m in _marsh)
            {
                if (m.Pond == NineMileCreekFields.BarachoisName) barachois++;
                else if (m.Pond == NineMileCreekFields.MarshPoolName) pool++;
            }

            Assert.That(barachois, Is.GreaterThan(0), "the barachois has no marsh round it");
            Assert.That(pool, Is.GreaterThan(0), "the marsh pool has no marsh round it — which would be " +
                                                 "a pond called the marsh pool with no marsh in it");
        }

        [Test]
        public void TheMarshKeepsTheNeckBetweenThePondsWalkable()
        {
            // ⚠ Wharf Road runs the twelve metres of dry ground between the barachois and the marsh pool
            // — that neck is why the second pond exists at all (NineMileCreekMainland §7). A marsh grown
            // across it would drown the causeway the photographs show.
            foreach (var m in _marsh)
                Assert.IsFalse(NineMileCreekFields.OnAnyCarriageway(m.Position, 0f),
                    $"a marsh plant at {m.Position} stands in a carriageway");
        }

        [Test]
        public void EveryMarshPlantIsInAZoneTheKitDeclares()
        {
            foreach (var m in _marsh)
            {
                // ⚠ `Or`, not `Is.AnyOf` — the latter is a newer NUnit than this project ships, and it
                // fails to COMPILE rather than to assert, which takes the whole suite down with it.
                Assert.That(m.Zone, Is.EqualTo(StPetersShorePlants.ZoneLowMarsh)
                                      .Or.EqualTo(StPetersShorePlants.ZoneHighMarsh),
                    $"a marsh plant at {m.Position} is in zone '{m.Zone}', which is not one of the two " +
                    "this pass plants");
                Assert.That(StPetersShorePlants.ZoneSpecies[m.Zone], Contains.Item(m.SpeciesKey),
                    $"'{m.SpeciesKey}' is not one of the species the kit puts in the {m.Zone} zone");
            }
        }

        [Test]
        public void TheMarshBandsFollowTheTideRatherThanATable()
        {
            // Low marsh below neap high water, high marsh above it — the line the ordinary tide stops at,
            // which is the real botanical boundary. Derived from the region's own tide, so re-tuning the
            // tide moves the band.
            foreach (var m in _marsh)
            {
                float e = _terrain.ElevationAt(m.Position);
                if (m.Zone == StPetersShorePlants.ZoneLowMarsh)
                    Assert.That(e, Is.LessThanOrEqualTo(
                        NineMileCreekFields.LowMarshCeilingElevation + 1e-3f),
                        $"low marsh at {m.Position} is above neap high water ({e:0.00} m)");
                else
                    Assert.That(e, Is.GreaterThan(NineMileCreekFields.LowMarshCeilingElevation - 1e-3f),
                        $"high marsh at {m.Position} is below neap high water ({e:0.00} m)");
            }
        }

        // =============================================================================================
        //  6. DETERMINISM (rule 5)
        // =============================================================================================

        [Test]
        public void EveryLayerIsAPureFunctionOfThePlan_AcrossRuns()
        {
            NineMileCreekFields.InvalidateRoadCache();

            var shrubs = NineMileCreekFields.ScatterHedges(_terrain, 4);
            var trees = NineMileCreekFields.ScatterTrees(_terrain, AllTreeSpecies, _ => 4);
            var marsh = NineMileCreekFields.ScatterMarsh(_terrain);
            var grass = NineMileCreekFields.ScatterGrass(_terrain);

            Assert.That(shrubs.Count, Is.EqualTo(_shrubs.Count));
            for (int i = 0; i < shrubs.Count; i++)
            {
                Assert.That(shrubs[i].Position, Is.EqualTo(_shrubs[i].Position));
                Assert.That(shrubs[i].Habitat, Is.EqualTo(_shrubs[i].Habitat));
            }

            Assert.That(trees.Count, Is.EqualTo(_trees.Count));
            for (int i = 0; i < trees.Count; i++)
            {
                Assert.That(trees[i].Position, Is.EqualTo(_trees[i].Position));
                Assert.That(trees[i].Species, Is.EqualTo(_trees[i].Species));
            }

            Assert.That(marsh.Count, Is.EqualTo(_marsh.Count));
            Assert.That(grass.Count, Is.EqualTo(_grass.Count));
            for (int i = 0; i < grass.Count; i++)
                Assert.That(grass[i].Position, Is.EqualTo(_grass[i].Position));
        }

        [Test]
        public void ThePlantingCarriesNoWorldSeedAndNoClock()
        {
            // The scatter is a pure function of POSITION — no System.Random, no DateTime, no worldSeed.
            // Said as an assertion by planting the same region twice through a re-created terrain, which
            // is the only way a hidden global would show up.
            var second = new GameObject("NineMileCreekMainland_FieldTest_Second");
            try
            {
                var terrain = second.AddComponent<MainlandTidalTerrain>();
                NineMileCreekMainland.ConfigureTerrain(terrain);

                var trees = NineMileCreekFields.ScatterTrees(terrain, AllTreeSpecies, _ => 4);
                Assert.That(trees.Count, Is.EqualTo(_trees.Count));
                for (int i = 0; i < trees.Count; i++)
                    Assert.That(trees[i].Position, Is.EqualTo(_trees[i].Position),
                        "a tree moved between two identically-configured terrains — something in the " +
                        "scatter is reading state that is not the plan");
            }
            finally
            {
                Object.DestroyImmediate(second);
            }
        }

        // =============================================================================================
        //  7. THE SEAM WITH THE ROAD PASS
        // =============================================================================================

        [Test]
        public void TheFieldsReadTheRoadPassesOwnWidthsRatherThanACopy()
        {
            // ⭐ The two passes meet here, and this is what keeps them from drifting: widen a road in
            // NineMileCreekRoads and its hedge must step back. Asserted by widening nothing and instead
            // proving the query is the road pass's own — a shrub at exactly the published half-width plus
            // the verge must be rejected, and one a whisker beyond it accepted.
            var road = default(NineMileCreekRoads.Way);
            foreach (var way in NineMileCreekRoads.Ways())
                if (way.Name == NineMileCreekRoads.ThroughRoadName) road = way;

            Vector2 on = road.Route[1];
            Vector2 across = new Vector2(1f, 0f);   // the through-road runs nearly north–south

            Assert.IsTrue(
                NineMileCreekFields.OnAnyCarriageway(on + across * (road.HalfWidth - 0.5f), 0f),
                "a point inside the published carriageway was not recognised as road");
            Assert.IsFalse(
                NineMileCreekFields.OnAnyCarriageway(on + across * (road.HalfWidth + 1f), 0f),
                "a point outside the published carriageway was treated as road");
        }

        // =============================================================================================
        //  helpers
        // =============================================================================================

        private static bool ContainsLineFor(HashSet<string> lines, string road)
        {
            foreach (string line in lines) if (line.Contains(road)) return true;
            return false;
        }
    }
}
