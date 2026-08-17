using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>ST PETERS' WOODLAND ZONES — are the woods denser, is the ground still readable, and did the
    /// clearings survive being moved into a table?</b>
    ///
    /// <para>The owner ruled on 2026-08-16 that both regions get denser, more forest-like woods and that
    /// <b>paths are cut, not implied</b>. Three things can go wrong with that and each has a test here:
    /// the density can arrive as a cost nobody measured (<see cref="TheIslandsForestFitsItsBudget"/>), a
    /// zone can be dropped on top of something the island needs
    /// (<see cref="NoZoneStandsOnGroundThatMustStayClear"/>), and a "cut" corridor can quietly stop
    /// cutting anything (<see cref="TheGinnyLaneIsCutThroughWoodsThatWouldOtherwiseCloseOverIt"/>).</para>
    ///
    /// <para><b>The seam this pass inherited.</b> #551 shipped
    /// <c>StPetersGinnyPlot.IsInsideClearing</c> asked from one line in <c>StPetersWoods.IsPlantable</c>,
    /// so a zone table could take it over by name. It has, and
    /// <see cref="GinnysDooryardIsStillHeldOpen_ThroughTheTableNow"/> is the test that the takeover did not
    /// quietly stop keeping trees off her garden.</para>
    /// </summary>
    public class StPetersWoodlandZoneTests
    {
        private GameObject _go;
        private TidalTerrain _terrain;

        /// <summary>The nine species the pass-2 kit ships. Passed explicitly rather than scanned, so this
        /// class asserts the same thing on a machine with a partly-baked kit.</summary>
        static readonly string[] NineSpecies =
        {
            "RedSpruce", "BlackSpruce", "BalsamFir", "WhitePine", "WhiteCedar",
            "WhiteBirch", "RedMaple", "RedOak", "TremblingAspen",
        };

        private List<StPetersWoods.TreeSite> _ambient;
        private List<WoodlandZones.LotTreeSite> _lots;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("StPetersTerrain_WoodlandZoneTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);

            // ⚠ The cache is dropped so no test in this class can be passing on a table another one built.
            StPetersWoodlandZones.InvalidateCache();

            _ambient = StPetersWoods.ScatterTrees(_terrain, NineSpecies, _ => 4);
            _lots = StPetersWoods.ScatterLotTrees(_terrain, NineSpecies, _ => 4, _ambient);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            StPetersWoodlandZones.InvalidateCache();
        }

        // =============================================================================================
        //  1. THE TABLE IS WELL FORMED
        // =============================================================================================

        [Test]
        public void TheTableDeclaresLotsAndALaneThroughEveryOneOfThem()
        {
            var zones = StPetersWoodlandZones.Zones;
            Assert.That(zones.Count, Is.GreaterThan(0), "the island declares no woodland zones at all");

            var lots = StPetersWoodlandZones.Lots.ToList();
            Assert.That(lots.Count, Is.GreaterThan(0),
                "no woodland LOTS — the owner's 2026-08-16 ruling was for denser, more forest-like woods, " +
                "and the lots are where that density lives");
            Assert.That(StPetersWoodlandZones.LotCount, Is.EqualTo(lots.Count),
                "LotCount and the table disagree — the build's own log line reads LotCount");

            // ⭐ A DENSE WOOD WITH NO WAY THROUGH IT is exactly what "paths are cut, not implied" rules
            // against, so the lane is not optional decoration on a lot: it ships with it.
            foreach (var lot in lots)
            {
                string laneName = lot.Name + StPetersWoodlandZones.LaneSuffix;
                var lane = zones.FirstOrDefault(z => z.Name == laneName);
                Assert.That(lane.Name, Is.EqualTo(laneName),
                    $"the lot '{lot.Name}' has no lane '{laneName}' cut through it — a dense stand with no " +
                    "corridor is the thing the owner's ruling is against");
                Assert.That(lane.Kind, Is.EqualTo(WoodlandZoneKind.Corridor));

                // It must actually CROSS the lot, not stop inside it: a lane that ends in a wood is a dead
                // end, which reads worse than no lane at all.
                Assert.That(lane.SpineLength, Is.GreaterThanOrEqualTo(lot.Radius * 2f),
                    $"'{laneName}' is {lane.SpineLength:0.0} m long against a {lot.Radius * 2f:0.0} m lot — " +
                    "it stops inside the wood instead of coming out the other side");
            }
        }

        [Test]
        public void NoTwoZonesOfTheSameKindStandOnEachOther()
        {
            // Lanes are EXPECTED to overlap their own lots — that is what cutting through means — so the
            // rule is per-kind: two lots on each other are one lot counted twice, and two lanes on each
            // other are a crossroads nobody authored.
            var zones = StPetersWoodlandZones.Zones;
            for (int i = 0; i < zones.Count; i++)
            for (int j = i + 1; j < zones.Count; j++)
            {
                if (zones[i].Kind != zones[j].Kind) continue;
                Assert.IsFalse(zones[i].Overlaps(zones[j]),
                    $"{zones[i]} and {zones[j]} share ground — two zones of one kind on one patch is a " +
                    "double declaration, and the second one's dial does nothing");
            }
        }

        [Test]
        public void EveryLotSitsOnShelteredInteriorGround_WhereAWoodBelongs()
        {
            foreach (var lot in StPetersWoodlandZones.Lots)
            {
                // A lot DECLARES a wood, so it cannot be sited where the habitat model would only ever
                // offer it a salt-blasted fringe — that would be a wood standing in a wind the island's
                // own exposure field says nothing grows in.
                float shelter = StPetersWoods.InlandFraction(lot.From);
                Assert.That(shelter, Is.GreaterThan(0.5f),
                    $"'{lot.Name}' at {lot.From} has an inland fraction of {shelter:0.00} — that is the " +
                    "windswept fringe, and a declared wood there fights the habitat gradient instead of " +
                    "using it");

                Assert.That(_terrain.ElevationAt(lot.From),
                    Is.GreaterThanOrEqualTo(StPetersWoods.TreeLineElevation),
                    $"'{lot.Name}' is centred below the tree line — trees do not grow to the tideline");
            }
        }

        // =============================================================================================
        //  2. NOTHING IS BURIED UNDER A ZONE  (+ the sabotage arm)
        // =============================================================================================

        /// <summary>
        /// Everything on this island a woodland LOT must never be dropped on, with the radius each one
        /// needs. Every radius is the region's own PUBLISHED number, so a re-sited building or a re-ruled
        /// clearance moves its own exclusion rather than needing this list re-typed.
        ///
        /// <para>⚠ The five village buildings are covered by the HEARTH row and are deliberately not listed
        /// separately: <see cref="StPetersWoods.VillageClearingRadius"/> is 44 m precisely because it
        /// "contains every one of them with room to walk between" (its own words), so a lot that clears the
        /// hearth row cannot be standing on the school. Listing them again with a radius invented here
        /// would be a second opinion about a question the village already answers.</para>
        /// </summary>
        static IEnumerable<(string what, Vector2 at, float radius)> MustStayClear()
        {
            yield return ("the village clearing (and so every building in it)",
                          StPetersBuilder.VillageHearthPos, StPetersWoods.VillageClearingRadius);
            yield return ("the start spawn", StPetersBuilder.StartSpawnPos,
                          StPetersWoods.SpawnClearingRadius);
            yield return ("the dock", StPetersBuilder.DockZonePos, StPetersWoods.DockClearance);
            yield return ("Ginny's plot", StPetersGinnyPlot.CottagePos,
                          StPetersGinnyPlot.RequiredClearingRadius());
            yield return ("the crossing's approach", StPetersBuilder.SandbarFrom,
                          StPetersWoods.CrossingClearance);
        }

        /// <summary>
        /// The check both <see cref="NoZoneStandsOnGroundThatMustStayClear"/> and its sabotage arm run —
        /// written once so the arm proves THIS logic bites rather than proving something adjacent to it.
        /// A LOT is the thing that must keep off; a clearing or a lane over a building is harmless (they
        /// remove trees, which a building's ground wants anyway).
        /// </summary>
        static List<string> LotConflicts(IReadOnlyList<WoodlandZone> zones)
        {
            var found = new List<string>();
            foreach (var zone in zones)
            {
                if (zone.Kind != WoodlandZoneKind.Lot) continue;
                foreach (var (what, at, radius) in MustStayClear())
                    if (zone.SpineDistance(at) < zone.Radius + radius)
                        found.Add($"the woodland lot '{zone.Name}' reaches {what} at {at} " +
                                  $"(spine {zone.SpineDistance(at):0.0} m away, needs " +
                                  $"{zone.Radius + radius:0.0} m)");
            }
            return found;
        }

        [Test]
        public void NoZoneStandsOnGroundThatMustStayClear()
        {
            var conflicts = LotConflicts(StPetersWoodlandZones.Zones);
            Assert.That(conflicts, Is.Empty,
                "a woodland lot was sited on ground the island needs: " + string.Join("; ", conflicts));
        }

        [Test]
        public void Sabotage_ALotDroppedOnTheSchoolhouse_IsRejected()
        {
            // ⭐ The arm the handoff asks for by name: "a zone over the schoolhouse must fail". Without it
            // the test above passes on any table, including an empty one.
            //
            // It is caught by the VILLAGE CLEARING row rather than by a schoolhouse row, and that is the
            // mechanism working as designed: the school stands inside the 44 m clearing, which exists
            // because it "contains every one of them with room to walk between".
            var bad = StPetersWoodlandZones.Zones.ToList();
            bad.Add(WoodlandZone.Circle("SchoolhouseWood", WoodlandZoneKind.Lot,
                                        StPetersBuilder.SchoolPos, 25f,
                                        StPetersWoodlandZones.LotCoreFraction,
                                        StPetersWoodlandZones.LotCoreSpacingMetres,
                                        "deliberately wrong: a wood planted through the one-room school"));

            var conflicts = LotConflicts(bad);
            Assert.That(conflicts, Is.Not.Empty,
                "a woodland lot planted straight through the schoolhouse was NOT rejected — the " +
                "clearance check above is decoration");
            Assert.That(string.Join("; ", conflicts), Does.Contain("SchoolhouseWood"),
                "something was rejected but not the bad lot, so the arm is not testing what it says");

            // And prove the school really is the ground being protected here, rather than the arm passing
            // because 25 m happens to reach some other exclusion.
            Assert.That(Vector2.Distance(StPetersBuilder.SchoolPos, StPetersBuilder.VillageHearthPos),
                Is.LessThan(StPetersWoods.VillageClearingRadius),
                "the schoolhouse is no longer inside the village clearing, so this arm has stopped being " +
                "about the schoolhouse — give it a row of its own in MustStayClear");
        }

        // =============================================================================================
        //  3. THE CUT LANES ACTUALLY CUT  (each test carries its own not-vacuous arm)
        // =============================================================================================

        [Test]
        public void TheGinnyLaneIsCutThroughWoodsThatWouldOtherwiseCloseOverIt()
        {
            var lane = WoodlandZones.Require(StPetersWoodlandZones.Zones,
                                             StPetersWoodlandZones.GinnyLaneZone);

            // The stretch of her walk that belongs to NEITHER clearing: past the village's 44 m and short
            // of her own 18 m. Sampling only here is what keeps this test about the LANE rather than about
            // two clearings that were already holding the ends open.
            var open = new List<Vector2>();
            for (float t = 0f; t <= 1f; t += 0.005f)
            {
                Vector2 p = Vector2.Lerp(lane.From, lane.To, t);
                if (Vector2.Distance(p, StPetersBuilder.VillageHearthPos)
                        < StPetersWoods.VillageClearingRadius + lane.Radius) continue;
                if (Vector2.Distance(p, StPetersGinnyPlot.CottagePos)
                        < StPetersGinnyPlot.ClearingRadius + lane.Radius) continue;
                open.Add(p);
            }
            Assert.That(open.Count, Is.GreaterThan(20),
                "her walk has almost no stretch outside the two clearings, so this test cannot see whether " +
                "the lane cuts anything — re-site or re-measure rather than deleting the assert");

            // (a) NOTHING STANDS IN THE TREAD. She walks 85 m of this twice a day (StPetersRoutines'
            //     LaneGinnyPlot, 1.13 game hours each way) and a trunk in it is a person walking through a
            //     tree — the exact bug StPetersWoods.PathClearance was written to fix for the painted paths.
            foreach (var tree in _ambient.Select(t => t.Position).Concat(_lots.Select(l => l.Position)))
                Assert.That(lane.SpineDistance(tree), Is.GreaterThanOrEqualTo(lane.Radius),
                    $"a tree at {tree} stands {lane.SpineDistance(tree):0.0} m from the centre of Ginny's " +
                    $"lane, inside its {lane.Radius:0.0} m tread — she walks through it twice a day");

            // (b) AND THE LANE IS CUT THROUGH SOMETHING. ⭐ THE NOT-VACUOUS ARM: if the corridor were
            //     re-sited across open meadow — or out over the flats — (a) would pass for free and the
            //     "cut path" would be a no-op. So the island's woods have to actually reach this ground.
            //
            //     ⚠ Measured as PLANTED TREES in a generous neighbourhood rather than as stand-field
            //     samples on the spine, and the reason is the mosaic's own grain: StandScale is 46 m, so a
            //     24 m stretch of lane can sit wholly inside one meadow gap by luck and a spine-sample
            //     assert would then red on a corridor that is perfectly well sited. A 25 m neighbourhood
            //     spans a feature of the mosaic either way.
            const float neighbourhoodMetres = 25f;
            int nearby = _ambient.Count(t => open.Any(p =>
                Vector2.Distance(p, t.Position) <= neighbourhoodMetres));

            Assert.That(nearby, Is.GreaterThan(2),
                $"only {nearby} trees stand within {neighbourhoodMetres:0} m of the open stretch of Ginny's " +
                "lane — the corridor is cut across ground the woods do not reach, so it cuts nothing and " +
                "'paths are cut, not implied' is satisfied on a technicality");
        }

        [Test]
        public void EveryLotsLaneIsCutThroughItsOwnWood()
        {
            foreach (var lot in StPetersWoodlandZones.Lots)
            {
                var lane = WoodlandZones.Require(StPetersWoodlandZones.Zones,
                                                 lot.Name + StPetersWoodlandZones.LaneSuffix);

                // (a) the tread is clear …
                int inTread = _lots.Count(s => s.Lot == lot.Name && lane.SpineDistance(s.Position) < lane.Radius);
                Assert.That(inTread, Is.Zero,
                    $"{inTread} trees of '{lot.Name}' stand in its own cut lane — the corridor is declared " +
                    "and then planted over");

                // (b) … and the wood it is cut through is a real wood. A lane through an empty lot is not
                //     a corridor, it is a line on a map.
                int inLot = _lots.Count(s => s.Lot == lot.Name);
                Assert.That(inLot, Is.GreaterThan(20),
                    $"'{lot.Name}' grew only {inLot} trees, so its lane runs through nothing much and " +
                    "this test would pass on an empty declaration");
            }
        }

        // =============================================================================================
        //  4. THE LOTS ARE WOODS — denser in the core, thinning at the rim
        // =============================================================================================

        [Test]
        public void EveryLotTreeStandsInsideADeclaredLot()
        {
            foreach (var site in _lots)
            {
                var lot = WoodlandZones.Require(StPetersWoodlandZones.Zones, site.Lot);
                Assert.IsTrue(lot.Contains(site.Position),
                    $"a tree the lot pass planted at {site.Position} is outside '{site.Lot}' — a bounded " +
                    "lot that leaks is not bounded");
                Assert.That(site.CoreShare, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            }
        }

        /// <summary>Core and fringe trunk densities (per m²) for a set of positions inside a lot.</summary>
        static (float core, float fringe) Densities(WoodlandZone lot, IEnumerable<Vector2> trunks)
        {
            float coreRadius = lot.Radius * StPetersWoodlandZones.LotCoreFraction;
            var inside = trunks.Where(lot.Contains).ToList();
            int inCore = inside.Count(p => lot.SpineDistance(p) <= coreRadius);

            float coreArea = Mathf.PI * coreRadius * coreRadius;
            float fringeArea = Mathf.PI * lot.Radius * lot.Radius - coreArea;
            return (inCore / coreArea, (inside.Count - inCore) / fringeArea);
        }

        /// <summary>
        /// ⭐ <b>THE TAPER, measured on the mechanism this pass owns.</b> Every lot's OWN planting must
        /// thin toward its rim — that is what <see cref="WoodlandZone.PlantingShare"/> is for and the only
        /// part of the picture the zone table controls.
        ///
        /// <para><b>⚠ Ambient trees are deliberately excluded here, and that is a correction rather than a
        /// convenience.</b> The first version measured ambient + lot together and EastWood failed at
        /// 41.4 core against 36.3 fringe — because a natural stand of the reverting mosaic happens to abut
        /// that lot's rim. That is not a defect: <b>a wood lot whose edge runs into another patch of woods
        /// has no visible edge there, which is what real ground does.</b> Measuring the combined picture
        /// made the test a hostage to where the noise field put its stands. The aggregate claim is still
        /// made, one test down, where an adjacency cannot red it.</para>
        /// </summary>
        [Test]
        public void EveryLotsOwnPlantingThinsTowardItsRim()
        {
            foreach (var lot in StPetersWoodlandZones.Lots)
            {
                var (core, fringe) = Densities(lot, _lots.Where(s => s.Lot == lot.Name)
                                                         .Select(s => s.Position));

                Assert.That(core, Is.GreaterThan(fringe * 1.5f),
                    $"'{lot.Name}' plants {core * 1000f:0.0} trunks per 1000 m² in its core against " +
                    $"{fringe * 1000f:0.0} in its fringe — the ruling asked for edges that READ, and a lot " +
                    "planted flat to a hard rim reads as a stamp. The dial is " +
                    "WoodlandZone.PlantingShare.");
            }
        }

        /// <summary>
        /// …and the aggregate claim the per-lot one gave up: taken together, the ground inside the lots'
        /// CORES carries more trunks than the ground in their fringes, ambient mosaic and all. This is what
        /// the player actually sees, and summing across the lots is what makes it robust to one of them
        /// running into a natural stand.
        /// </summary>
        [Test]
        public void TakenTogether_TheLotCoresAreTheDensestGroundOnTheIsland()
        {
            var all = _ambient.Select(t => t.Position).Concat(_lots.Select(l => l.Position)).ToList();

            float coreSum = 0f, fringeSum = 0f;
            foreach (var lot in StPetersWoodlandZones.Lots)
            {
                var (core, fringe) = Densities(lot, all);
                coreSum += core;
                fringeSum += fringe;
            }

            Assert.That(coreSum, Is.GreaterThan(fringeSum * 1.2f),
                $"across the {StPetersWoodlandZones.LotCount} lots the cores carry " +
                $"{coreSum / StPetersWoodlandZones.LotCount * 1000f:0.0} trunks per 1000 m² against the " +
                $"fringes' {fringeSum / StPetersWoodlandZones.LotCount * 1000f:0.0} — the woods are not " +
                "closing where they were declared to close");
        }

        [Test]
        public void NoTwoTrunksStandInsideEachOther()
        {
            // ⚠ The one artefact a DENSE stand can make that a sparse one cannot. Trees pivot at the trunk
            // foot, so two trunks a metre apart read as one fat tree with two crowns rather than as a
            // closed wood — which is the whole thing the lot pass is trying to draw.
            var all = _ambient.Select(t => t.Position).Concat(_lots.Select(l => l.Position)).ToList();

            // Only pairs the LOT pass could have made: it is the pass that promises a min gap, and the
            // ambient grid's own jitter law is a separate, already-tested claim. Collected rather than
            // asserted per pair — a few hundred lot trees against a few hundred trunks is ~10⁵ pairs, and
            // an NUnit assert each would put a minute of constraint-building into the suite.
            // ⚠ Self-skip by INDEX, not by `other == site.Position`. Unity's Vector2 == is an approximate
            // compare (~1e-5 m), so the identity test would also swallow a genuine pair of distinct trunks
            // planted on top of each other — masking the exact defect this test exists to find.
            var lotPositions = _lots.Select(s => s.Position).ToList();
            int ambientCount = _ambient.Count;

            var violations = new List<string>();
            for (int i = 0; i < _lots.Count && violations.Count < 5; i++)
            for (int j = 0; j < all.Count; j++)
            {
                if (j == ambientCount + i) continue;          // the site itself
                float d = Vector2.Distance(lotPositions[i], all[j]);
                if (d >= WoodlandZones.MinTrunkGapMetres - 0.01f) continue;
                violations.Add($"'{_lots[i].Lot}' put a trunk at {lotPositions[i]}, {d:0.00} m from one " +
                               $"at {all[j]}");
                break;
            }

            Assert.That(violations, Is.Empty,
                $"trunks closer than the {WoodlandZones.MinTrunkGapMetres:0.0} m gap, so they read as one " +
                "tree with two crowns: " + string.Join("; ", violations));
        }

        // =============================================================================================
        //  5. GINNY'S CLEARING SURVIVED THE TAKEOVER
        // =============================================================================================

        [Test]
        public void GinnysDooryardIsStillHeldOpen_ThroughTheTableNow()
        {
            // The #551 seam: her predicate was the one call StPetersWoods.IsPlantable made about zones, and
            // this pass spent it. The takeover must not have stopped keeping trees off her garden.
            foreach (var tree in _ambient.Select(t => t.Position).Concat(_lots.Select(l => l.Position)))
                Assert.That(Vector2.Distance(tree, StPetersGinnyPlot.CottagePos),
                    Is.GreaterThanOrEqualTo(StPetersGinnyPlot.ClearingRadius),
                    $"a tree at {tree} stands inside Ginny's {StPetersGinnyPlot.ClearingRadius:0.#} m " +
                    "clearing — the zone table took her predicate over and stopped answering it");

            // And her own narrow question still answers narrowly. Asking the table "is this ground cleared"
            // instead of "is this Ginny's" would put the village green and every lane on her land.
            Assert.IsTrue(StPetersGinnyPlot.IsInsideClearing(StPetersGinnyPlot.CottagePos),
                "her own cottage is not on her land");
            Assert.IsTrue(StPetersGinnyPlot.IsInsideClearing(StPetersGinnyPlot.GardenPos),
                "her garden is not on her land");
            Assert.IsFalse(StPetersGinnyPlot.IsInsideClearing(StPetersBuilder.VillageGreen),
                "the VILLAGE GREEN reports as being inside Ginny's clearing — IsInsideClearing is " +
                "answering 'is this ground cleared' instead of 'is this hers', which is a different " +
                "question and the wrong one for a caller siting something on her plot");
        }

        [Test]
        public void GinnysStandIsGenuinelyWoodedNow()
        {
            // #551 measured 12 trees in her 18–34 m ring at the OLD density and the handoff asks for the
            // plot to "read genuinely wooded after". Same ring, same measurement, so the two numbers are
            // comparable — and the ring starts at her clearing's edge, so this cannot be satisfied by
            // planting through her dooryard.
            const float ringOuterMetres = 34f;
            int inRing = _ambient.Select(t => t.Position)
                                 .Concat(_lots.Select(l => l.Position))
                                 .Count(p =>
                                 {
                                     float d = Vector2.Distance(p, StPetersGinnyPlot.CottagePos);
                                     return d >= StPetersGinnyPlot.ClearingRadius && d <= ringOuterMetres;
                                 });

            Assert.That(inRing, Is.GreaterThan(12),
                $"only {inRing} trees stand in the 18–34 m ring around Ginny's cottage. #551 measured 12 " +
                "there at the OLD density, and the owner's 2026-08-16 ruling was for denser woods — her " +
                "plot is supposed to read as being IN them");
        }

        // =============================================================================================
        //  6. THE COST  (rule 7 — the budget is a feature)
        // =============================================================================================

        [Test]
        public void TheIslandsForestFitsItsBudget()
        {
            int ambient = _ambient.Count;
            int lots = _lots.Count;
            int total = ambient + lots;

            // Logged unconditionally: these are the numbers the PR quotes, and the density knobs were
            // tuned from this line rather than by eye. Per-lot core/fringe densities are in here too,
            // because that ratio is the one thing about a lot that cannot be read off its radius.
            var all = _ambient.Select(t => t.Position).Concat(_lots.Select(l => l.Position)).ToList();
            var perLot = new List<string>();
            foreach (var l in StPetersWoodlandZones.Lots)
            {
                var (ownCore, ownFringe) = Densities(l, _lots.Where(s => s.Lot == l.Name)
                                                             .Select(s => s.Position));
                var (allCore, allFringe) = Densities(l, all);
                int ambientInside = _ambient.Count(t => l.Contains(t.Position));

                perLot.Add($"{l.Name}: {_lots.Count(s => s.Lot == l.Name)} lot trees + {ambientInside} " +
                           $"ambient; own taper {ownCore * 1000f:0.0}/{ownFringe * 1000f:0.0}, combined " +
                           $"{allCore * 1000f:0.0}/{allFringe * 1000f:0.0} trunks per 1000 m² core/fringe");
            }

            // ⭐ The two bars StPetersWoodsTests holds the MOSAIC to, measured here as well, so this one
            // line says how much headroom the ambient thickening has left. §5.1's reverting interior caps
            // the wooded fraction at 0.7 and the sabotage arm needs plantable > standing × 1.5.
            int stand = 0, open = 0;
            for (float x = -150f; x < 290f; x += 4f)
            for (float y = -125f; y < 125f; y += 4f)
            {
                var p = new Vector2(x, y);
                if (!StPetersWoods.IsPlantable(_terrain, p)) continue;
                if (StPetersWoods.InStand(p, _terrain.ElevationAt(p))) stand++; else open++;
            }
            float wooded = stand / (float)Mathf.Max(1, stand + open);

            Debug.Log($"[StPetersWoodlandZoneTests] CENSUS — {total} trees on St Peters: {ambient} in the " +
                      $"reverting mosaic (TreeStep {StPetersWoods.TreeStep} m, StandThreshold " +
                      $"{StPetersWoods.StandThreshold}) + {lots} closing " +
                      $"{StPetersWoodlandZones.LotCount} woodland lots at " +
                      $"{StPetersWoodlandZones.LotCoreSpacingMetres} m core spacing " +
                      $"[{string.Join("; ", perLot)}]. MOSAIC HEADROOM: wooded fraction {wooded:P1} " +
                      "against the 70% ceiling; sabotage margin plantable/standing " +
                      $"{(stand + open) / (float)Mathf.Max(1, stand):0.00} against the 1.50 floor.");

            Assert.That(ambient, Is.GreaterThan(40),
                $"only {ambient} ambient trees — §5.1 wants interior cover, and the mosaic is still the " +
                "thing that provides it between the lots");

            // ⚠ The ceiling is on the TOTAL, not on either pass. Splitting a population across two
            // scatters is exactly how a GameObject budget gets evaded without anyone lying.
            Assert.That(total, Is.LessThan(900),
                $"{total} tree GameObjects on one region is past what this island should carry (rule 7). " +
                "The density knobs are StPetersWoods.TreeStep for the mosaic and " +
                "StPetersWoodlandZones.LotCoreSpacingMetres / LotRadius for the lots — thin one of them " +
                "rather than raising this bar");

            Assert.That(lots, Is.GreaterThan(60),
                $"the three woodland lots grew only {lots} trees between them, which is not the denser, " +
                "more forest-like woods the owner ruled for");
        }

        // =============================================================================================
        //  7. DETERMINISM (rule 5)
        // =============================================================================================

        [Test]
        public void TheLotPlantingIsDeterministic_NoRng()
        {
            var again = StPetersWoods.ScatterLotTrees(_terrain, NineSpecies, _ => 4, _ambient);

            Assert.That(again.Count, Is.EqualTo(_lots.Count),
                "two runs of the lot scatter grew different numbers of trees — something in it is not a " +
                "pure function of position (rule 5)");
            for (int i = 0; i < again.Count; i++)
            {
                Assert.That(again[i].Position, Is.EqualTo(_lots[i].Position));
                Assert.That(again[i].Species, Is.EqualTo(_lots[i].Species));
                Assert.That(again[i].Variant, Is.EqualTo(_lots[i].Variant));
                Assert.That(again[i].Lot, Is.EqualTo(_lots[i].Lot));
            }
        }

        [Test]
        public void TheZoneTableIsAPureFunctionOfTheRegionsConstants()
        {
            var first = StPetersWoodlandZones.Zones.Select(z => z.ToString()).ToList();
            StPetersWoodlandZones.InvalidateCache();
            var second = StPetersWoodlandZones.Zones.Select(z => z.ToString()).ToList();

            CollectionAssert.AreEqual(first, second,
                "the zone table came back different after its cache was dropped — the memo is hiding " +
                "state, and a build and a test would disagree about where the woods are");
        }
    }
}
