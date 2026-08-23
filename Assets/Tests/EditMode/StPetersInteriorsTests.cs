using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Guards the PLACEMENT half of the interiors pilot — the pass that stands a baked room under a
    /// placed village building, walls it, furnishes it and hands it a player.
    ///
    /// <para>Everything here runs without a baked sheet on disk. That is deliberate: the bake is the
    /// owner's menu click, and the geometry, the furnishing table and the re-run behaviour are all
    /// checkable before a single pixel exists. What is NOT checkable here is what the room looks like —
    /// see the PR's Play-mode checklist.</para>
    /// </summary>
    public class StPetersInteriorsTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();

        GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>
        /// Nothing is baked in a test run, so anything reaching the contract logs "no contract at …" —
        /// which is the CORRECT behaviour (the bake is the owner's menu click) and would otherwise fail
        /// the test for saying so. Called only by the tests that deliberately walk the missing-art path.
        /// </summary>
        static void AllowTheMissingContractError() => LogAssert.ignoreFailingMessages = true;

        // =============================================================================
        //  the constant that cannot be shared across an assembly boundary
        // =============================================================================

        [Test]
        public void TheRuntimeSquashMatchesTheSharedBakeCamera()
        {
            // BuildingInterior lives in World, which does not reference Art, so it cannot read
            // SpriteLightMath.GroundDepthScale directly and carries a serialized default instead. This
            // test is the mechanism that stops the two drifting: a default that no longer equals the
            // camera would make every interior's collision quietly deeper or shallower than its art.
            var go = Spawn("interior");
            var interior = go.AddComponent<BuildingInterior>();

            Assert.AreEqual(SpriteLightMath.GroundDepthScale, interior.Footprint.DepthScale, 1e-5f,
                            "BuildingInterior's default ground-depth scale must equal sin(40°), the " +
                            "shared bake camera's elevation term");
        }

        [Test]
        public void TheWallThicknessTheColliderUsesIsTheOneTheInsideTestUses()
        {
            // Two numbers that have to agree is the bug; the builder writes its own constants into the
            // component so there is only ever one of each.
            var go = Spawn("interior");
            var interior = go.AddComponent<BuildingInterior>();
            interior.Configure(null, null, null, 6.6f, 8.05f, 4, 8,
                               SpriteLightMath.GroundDepthScale,
                               StPetersInteriors.WallThicknessMetres,
                               StPetersInteriors.DoorwayWidthMetres);

            Assert.AreEqual(StPetersInteriors.WallThicknessMetres, interior.WallThicknessMetres, 1e-6f);
            Assert.AreEqual(StPetersInteriors.DoorwayWidthMetres, interior.DoorwayWidthMetres, 1e-6f);
        }

        [Test]
        public void TheWallIsThickEnoughNotToBeTunnelledAtSprintSpeed()
        {
            // 5.5 m/s sprint at a 0.02 s fixed step is 0.11 m per step. The rig draws a 0.16 m wall,
            // which is inside a factor of 1.5 of that — a fast diagonal into a corner is exactly the
            // input that finds it, so the collider is deliberately thicker than the art.
            const float sprintMetresPerStep = 5.5f * 0.02f;
            Assert.Greater(StPetersInteriors.WallThicknessMetres, sprintMetresPerStep * 2f,
                           "a wall thinner than twice the per-step travel can be walked through");
        }

        // =============================================================================
        //  enter / exit
        // =============================================================================

        [Test]
        public void TheShellAndTheRoomAreNeverBothVisible()
        {
            var (interior, shell, room, props) = MakeInterior(facing: 0, centre: Vector2.zero);
            var occupant = Spawn("player");
            interior.SetOccupant(occupant.transform);

            // OUTSIDE: shell only. (Enable is what OnEnable would have done; EditMode does not run it —
            // the component was added to an already-active object.)
            occupant.transform.position = new Vector3(0f, -40f, 0f);
            Tick(interior);
            Assert.IsTrue(shell.enabled, "outside: the shell is what you see");
            Assert.IsFalse(room.enabled, "…and the room is not, or you would see the furniture through " +
                                         "the walls");
            Assert.IsFalse(props.activeSelf);

            // INSIDE: room only.
            occupant.transform.position = Vector3.zero;
            Tick(interior);
            Assert.IsTrue(interior.IsInside);
            Assert.IsFalse(shell.enabled, "inside: the shell yields");
            Assert.IsTrue(room.enabled);
            Assert.IsTrue(props.activeSelf);

            // BACK OUT.
            occupant.transform.position = new Vector3(0f, -40f, 0f);
            Tick(interior);
            Assert.IsFalse(interior.IsInside);
            Assert.IsTrue(shell.enabled);
            Assert.IsFalse(room.enabled);
        }

        [Test]
        public void StandingInTheWallIsNotInside()
        {
            var (interior, _, _, _) = MakeInterior(facing: 0, centre: Vector2.zero);
            var occupant = Spawn("player");
            interior.SetOccupant(occupant.transform);

            // Just inside the footprint but still in the front wall.
            InteriorFootprint f = interior.Footprint;
            Vector2 inWall = f.ModelToWorld(new Vector2(0f, -8.05f * 0.5f + 0.1f));
            occupant.transform.position = inWall;
            Tick(interior);

            Assert.IsFalse(interior.IsInside,
                           "you are inside once you are PAST the wall — the same moment the doorway " +
                           "lets you through");
        }

        [Test]
        public void ARoomWithNoOccupantStaysShut()
        {
            var (interior, shell, room, _) = MakeInterior(facing: 0, centre: Vector2.zero);
            Tick(interior);

            Assert.IsFalse(interior.IsInside);
            Assert.IsTrue(shell.enabled);
            Assert.IsFalse(room.enabled);
        }

        // =============================================================================
        //  the furnishing table
        // =============================================================================

        [Test]
        public void EveryFurnishingNamesAPropTheKitBakes()
        {
            var baked = new HashSet<string>();
            foreach (var b in InteriorKit.PropSet) baked.Add(b.Key);

            foreach (var room in InteriorKit.RoomSet)
                foreach (var f in StPetersInteriors.FurnishingsFor(room.Key))
                    CollectionAssert.Contains(baked, f.PropKey,
                                              $"'{room.Key}' asks for a '{f.PropKey}' the kit does not " +
                                              "bake — it would be skipped, leaving a hole in the " +
                                              "collision where a solid object should be");
        }

        [Test]
        public void NoFurnishingStandsInsideAWallOrTheDoorway()
        {
            // ⚠️ Every room, at ITS OWN dimensions. This used to hard-code the cottage's 6.6 × 8.05 m
            // and read only the cottage's list, so the moment a second room was furnished it was
            // silently checking three rooms' worth of nothing — and a chair in the schoolroom's wall
            // would have passed. The rooms differ by 1.3 m of width and 2.3 m of length across the set.
            float wall = StPetersInteriors.WallThicknessMetres;
            float halfDoor = StPetersInteriors.DoorwayWidthMetres * 0.5f;

            foreach (var room in InteriorKit.RoomSet)
            {
                (float halfWidth, float halfLength) = HalfFootprint(room);
                var furnishings = StPetersInteriors.FurnishingsFor(room.Key);

                foreach (var f in furnishings)
                {
                    Assert.Less(Mathf.Abs(f.RoomMetres.x), halfWidth - wall,
                                $"'{room.Key}': '{f.PropKey}' at {f.RoomMetres} is inside a side wall " +
                                $"(the room is {halfWidth * 2:0.00} m across)");
                    Assert.Less(Mathf.Abs(f.RoomMetres.y), halfLength - wall,
                                $"'{room.Key}': '{f.PropKey}' at {f.RoomMetres} is inside the front or " +
                                $"back wall (the room is {halfLength * 2:0.00} m deep)");

                    // ⭐ The doorway lane. A prop parked in the threshold is a prop you cannot get past,
                    // and its collider closes the ONE gap in the house — you would be locked out of
                    // your own cottage by a chair.
                    bool inDoorLane = Mathf.Abs(f.RoomMetres.x) < halfDoor + 0.5f &&
                                      f.RoomMetres.y < -halfLength + 2.0f;
                    Assert.IsFalse(inDoorLane,
                                   $"'{room.Key}': '{f.PropKey}' at {f.RoomMetres} is parked in the " +
                                   "doorway");
                }
            }
        }

        [Test]
        public void NoFurnishingSTICKSOUTOFTheHouse_ItsFootprintCounts_NotJustItsCentre()
        {
            // The centre test above is not enough: a bed is 1.50 × 2.05 m, so a centre that clears the
            // wall by 0.2 m still puts three quarters of a metre of bed past it. The prop's own
            // footprint comes from the bake's contract, which is also the rectangle its collider is
            // built from — so this is what the player actually bumps into.
            //
            // ⚠️ THE BOUND IS THE WALL'S OUTER FACE, NOT ITS INNER ONE, and that is deliberate.
            // Furniture is SUPPOSED to sit flush: the shipped cottage's bed reaches 3.10 m in a room
            // whose inner face is at 3.00 m, i.e. 100 mm into the wall's thickness — which is what
            // "against the wall" looks like, and it has always drawn correctly. Asserting the inner
            // face would fail the owner's own approved arrangement. What is never acceptable is a prop
            // protruding through the wall into the open air outside the house.
            InteriorKit.Contract contract = InteriorKit.Load();
            if (contract?.props == null || contract.props.Length == 0)
                Assert.Ignore("no baked interior contract — nothing to read prop footprints from");

            foreach (var room in InteriorKit.RoomSet)
            {
                (float halfWidth, float halfLength) = HalfFootprint(room);

                foreach (var f in StPetersInteriors.FurnishingsFor(room.Key))
                {
                    InteriorKit.Entry prop = InteriorKit.FindProp(contract, f.PropKey);
                    if (prop == null) continue;             // covered by EveryFurnishingNamesAProp…

                    // ⚠️ A FacingOffset is a 45° step, not a 90° one. Only 2 and 6 present the prop's
                    // depth across and its width along; 0 and 4 leave it square to the room; and the
                    // four ODD offsets stand it on the diagonal, where the honest bound is the
                    // half-diagonal in both axes.
                    float w = prop.propFootprintWidth, d = prop.propFootprintDepth;
                    int q = ((f.FacingOffset % 8) + 8) % 8;
                    float across, along;
                    if (q % 2 != 0) across = along = Mathf.Sqrt(w * w + d * d);
                    else if (q == 2 || q == 6) { across = d; along = w; }
                    else { across = w; along = d; }

                    Assert.LessOrEqual(Mathf.Abs(f.RoomMetres.x) + across * 0.5f, halfWidth + 1e-3f,
                                       $"'{room.Key}': '{f.PropKey}' at {f.RoomMetres} is " +
                                       $"{across:0.00} m across, so it reaches " +
                                       $"{Mathf.Abs(f.RoomMetres.x) + across * 0.5f:0.00} m and sticks " +
                                       $"out through the side wall of a {halfWidth * 2:0.00} m room");
                    Assert.LessOrEqual(Mathf.Abs(f.RoomMetres.y) + along * 0.5f, halfLength + 1e-3f,
                                       $"'{room.Key}': '{f.PropKey}' at {f.RoomMetres} is " +
                                       $"{along:0.00} m deep, so it reaches " +
                                       $"{Mathf.Abs(f.RoomMetres.y) + along * 0.5f:0.00} m and sticks " +
                                       $"out through the front or back wall of a " +
                                       $"{halfLength * 2:0.00} m room");
                }
            }
        }

        [Test]
        public void TheFurnishingsDoNotAllPileUpOnOnePoint()
        {
            foreach (var room in InteriorKit.RoomSet)
            {
                var seen = new List<Vector2>();
                foreach (var f in StPetersInteriors.FurnishingsFor(room.Key))
                {
                    foreach (Vector2 other in seen)
                        Assert.Greater(Vector2.Distance(f.RoomMetres, other), 0.3f,
                                       $"'{room.Key}': two pieces of furniture on top of each other " +
                                       "read as one, and their colliders merge into a blob");
                    seen.Add(f.RoomMetres);
                }
                Assert.GreaterOrEqual(seen.Count, 4,
                                      $"'{room.Key}' is baked but barely furnished — a room the player " +
                                      "can walk into should look lived in, not like a rehearsal space");
            }
        }


        // =============================================================================
        //  THE PILLOW — content validation, over BOTH furnishing tables
        // =============================================================================

        /// <summary>
        /// Every furnishing the island places, from BOTH tables — a building's own room
        /// (<see cref="StPetersInteriors.FurnishingsFor"/>) and every declared storey above it
        /// (<see cref="StPetersInteriors.UpperLevelFor"/>, whose plan furnishes the floor above AND adds
        /// to the one below).
        ///
        /// <para><b>Walking one table is walking half the beds.</b> The two that matter most — yours and
        /// your host's, upstairs at Ginny's — are only in the second, so a check that reads
        /// <c>FurnishingsFor</c> alone passes an unvalidated player's bed. That is the exact shape of the
        /// gap this whole file exists to close.</para>
        /// </summary>
        static IEnumerable<(string Where, StPetersInteriors.Furnishing F)> EveryFurnishing()
        {
            foreach (var room in InteriorKit.RoomSet)
                foreach (var f in StPetersInteriors.FurnishingsFor(room.Key))
                    yield return ($"room '{room.Key}'", f);

            foreach (string planKey in StPetersInteriors.UpperLevelPlanKeys)
            {
                if (!StPetersInteriors.HasUpperLevel(planKey)) continue;
                StPetersInteriors.UpperLevelPlan plan = StPetersInteriors.UpperLevelFor(planKey);

                foreach (var f in plan.Furnishings)
                    yield return ($"upstairs at '{planKey}'", f);

                if (plan.GroundAdditions == null) continue;
                foreach (var f in plan.GroundAdditions)
                    yield return ($"the storey below '{planKey}'", f);
            }
        }

        /// <summary>A bed row exactly as a builder table would carry one, for the failure cases below.</summary>
        static StPetersInteriors.Furnishing Bed(int facingOffset, PillowSide pillow) =>
            new StPetersInteriors.Furnishing("bed", new Vector2(-2.10f, -2.30f), facingOffset,
                                             "a test bed", StPetersInteriors.InteriorFixture.PlayerBed,
                                             pillow);

        [Test]
        public void EveryBedInBOTHTablesDeclaresWhichEndItsPillowIsOn()
        {
            // ⭐ THE RULE. A bed that never says draws perfectly and is slept on backwards, so the only
            // thing that can catch it is a check that walks every bed the island places and asks.
            int beds = 0;
            foreach (var (where, f) in EveryFurnishing())
            {
                if (!f.IsBed) continue;
                beds++;

                Assert.IsFalse(StPetersInteriors.TryFindPillowFault(f, out string fault),
                               $"{where}: {fault}");
            }

            Assert.GreaterOrEqual(beds, 4,
                                  "the island places four beds — two upstairs at Ginny's and one in each " +
                                  "of the two furnished cottages. Finding fewer means this walk stopped " +
                                  "reading one of the two tables, which is how an unvalidated bed ships");
        }

        [Test]
        public void ABedWithNoDeclarationFAILSContentValidation()
        {
            // The trip-wire, exercised. This is the state a new bed row starts in.
            Assert.IsTrue(StPetersInteriors.TryFindPillowFault(Bed(0, PillowSide.Undeclared),
                                                               out string fault));
            StringAssert.Contains("PillowSide", fault,
                                  "the complaint must say what to add, not merely that something is wrong");
        }

        [Test]
        public void ABedTurnedAwayFromItsOwnDeclarationFAILSContentValidation()
        {
            // The authoring slip that survives a "did you declare it?" check: the bed was turned a quarter
            // and the declaration stayed where it was. The pose would follow the declaration and the
            // headboard would be at the other end — and it would look like art.
            Assert.IsTrue(StPetersInteriors.TryFindPillowFault(Bed(2, PillowSide.MinusY),
                                                               out string fault));
            StringAssert.Contains("MinusX", fault, "and it must name where the art actually draws it");
        }

        [Test]
        public void ABedStoodOnTheDIAGONALFAILSContentValidation()
        {
            // An odd facing offset is a 45° step. No cardinal side is the truth about that bed, so the
            // honest answer is to refuse it rather than round it to the nearer wall.
            Assert.IsTrue(StPetersInteriors.TryFindPillowFault(Bed(1, PillowSide.MinusY),
                                                              out string fault));
            StringAssert.Contains("EVEN", fault);
        }

        [Test]
        public void ABedTurnedWITHItsDeclarationPasses()
        {
            // The other half of the rule: turning IS allowed, it just has to be said. A quarter-turn puts
            // the kit bed's -y head end on the room's -x.
            Assert.IsFalse(StPetersInteriors.TryFindPillowFault(Bed(2, PillowSide.MinusX), out string fault),
                           fault);
            Assert.IsFalse(StPetersInteriors.TryFindPillowFault(Bed(4, PillowSide.PlusY), out fault), fault);
            Assert.IsFalse(StPetersInteriors.TryFindPillowFault(Bed(6, PillowSide.PlusX), out fault), fault);
        }

        [Test]
        public void APropThatIsNotABedIsAskedNothingAboutPillows()
        {
            // A chair with no pillow side is a chair, not a fault — the check has to be about beds or the
            // table becomes ceremony and people stop reading the failures.
            foreach (var (where, f) in EveryFurnishing())
            {
                if (f.IsBed) continue;

                Assert.AreEqual(PillowSide.Undeclared, f.Pillow,
                                $"{where}: '{f.PropKey}' is not a bed and must not claim a pillow side");
                Assert.IsFalse(StPetersInteriors.TryFindPillowFault(f, out _),
                               $"{where}: '{f.PropKey}' is not a bed and must not be asked for one");
            }
        }

        [Test]
        public void TheDeclaredSideIsTheSideTheKitActuallyDrawsThePillowOn()
        {
            // Stated separately from the fault check so a regression reads as "the art and the table
            // disagree" rather than as a generic validation failure.
            foreach (var (where, f) in EveryFurnishing())
            {
                if (!f.IsBed) continue;

                // A fixture bed built out of a prop the kit gives no head end to (a sea chest standing in
                // for a bunk) has nothing to compare against — the same case TryFindPillowFault lets
                // through, and the two must not disagree about it.
                if (f.DrawnPillowSide == PillowSide.Undeclared) continue;

                Assert.AreEqual(f.DrawnPillowSide, f.Pillow,
                                $"{where}: the bed at {f.RoomMetres} is drawn with its head end on " +
                                $"{f.DrawnPillowSide} but declares {f.Pillow}");
            }
        }

        [Test]
        public void ThePillowReachIsTheBAKESDepthMinusTheRIGSInset()
        {
            InteriorKit.Contract contract = InteriorKit.Load();
            if (contract?.props == null || contract.props.Length == 0)
                Assert.Ignore("no baked interior contract — nothing to read prop depths from");

            InteriorKit.Entry bed = InteriorKit.FindProp(contract, "bed");
            if (bed == null) Assert.Ignore("the contract carries no bed");

            // Neither number is typed into the builder: the depth is the bake's and the inset is the rig's,
            // so a re-bake at another size moves the pillow with the bed rather than leaving a tuned
            // constant pointing at where it used to be.
            Assert.AreEqual(bed.propFootprintDepth * 0.5f - InteriorKit.BedPillowInsetMetres,
                            StPetersInteriors.PillowReachMetresFor("bed", bed.propFootprintDepth), 1e-5f);

            Assert.Greater(StPetersInteriors.PillowReachMetresFor("bed", bed.propFootprintDepth), 0f,
                           "a reach of zero would put the sleeper's head in the middle of the mattress, " +
                           "which is the picture an UNDECLARED bed gets");
            Assert.Less(StPetersInteriors.PillowReachMetresFor("bed", bed.propFootprintDepth),
                        bed.propFootprintDepth * 0.5f,
                        "and a reach past the half-depth would put the head off the end of the bed");
        }

        [Test]
        public void APropWithNoPillowHasNoReach()
        {
            Assert.AreEqual(0f, StPetersInteriors.PillowReachMetresFor("chair", 0.42f), 1e-6f);
            Assert.AreEqual(0f, StPetersInteriors.PillowReachMetresFor("no_such_prop", 2f), 1e-6f);
        }

        [Test]
        public void EachDeclaredBedYieldsAHeadPositionOnItsOwnPillowSide_AtEveryFacing()
        {
            // ⭐ THE ACCEPTANCE CRITERION. Placed exactly as the builder places it — model point through
            // the room's own footprint — and then read back in the room's frame, which is the frame the
            // declaration is in. Doing the round trip is the point: it is the same arithmetic the runtime
            // does, so a sign error anywhere in it fails here rather than in the owner's bedroom.
            InteriorKit.Contract contract = InteriorKit.Load();
            if (contract?.props == null || contract.props.Length == 0)
                Assert.Ignore("no baked interior contract — nothing to read prop depths from");

            int checkedBeds = 0;

            foreach (var (where, f) in EveryFurnishing())
            {
                if (!f.IsBed) continue;

                InteriorKit.Entry prop = InteriorKit.FindProp(contract, f.PropKey);
                if (prop == null) continue;             // covered by EveryFurnishingNamesAProp…

                float reach = StPetersInteriors.PillowReachMetresFor(f.PropKey, prop.propFootprintDepth);
                Vector2 want = BedPillow.Direction(f.Pillow);
                checkedBeds++;

                for (int facing = 0; facing < InteriorKit.Facings; facing++)
                {
                    // The cottage's own footprint; every furnished room on the island is this shape or
                    // bigger, and the assertion below is frame-relative so the size only has to be real.
                    var footprint = new InteriorFootprint(new Vector2(11f, -4f), 6.6f, 8.05f,
                                                          facing, InteriorKit.Facings,
                                                          SpriteLightMath.GroundDepthScale);

                    Vector2 bedWorld = footprint.ModelToWorld(f.RoomMetres);
                    Vector2 headWorld = BedPillow.HeadWorld(bedWorld, f.Pillow, footprint.YawDegrees,
                                                            reach, footprint.DepthScale);

                    Vector2 headModel = footprint.WorldToModel(headWorld);
                    Vector2 travelled = headModel - f.RoomMetres;

                    Assert.AreEqual(want.x * reach, travelled.x, 2e-3f,
                                    $"{where}, facing {facing}: the head must land {reach:0.###} m " +
                                    $"toward {f.Pillow}, across the room");
                    Assert.AreEqual(want.y * reach, travelled.y, 2e-3f,
                                    $"{where}, facing {facing}: the head must land {reach:0.###} m " +
                                    $"toward {f.Pillow}, along the room");

                    // And it is ON the bed, not off the end of it — the failure a too-generous inset
                    // would produce, which draws as a fisher with her head on the floorboards.
                    Assert.LessOrEqual(travelled.magnitude, prop.propFootprintDepth * 0.5f + 1e-3f,
                                       $"{where}, facing {facing}: the head is off the end of the bed");
                }
            }

            Assert.GreaterOrEqual(checkedBeds, 4, "all four of the island's beds must have been checked");
        }

        /// <summary>Half the room's footprint in metres, from its own dialled <c>size</c>. Both rigs
        /// derive <c>Wd = 6 + size*2.4</c> and <c>Ln = 7 + size*4.2</c> from it (mirrored in
        /// <c>InteriorKitTests.Footprint</c>, which is where the shell/room agreement is pinned).</summary>
        static (float HalfWidth, float HalfLength) HalfFootprint(InteriorKit.Build room)
        {
            Assert.IsNotNull(room.Dialled, $"room '{room.Key}' must be dialled to have a size");
            Assert.IsTrue(room.Dialled.TryGetValue("size", out object v),
                          $"room '{room.Key}' must dial an explicit size");
            float size = System.Convert.ToSingle(v);
            return ((6f + size * 2.4f) * 0.5f, (7f + size * 4.2f) * 0.5f);
        }

        [Test]
        public void AnUnfurnishedRoomKeyReturnsAnEmptyTableRatherThanThrowing()
        {
            CollectionAssert.IsEmpty(StPetersInteriors.FurnishingsFor("noSuchRoom"));
        }

        // =============================================================================
        //  re-runnable
        // =============================================================================

        [Test]
        public void StandingTwiceLeavesOneRoom_NotTwo()
        {
            // The builder re-runs on every region build, and the whole scene is authored from it. A
            // pass that appended would leave the owner with two rooms, two sets of furniture and two
            // sets of wall colliders inside one cottage — and the second set is invisible.
            AllowTheMissingContractError();
            var building = Spawn("sageCottage");
            var shell = building.AddComponent<SpriteRenderer>();

            // No sheet is baked in a test run, so Stand() declines — which is exactly the partial-art
            // path the builder has to survive. What it must NOT do is leave debris behind.
            StPetersInteriors.Stand(building, shell, "sageCottage", 4, null);
            StPetersInteriors.Stand(building, shell, "sageCottage", 4, null);

            Assert.LessOrEqual(CountChildren(building, StPetersInteriors.RoomChildName), 1);
            Assert.LessOrEqual(CountChildren(building, StPetersInteriors.PropsChildName), 1);
            Assert.LessOrEqual(CountChildren(building, StPetersInteriors.WallsChildName), 1);
            Assert.LessOrEqual(building.GetComponents<BuildingInterior>().Length, 1);
        }

        [Test]
        public void StandingOnABuildingWithNoBakedRoomChangesNothing()
        {
            AllowTheMissingContractError();

            // ⚠️ Derived, not named. This used to say "school", which was a building with no room right
            // up until the school got one — and then the test failed for the best possible reason
            // while still claiming "four of the five have no room baked". Ask the kit instead.
            string unroomed = null;
            foreach (var b in VillageBuildingKit.M1Set)
                if (InteriorKit.FindRoom(b.Key) == null) { unroomed = b.Key; break; }

            if (unroomed == null)
                Assert.Ignore("every village building now has a baked room — there is no un-roomed " +
                              "case left to exercise, which is a good problem");

            var building = Spawn(unroomed);
            var shell = building.AddComponent<SpriteRenderer>();

            bool stood = StPetersInteriors.Stand(building, shell, unroomed, 4, null);

            Assert.IsFalse(stood, $"'{unroomed}' has no room baked for it");
            Assert.AreEqual(0, building.transform.childCount);
            Assert.IsTrue(shell.enabled, "and its shell is left exactly as the village placed it");
        }

        // =============================================================================
        //  helpers
        // =============================================================================

        /// <summary>A BuildingInterior wired to real renderers, at the pilot cottage's footprint.</summary>
        (BuildingInterior, SpriteRenderer, SpriteRenderer, GameObject) MakeInterior(int facing,
                                                                                    Vector2 centre)
        {
            var building = Spawn("building");
            building.transform.position = centre;
            var shell = building.AddComponent<SpriteRenderer>();

            var roomGo = new GameObject("Interior");
            roomGo.transform.SetParent(building.transform, false);
            var room = roomGo.AddComponent<SpriteRenderer>();
            room.enabled = false;

            var propsGo = new GameObject("Furniture");
            propsGo.transform.SetParent(building.transform, false);
            propsGo.SetActive(false);

            var interior = building.AddComponent<BuildingInterior>();
            interior.Configure(shell, room, propsGo.transform, 6.6f, 8.05f, facing, 8,
                               SpriteLightMath.GroundDepthScale,
                               StPetersInteriors.WallThicknessMetres,
                               StPetersInteriors.DoorwayWidthMetres);

            return (interior, shell, room, propsGo);
        }

        /// <summary>Drive one frame of the component. EditMode runs no game loop, so <c>Update</c> is
        /// invoked directly — the whole point of deciding "inside" with a pure function is that this
        /// works at all. Via reflection, NOT <c>SendMessage</c>: in edit mode the engine gates magic
        /// methods behind its internal <c>ShouldRunBehaviour()</c> check, and SendMessage("Update") on
        /// a plain MonoBehaviour trips that assert as an [Assert] log the test framework then fails —
        /// which is what turned every Tick-driven test red on CI while the logic itself was correct.</summary>
        static void Tick(BuildingInterior interior) =>
            typeof(BuildingInterior)
                .GetMethod("Update", System.Reflection.BindingFlags.Instance |
                                     System.Reflection.BindingFlags.NonPublic)
                .Invoke(interior, null);

        static int CountChildren(GameObject go, string name)
        {
            int n = 0;
            for (int i = 0; i < go.transform.childCount; i++)
                if (go.transform.GetChild(i).name == name) n++;
            return n;
        }
    }
}
