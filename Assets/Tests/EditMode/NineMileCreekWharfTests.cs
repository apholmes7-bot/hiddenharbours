using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE SQUARED-U WHARF ON THE MADE SPIT</b> — an 84 × 10 m north wall the fleet moors along, a
    /// 10 × 48 m west wall carrying the unloading apron, and a 92 m crib breakwater sheltering the basin.
    ///
    /// <para><b>What changed, and why these tests are different in kind.</b> The quay used to be 48 cells
    /// of the wharf tile kit over an 8 × 6 m deck, and most of this file's ancestor was about the KIT: the
    /// back-to-front draw order, which auto-tile variant each cell asks for, that a rectangle never needs
    /// an end cap. None of that survives, because the quay is not drawn here any more — A-1 authors both
    /// walls as terrain FILLS, and the drawn ISO quay is Phase B's (owner's ruling, 2026-08-07: the old
    /// kit does not scale to an 84 m wall and is ruled for migration). Testing a draw order that no longer
    /// happens would be testing nothing.</para>
    ///
    /// <para><b>So these test what a quay must be true of whatever it is drawn with:</b> that the walls
    /// ARE the ground the terrain says is there, that you can stand on them, that a boat lies alongside on
    /// the ruled gate rather than on dry land, that every berth has something to tie to, and that the
    /// breakwater shelters the basin without closing the way in.</para>
    ///
    /// <para><b>Every bound is DERIVED from <see cref="NineMileCreekMainland"/>, never copied out of
    /// it</b> — that is what makes the file a check rather than a second copy of the plan. Where a number
    /// appears below it is because a test needs to say what a metre means (a hull's draught, a walking
    /// lane), not because the geography was restated.</para>
    ///
    /// <para><b>NMC H2: the scene stands on G3, and this file splits.</b> The claims about the quay the SCENE
    /// carries (it stays dry, its berths float and lie off its face, the flats still bare, the boat parks in
    /// water, the arm leaves the basin open) are probed on G3 (<see cref="NineMileCreekLiveGround"/>) at the sites
    /// <see cref="NineMileCreekGroundPlan"/> and 05-wharf-support-g3 give them. The rest test the plan's own wharf,
    /// which the builder still authors on the analytic premise: they prove the plan, not the activated scene.</para>
    /// </summary>
    public class NineMileCreekWharfTests
    {
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        private MainlandTidalTerrain MakeCreekTerrain()
        {
            var go = new GameObject("TidalTerrain");
            _spawned.Add(go);
            var terrain = go.AddComponent<MainlandTidalTerrain>();
            NineMileCreekBuilder.ConfigureNineMileCreekTerrain(terrain);
            return terrain;
        }

        private static float SpringHigh => NineMileCreekMainland.SpringHighWater;
        private static float SpringLow => NineMileCreekMainland.SpringLowWater;

        /// <summary>The ground the scene stands on since NMC H2.</summary>
        private static ITidalTerrain Ground => NineMileCreekLiveGround.Plan;

        /// <summary>The quay the scene stands on since NMC H2: G3's north apron, from the north cap's brow back to
        /// the plan's own deck edge, and its crane apron, from the crane cap's brow to the north face.</summary>
        private static IEnumerable<Rect> SceneAprons()
        {
            Rect north = NineMileCreekGroundPlan.NorthCap, crane = NineMileCreekGroundPlan.CraneCap;
            yield return Rect.MinMaxRect(north.xMin, north.yMax, north.xMax, NineMileCreekWharf.DeckFootprint().yMax);
            yield return Rect.MinMaxRect(crane.xMin, crane.yMax, crane.xMax, NineMileCreekGroundPlan.NorthFaceY);
        }

        /// <summary>The berths the scene lays its boats in since NMC H2 (05-wharf-support-g3): the player's berth 0
        /// alongside the north face, and each wall owner's berth on the plan's line one metre south, at the widest
        /// resident's standoff (every hull's own lie is inboard of it). The channel tests walk the same berths.</summary>
        internal static IEnumerable<(int index, Vector2 at)> SceneBerths()
        {
            yield return (0, NineMileCreekGroundPlan.ArrivalPos);
            for (int i = 1; i < NineMileCreekWharf.BerthCount; i++)
                yield return (i, NineMileCreekGroundSteps.WallBerth(i, 0f));
        }

        /// <summary>The rectangle a plan fill occupies — computed here the same way the wharf computes it,
        /// so "the deck IS the fill" is a real comparison rather than a tautology through one helper.</summary>
        private static Rect RectOfZone(MainlandZone z) =>
            new Rect(z.Center.x - z.HalfSize.x, z.Center.y - z.HalfSize.y,
                     z.HalfSize.x * 2f, z.HalfSize.y * 2f);

        private static void AssertSameRect(Rect expected, Rect actual, string what)
        {
            Assert.That(actual.xMin, Is.EqualTo(expected.xMin).Within(1e-3f), $"{what}: west edge");
            Assert.That(actual.xMax, Is.EqualTo(expected.xMax).Within(1e-3f), $"{what}: east edge");
            Assert.That(actual.yMin, Is.EqualTo(expected.yMin).Within(1e-3f), $"{what}: south edge");
            Assert.That(actual.yMax, Is.EqualTo(expected.yMax).Within(1e-3f), $"{what}: north edge");
        }

        // =============================================================================================
        //  1. the walls ARE the ground — no second copy of the wharf
        // =============================================================================================

        [Test]
        public void BothWalls_AreExactlyTheFillsTheTerrainStandsThemOn()
        {
            AssertSameRect(RectOfZone(NineMileCreekMainland.NorthWallFill),
                           NineMileCreekWharf.DeckFootprint(),
                           "the north wall the fleet moors along");
            AssertSameRect(RectOfZone(NineMileCreekMainland.WestWallFill),
                           NineMileCreekWharf.ApronFootprint(),
                           "the west wall carrying the unloading apron");
        }

        [Test]
        public void TheWallsAreTheShapeThePlanDescribes_NotSomethingThatDriftedIntoIt()
        {
            Rect north = NineMileCreekWharf.DeckFootprint();
            Rect west = NineMileCreekWharf.ApronFootprint();

            Assert.That(north.width, Is.GreaterThan(north.height),
                "the north wall runs EAST-WEST — it is the long face the fleet lies against");
            Assert.That(west.height, Is.GreaterThan(west.width),
                "the west wall runs NORTH-SOUTH — the apron arm of the U");

            // They meet: the U is one structure, not two piers with a gap between them.
            Assert.That(north.Overlaps(west), Is.True,
                $"the two walls must join at the U's corner — north {north}, west {west}. A quay in two " +
                "pieces is two quays, and the fleet would be moored against the gap");
        }

        [Test]
        public void TheDeckHeight_IsMEASUREDOffTheTerrain_AndIsTheDeckThePlanAuthored()
        {
            var terrain = MakeCreekTerrain();

            Assert.That(NineMileCreekWharf.DeckElevationFrom(terrain),
                Is.EqualTo(NineMileCreekMainland.WharfDeckElevation).Within(0.01f),
                "the deck the sim stands on must be the deck the plan filled to — measured, so that a " +
                "terrain edit which lowered the wall takes the standing height down with it instead of " +
                "leaving it quietly lying about where your feet are");
            Assert.That(NineMileCreekWharf.ApronElevationFrom(terrain),
                Is.EqualTo(NineMileCreekMainland.WharfDeckElevation).Within(0.01f),
                "…and the apron is the same deck: a wharf is LEVEL");
        }

        [Test]
        public void TheWholeQuay_StaysDryAtSpringHigh()
        {
            // NMC H2: the scene's quay is G3's two aprons, probed every metre. Half a metre inside each edge: an
            // edge sits on the face's own transition, and the claim is about the DECK rather than about its lip.
            foreach (Rect apron in SceneAprons())
            for (float x = apron.xMin + 0.5f; x < apron.xMax; x += 1f)
            for (float y = apron.yMin + 0.5f; y < apron.yMax; y += 1f)
            {
                var p = new Vector2(x, y);
                float ground = Ground.ElevationAt(p);
                Assert.That(TidalExposure.IsExposed(SpringHigh, ground), Is.True,
                    $"the quay at {p} stands at {ground:0.00} m against a spring high of {SpringHigh:0.00} m " +
                    "— a working wharf does not go under. Move the deck; do not lower the tide");
            }
        }

        // =============================================================================================
        //  2. ⭐ THE RULED GATE — a berth is water, and the shoal is what decides who lies in it
        // =============================================================================================

        [Test]
        public void EveryBerth_IsWaterTheFleetCanLieIn_NotDryLandAndNotTheOpenBay()
        {
            // ⚠ OWNER RULING 2026-09-04, and it moved this assertion: *"the bullpen should always have water at low tide so all the lobster boats can park on the wall."* The −1.6 m shoal is still the gate everywhere else in the basin — it is what the flats, the crossing and the guts are measured against — but along the berth line the trench (NineMileCreekMainland §8b¾) has cut it to the depth the fleet lies in. What used to be "exactly the gate" is now "deeper than the gate, by the trench's own arithmetic".
            //
            // ⚠ The GATE has not gone: what admits a hull to this harbour was never the berth, it is the
            // FAIRWAY that leads to it (NineMileCreekChannelTests names that finding). The trench is cut
            // for the same deepest resident the fairway is, so nothing new is admitted by it.
            //
            // NMC H2: the berths are the scene's and the bed is G3's, whose basin is painted to the berths' depth
            // from wall to wall. The ruling's floor is still the bar each berth is held to.
            float needed = NineMileCreekMainland.BerthDepthNeededMetres;

            foreach (var (i, berth) in SceneBerths())
            {
                float bed = Ground.ElevationAt(berth);

                Assert.That(NineMileCreekMainland.SpringLowWater - bed,
                    Is.GreaterThanOrEqualTo(needed - 1e-3f),
                    $"berth {i} at {berth} lies over {bed:0.00} m and carries " +
                    $"{NineMileCreekMainland.SpringLowWater - bed:0.00} m at spring low, against the " +
                    $"{needed:0.00} m the deepest resident needs to lie there");
                Assert.That(bed, Is.GreaterThan(NineMileCreekMainland.BayFloorElevation),
                    $"berth {i} has been cut to the open bay's floor");
            }
        }

        [Test]
        public void NoBerth_IsMooredOnTheDeck_OrPastTheEndOfTheWall()
        {
            // ⚠ THE DEFECT A-1 CAUGHT IN ITS OWN FIRST DRAFT, kept as a guard: the west wall stood at
            // x ∈ [85, 95] with the first berth at x = 92.5, i.e. the first two boats were moored ON the
            // concrete. Re-derived here against the face the scene's ground carries since NMC H2: G3's north
            // face, from the crane step to the east arm, with each hull's inboard side on its fender line.
            foreach (var (i, berth) in SceneBerths())
            {
                float half = i == 0 ? NineMileCreekGroundPlan.DesignHalfBeamMetres
                                    : NineMileCreekMainland.WidestResidentBeamMetres * 0.5f;

                Assert.That(TidalExposure.IsExposed(SpringHigh, Ground.ElevationAt(berth)), Is.False,
                    $"berth {i} at {berth} is on the quay");
                Assert.That(berth.y, Is.LessThan(NineMileCreekGroundPlan.NorthFaceY),
                    $"berth {i} must lie SOUTH of the mooring edge — that is the face with the fleet on it");
                Assert.That(berth.y + half, Is.LessThanOrEqualTo(NineMileCreekGroundPlan.FenderLineY + 1e-3f),
                    $"berth {i} at {berth} lays her side past the fender line and onto the face");
                Assert.That(berth.x, Is.InRange(NineMileCreekGroundPlan.FaceStepX, NineMileCreekGroundPlan.NorthFaceEastX),
                    $"berth {i} at {berth} has run past the end of the wall it is supposed to lie against");
            }
        }

        [Test]
        public void TheGateIsEMERGENT_AndItIsTheBASINThatBares_NotTheBerths()
        {
            // ⭐ THE REGION'S WHOLE TEETH, as arithmetic, and — since 2026-09-04 — measured one wharf's
            // width further out. Gating here is not a rule anybody wrote: it is waterLevel − bed > draught
            // against a −1.6 m shoal under a ±2.2 m swing, and a working wharf on a big-tide coast SHOULD
            // dry out at spring low — the ladders and tyre fenders in the owner's photograph exist for
            // exactly that.
            //
            // ⚠ OWNER RULING 2026-09-04, and it moved this assertion: *"the bullpen should always have water at low tide so all the lobster boats can park on the wall."* The −1.6 m shoal is still the gate everywhere else in the basin — it is what the flats, the crossing and the guts are measured against — but along the berth line the trench (NineMileCreekMainland §8b¾) has cut it to the depth the fleet lies in. What used to be "exactly the gate" is now "deeper than the gate, by the trench's own arithmetic".
            // So the drying is asserted where it still happens — the FLATS — and the berth is asserted to
            // float, which is what the ruling bought. The ladders keep their job: on G3 the deck stands 3.40 m
            // over a berth that carries 2.1 m at dead low water, so you still climb over 5 m of wall.
            //
            // NMC H2: G3's basin is dredged to the berths' depth from wall to wall, so the flats that still bare
            // are its undredged corners inside the arms: the south-west corner off the old beach, and the
            // south-east corner under the east arm. Berth 0 is the player's, alongside the north face.
            float berth = Ground.ElevationAt(NineMileCreekGroundPlan.ArrivalPos);

            const float LobsterBoatDraught = 1.30f;
            Assert.That(TidalExposure.WaterDepth(SpringHigh, berth), Is.GreaterThan(LobsterBoatDraught),
                "the lobster boat this berth exists for must float at high water, or the wharf is a wall");
            Assert.That(TidalExposure.WaterDepth(SpringLow, berth),
                Is.GreaterThanOrEqualTo(NineMileCreekMainland.BerthDepthNeededMetres - 1e-3f),
                "…and she must still float at dead low spring, which is the ruling");

            foreach (var p in new[] { new Vector2(58f, 16f), new Vector2(190f, 32f) })
            {
                float flat = Ground.ElevationAt(p);
                Assert.That(TidalExposure.WaterDepth(SpringLow, flat), Is.LessThanOrEqualTo(0f),
                    $"the harbour FLATS must still bare at spring low, and the one at {p} stands at {flat:0.00} m. " +
                    "If nothing dries, the shoal has stopped being a gate and Nine Mile Creek has quietly become " +
                    "the dredged harbour Port Greywick is");
                Assert.That(TidalExposure.WaterDepth(SpringHigh, flat), Is.GreaterThan(0f),
                    "…and flood again at spring high, or it is not a flat but a field");
            }
        }

        [Test]
        public void TheBoatParksWhereItCanLie_AndTheApproachIsWaterTheWholeWayIn()
        {
            // NMC H2: berth 0 alongside G3's north face, probed on G3.
            foreach (var (what, p) in new (string, Vector3)[]
                     {
                         ("the arrival park", NineMileCreekBuilder.ArrivalPos),
                         ("the dock zone", NineMileCreekBuilder.DockZonePos),
                     })
            {
                float bed = Ground.ElevationAt(new Vector2(p.x, p.y));
                Assert.That(bed, Is.LessThanOrEqualTo(NineMileCreekMainland.BasinBedElevation + 0.01f),
                    $"{what} at {p} sits on {bed:0.00} m — a boat cannot park on the quay");
                Assert.That(TidalExposure.WaterDepth(SpringHigh, bed), Is.GreaterThan(0.3f),
                    $"{what} must carry the start dory at high water");
            }
        }

        // =============================================================================================
        //  3. ⭐ THE BOLLARD YOU SEE IS THE BOLLARD YOU TIE TO (#451, carried forward per the handoff)
        // =============================================================================================

        [Test]
        public void EveryBerth_HasABollardOnTheWallBesideIt()
        {
            var bollards = NineMileCreekWharf.Fittings()
                                             .Where(f => f.Name == "bollard")
                                             .Select(f => f.Position.x)
                                             .ToList();

            Assert.That(bollards.Count, Is.EqualTo(NineMileCreekWharf.BerthCount),
                "one bollard per berth is what 'there is always one where you come alongside' means. " +
                "Derived from the berth line, so the two cannot drift apart");

            for (int i = 0; i < NineMileCreekWharf.BerthCount; i++)
                Assert.That(bollards, Has.Some.EqualTo(NineMileCreekWharf.BerthPos(i).x).Within(1e-3f),
                    $"berth {i} has no bollard beside it");
        }

        [Test]
        public void EveryFitting_StandsOnTheQuay_NotOverTheBasin()
        {
            foreach (var f in NineMileCreekWharf.Fittings())
                Assert.That(NineMileCreekWharf.Decks().Any(d => d.Contains(f.Position)), Is.True,
                    $"the {f.Name} at {f.Position} is not on either wall — a fitting hanging over the " +
                    "basin is a fitting nobody can reach");
        }

        [Test]
        public void EveryMooringFitting_BecomesARealCleat_AndTheKitDecidesWhichOnesThoseAre()
        {
            var fittings = NineMileCreekWharf.Fittings();
            int expected = fittings.Count(f => WharfKitCatalog.IsMooringFitting(f.Name));

            Assert.That(NineMileCreekWharf.MooringFittingCount(), Is.EqualTo(expected));
            Assert.That(expected, Is.GreaterThanOrEqualTo(NineMileCreekWharf.BerthCount),
                "at least one tie-off per berth, or a boat can be moored where no rope can be made fast");

            // The kit's own list is the decider — a tyre is a fender, not a tie-off, and this file must
            // not have an opinion about that.
            Assert.That(WharfKitCatalog.IsMooringFitting("tyre"), Is.False);
            Assert.That(WharfKitCatalog.IsMooringFitting("ladder"), Is.False);
        }

        [Test]
        public void TheFittingsAreOnesTheKitActuallyPublishes()
        {
            foreach (var f in NineMileCreekWharf.Fittings())
            {
                Assert.That(WharfKitCatalog.Fittings, Contains.Item(f.Name),
                    $"'{f.Name}' is not a fitting the kit bakes — it would place nothing, silently");
                // Throws on an unknown name, which is the point.
                Assert.That(WharfKitCatalog.FittingSlice(f.Name), Is.Not.Null.And.Not.Empty);
            }
        }

        [Test]
        public void NoTwoFittings_ShareAMetreOfLip()
        {
            var lip = NineMileCreekWharf.Fittings()
                                        .Where(f => Mathf.Approximately(f.Position.y, NineMileCreekWharf.MooringEdgeY))
                                        .OrderBy(f => f.Position.x)
                                        .ToList();

            for (int i = 1; i < lip.Count; i++)
                Assert.That(lip[i].Position.x - lip[i - 1].Position.x, Is.GreaterThanOrEqualTo(1f),
                    $"the {lip[i - 1].Name} and the {lip[i].Name} are stacked on the same metre of the " +
                    "wall face — hung gear reads as one broken object when it overlaps");
        }

        // =============================================================================================
        //  4. the breakwater shelters the basin without closing the way in
        // =============================================================================================

        [Test]
        public void TheBreakwater_IsTheArmThePlanFilled()
        {
            AssertSameRect(RectOfZone(NineMileCreekMainland.BreakwaterFill),
                           NineMileCreekWharf.BreakwaterFootprint(),
                           "the crib breakwater");
        }

        [Test]
        public void TheBreakwater_IsAContiguousCribRun_CappedAtTheSeawardTip()
        {
            var blocks = NineMileCreekWharf.BreakwaterBlocks();
            Assert.That(blocks.Count, Is.EqualTo(NineMileCreekWharf.BreakwaterBlockCount));
            Assert.That(blocks.Count, Is.GreaterThan(1), "an arm of one block is not an arm");
            Assert.That(WharfKitCatalog.ArmourTypes, Contains.Item(NineMileCreekWharf.BreakwaterArmour));

            for (int i = 0; i < blocks.Count; i++)
            {
                string expected = i == blocks.Count - 1 ? "end" : "straight";
                Assert.That(blocks[i].Name, Is.EqualTo(expected),
                    "the tileable run carries the arm and the battered cap finishes it");
                Assert.That(WharfKitCatalog.ArmourVariants, Contains.Item(blocks[i].Name));
            }

            // The positions are CENTRES (the armour sheet pivots top-centre), so the run must butt the
            // arm's west end and stay inside its east end — a half-block on the beach is the failure.
            float half = NineMileCreekWharf.ArmourWidthMetres * 0.5f;
            Assert.That(blocks[0].Position.x - half,
                Is.EqualTo(NineMileCreekWharf.BreakwaterWestX).Within(1e-3f));
            Assert.That(blocks[blocks.Count - 1].Position.x + half,
                Is.LessThanOrEqualTo(NineMileCreekWharf.BreakwaterEastX + 1e-3f));
            for (int i = 1; i < blocks.Count; i++)
                Assert.That(blocks[i].Position.x - blocks[i - 1].Position.x,
                    Is.EqualTo(NineMileCreekWharf.ArmourWidthMetres).Within(1e-3f),
                    "gaps in a breakwater are holes the sea comes through");
        }

        [Test]
        public void TheBreakwater_LeavesTheBasinAndTheWayHomeClear()
        {
            Rect deck = NineMileCreekWharf.DeckFootprint();
            Assert.That(NineMileCreekWharf.BreakwaterY, Is.LessThan(deck.yMin),
                "the arm lies south of the basin, not across the quay it shelters");

            foreach (var (what, p) in new (string, Vector3)[]
                     {
                         ("the arrival park", NineMileCreekBuilder.ArrivalPos),
                         ("the dock zone", NineMileCreekBuilder.DockZonePos),
                         ("the step ashore", NineMileCreekBuilder.DisembarkPos),
                     })
                Assert.That(p.y, Is.GreaterThan(NineMileCreekWharf.BreakwaterY),
                    $"{what} at {p} is on the seaward side of the arm — the boat would have to cross it");

            Assert.That(NineMileCreekBuilder.ToCovePassagePos.x,
                Is.GreaterThan(NineMileCreekWharf.BreakwaterEastX),
                "the passage home must be clear of the arm's seaward tip");

            // NMC H2: the scene's blocking arm runs on the wharf source's two arms (05-wharf-support-g3), so the
            // same separation is held against those: the berth, the dock and the step ashore lie north of the south
            // arm and west of the east arm's run down from the north face, and the passage home lies east of it.
            float south = Mathf.Max(NineMileCreekGroundPlan.SouthArm[0].y, NineMileCreekGroundPlan.SouthArm[1].y);
            Vector2 eastTop = NineMileCreekGroundPlan.EastArm[0], eastFoot = NineMileCreekGroundPlan.EastArm[1];
            Assert.That(eastFoot.x, Is.EqualTo(eastTop.x), "the east arm runs due south from the north face");
            foreach (var (what, p) in new (string, Vector3)[]
                     {
                         ("the arrival park", NineMileCreekBuilder.ArrivalPos),
                         ("the dock zone", NineMileCreekBuilder.DockZonePos),
                         ("the step ashore", NineMileCreekBuilder.DisembarkPos),
                     })
            {
                Assert.That(p.y, Is.GreaterThan(south), $"{what} at {p} is seaward of the scene's south arm");
                Assert.That(p.y, Is.GreaterThan(eastFoot.y), $"{what} at {p} is south of the east arm's straight run");
                Assert.That(p.x, Is.LessThan(eastTop.x), $"{what} at {p} is seaward of the scene's east arm");
            }
            Assert.That(NineMileCreekBuilder.ToCovePassagePos.x, Is.GreaterThan(eastTop.x),
                "the passage home must be clear of the scene's east arm");
        }

        /// <summary>The PLAN's beacon on the PLAN's arm: a premise test. Since NMC H2 the scene's blocking arm runs on
        /// the wharf source's two arms (05-wharf-support-g3), and the two dry head lights that move with it are a
        /// later wave's, not PR 2's; the beacon still stands where this test puts it, off the scene's arm.</summary>
        [Test]
        public void TheBreakwaterHead_IsWhereTheBeaconStands()
        {
            Vector3 beacon = NineMileCreekMainland.HarbourBeaconPos;
            Assert.That(NineMileCreekWharf.BreakwaterFootprint().Contains(beacon), Is.True,
                $"the range light at {beacon} must stand ON the arm it marks — a beacon in the water is " +
                "a beacon nobody built");
            Assert.That(beacon.x, Is.GreaterThan(NineMileCreekWharf.BreakwaterFootprint().center.x),
                "…and at its SEAWARD end, which is the end a boat coming in has to find");
        }

        // =============================================================================================
        //  5. the stack — read off the partition, not typed
        // =============================================================================================

        [Test]
        public void TheWharfsStructureBand_IsThePartitionsOwn()
        {
            Assert.That(NineMileCreekWharf.SortingOrderMin, Is.EqualTo(SortingBands.WharfDeckMin));
            Assert.That(NineMileCreekWharf.SortingOrderMax, Is.EqualTo(SortingBands.WharfDeckMax));

            Assert.That(NineMileCreekWharf.BreakwaterSortingOrder, Is.GreaterThan(SortingBands.Sea),
                "the breakwater has to be above the sea it is armour against, or the harbour is drawn " +
                "over its own arm");
            Assert.That(NineMileCreekWharf.BreakwaterSortingOrder, Is.LessThan(SortingBands.DecorFloor),
                "…and below everything that stands on the coast");
        }

        [Test]
        public void TheGreyboxGround_DrawsUnderTheSea_SoTheWetDryClipDecidesWhatYouSee()
        {
            Assert.That(NineMileCreekBuilder.GroundSortingOrder, Is.LessThan(SortingBands.Sea),
                "the ground plane must sit UNDER the sea plane: the shader's wet-dry clip is what shows " +
                "ground where the terrain is bared and water where it is not. Above the sea it would " +
                "paint the whole bay green");
            Assert.That(NineMileCreekBuilder.GroundSortingOrder,
                Is.GreaterThanOrEqualTo(SortingBands.PaintedSeabedMax),
                "…but above the painted seabed, which is the rung below it");
        }
    }
}
