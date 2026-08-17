using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐⭐ <b>THE WOOD LOTS OF NINE MILE CREEK — can this coast carry woods and still be a farmed coast?</b>
    ///
    /// <para>The owner ruled two things on 2026-08-16 that pull against each other: <i>both regions get
    /// denser, more forest-like woods</i>, and <i>the woodland zones go ALONGSIDE the NMC fields</i> — while
    /// this region's founding canon, quoted by its own builder as <i>"the one thing this region's hinterland
    /// must never stop being"</i>, is that the land behind the wharf is <b>FIELDS, NOT FOREST</b>. They are
    /// compatible on exactly one condition: the woods have to be <b>LOTS</b>, and "lot" is not a claim you
    /// can make about a shape — only about a SHARE of the ground. <see cref="TheWoodLotsAreASmallShareOfTheHinterland"/>
    /// is therefore the load-bearing test in this file, and <see cref="MaxHinterlandShare"/> is where the
    /// owner's two rulings are reconciled numerically rather than in prose.</para>
    ///
    /// <para><b>The second half of the reconciliation is architectural, and it is tested here too.</b>
    /// <c>NineMileCreekFieldsTests.TheHinterlandIsFieldsAndNotForest</c> measures the FARMED pass and fails
    /// if any of its trees has nine neighbours inside 24 m. That test was not touched, re-scoped or relaxed
    /// by this pass — <see cref="NoFarmedTreeStandsInsideAWoodLot"/> is the proof that it is still measuring
    /// the population it was written to measure, with the lots kept out of it.</para>
    /// </summary>
    public class NineMileCreekWoodLotTests
    {
        private GameObject _terrainGo;
        private MainlandTidalTerrain _terrain;

        private List<NineMileCreekFields.TreeSite> _farmed;
        private List<NineMileCreekFields.ShrubSite> _hedges;
        private List<WoodlandZones.LotTreeSite> _lots;

        /// <summary>The lots' UNDERSTOREY — the shrub layer under their canopy, and the only shrubs inside
        /// a lot on this coast (the hedges give way to one rather than running through it).</summary>
        private List<NineMileCreekFields.ShrubSite> _understorey;

        /// <summary>Every species the tree kit can bake, so the gradient is exercised at full width whatever
        /// a given machine has on disk — the fields tests' own reasoning.</summary>
        static readonly string[] AllTreeSpecies =
        {
            "BlackSpruce", "RedSpruce", "BalsamFir", "WhiteCedar",
            "RedMaple", "RedOak", "WhiteBirch", "TremblingAspen", "WhitePine",
        };

        /// <summary>⚠ ONE-TIME: the scatters walk tens of thousands of candidate sites and read the authored
        /// terrain at each. They are pure, so sharing their output cannot make one test's result depend on
        /// another's — the fields fixture's own note.</summary>
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _terrainGo = new GameObject("NineMileCreekMainland_WoodLotTest");
            _terrain = _terrainGo.AddComponent<MainlandTidalTerrain>();
            NineMileCreekMainland.ConfigureTerrain(_terrain);

            NineMileCreekFields.InvalidateRoadCache();
            NineMileCreekWoodLots.InvalidateCache();

            _farmed = NineMileCreekFields.ScatterTrees(_terrain, AllTreeSpecies, _ => 4);
            _hedges = NineMileCreekFields.ScatterHedges(_terrain, 4);
            _lots = NineMileCreekWoodLots.ScatterTrees(_terrain, AllTreeSpecies, _ => 4, _farmed);
            _understorey = NineMileCreekWoodLots.ScatterUnderstorey(_terrain, 4, Standing());
        }

        /// <summary>Everything already on the ground when the understorey runs — exactly what the planter
        /// hands it. Composed here rather than in the fixture body so a test that wants to re-run the
        /// scatter (determinism, sabotage) hands it the same second argument.</summary>
        List<Vector2> Standing()
        {
            var standing = new List<Vector2>();
            foreach (var t in _farmed) standing.Add(t.Position);
            foreach (var t in _lots) standing.Add(t.Position);
            foreach (var s in _hedges) standing.Add(s.Position);
            return standing;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (_terrainGo != null) Object.DestroyImmediate(_terrainGo);
            NineMileCreekWoodLots.InvalidateCache();
            GameServices.Reset();
        }

        // =============================================================================================
        //  1. THE LAW — bounded lots, not a blanket
        // =============================================================================================

        /// <summary>
        /// The pre-lot ground rules: everything this region reserves for a reason other than "a wood is
        /// growing here". The denominator of the boundedness law, and deliberately assembled from
        /// <see cref="NineMileCreekFields"/>' own published predicates rather than from thresholds restated
        /// here — otherwise the law would be measured against a hinterland this test invented.
        /// </summary>
        bool GroundAWoodyThingCouldHaveHad(Vector2 p)
        {
            if (!NineMileCreekFields.IsFieldGround(_terrain, p, NineMileCreekFields.FieldFloorElevation))
                return false;
            if (NineMileCreekFields.OnAnyCarriageway(p, NineMileCreekFields.RoadVergeMetres)) return false;
            if (NineMileCreekFields.OnAnyPad(p, NineMileCreekFields.RoadVergeMetres)) return false;
            if (NineMileCreekFields.OnATownLot(p, NineMileCreekFields.TownLotBreathingMetres)) return false;
            if (NineMileCreekFields.OnAWorkingSite(p)) return false;
            if (Vector2.Distance(p, NineMileCreekMainland.BarRoad[0])
                    < NineMileCreekFields.LandingClearanceMetres) return false;
            return true;
        }

        [Test]
        public void TheWoodLotsAreASmallShareOfTheHinterland()
        {
            // ⭐ THE TEST THE OWNER'S TWO RULINGS MEET IN. Measured on the real ground at a 4 m grid — both
            // numerator and denominator, so a lot that happens to lie half over a pond counts for what it
            // actually covers rather than for its radius.
            const float step = 4f;
            Rect region = NineMileCreekRoads.RegionRect();

            int hinterland = 0, inLots = 0;
            for (float x = region.xMin; x < region.xMax; x += step)
            for (float y = region.yMin; y < region.yMax; y += step)
            {
                var p = new Vector2(x, y);
                if (!GroundAWoodyThingCouldHaveHad(p)) continue;
                hinterland++;
                if (NineMileCreekWoodLots.InALot(p)) inLots++;
            }

            Assert.That(hinterland, Is.GreaterThan(5000),
                "sanity: the hinterland was not actually walked, so any share below would be noise");

            float share = inLots / (float)hinterland;
            Debug.Log($"[NineMileCreekWoodLotTests] CENSUS — the {NineMileCreekWoodLots.LotCount} wood lots " +
                      $"cover {share:P2} of the plantable hinterland ({inLots} of {hinterland} cells at " +
                      $"{step:0.#} m), and carry {_lots.Count} trees against the farmed pass's " +
                      $"{_farmed.Count}.");

            Assert.That(share, Is.LessThan(NineMileCreekWoodLots.MaxHinterlandShare),
                $"the wood lots cover {share:P1} of the plantable hinterland, over the " +
                $"{NineMileCreekWoodLots.MaxHinterlandShare:P0} ceiling. This is the number that keeps the " +
                "owner's 'denser, more forest-like woods' compatible with this region's 'FIELDS, NOT " +
                "FOREST' — past it the hinterland stops photographing as farmland. Shrink a lot or drop " +
                "one; do not raise the ceiling without the owner.");

            Assert.That(share, Is.GreaterThan(0.01f),
                $"the wood lots cover only {share:P2} of the hinterland — that is not woods ALONGSIDE the " +
                "fields, it is three thickets, and the ruling asked for something you can walk into");

            // ⚠ THIS CEILING MEASURES GROUND, NOT PLANTING, AND THE TWO ARE EASY TO CONFUSE. The share
            // above is `InALot` over the hinterland — the LOT CAPSULES' own footprint. Nothing that grows
            // inside a lot can move it: not the canopy, and not the UNDERSTOREY this region gained on top
            // of it. Likewise `TheHinterlandIsFieldsAndNotForest` counts TREES in the FARMED pass, so a
            // shrub is outside its population by construction. If either number ever moves, the cause is a
            // change to the zone table or to the farmed scatter — do not go looking in the shrub layer,
            // and do not let a future pass "make room" for shrubs by widening a law written for canopy.
        }

        [Test]
        public void EveryLotIsSquaredAgainstAFieldBoundaryAndSetBackFromItsOwnShore()
        {
            // Nothing in the table is typed as a coordinate: a lot's latitude is a field boundary from the
            // hedgerow lattice and its longitude is that boundary's own coastline minus the setback. If
            // either derivation is ever replaced by a literal, this is what notices.
            var boundaries = NineMileCreekFields.FieldBoundaries();

            foreach (var lot in NineMileCreekWoodLots.Lots)
            {
                Assert.That(boundaries.Any(y => Mathf.Abs(y - lot.From.y) < 0.01f), Is.True,
                    $"'{lot.Name}' sits at y = {lot.From.y:0.#}, which is not one of the region's field " +
                    $"boundaries [{string.Join(", ", boundaries.Select(b => b.ToString("0")))}] — a farm " +
                    "wood lot is squared against a boundary, and deriving it from the lattice is what makes " +
                    "re-spacing the strips move the lot");

                float setback = NineMileCreekFields.CoastXAt(lot.From.y) - lot.From.x;
                Assert.That(setback, Is.EqualTo(NineMileCreekWoodLots.ShoreSetbackMetres).Within(0.01f),
                    $"'{lot.Name}' stands {setback:0.0} m back from its boundary's shoreline against a " +
                    $"declared {NineMileCreekWoodLots.ShoreSetbackMetres:0} m");
            }
        }

        [Test]
        public void NoFarmedTreeStandsInsideAWoodLot()
        {
            // ⭐ THE PROOF THAT THE REGION'S LOAD-BEARING TEST STILL MEASURES WHAT IT MEASURED. The
            // anti-stand rule in NineMileCreekFieldsTests walks the FARMED trees; if a lot's trees leaked
            // into that population it would have had to be re-scoped to let a stand through, which is how
            // a constraint dies quietly.
            foreach (var t in _farmed)
                Assert.IsFalse(NineMileCreekWoodLots.InALot(t.Position),
                    $"a farmed tree ({(t.InHedge ? "hedgerow" : "field")}) at {t.Position} stands inside a " +
                    "wood lot — the farmed and lot populations have merged, and the region's FIELDS-NOT-" +
                    "FOREST test is now measuring a stand");
        }

        [Test]
        public void NoHedgerowRunsThroughAWoodLot()
        {
            // A hedge that ran into a wood and out the other side would be a fence through a forest. The
            // give-way idiom NineMileCreekFields.NearAHedgedRoad already applies at a road's verge.
            foreach (var shrub in _hedges)
                Assert.IsFalse(NineMileCreekWoodLots.InALot(shrub.Position),
                    $"a hedge shrub on '{shrub.Line}' at {shrub.Position} stands inside a wood lot — the " +
                    "hedges are supposed to GIVE WAY to a lot, not run through it");
        }

        // =============================================================================================
        //  2. NOTHING IS BURIED UNDER A LOT  (+ the sabotage arm)
        // =============================================================================================

        /// <summary>The check the real test and its sabotage arm share, so the arm proves THIS logic bites.
        /// A lot may not reach the town, the roads, the wharf's working ground or the bar landing.</summary>
        List<string> LotConflicts(IReadOnlyList<WoodlandZone> zones)
        {
            var found = new List<string>();
            foreach (var zone in zones)
            {
                if (zone.Kind != WoodlandZoneKind.Lot) continue;

                foreach (var lot in NineMileCreekMainland.TownLots)
                {
                    var at = new Vector2(lot.x, lot.y);
                    float need = zone.Radius + NineMileCreekMainland.TownLotRadius
                                 + NineMileCreekFields.TownLotBreathingMetres;
                    if (zone.SpineDistance(at) < need)
                        found.Add($"'{zone.Name}' reaches the town lot at {at}");
                }

                foreach (var way in NineMileCreekRoads.Ways())
                    if (NineMileCreekMainland.DistanceToRoute(way.Route, zone.From)
                            < zone.Radius + way.HalfWidth + NineMileCreekFields.RoadVergeMetres)
                        found.Add($"'{zone.Name}' reaches {way.Name}");

                if (Vector2.Distance(zone.From, NineMileCreekMainland.BarRoad[0])
                        < zone.Radius + NineMileCreekFields.LandingClearanceMetres)
                    found.Add($"'{zone.Name}' reaches the bar landing");
            }
            return found;
        }

        [Test]
        public void NoWoodLotStandsOnGroundTheRegionNeeds()
        {
            var conflicts = LotConflicts(NineMileCreekWoodLots.Zones);
            Assert.That(conflicts, Is.Empty,
                "a wood lot was sited on ground this region needs: " + string.Join("; ", conflicts));
        }

        [Test]
        public void Sabotage_ALotDroppedOnTheTownIsRejected()
        {
            var bad = NineMileCreekWoodLots.Zones.ToList();
            var onTheTavern = new Vector2(NineMileCreekMainland.TavernPos.x,
                                          NineMileCreekMainland.TavernPos.y);
            bad.Add(WoodlandZone.Circle("TavernWood", WoodlandZoneKind.Lot, onTheTavern,
                                        NineMileCreekWoodLots.LotRadiusMetres,
                                        NineMileCreekWoodLots.LotCoreFraction,
                                        NineMileCreekWoodLots.LotCoreSpacingMetres,
                                        "deliberately wrong: a wood lot planted through the tavern"));

            var conflicts = LotConflicts(bad);
            Assert.That(conflicts, Is.Not.Empty,
                "a wood lot planted straight through the tavern was NOT rejected — the clearance check " +
                "above is decoration");
            Assert.That(string.Join("; ", conflicts), Does.Contain("TavernWood"));
        }

        [Test]
        public void NothingInAWoodLotStandsOnTheWharfsMadeGroundOrInAPond()
        {
            foreach (var site in _lots)
            {
                Assert.IsFalse(NineMileCreekShoreMap.IsMadeGround(site.Position),
                    $"a wood-lot tree at {site.Position} stands on the wharf's made ground");
                Assert.IsFalse(NineMileCreekShoreMap.IsPondGround(site.Position),
                    $"a wood-lot tree at {site.Position} stands in a pond");
            }
        }

        // =============================================================================================
        //  3. THE LANES CUT  (+ the sabotage arm the handoff asks for by name)
        // =============================================================================================

        [Test]
        public void EveryLotHasALaneCutCleanThroughIt()
        {
            foreach (var lot in NineMileCreekWoodLots.Lots)
            {
                var lane = WoodlandZones.Require(NineMileCreekWoodLots.Zones,
                                                 lot.Name + NineMileCreekWoodLots.LaneSuffix);

                Assert.That(lane.SpineLength, Is.GreaterThanOrEqualTo(lot.Radius * 2f),
                    $"'{lane.Name}' stops inside '{lot.Name}' instead of coming out the other side");

                int inTread = _lots.Count(s => lane.SpineDistance(s.Position) < lane.Radius);
                Assert.That(inTread, Is.Zero,
                    $"{inTread} wood-lot trees stand in the tread of '{lane.Name}' — the corridor is " +
                    "declared and then planted over");

                int inLot = _lots.Count(s => s.Lot == lot.Name);
                Assert.That(inLot, Is.GreaterThan(20),
                    $"'{lot.Name}' grew only {inLot} trees, so its lane runs through nothing much and this " +
                    "test would pass on an empty declaration");
            }
        }

        [Test]
        public void Sabotage_ALaneThatStopsCutting_FillsItsTreadWithTrunks()
        {
            // ⭐ "A cutout that stops cutting must fail." The cut comes from ONE gate —
            // NineMileCreekFields.IsWoodyGround asking NineMileCreekWoodLots.IsCleared — so the arm rebuilds
            // the scatter with exactly that gate removed and nothing else changed. If the tread stays clear
            // anyway, the corridors are being held open by something incidental and
            // EveryLotHasALaneCutCleanThroughIt is passing for the wrong reason.
            var withoutTheCut = WoodlandZones.ScatterLots(
                NineMileCreekWoodLots.Zones, _terrain,
                p => NineMileCreekFields.IsFieldGround(_terrain, p,
                         NineMileCreekFields.FieldFloorElevation)
                     && !NineMileCreekFields.OnAnyCarriageway(p, NineMileCreekFields.RoadVergeMetres)
                     && !NineMileCreekFields.OnAnyPad(p, NineMileCreekFields.RoadVergeMetres)
                     && !NineMileCreekFields.OnATownLot(p, NineMileCreekFields.TownLotBreathingMetres)
                     && !NineMileCreekFields.OnAWorkingSite(p),
                NineMileCreekFields.ExposureAt, NineMileCreekFields.WetnessAt,
                AllTreeSpecies, _ => 4, new List<Vector2>());

            int inTreads = 0;
            foreach (var lot in NineMileCreekWoodLots.Lots)
            {
                var lane = WoodlandZones.Require(NineMileCreekWoodLots.Zones,
                                                 lot.Name + NineMileCreekWoodLots.LaneSuffix);
                inTreads += withoutTheCut.Count(s => lane.SpineDistance(s.Position) < lane.Radius);
            }

            Assert.That(inTreads, Is.GreaterThan(10),
                "with the cut-lane gate removed the lanes' treads still came back nearly empty — so the " +
                "corridors are not actually being cut by NineMileCreekWoodLots.IsCleared, and the test " +
                "that says they are is passing on something else");
        }

        // =============================================================================================
        //  4. THE LOTS ARE WOODS
        // =============================================================================================

        [Test]
        public void EveryLotTreeStandsInsideADeclaredLot()
        {
            Assert.That(_lots.Count, Is.GreaterThan(0), "the region's wood lots grew nothing at all");

            foreach (var site in _lots)
            {
                var lot = WoodlandZones.Require(NineMileCreekWoodLots.Zones, site.Lot);
                Assert.IsTrue(lot.Contains(site.Position),
                    $"a tree the lot pass planted at {site.Position} is outside '{site.Lot}' — a bounded " +
                    "lot that leaks is not bounded, and this region's whole canon rests on the bound");
            }
        }

        [Test]
        public void AWoodLotIsDenserThanTheFarmedGroundAroundIt()
        {
            // The point of the ruling. A "wood lot" whose trees are as far apart as the field trees is a
            // field with a name on it — NineMileCreekFields.FieldTreeStepMetres is 48 m, and a wood is not.
            foreach (var lot in NineMileCreekWoodLots.Lots)
            {
                int inLot = _lots.Count(s => s.Lot == lot.Name)
                            + _farmed.Count(t => lot.Contains(t.Position));
                float density = inLot / lot.AreaMetresSquared;

                // One tree per 48 m grid cell is the farmed rate this must beat, by a lot.
                float farmedRate = NineMileCreekFields.FieldTreeChance
                                   / (NineMileCreekFields.FieldTreeStepMetres
                                      * NineMileCreekFields.FieldTreeStepMetres);

                Assert.That(density, Is.GreaterThan(farmedRate * 20f),
                    $"'{lot.Name}' carries {density * 10000f:0.0} trees per hectare against the open " +
                    $"fields' {farmedRate * 10000f:0.0} — that is a field with a name on it, not a wood");
            }
        }

        /// <summary>
        /// ⭐ <b>THE TAPER.</b> A lot's own planting must thin toward its rim.
        ///
        /// <para>Measured on the LOT's trees alone, which on this coast is the whole population inside a
        /// lot anyway — there is no ambient mosaic here to add a uniform term. (St Peters' twin of this test
        /// has to say so explicitly, because there it does.) The bar is the same 1.5 as the island's, so
        /// the two regions are held to one standard by <see cref="WoodlandZone.PlantingShare"/>.</para>
        /// </summary>
        [Test]
        public void EveryLotsOwnPlantingThinsTowardItsRim()
        {
            foreach (var lot in NineMileCreekWoodLots.Lots)
            {
                float coreRadius = lot.Radius * NineMileCreekWoodLots.LotCoreFraction;
                var trunks = _lots.Where(s => s.Lot == lot.Name).Select(s => s.Position).ToList();

                int inCore = trunks.Count(p => lot.SpineDistance(p) <= coreRadius);
                float coreArea = Mathf.PI * coreRadius * coreRadius;
                float fringeArea = Mathf.PI * lot.Radius * lot.Radius - coreArea;

                float coreDensity = inCore / coreArea;
                float fringeDensity = (trunks.Count - inCore) / fringeArea;

                Assert.That(coreDensity, Is.GreaterThan(fringeDensity * 1.5f),
                    $"'{lot.Name}' carries {coreDensity * 1000f:0.0} trunks per 1000 m² in its core against " +
                    $"{fringeDensity * 1000f:0.0} in its fringe — the ruling asked for edges that READ, and " +
                    "a flat block to a hard rim reads as a stamp on a field");
            }
        }

        [Test]
        public void TheSpeciesInALotAreThisCoastsOwnGradient()
        {
            // The lots ask StPetersWoods.SpeciesPreference with THIS region's exposure and wetness, which is
            // the whole reason two regions on one coast agree about which tree takes a salt wind. If a lot
            // ever grew one species it would mean the habitat model was not being consulted.
            var species = new HashSet<string>(_lots.Select(s => s.Species));
            Assert.That(species.Count, Is.GreaterThan(2),
                $"the wood lots grew only {species.Count} species ({string.Join(", ", species)}) — a real " +
                "Acadian wood is mixed, and one species means the gradient is not being asked");

            foreach (var s in species)
                CollectionAssert.Contains(AllTreeSpecies, s,
                    $"'{s}' was planted but is not a species the kit was asked to offer");
        }

        // =============================================================================================
        //  5. DETERMINISM (rule 5)
        // =============================================================================================

        [Test]
        public void TheWoodLotsAreDeterministic_NoRng()
        {
            var again = NineMileCreekWoodLots.ScatterTrees(_terrain, AllTreeSpecies, _ => 4, _farmed);

            Assert.That(again.Count, Is.EqualTo(_lots.Count),
                "two runs of the wood-lot scatter grew different numbers of trees — something in it is not " +
                "a pure function of position (rule 5)");
            for (int i = 0; i < again.Count; i++)
            {
                Assert.That(again[i].Position, Is.EqualTo(_lots[i].Position));
                Assert.That(again[i].Species, Is.EqualTo(_lots[i].Species));
                Assert.That(again[i].Lot, Is.EqualTo(_lots[i].Lot));
            }
        }

        [Test]
        public void TheLotTableSurvivesItsCacheBeingDropped()
        {
            var first = NineMileCreekWoodLots.Zones.Select(z => z.ToString()).ToList();
            NineMileCreekWoodLots.InvalidateCache();
            var second = NineMileCreekWoodLots.Zones.Select(z => z.ToString()).ToList();

            CollectionAssert.AreEqual(first, second,
                "the wood-lot table came back different after its cache was dropped — the memo is hiding " +
                "state, so a build and a test would disagree about where the woods are");
        }

        // =============================================================================================
        //  6. THE COST (rule 7)
        // =============================================================================================

        [Test]
        public void TheWoodLotsFitTheirGameObjectBudget()
        {
            int total = _farmed.Count + _lots.Count;
            Debug.Log($"[NineMileCreekWoodLotTests] CENSUS — {total} tree GameObjects in the hinterland: " +
                      $"{_farmed.Count} farmed ({_farmed.Count(t => t.InHedge)} in hedges) + {_lots.Count} " +
                      $"in {NineMileCreekWoodLots.LotCount} wood lots at " +
                      $"{NineMileCreekWoodLots.LotCoreSpacingMetres} m core spacing, radius " +
                      $"{NineMileCreekWoodLots.LotRadiusMetres} m.");

            Assert.That(total, Is.LessThan(900),
                $"{total} tree GameObjects on one region is past what this coast should carry (rule 7). " +
                "The knobs are NineMileCreekWoodLots.LotCoreSpacingMetres and LotRadiusMetres — thin one " +
                "rather than raising this bar");

            Assert.That(_lots.Count, Is.GreaterThan(60),
                $"the wood lots grew only {_lots.Count} trees between them, which is not woods you can " +
                "walk into");
        }

        // =============================================================================================
        //  7. THE UNDERSTOREY — the floor of the lots
        //
        //  ⭐ This is the region's FIRST and ONLY ask for the shrub kit's `woods` habitat, and it is what
        //  retired NineMileCreekFieldsTests.NoWoodsHabitatIsEverAskedFor. Before this the lots were a
        //  canopy standing on a meadow floor: the hedges give way to a lot, so a lot's ground grew
        //  nothing at all below head height.
        // =============================================================================================

        [Test]
        public void EveryUnderstoreyShrubStandsInsideADeclaredLot()
        {
            Assert.That(_understorey.Count, Is.GreaterThan(0),
                "the wood lots grew no understorey at all — the woods habitat is declared and then never " +
                "reached, which is the state #554 flagged and this pass exists to end");

            foreach (var site in _understorey)
            {
                var lot = WoodlandZones.Require(NineMileCreekWoodLots.Zones, site.Line);
                Assert.IsTrue(lot.Contains(site.Position),
                    $"an understorey shrub at {site.Position} is outside '{site.Line}' — a bounded lot " +
                    "that leaks is not bounded, and on this coast the bound is the whole argument");
                Assert.That(site.Habitat, Is.EqualTo(StPetersShrubs.WoodsHabitat)
                                            .Or.EqualTo("bog").Or.EqualTo("swale"),
                    $"an understorey shrub at {site.Position} is on '{site.Habitat}' ground. Under a " +
                    "canopy the honest answers are the woods understorey and — where the ground is wet — " +
                    "a bog or a swale, which really do occur under trees. `edge` or `barren` there would " +
                    "mean the habitat field has stopped seeing the lot.");
            }
        }

        [Test]
        public void NoUnderstoreyShrubStandsInALanesTread()
        {
            // The owner's ruling cuts the canopy AND what grows under it: a corridor whose trunks were
            // cleared and then filled with waist-high brush is not a corridor.
            foreach (var lot in NineMileCreekWoodLots.Lots)
            {
                var lane = WoodlandZones.Require(NineMileCreekWoodLots.Zones,
                                                 lot.Name + NineMileCreekWoodLots.LaneSuffix);
                int inTread = _understorey.Count(s => lane.SpineDistance(s.Position) < lane.Radius);
                Assert.That(inTread, Is.Zero,
                    $"{inTread} understorey shrub(s) stand in the tread of '{lane.Name}' — the lane is cut " +
                    "for the trees and grown over by the shrubs, which leaves the player walking through a " +
                    "thicket on a path that is declared clear");
            }
        }

        [Test]
        public void Sabotage_AnUnderstoreyThatIgnoresTheCutLanes_FillsTheirTreads()
        {
            // ⭐ "A cutout that stops cutting must fail", applied to the new layer. The cut reaches the
            // understorey through ONE gate — NineMileCreekFields.IsWoodyGround asking
            // NineMileCreekWoodLots.IsCleared — so the arm rebuilds the scatter with exactly that gate
            // removed and nothing else changed. If the treads stay clear anyway, the test above is passing
            // on something incidental (the fringe roll, say) rather than on the corridor being cut.
            var withoutTheCut = WoodlandZones.ScatterUnderstorey(
                NineMileCreekWoodLots.Zones, _terrain,
                p => NineMileCreekFields.IsFieldGround(_terrain, p,
                         NineMileCreekFields.FieldFloorElevation)
                     && !NineMileCreekFields.OnAnyCarriageway(p, NineMileCreekFields.RoadVergeMetres)
                     && !NineMileCreekFields.OnAnyPad(p, NineMileCreekFields.RoadVergeMetres)
                     && !NineMileCreekFields.OnATownLot(p, NineMileCreekFields.TownLotBreathingMetres)
                     && !NineMileCreekFields.OnAWorkingSite(p),
                4, new List<Vector2>());

            int inTreads = 0;
            foreach (var lot in NineMileCreekWoodLots.Lots)
            {
                var lane = WoodlandZones.Require(NineMileCreekWoodLots.Zones,
                                                 lot.Name + NineMileCreekWoodLots.LaneSuffix);
                inTreads += withoutTheCut.Count(s => lane.SpineDistance(s.Position) < lane.Radius);
            }

            Assert.That(inTreads, Is.GreaterThan(3),
                "with the cut-lane gate removed the lanes' treads still came back empty of shrubs — so the " +
                "understorey is not actually being cut out of the corridors, and the test that says it is " +
                "is passing on something else");
        }

        [Test]
        public void TheUnderstoreyIsSparserThanTheCanopyOverIt()
        {
            // ⭐ THE RULING AS A MEASUREMENT: "an understorey reads as depth, not as a second canopy."
            // WoodlandZones.UnderstoreyTrunksPerShrub is the dial and this is what holds it honest — a
            // shrub layer as dense as the trees is a thicket, and you cannot see into a thicket.
            //
            // ⚠ The RATIO is pooled across the three lots and only the weaker claim is made per lot: one
            // 30 m lot is a few dozen candidate cells, so a per-lot 2:1 bar would partly be measuring
            // where its lane happened to fall. The island's twin of this test says the same thing.
            int trunksAll = 0, shrubsAll = 0;

            foreach (var lot in NineMileCreekWoodLots.Lots)
            {
                int trunks = _lots.Count(s => s.Lot == lot.Name)
                             + _farmed.Count(t => lot.Contains(t.Position));
                int shrubs = _understorey.Count(s => s.Line == lot.Name);
                trunksAll += trunks; shrubsAll += shrubs;

                Assert.That(shrubs, Is.GreaterThan(0),
                    $"'{lot.Name}' grew no understorey at all — on this coast the hedges give way to a " +
                    "lot, so that lot's floor is bare ground and nothing else will cover it");
                Assert.That(shrubs, Is.LessThan(trunks),
                    $"'{lot.Name}' carries {shrubs} understorey shrubs under {trunks} trunks — a floor at " +
                    "least as dense as the canopy over it is a thicket, not a wood you can walk into");
            }

            Assert.That(shrubsAll, Is.GreaterThan(15),
                $"the three wood lots grew {shrubsAll} understorey shrubs between them, which is not a " +
                "layer — every other assertion in this section would pass vacuously on it");
            Assert.That(shrubsAll * 2, Is.LessThan(trunksAll),
                $"the lots carry {shrubsAll} understorey shrubs under {trunksAll} trunks. The dial asks " +
                $"for one shrub per {WoodlandZones.UnderstoreyTrunksPerShrub} trunks; at better than one " +
                "in two the layer has stopped reading as depth under a canopy and started reading as a " +
                "second canopy at knee height");

            Debug.Log($"[NineMileCreekWoodLotTests] understorey vs canopy: {shrubsAll} shrubs under " +
                      $"{trunksAll} trunks (1 : {trunksAll / (float)Mathf.Max(1, shrubsAll):0.0}).");
        }

        [Test]
        public void TheUnderstoreyStandsBetweenTheTrunksRatherThanOnThem()
        {
            // Trees pivot at the trunk FOOT and shrubs at the root crown, so a shrub rolled onto a trunk's
            // position is drawn growing out of the tree — the one artefact a layered wood can produce that
            // a single layer cannot.
            // ⚠ ONE assertion over ~60,000 pairs, not 60,000 assertions: the nearest neighbour is found
            // first and the constraint runs once. NUnit's constraint machinery is not free and this
            // fixture already walks two whole scatters.
            var standing = Standing();
            float worst = float.MaxValue;
            Vector2 worstShrub = Vector2.zero, worstNeighbour = Vector2.zero;

            foreach (var s in _understorey)
            foreach (var p in standing)
            {
                float d = Vector2.Distance(s.Position, p);
                if (d >= worst) continue;
                worst = d; worstShrub = s.Position; worstNeighbour = p;
            }

            Assert.That(worst, Is.GreaterThanOrEqualTo(WoodlandZones.MinUnderstoreyGapMetres - 1e-3f),
                $"the closest an understorey shrub gets to something already planted is {worst:0.00} m — " +
                $"a shrub at {worstShrub} against a trunk or hedge at {worstNeighbour}, inside the " +
                $"{WoodlandZones.MinUnderstoreyGapMetres:0.00} m that keeps a shrub BESIDE a trunk rather " +
                "than drawn growing out of one");
        }

        [Test]
        public void TheUnderstoreyThinsTowardTheLotsRims()
        {
            // The same taper the canopy has, read the same way — proof that the fringe roll is being
            // asked rather than ignored. ⚠ POOLED ACROSS THE LOTS, unlike the canopy's twin: an
            // understorey is a fraction of the trunks over it by design, so a single 30 m lot carries a
            // couple of dozen shrubs and a per-lot core/fringe split of that is noise. The claim is the
            // same claim; only the sample is wider.
            float coreTotal = 0f, fringeTotal = 0f;
            int inCore = 0, inFringe = 0;

            foreach (var lot in NineMileCreekWoodLots.Lots)
            {
                float coreRadius = lot.Radius * NineMileCreekWoodLots.LotCoreFraction;
                float coreArea = Mathf.PI * coreRadius * coreRadius;
                coreTotal += coreArea;
                fringeTotal += Mathf.PI * lot.Radius * lot.Radius - coreArea;

                foreach (var s in _understorey.Where(s => s.Line == lot.Name))
                    if (lot.SpineDistance(s.Position) <= coreRadius) inCore++; else inFringe++;
            }

            float coreDensity = inCore / coreTotal;
            float fringeDensity = inFringe / fringeTotal;

            Assert.That(coreDensity, Is.GreaterThan(fringeDensity * 1.5f),
                $"the understorey carries {coreDensity * 1000f:0.0} shrubs per 1000 m² in the lots' cores " +
                $"against {fringeDensity * 1000f:0.0} in their fringes. A wood whose trees thin out at the " +
                "rim while its brush runs flat to a hard edge reads as a hedge round a plantation");
        }

        [Test]
        public void TheUnderstoreyIsDeterministic_NoRng()
        {
            var again = NineMileCreekWoodLots.ScatterUnderstorey(_terrain, 4, Standing());

            Assert.That(again.Count, Is.EqualTo(_understorey.Count),
                "two runs of the understorey scatter grew different numbers of shrubs — something in it " +
                "is not a pure function of position (rule 5)");
            for (int i = 0; i < again.Count; i++)
            {
                Assert.That(again[i].Position, Is.EqualTo(_understorey[i].Position));
                Assert.That(again[i].Habitat, Is.EqualTo(_understorey[i].Habitat));
                Assert.That(again[i].Line, Is.EqualTo(_understorey[i].Line));
                Assert.That(again[i].Variant, Is.EqualTo(_understorey[i].Variant));
            }
        }

        [Test]
        public void TheUnderstoreyIsNotAHedgerowAndTheHedgerowsDidNotMove()
        {
            // ⚠ TWO POPULATIONS, RETURNED BY TWO CALLS, AND THEY MUST NOT MERGE. Every hedge shrub is
            // explicable by the line that owns it (NineMileCreekFieldsTests' "derive, never eyeball"
            // test); an understorey shrub carries a LOT's name in the same field and would fail it. This
            // is the seam said out loud, so a later pass that concatenates the two lists for convenience
            // fails here with the reason rather than there with a puzzle.
            var lotNames = new HashSet<string>(NineMileCreekWoodLots.Lots.Select(l => l.Name));

            foreach (var s in _understorey)
                Assert.IsTrue(lotNames.Contains(s.Line),
                    $"an understorey shrub claims to belong to '{s.Line}', which is not one of this " +
                    $"region's wood lots [{string.Join(", ", lotNames)}]");

            foreach (var s in _hedges)
                Assert.IsFalse(lotNames.Contains(s.Line),
                    $"a hedge shrub claims to belong to the wood lot '{s.Line}' — the two shrub " +
                    "populations have merged");
        }

        [Test]
        public void TheUnderstoreyFitsItsGameObjectBudget()
        {
            int total = _hedges.Count + _understorey.Count;
            Debug.Log($"[NineMileCreekWoodLotTests] CENSUS — {total} shrub GameObjects in the hinterland: " +
                      $"{_hedges.Count} hedgerow + {_understorey.Count} understorey in " +
                      $"{NineMileCreekWoodLots.LotCount} wood lots, at one shrub per " +
                      $"{WoodlandZones.UnderstoreyTrunksPerShrub} trunks over them.");

            // The understorey is bounded by the lots and the lots are bounded by MaxHinterlandShare, so
            // this cannot run away without one of those failing first — which is exactly why the bar is
            // set against the layer it was ADDED to rather than at some absolute number: if the shrubs
            // ever outnumber the hedges, a bounded 4% of the hinterland is carrying more brush than every
            // field boundary and gravel road in the region put together, and something is wrong upstream.
            Assert.That(_understorey.Count, Is.LessThan(_hedges.Count),
                $"{_understorey.Count} understorey shrubs against {_hedges.Count} in the hedgerows — the " +
                "lots are a few percent of the hinterland and cannot honestly carry more shrubs than every " +
                "hedge in the region");
        }
    }
}
