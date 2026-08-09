using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>PHASE B — THE DRESSING OF NINE MILE CREEK.</b> The gear, the services and the tideline that
    /// #462 left to this phase, plus the measurement of the one thing Phase B could NOT do.
    ///
    /// <para><b>These are builder-wiring tests, which is the point.</b> The A-2 handoff names the trap by
    /// name: <i>scene-wired is not builder-wired</i>. A prop dragged into <c>NineMileCreek.unity</c>
    /// survives exactly until the owner rebuilds the scene. So everything below is asserted against the
    /// <b>pure placement tables</b> that <see cref="NineMileCreekBuilder"/> consumes — which means a
    /// rebuild reproduces it, and a test can check it with no art imported at all.</para>
    ///
    /// <para><b>⚠️ Deliberately art-independent where it can be.</b> The sheets are Git-LFS binaries and
    /// the pack bakes to order, so "has this prop got pixels" is a different question from "is this prop
    /// correctly placed" — and only the second one is world-content's. The sorting sweep is done against
    /// the BAND ARITHMETIC (does the decor band resolve this world Y at all — the ADR 0032 saturation
    /// defect) rather than against a loaded sprite, so it holds whether or not the pack has imported. The
    /// one test that needs pixels says so and passes vacuously without them.</para>
    /// </summary>
    public class NineMileCreekDressingTests
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

        private static List<NineMileCreekDressing.Prop> Props() =>
            NineMileCreekDressing.AllProps().ToList();

        // =============================================================================================
        //  1. THE QUAY FACE — the sub-item Phase B stopped on, and the arithmetic that says why
        // =============================================================================================

        [Test]
        public void TheFaceThisWharfNeeds_IsDerivedFromItsOwnTideAndDeck_NotTypedIn()
        {
            // The rig's frame puts z = 0 at the LOWEST water; the game's datum is mean water. Everything
            // else in this section is wrong by an amplitude if that conversion is.
            Assert.That(NineMileCreekQuayFace.ToRigZ(SpringLow), Is.EqualTo(0f).Within(1e-4f),
                "the rig's chart datum IS this region's lowest water — that is the whole conversion");

            Assert.That(NineMileCreekQuayFace.RequiredTideRangeMetres,
                Is.EqualTo(SpringHigh - SpringLow).Within(1e-4f),
                "the rig's tideRange is datum to highest water: 4.4 m here, twice the amplitude");

            Assert.That(NineMileCreekQuayFace.RequiredClearanceMetres,
                Is.EqualTo(NineMileCreekMainland.WharfDeckElevation - SpringHigh).Within(1e-4f),
                "…and its clearance is the freeboard NineMileCreekMainland authored (0.8 m)");

            // ⭐ THE AGREEMENT THAT MAKES THIS A ONE-PARAMETER PROBLEM. The rig's own auto rule is
            // deckZ = tideRange + clearance. This wharf's authored deck satisfies it exactly, which is
            // why no new geometry is needed — only a bake at this coast's tide.
            Assert.That(NineMileCreekQuayFace.RequiredDeckZMetres,
                Is.EqualTo(NineMileCreekQuayFace.RequiredTideRangeMetres +
                           NineMileCreekQuayFace.RequiredClearanceMetres).Within(1e-4f),
                "the authored deck is exactly the rig's own deckZ = tideRange + clearance. If this ever " +
                "fails, the wharf has been re-authored to something the rig cannot quote and the " +
                "re-bake order in NineMileCreekQuayFace is no longer a simple parameter change");
        }

        [Test]
        public void TheFaceIsMEASUREDOffTheTerrain_SoALoweredWallShowsUpHere()
        {
            var terrain = MakeCreekTerrain();

            Assert.That(NineMileCreekQuayFace.StructuralFaceFrom(terrain),
                Is.EqualTo(NineMileCreekQuayFace.StructuralFaceMetres).Within(0.01f),
                "the drawn face has to be the face the terrain actually stands up — measured, so that a " +
                "terrain edit which lowered the wall changes the bake order instead of leaving this " +
                "class quietly describing a wharf the region no longer has");

            Assert.That(NineMileCreekQuayFace.StructuralFaceMetres,
                Is.LessThan(NineMileCreekQuayFace.RequiredDeckZMetres),
                "the structural face is shorter than the drop to lowest water, because the harbour shoal " +
                "is filled above datum and the last stretch of the rig's frame is under the seabed");
        }

        [Test]
        public void TheCommittedPackCannotStackToThisFace_WhichIsWhyTheQuayIsStillUndrawn()
        {
            // The shortfall is not an arbitrary gap — it IS the difference between the tide the pack
            // baked for and the tide this coast has. That equality is the finding.
            Assert.That(NineMileCreekQuayFace.ShortfallMetres,
                Is.EqualTo(NineMileCreekQuayFace.RequiredTideRangeMetres -
                           NineMileCreekQuayFace.BakedRigTideRange).Within(1e-4f),
                "every structural preset is short by exactly the tide difference, because the rig sizes " +
                "deck height off tideRange. A shortfall that stopped agreeing with that would mean the " +
                "pack was re-baked and this whole finding needs re-deriving");

            NineMileCreekQuayFace.BestStackTo(
                NineMileCreekQuayFace.RequiredDeckZMetres, out float best, out int courses);

            // ⭐ THE HONEST HALF FIRST. The height DOES land — two sheetCell courses come to 5.20 m
            // exactly — and pinning that here is what stops this stop from later being restated as
            // "the pieces do not add up", which would send art-director looking for the wrong fix.
            Assert.That(NineMileCreekQuayFace.AStackReachesTheHeight(), Is.True,
                $"{courses} courses reach {best:0.00} m against a required " +
                $"{NineMileCreekQuayFace.RequiredDeckZMetres:0.00} m — the arithmetic is not the problem");

            // …and the half that actually stops it: the only course that reaches the height is the wrong
            // material and has no working top.
            var courseList = NineMileCreekQuayFace.StackableCourses();
            Assert.That(courseList.Count, Is.EqualTo(1),
                "the pack's stackable-course list has changed — re-derive the stop before trusting it");
            Assert.That(courseList[0].UsableHere, Is.False,
                $"'{courseList[0].Key}' is recorded as usable on this wharf. #462 ruled it sheet pile, " +
                "which this wharf is not built of, and a capped cell has no deck to land a catch on");

            Assert.That(NineMileCreekQuayFace.BakedPackCanDrawTheFace(), Is.False,
                "⭐ IF THIS FAILS, THAT IS GOOD NEWS: the pack has been re-baked (or a usable course has " +
                "landed) and the quay can now be drawn. Delete the stop, place the face, and read the " +
                "deck height off NineMileCreekQuayFace rather than re-deriving it.");
        }

        // =============================================================================================
        //  2. THE DRESSING IS BUILDER-WIRED — the named trap
        // =============================================================================================

        [Test]
        public void TheBuilderPlacesTheDressing_SoARebuildKeepsIt()
        {
            var terrain = MakeCreekTerrain();
            NineMileCreekDressing.Place(terrain);

            var root = GameObject.Find(NineMileCreekDressing.RootName);
            _spawned.Add(root);
            Assert.That(root, Is.Not.Null,
                "the dressing has to come from the BUILDER. A prop wired into the scene survives exactly " +
                "until the owner rebuilds it, which is the whole reason this is a table and not a scene");

            foreach (string group in new[]
                     {
                         NineMileCreekDressing.QuayRootName, NineMileCreekDressing.ApronRootName,
                         NineMileCreekDressing.YardRootName, NineMileCreekDressing.UtilityRootName,
                     })
                Assert.That(root.transform.Find(group), Is.Not.Null,
                    $"'{group}' is missing — the four jobs this pass does should each be a group the " +
                    "owner can find and hide");
        }

        [Test]
        public void EveryPlacedSprite_JoinsTheYSortBand_NeverAHandPickedOrder()
        {
            var terrain = MakeCreekTerrain();
            NineMileCreekDressing.Place(terrain);

            var root = GameObject.Find(NineMileCreekDressing.RootName);
            _spawned.Add(root);
            Assert.That(root, Is.Not.Null);

            var renderers = root.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            // Vacuous when the pack has not imported, and deliberately so — see the class note. The
            // PLACEMENT is asserted without art by the band-arithmetic sweep below.
            foreach (var sr in renderers)
            {
                Assert.That(sr.GetComponent<YSortSprite>(), Is.Not.Null,
                    $"'{sr.name}' has no YSortSprite — dressing joins the decor band (ADR 0032), it does " +
                    "not pick an order. A prop with a fixed order cannot be walked in front of");
                Assert.That(sr.sortingOrder,
                    Is.InRange(SortingBands.DecorFloor, SortingBands.DecorCeiling),
                    $"'{sr.name}' sorts at {sr.sortingOrder}, outside the decor band " +
                    $"[{SortingBands.DecorFloor}, {SortingBands.DecorCeiling}]");
            }
        }

        [Test]
        public void EveryPieceOfDressing_SitsWhereTheDecorBandStillRESOLVES()
        {
            // ⚠️ THE ADR 0032 DEFECT, and the reason this is checked on POSITIONS rather than on loaded
            // sprites: where the band's clamp bites, sorting silently STOPS — every sprite past the end
            // saturates on one order and interleaves by draw order instead of by position. Nothing fails;
            // the wharf just stops layering.
            var terrain = MakeCreekTerrain();

            foreach (var prop in Props())
                Assert.That(SortingBands.ResolvesWorldY(prop.Position.y), Is.True,
                    $"'{prop.Key}' at Y = {prop.Position.y:0.#} is outside the decor band's " +
                    $"±{SortingBands.DecorHalfExtentMetres} m resolving range — it would saturate and " +
                    "stop layering, silently");

            foreach (var find in NineMileCreekDressing.Finds(terrain))
                Assert.That(SortingBands.ResolvesWorldY(find.Position.y), Is.True,
                    $"a {find.Key} at Y = {find.Position.y:0.#} is outside the decor band's resolving range");
        }

        [Test]
        public void TheDressingIsSparse_AndEveryPieceSaysWhatItIsFor()
        {
            var props = Props();

            Assert.That(props.Count, Is.GreaterThan(20),
                "a wharf with a handful of props on it reads as unbuilt rather than as quiet");
            Assert.That(props.Count, Is.LessThan(80),
                "…and the owner's photographs are of a working wharf on a coast of FIELDS. The 61-piece " +
                "decor kit is a temptation, not a budget: a carpeted wharf reads as a diorama");

            foreach (var prop in props)
            {
                Assert.That(prop.Key, Is.Not.Null.And.Not.Empty);
                Assert.That(prop.Reason, Is.Not.Null.And.Not.Empty,
                    $"'{prop.Key}' has no reason recorded. A prop that cannot say what it is for is the " +
                    "one to cut — that is how this stays sparse under the next pass");
            }
        }

        // =============================================================================================
        //  3. WHERE THINGS STAND — derived, and clear of everything already there
        // =============================================================================================

        [Test]
        public void NothingStandsInTheWorkingStripAlongTheMooringEdge()
        {
            Rect quay = NineMileCreekWharf.DeckFootprint();

            // Only the safety and tide gear stand forward of the gear band, and even they keep back by
            // the fitting clearance. Fish are landed across this ground and lines are handled on it.
            foreach (var prop in Props())
            {
                if (!quay.Contains(prop.Position)) continue;
                Assert.That(prop.Position.y,
                    Is.GreaterThanOrEqualTo(quay.yMin + NineMileCreekDressing.FittingClearanceMetres - 1e-3f),
                    $"'{prop.Key}' stands within {NineMileCreekDressing.FittingClearanceMetres} m of the " +
                    "mooring lip — that is where a line is handled and a hull comes alongside");
                Assert.That(prop.Position.y,
                    Is.LessThanOrEqualTo(quay.yMax - NineMileCreekDressing.BackSetbackMetres + 1e-3f),
                    $"'{prop.Key}' has spilled off the back of the deck into the yard");
            }

            // …and the gear band itself leaves the working strip empty.
            var inStrip = Props()
                .Where(p => quay.Contains(p.Position) &&
                            p.Position.y < quay.yMin + NineMileCreekDressing.WorkingStripMetres)
                .Select(p => p.Key)
                .ToList();
            Assert.That(inStrip, Is.EquivalentTo(new[] { "tideStaff", "ringStation", "ringStation" }),
                "only the tide board and the life rings belong forward of the working strip. Anything " +
                $"else inside {NineMileCreekDressing.WorkingStripMetres} m of the lip is in the way of " +
                $"landing a catch — found: {string.Join(", ", inStrip)}");
        }

        [Test]
        public void NoPropCrowdsOneOf462sMooringFittings()
        {
            // ⭐ The fittings are the wharf's one hard promise — the bollard you see IS the bollard you
            // tie to. A crate at arm's length of one is in the way of the only thing the wharf is for.
            var fittings = NineMileCreekWharf.Fittings();

            foreach (var prop in Props())
                foreach (var fitting in fittings)
                {
                    float d = Vector2.Distance(prop.Position, fitting.Position);
                    Assert.That(d, Is.GreaterThanOrEqualTo(
                            NineMileCreekDressing.FittingClearanceMetres - 1e-3f),
                        $"'{prop.Key}' at {prop.Position} is {d:0.00} m from the '{fitting.Name}' at " +
                        $"{fitting.Position} — inside the " +
                        $"{NineMileCreekDressing.FittingClearanceMetres} m a fitting needs to be used");
                }
        }

        [Test]
        public void NothingBlocksTheViewOfTheDerelictDoryFromWhereThePlayerLands()
        {
            // ⚠️ THE REGION'S OPENING BEAT. NineMileCreekDory measures this line and the whole §7.2 exit
            // condition hangs off it: you step ashore and you SEE the boat you are going to buy. A first
            // draft of this pass put the fuel pump 0.28 m off it.
            foreach (var prop in Props())
                Assert.That(
                    NineMileCreekDory.SightlineIsClear(NineMileCreekDory.HaulOutPos, prop.Position,
                                                       NineMileCreekDressing.DoryYardClearanceMetres),
                    Is.True,
                    $"'{prop.Key}' at {prop.Position} stands on the line from where the player lands to " +
                    "the derelict. Move the prop; the beat is not negotiable");
        }

        [Test]
        public void NothingSitsInTheRoadsClearedCorridor()
        {
            foreach (var prop in Props())
            {
                float d = NineMileCreekMainland.DistanceToRoute(
                    NineMileCreekMainland.WharfRoad, prop.Position);
                Assert.That(d, Is.GreaterThanOrEqualTo(NineMileCreekMainland.RoadHalfWidth - 1e-3f),
                    $"'{prop.Key}' at {prop.Position} is {d:0.00} m from Wharf Road's centre-line, " +
                    $"inside the {NineMileCreekMainland.RoadHalfWidth} m corridor the plan clears. A " +
                    "hydrant in the road is a hydrant a truck hits");
            }
        }

        [Test]
        public void EveryPieceOfDressingStandsOnDryGround()
        {
            var terrain = MakeCreekTerrain();

            // The same bar the road test uses: above the HIGHEST water, not above mean. The poles are
            // what this really asks about — the plan offsets them 5 m north of a road that is itself
            // only just clear of the barachois — but the gear is swept too, because the deck carries
            // only 0.8 m of freeboard at spring high and a terrain edit is all it would take.
            foreach (var prop in Props())
            {
                float e = terrain.ElevationAt(prop.Position);
                Assert.That(e, Is.GreaterThan(SpringHigh),
                    $"'{prop.Key}' stands at {prop.Position} where the ground is {e:0.00} m — at or " +
                    $"below spring high water ({SpringHigh:0.0} m). {prop.Reason}");
            }
        }

        [Test]
        public void ThePoleLineWalksWharfRoadsOwnPublishedRoute()
        {
            var poles = NineMileCreekDressing.Poles();
            float spacing = NineMileCreekMainland.UtilityPoleSpacingMetres;
            float offset = NineMileCreekMainland.UtilityPoleOffsetMetres;
            float length = NineMileCreekMainland.RouteLength(NineMileCreekMainland.WharfRoad);

            Assert.That(poles.Count, Is.EqualTo(Mathf.FloorToInt(length / spacing) + 1),
                "the pole count is the route's length over the plan's own spacing — never a typed number");

            foreach (var pole in poles)
            {
                float d = NineMileCreekMainland.DistanceToRoute(
                    NineMileCreekMainland.WharfRoad, pole.Position);
                Assert.That(d, Is.EqualTo(offset).Within(0.05f),
                    $"a pole stands {d:0.00} m from the road instead of the plan's {offset} m");
            }

            // NORTH of the centre-line, which §12 states in as many words.
            for (int i = 0; i < poles.Count; i++)
            {
                float along = i * spacing;
                Vector2 on = MainlandCoast.PositionAt(NineMileCreekMainland.WharfRoad, along);
                Assert.That(poles[i].Position.y, Is.GreaterThan(on.y),
                    $"pole {i} is on the SOUTH side of Wharf Road; the plan puts the line north of it");
            }

            // …and the line actually gets to the wharf, which is the only reason it exists.
            float toWharf = poles.Min(p => Vector2.Distance(p.Position, NineMileCreekDressing.WharfEntrance()));
            Assert.That(toWharf, Is.LessThan(spacing),
                "the pole line has to REACH the wharf — the yard light is the end of it");
        }

        [Test]
        public void TheWinchIsTheTallLegibleObjectThePlanAskedFor()
        {
            // NineMileCreekMainland: the west wall's water side is a curb-only edge at this camera,
            // "which is why the plan wants the winch to be a tall legible object rather than a detail on
            // a wall". This is that promise, kept.
            var at = new Vector2(NineMileCreekMainland.WinchPos.x, NineMileCreekMainland.WinchPos.y);
            var winch = NineMileCreekDressing.ApronGear()
                .FirstOrDefault(p => Vector2.Distance(p.Position, at) < 1e-3f);

            Assert.That(winch.Key, Is.Not.Null.And.Not.Empty,
                "nothing stands at the authored WinchPos — the apron has no machine on it");
            Assert.That(winch.Heading,
                Is.EqualTo(NineMileCreekDressing.ApronSeawardHeading).Within(1e-3f),
                "the winch has to look at the water it lifts out of");
        }

        [Test]
        public void TheDressingDoesNotDuplicateAFittingThatIsALREADYReal()
        {
            // ⭐ #462's two guarantees: the ladder you can SEE is the ladder you can CLIMB, and the
            // bollard you can see is the bollard you can tie to. A decorative twin of either is exactly
            // the promise those components exist to keep.
            var keys = Props().Select(p => p.Key).ToList();

            foreach (string forbidden in new[] { "rescueLadder", "ringPost" })
                Assert.That(keys, Does.Not.Contain(forbidden),
                    $"'{forbidden}' is a decorative copy of something #462 made REAL. A ladder you " +
                    "cannot climb standing beside one you can is worse than no ladder");

            // ringStation is fine and is the reason this test names the two rather than banning a theme:
            // a life ring is rescue gear, not a tie-off, and nothing in the sim claims it.
            Assert.That(keys, Does.Contain("ringStation"),
                "the wharf should still carry life rings — those are not mooring fittings");
        }

        // =============================================================================================
        //  4. THE FORESHORE
        // =============================================================================================

        [Test]
        public void EveryFindsStateIsTheOneItsOwnTideBandCallsFor()
        {
            var terrain = MakeCreekTerrain();
            var finds = NineMileCreekDressing.Finds(terrain);
            var bands = NineMileCreekDressing.Bands().ToDictionary(b => b.Zone, b => b);

            Assert.That(finds.Count, Is.GreaterThan(0),
                "the foreshore should have something on it — the finds contract is committed text and " +
                "does not need the sheets to have baked");

            foreach (var find in finds)
            {
                Assert.That(bands.ContainsKey(find.Zone), Is.True,
                    $"'{find.Key}' is in zone '{find.Zone}', which no band covers");
                Assert.That(find.State, Is.EqualTo(bands[find.Zone].State),
                    $"a {find.Key} in the '{find.Zone}' band is drawn '{find.State}'. The kit bakes " +
                    "three states per find for exactly this axis, and the band decides it");
            }
        }

        [Test]
        public void EveryFindLiesAtAnElevationItsBandActuallyCovers()
        {
            var terrain = MakeCreekTerrain();
            var bands = NineMileCreekDressing.Bands().ToDictionary(b => b.Zone, b => b);

            foreach (var find in NineMileCreekDressing.Finds(terrain))
            {
                var band = bands[find.Zone];
                // The search targets the band's MIDDLE, so a hit should be inside the band with room —
                // a tolerance only because the bisection stops at a finite depth.
                Assert.That(find.Elevation,
                    Is.InRange(band.MinElevation - 0.05f, band.MaxElevation + 0.05f),
                    $"a {find.Key} ({find.Zone}/{find.State}) is lying at {find.Elevation:0.00} m, " +
                    $"outside its band [{band.MinElevation:0.00}, {band.MaxElevation:0.00}]. A 'wet' " +
                    "find above the tide is the whole banding scheme not working");
            }
        }

        [Test]
        public void NoFindLiesOnTheWharfsMadeGround()
        {
            var terrain = MakeCreekTerrain();

            foreach (var find in NineMileCreekDressing.Finds(terrain))
                Assert.That(NineMileCreekDressing.IsOnMadeGround(find.Position), Is.False,
                    $"a {find.Key} is lying at {find.Position}, on the spit or the harbour shoal. Finds " +
                    "are what the tide left on the NATURAL foreshore; a shell on a concrete deck is a prop");
        }

        [Test]
        public void TheScatterIsAPureFunctionOfItsIndex_SoARebuildReproducesIt()
        {
            // Rule 5: no hidden randomness. Two builds of the same region must lay the same beach, or
            // every screenshot, playtest note and bug report is about a different shore.
            var terrain = MakeCreekTerrain();
            var first = NineMileCreekDressing.Finds(terrain);
            var second = NineMileCreekDressing.Finds(terrain);

            Assert.That(second.Count, Is.EqualTo(first.Count), "the scatter changed size between builds");
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(second[i].Key, Is.EqualTo(first[i].Key));
                Assert.That(second[i].State, Is.EqualTo(first[i].State));
                Assert.That(second[i].LieAngle, Is.EqualTo(first[i].LieAngle));
                Assert.That(second[i].Variant, Is.EqualTo(first[i].Variant));
                Assert.That(Vector2.Distance(second[i].Position, first[i].Position), Is.LessThan(1e-4f),
                    $"find {i} ({first[i].Key}) moved between two builds of the same region");
            }
        }

        [Test]
        public void EveryZoneTheFindsContractDeclaresHasABandToLandIn()
        {
            var zones = NineMileCreekDressing.DeclaredFindZones();
            Assert.That(zones.Count, Is.GreaterThan(0),
                "the shore-finds contract is committed text — if it read as empty, it failed to load");

            var covered = NineMileCreekDressing.Bands().Select(b => b.Zone).ToList();
            foreach (string zone in zones)
                Assert.That(covered, Does.Contain(zone),
                    $"the kit bakes finds for the '{zone}' zone and no band places them, so they are " +
                    "baked and never seen. Add the band rather than dropping the finds");
        }

        [Test]
        public void EveryFindAsksForASliceItsSheetActuallyHas()
        {
            var terrain = MakeCreekTerrain();

            foreach (var find in NineMileCreekDressing.Finds(terrain))
            {
                Assert.That(find.LieAngle,
                    Is.InRange(0, NineMileCreekDressing.FindLieAngles - 1),
                    $"a {find.Key} asks for lie angle {find.LieAngle}; the sheet bakes " +
                    $"{NineMileCreekDressing.FindLieAngles}");
                Assert.That(find.Variant,
                    Is.InRange(0, NineMileCreekDressing.FindVariants - 1),
                    $"a {find.Key} asks for variant {find.Variant}; the sheet bakes " +
                    $"{NineMileCreekDressing.FindVariants}");
            }
        }

        [Test]
        public void TheBandsCoverTheTideWithoutOverlappingIntoNonsense()
        {
            var bands = NineMileCreekDressing.Bands();

            var tide = bands.First(b => b.Zone == "tide");
            Assert.That(tide.MinElevation, Is.EqualTo(SpringLow).Within(1e-4f),
                "the intertidal band starts at the lowest water — that is what intertidal means");
            Assert.That(tide.MaxElevation, Is.EqualTo(SpringHigh).Within(1e-4f));

            var wrack = bands.First(b => b.Zone == "wrack");
            Assert.That(wrack.Middle, Is.EqualTo(SpringHigh).Within(1e-4f),
                "the wrack line is where the biggest tide stopped");

            var upper = bands.First(b => b.Zone == "upper");
            Assert.That(upper.MinElevation, Is.GreaterThanOrEqualTo(SpringHigh - 1e-4f),
                "the upper beach is above the strandline, or it is not upper beach");
        }

        // =============================================================================================
        //  5. THE FACING — the mislabel that has shipped five times
        // =============================================================================================

        [Test]
        public void EveryPropResolvesToAFacingItsSheetActuallyBaked()
        {
            foreach (var prop in Props())
                Assert.That(NineMileCreekDressing.FacingFor(prop),
                    Is.InRange(0, NineMileCreekDressing.Facings - 1),
                    $"'{prop.Key}' at heading {prop.Heading:0.#}° resolves outside the sheet's " +
                    $"{NineMileCreekDressing.Facings} facings");
        }

        [Test]
        public void TheQuayGearLooksAtTheWaterTheWharfWorks()
        {
            // Derived from #462's mooring-face heading rather than typed as "south", so re-pointing the
            // wall turns the gear with it.
            var quay = NineMileCreekDressing.QuayGear();
            var working = quay.Where(p => p.Key != "harbourSign" && p.Key != "noticeBoard" &&
                                          p.Key != "bunting").ToList();

            Assert.That(working, Is.Not.Empty);
            foreach (var prop in working)
                Assert.That(prop.Heading,
                    Is.EqualTo(NineMileCreekWharf.MooringFaceHeadingDegrees).Within(1e-3f),
                    $"'{prop.Key}' does not face the water. Working gear on a quay is used from the " +
                    "water side");

            // …and the sign group faces the other way, at whoever is coming down the road.
            float arrival = NineMileCreekDressing.RoadArrivalHeading();
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(arrival, NineMileCreekWharf.MooringFaceHeadingDegrees)),
                Is.GreaterThan(45f),
                "the sign at the entrance should not be pointing the same way as the working gear — it " +
                "is read by somebody arriving, not by somebody landing fish");
        }
    }
}
