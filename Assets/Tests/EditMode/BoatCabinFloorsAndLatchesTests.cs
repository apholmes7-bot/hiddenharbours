using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Tests.Support;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>Phase B, 2026-09-19, C6 — what the rest of the fleet walks on, one piece at a time.</b> The
    /// fleet guards walk every hull end to end; these pin the pieces those walks stand on, each on a
    /// small hull built here, so a red one names the piece rather than the hull.
    ///
    /// <para>The hull is a sport fisher in section: a cockpit at 0.5 m, a flybridge at 3.3 m over its
    /// forward half, and a house sole at 1.78 m forward of both, joined to the cockpit by a
    /// companionway. Floors are built the way the importer builds them (<see cref="DeckArea.From"/>,
    /// levels as the reader makes them), and the floor tolerance is this file's own 0.1 m, written
    /// here: a guard never asks the code for its bar.</para>
    /// </summary>
    public sealed class BoatCabinFloorsAndLatchesTests
    {
        private const float Iso = 40f;
        private const float Tol = 1e-4f;
        private const float FloorTolerance = 0.1f;

        private const float CockpitZ = 0.5f;
        private const float SoleZ = 1.78f;
        private const float FlybridgeZ = 3.3f;

        /// <summary>The rig's main doorway, in hull metres, on the aft face of the house.</summary>
        private static readonly Vector2 Doorway = new Vector2(0f, -2.1f);

        /// <summary>The rig's side doorway, on the starboard face of the house.</summary>
        private static readonly Vector2 SideDoorway = new Vector2(1.3f, 0.8f);

        /// <summary>The middle of the house sole: 2.1 m from one doorway and 1.53 m from the other,
        /// both beyond the 0.72 m release (a door's clear width).</summary>
        private static readonly Vector3 MidSole = new Vector3(0f, 0f, SoleZ);

        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            GameServices.Config = null;
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        // =====================================================================================
        //  1 · FLOORS STACKED IN PLAN: the deck is asked in height as well as in plan
        // =====================================================================================

        [Test]
        public void WhereTwoDecksStandOverOnePoint_SheIsSeatedOnTheOneAtHerHeight()
        {
            BoatDeckDef deck = Deck(Cockpit(), Flybridge());
            Vector2 under = new Vector2(0.3f, -3.5f);

            int hint = -1;
            Vector2 seat = deck.SeatNearest(new Vector3(under.x, under.y, CockpitZ), ref hint, out float height);
            Assert.AreEqual(0, hint, "at the cockpit's height, the cockpit");
            Assert.AreEqual(CockpitZ, height, Tol);
            Assert.Less(Vector2.Distance(seat, under), Tol, "and where she stood: she is on it already");

            hint = -1;
            seat = deck.SeatNearest(new Vector3(under.x, under.y, FlybridgeZ), ref hint, out height);
            Assert.AreEqual(1, hint, "at the flybridge's height, the flybridge over the same point");
            Assert.AreEqual(FlybridgeZ, height, Tol);
            Assert.Less(Vector2.Distance(seat, under), Tol);

            hint = -1;
            deck.SeatNearest(new Vector3(under.x, under.y, SoleZ), ref hint, out height);
            Assert.AreEqual(0, hint, "from the sole's height, 1.28 m down is nearer than 1.52 m up");
        }

        [Test]
        public void ThereIsDeckUnderAPoint_OnlyToTheToleranceInPlanAndInHeight()
        {
            BoatDeckDef deck = Deck(Cockpit(), Flybridge());

            Assert.IsTrue(deck.HasFloorAt(new Vector3(0f, -5.5f, CockpitZ), FloorTolerance), "on the cockpit");
            Assert.IsTrue(deck.HasFloorAt(new Vector3(0f, -5.5f, CockpitZ + 0.08f), FloorTolerance),
                          "8 cm over it is on it");
            Assert.IsFalse(deck.HasFloorAt(new Vector3(0f, -5.5f, CockpitZ + 0.15f), FloorTolerance),
                           "15 cm over it is not");
            Assert.IsTrue(deck.HasFloorAt(new Vector3(1.58f, -5.5f, CockpitZ), FloorTolerance),
                          "8 cm off its edge is on it");
            Assert.IsFalse(deck.HasFloorAt(new Vector3(1.65f, -5.5f, CockpitZ), FloorTolerance),
                           "15 cm off its edge is not");
            Assert.IsFalse(deck.HasFloorAt(new Vector3(0f, -3.5f, SoleZ), FloorTolerance),
                           "between two decks stacked in plan is on neither");
        }

        // =====================================================================================
        //  2 · ROUTE ENDS: a room, a place on the deck, or nowhere — asked of the data she wears
        // =====================================================================================

        [Test]
        public void ARouteEnd_IsARoomOnADrawnLevel_TheDeckWhereThereIsDeck_AndOtherwiseNowhere()
        {
            BoatDeckDef deck = Deck(Cockpit());
            BoatInteriorDef def = Interior(new[] { HouseSole(), HelmDeck() });
            var floors = new FrozenFloors(def, new[] { true, false });

            Assert.AreEqual(DeckWalkController.RouteEnd.Room,
                            DeckWalkController.ClassifyRouteEnd(floors, deck, "house_sole", MidSole, out int level));
            Assert.AreEqual(0, level);

            Assert.AreEqual(DeckWalkController.RouteEnd.Deck,
                            DeckWalkController.ClassifyRouteEnd(floors, deck, "cockpit",
                                                                new Vector3(0f, -4f, CockpitZ), out level),
                            "an exterior area's name, with deck under it");
            Assert.AreEqual(-1, level);

            Assert.AreEqual(DeckWalkController.RouteEnd.Deck,
                            DeckWalkController.ClassifyRouteEnd(floors, deck, "helm_deck",
                                                                new Vector3(0f, -4f, CockpitZ), out _),
                            "a level no picture draws, with deck under it (the ships' main_deck)");

            Assert.AreEqual(DeckWalkController.RouteEnd.None,
                            DeckWalkController.ClassifyRouteEnd(floors, deck, "helm_deck",
                                                                new Vector3(0f, 0.5f, 4.6f), out _),
                            "a level no picture draws, over nothing (the Convertible's helm_deck): the air");

            Assert.AreEqual(DeckWalkController.RouteEnd.None,
                            DeckWalkController.ClassifyRouteEnd(null, deck, "house_sole", MidSole, out _),
                            "and with no cabin at all, nowhere");
        }

        [Test]
        public void AFloorCarriesARoute_OnlyWhenItIsPlacedAndBothItsEndsStandSomewhere()
        {
            BoatDeckDef deck = Deck(Cockpit());
            BoatInteriorRoute intoTheAir = Route("helm_ladder", "house_sole", new Vector3(0.8f, 1.5f, SoleZ),
                                                 "helm_deck", new Vector3(0.8f, 1.5f, 4.6f));
            BoatInteriorRoute companionway = Companionway();
            BoatInteriorDef def = Interior(new[] { HouseSole(), HelmDeck() }, intoTheAir);
            var floors = new FrozenFloors(def, new[] { true, false });

            Assert.IsFalse(DeckWalkController.CarriesATakeableRoute(floors, deck, "house_sole"),
                           "a stair into the air is no way off her floor");

            def.Routes = new[] { intoTheAir, companionway };
            Assert.IsTrue(DeckWalkController.CarriesATakeableRoute(floors, deck, "house_sole"),
                          "a companionway down to the cockpit is");

            companionway.Placed = false;
            Assert.IsFalse(DeckWalkController.CarriesATakeableRoute(floors, deck, "house_sole"),
                           "an unplaced route is taken by nobody — the cape's and the lobster boat's own");
        }

        // =====================================================================================
        //  3 · THE FLOOR NEAREST A STATION: a room only when it is genuinely nearer, and one she can leave
        // =====================================================================================

        [Test]
        public void LeavingAWheel_SheStandsInTheRoomUnderIt_OnlyWhenTheRoomIsNearerAndHasAWayOff()
        {
            BoatDeckDef deck = Deck(Cockpit());
            BoatInteriorRoute companionway = Companionway();
            BoatInteriorDef def = Interior(new[] { HouseSole() }, companionway);
            FrozenFloors floors = FrozenFloors.EveryUsableLevelDrawn(def);
            Vector3 wheel = new Vector3(0.4f, 1.0f, SoleZ);

            int room = DeckWalkController.ChooseTheFloorNearest(deck, floors, wheel, false, out Vector2 roomSeat,
                                                                out bool measured, out _, out int deckArea,
                                                                out float deckHeight);
            Assert.IsTrue(measured);
            Assert.AreEqual(0, room, "the sole under her wheel, not the cockpit 1.28 m down and 3 m aft");
            Assert.Less(Vector2.Distance(roomSeat, new Vector2(wheel.x, wheel.y)), Tol);
            Assert.AreEqual(0, deckArea, "the deck's own answer is still given");
            Assert.AreEqual(CockpitZ, deckHeight, Tol);

            Assert.AreEqual(-1, DeckWalkController.ChooseTheFloorNearest(deck, floors, wheel, true, out _, out _,
                                                                         out _, out _, out _),
                            "on a washboard no room is asked");

            companionway.Placed = false;
            Assert.AreEqual(-1, DeckWalkController.ChooseTheFloorNearest(deck, floors, wheel, false, out _, out _,
                                                                         out _, out _, out _),
                            "a room with no way off it but her door is walked on the deck, as it always was");
            companionway.Placed = true;

            BoatDeckDef flush = Deck(Cockpit(), Flat("house_top", -1.2f, 1.2f, -2f, 2f, SoleZ));
            Assert.AreEqual(-1, DeckWalkController.ChooseTheFloorNearest(flush, floors, wheel, false, out _, out _,
                                                                         out _, out int flushArea, out _),
                            "the deck keeps a tie: a room has to be genuinely nearer");
            Assert.AreEqual(1, flushArea);
        }

        // =====================================================================================
        //  4 · THE SEED HELD TO A FLOOR: the same answer on one floor, the named floor on two
        // =====================================================================================

        [Test]
        public void OnADeckOfOneFloor_TheSeedHeldToAFloorIsTheSeedItAlwaysWas()
        {
            BoatDeckDef deck = Deck(Cockpit());
            Vector2 spot = new Vector2(0.6f, -3.1f);
            foreach (float heading in new[] { 0f, 35f, 200f })
            {
                Vector2 world = DeckAreaMath.DeckToWorld(spot, CockpitZ, heading, Iso);
                int heldArea = -1, plainArea = -1;
                Vector2 held = DeckWalkController.SeedDeckLocalOnFloorPure(world, heading, Iso, deck, CockpitZ,
                                                                           FloorTolerance, ref heldArea,
                                                                           out float heldHeight);
                Vector2 plain = DeckWalkController.SeedDeckLocalPure(world, heading, Iso, deck, false,
                                                                     ref plainArea, out float plainHeight);

                Assert.Less(Vector2.Distance(held, plain), Tol, $"heading {heading}: one floor, one answer");
                Assert.AreEqual(plainHeight, heldHeight, Tol);
                Assert.AreEqual(plainArea, heldArea);
                Assert.Less(Vector2.Distance(held, spot), 1e-3f, $"heading {heading}: where she was named");
            }
        }

        [Test]
        public void OnTwoFloorsStackedInPlan_TheSeedStaysOnTheFloorItWasNamedOn()
        {
            // The flybridge first, so the order of the areas cannot hand her the lower one.
            BoatDeckDef deck = Deck(Flybridge(), Cockpit());
            Vector2 spot = new Vector2(0.3f, -3.5f);
            const float heading = 20f;

            foreach ((float z, int area) in new[] { (CockpitZ, 1), (FlybridgeZ, 0) })
            {
                Vector2 world = DeckAreaMath.DeckToWorld(spot, z, heading, Iso);
                int hint = -1;
                Vector2 seat = DeckWalkController.SeedDeckLocalOnFloorPure(world, heading, Iso, deck, z,
                                                                           FloorTolerance, ref hint,
                                                                           out float height);
                Assert.AreEqual(z, height, Tol, $"named at {z} m, seated at {z} m");
                Assert.AreEqual(area, hint);
                Assert.Less(Vector2.Distance(seat, spot), 1e-3f, $"named at {z} m: where she was named");
            }
        }

        // =====================================================================================
        //  5 · A STEP ON A LEVEL: the deck walk's step, held to the sole
        // =====================================================================================

        [Test]
        public void AStepOnALevel_IsTheDeckWalksStep_SoAKeyMeansTheSameEitherSideOfADoorway()
        {
            BoatInteriorLevel sole = HouseSole();
            BoatDeckDef deck = Deck(Flat("house_top", -1.2f, 1.2f, -2f, 2f, SoleZ));
            Vector2 from = new Vector2(0.1f, 0.2f);
            const float speed = 2.5f, dt = 0.1f, heading = 30f;

            foreach (Vector2 input in new[] { Vector2.up, Vector2.right, new Vector2(-0.6f, 0.8f) })
            {
                int hint = 0;
                Vector2 onLevel = DeckWalkController.StepOnInteriorLevel(from, input, speed, dt, heading, Iso, sole);
                Vector2 onDeck = DeckWalkController.StepOnDeckPolygon(from, input, speed, dt, heading, Iso, deck,
                                                                      ref hint, out _);
                Assert.Less(Vector2.Distance(onLevel, onDeck), Tol, $"input {input}: the same step as on deck");
                Assert.AreEqual(speed * dt, Vector2.Distance(onLevel, from), Tol, $"input {input}: a whole step");
            }

            // A key held toward the starboard wall, 10 cm from it: the step would carry her 15 cm through.
            Vector2 atTheWall = new Vector2(1.1f, 0f);
            Vector2 intoTheWall = DeckAreaMath.DeckToWorld(Vector2.right, 0f, heading, Iso).normalized;
            Vector2 held = DeckWalkController.StepOnInteriorLevel(atTheWall, intoTheWall, speed, dt, heading, Iso,
                                                                  sole);
            Assert.IsTrue(BoatCabinWalkMath.IsStandable(sole, held), $"a step through the wall leaves her at {held}");
            Assert.LessOrEqual(held.x, 1.2f, "on the sole, not through its wall");
            Assert.Greater(held.x, 1.15f, "and at the wall, not stopped short of it");
        }

        // =====================================================================================
        //  6 · THE LATCH, FROM A FLOOR SHE NAMES: over the doorway on another floor is not in it
        // =====================================================================================

        [Test]
        public void StandingOverTheDoorwayOnAnotherFloor_SheIsNotWalkedThrough_AndKeepsHerApproach()
        {
            Rig rig = NewRig(false);
            Open(rig.Door);

            Assert.IsFalse(rig.Door.TryWalkThroughAt(MidSole));
            Assert.IsTrue(rig.Door.PassageIsArmed, "the premise: her first step, clear of the band, arms it");

            Assert.IsFalse(rig.Door.TryWalkThroughAt(new Vector3(Doorway.x, Doorway.y, FlybridgeZ)),
                           "on the flybridge over the doorway is not in it");
            Assert.IsFalse(rig.Door.TryWalkThroughAt(new Vector3(Doorway.x, Doorway.y, SoleZ + 1.5f * FloorTolerance)),
                           "nor is 15 cm over its sill");
            Assert.IsFalse(rig.Interior.IsInside);
            Assert.IsTrue(rig.Door.PassageIsArmed, "and neither spends her approach: she never crossed");

            Assert.IsTrue(rig.Door.TryWalkThroughAt(new Vector3(Doorway.x, Doorway.y, SoleZ + 0.5f * FloorTolerance)),
                          "5 cm over its sill is on it, and she goes through");
            Assert.IsTrue(rig.Interior.IsInside);
            Assert.IsFalse(rig.Door.PassageIsArmed, "which spends it");
        }

        [Test]
        public void EveryDoorKeepsItsOwnLatch_SoSheGoesInThroughOneAndOutThroughTheOther()
        {
            Rig rig = NewRig(true);
            Open(rig.Door);
            Open(rig.Side);
            Vector3 main = new Vector3(Doorway.x, Doorway.y, SoleZ);
            Vector3 side = new Vector3(SideDoorway.x, SideDoorway.y, SoleZ);

            Assert.IsNull(StepEveryDoor(rig, MidSole), "the middle of the sole is in no doorway");
            Assert.AreSame(rig.Door, StepEveryDoor(rig, main), "in through the main door");
            Assert.IsTrue(rig.Interior.IsInside);
            Assert.IsTrue(rig.Side.PassageIsArmed, "and the side door's approach is untouched");

            Assert.AreSame(rig.Side, StepEveryDoor(rig, side), "out through the side door");
            Assert.IsFalse(rig.Interior.IsInside);
            Assert.IsTrue(rig.Door.PassageIsArmed, "the main door re-armed when she walked clear of it");
            Assert.IsFalse(rig.Side.PassageIsArmed, "and the side door's crossing spent its own approach");
        }

        // =====================================================================================
        //  7 · THE WALK'S OWN STEERING (Tests.Support): what the PlayMode walk presses is what the game reads
        // =====================================================================================

        [Test]
        public void APathOnThePlanRaster_GoesRoundWhatBlocksIt()
        {
            var table = new BoatInteriorObstruction
            {
                Id = "table",
                HeightAboveSoleMeters = 0.75f,
                Footprint = new[]
                {
                    new Vector2(-1.2f, -0.3f), new Vector2(0.8f, -0.3f),
                    new Vector2(0.8f, 0.3f), new Vector2(-1.2f, 0.3f),
                },
            };
            BoatInteriorLevel sole = HouseSole(table);
            var raster = new CabinPlanRaster(CabinPlanRaster.BoxAround(CabinLegs.RasterCell * 2f, sole.Outline),
                                             CabinLegs.RasterCell, p => BoatCabinWalkMath.IsStandable(sole, p));
            Vector2 from = new Vector2(-0.8f, -1.5f), to = new Vector2(-0.8f, 1.5f);

            List<Vector2> path = raster.Path(from, to);

            Assert.IsNotNull(path, "the way round the table's starboard end is open");
            Assert.Less(Vector2.Distance(path[path.Count - 1], to), Tol, "it ends where it was asked to");
            for (int i = 0; i < path.Count - 1; i++)
                Assert.IsTrue(BoatCabinWalkMath.IsStandable(sole, path[i]), $"point {i} {path[i]} is on the sole");
            Assert.IsTrue(path.Exists(p => p.x > 0.8f), "and it goes round the table rather than through it");
        }

        [Test]
        public void ALegSteeredInTheDeckWalksFrame_BringsHerToItsTarget_AtEveryHeading()
        {
            BoatInteriorLevel sole = HouseSole();
            var raster = new CabinPlanRaster(CabinPlanRaster.BoxAround(CabinLegs.RasterCell * 2f, sole.Outline),
                                             CabinLegs.RasterCell, p => BoatCabinWalkMath.IsStandable(sole, p));
            Vector2 from = new Vector2(-0.8f, -1.5f), to = new Vector2(0.9f, 1.6f);
            const float speed = 2.5f, dt = 1f / 60f, lookahead = 0.12f, within = 0.03f;

            foreach (float heading in new[] { 0f, 30f, 145f, 290f })
            {
                var leg = new CabinLeg("across the sole", raster.Path(from, to), to);
                Assert.IsTrue(leg.IsWalkable);
                Vector2 at = from;
                for (int frame = 0; frame < 600 && !leg.Arrived(at, within); frame++)
                    at = DeckWalkController.StepOnInteriorLevel(at, leg.Steer(at, lookahead, speed * dt, heading, Iso),
                                                                speed, dt, heading, Iso, sole);
                Assert.IsTrue(leg.Arrived(at, within), $"heading {heading}: she ended at {at}, aiming for {to}");
            }
        }

        // =====================================================================================
        //  the hull
        // =====================================================================================

        private static DeckArea Flat(string id, float x0, float x1, float y0, float y1, float z) =>
            DeckArea.From(id, DeckAreaKind.Deck, new[]
            {
                new Vector3(x0, y0, z), new Vector3(x1, y0, z), new Vector3(x1, y1, z), new Vector3(x0, y1, z),
            });

        private static DeckArea Cockpit() => Flat("cockpit", -1.5f, 1.5f, -6f, -2f, CockpitZ);

        private static DeckArea Flybridge() => Flat("flybridge", -1.2f, 1.2f, -5f, -1f, FlybridgeZ);

        private static BoatInteriorLevel HouseSole(params BoatInteriorObstruction[] furniture) =>
            new BoatInteriorLevel
            {
                Id = "house_sole",
                SoleZMeters = SoleZ,
                Outline = new[]
                {
                    new Vector2(-1.2f, -2f), new Vector2(1.2f, -2f), new Vector2(1.2f, 2f), new Vector2(-1.2f, 2f),
                },
                Obstructions = furniture,
            };

        /// <summary>A level the rig names and no picture draws: a helm deck up in the air.</summary>
        private static BoatInteriorLevel HelmDeck() =>
            new BoatInteriorLevel
            {
                Id = "helm_deck",
                SoleZMeters = 4.6f,
                Outline = new[]
                {
                    new Vector2(-0.9f, 0f), new Vector2(0.9f, 0f), new Vector2(0.9f, 1.8f), new Vector2(-0.9f, 1.8f),
                },
            };

        private static BoatInteriorRoute Route(string id, string from, Vector3 fromPoint, string to, Vector3 toPoint) =>
            new BoatInteriorRoute
            {
                Id = id,
                FromLevel = from,
                ToLevel = to,
                FromPoint = fromPoint,
                ToPoint = toPoint,
                Placed = true,
            };

        /// <summary>From the house sole at its aft face down to the cockpit, both ends on their floors.</summary>
        private static BoatInteriorRoute Companionway() =>
            Route("companionway", "house_sole", new Vector3(0f, -1.8f, SoleZ), "cockpit",
                  new Vector3(0f, -2.2f, CockpitZ));

        private BoatDeckDef Deck(params DeckArea[] areas)
        {
            var def = ScriptableObject.CreateInstance<BoatDeckDef>();
            def.Id = "deck.floors_and_latches_test";
            def.Areas = areas;
            _spawned.Add(def);
            return def;
        }

        private BoatInteriorDef Interior(BoatInteriorLevel[] levels, params BoatInteriorRoute[] routes)
        {
            var def = ScriptableObject.CreateInstance<BoatInteriorDef>();
            def.Id = "interior.floors_and_latches_test";
            def.PixelsPerMetre = 32;
            def.Levels = levels;
            def.Routes = routes;
            def.FloorToleranceMetres = FloorTolerance;
            _spawned.Add(def);
            return def;
        }

        // =====================================================================================
        //  the doors, wired the way the placement pass wires them
        // =====================================================================================

        private struct Rig
        {
            public BoatInterior Interior;
            public BoatCabinDoor Door;
            public BoatCabinDoor Side;
        }

        /// <summary>Press a door open and run its leaf out.</summary>
        private static void Open(BoatCabinDoor door)
        {
            Assert.IsTrue(door.TryUse());
            door.Tick(door.CueSeconds + 0.01f);
            Assert.IsTrue(door.IsOpen);
        }

        /// <summary>One step, handed to every door the way the deck walk hands it — every door, every
        /// frame — returning the door that took her, if one did.</summary>
        private static BoatCabinDoor StepEveryDoor(Rig rig, Vector3 at)
        {
            BoatCabinDoor took = null;
            if (rig.Door.TryWalkThroughAt(at)) took = rig.Door;
            if (rig.Side != null && rig.Side.TryWalkThroughAt(at)) took = took == null ? rig.Side : null;
            return took;
        }

        private static BoatInteriorDoor MeasuredDoor(string id, string side, Vector2 at) =>
            new BoatInteriorDoor
            {
                Id = id,
                Side = side,
                ClearWidthMeters = 0.72f,
                ClearHeightMeters = 1.85f,
                ThresholdPoint = new Vector3(at.x, at.y, SoleZ),
                Mechanism = BoatInteriorDoorMechanism.Sliding,
                CueFrames = 8,
                CueMillisecondsPerFrame = 70f,
                CueReversedOnExit = true,
            };

        private Rig NewRig(bool withASideDoor)
        {
            BoatInteriorDef def = Interior(new[] { HouseSole() });
            def.Door = MeasuredDoor("entry", "aft", Doorway);
            if (withASideDoor)
                def.AdditionalDoors = new[] { MeasuredDoor("side_entry", "starboard", SideDoorway) };

            var root = new GameObject("FloorsAndLatchesTestHull");
            _spawned.Add(root);
            var exterior = new GameObject("House").AddComponent<SpriteRenderer>();
            exterior.transform.SetParent(root.transform, false);
            var room = new GameObject("Room").AddComponent<SpriteRenderer>();
            room.transform.SetParent(root.transform, false);

            var tex = new Texture2D(4, 4);
            _spawned.Add(tex);
            var cells = new Sprite[8];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 32f);
                _spawned.Add(cells[i]);
            }

            var interior = root.AddComponent<BoatInterior>();
            interior.Configure(def, exterior, room, null, room.transform, root.transform, cells, 8, true, 0f,
                               5f, 1.6f, 0.02f);

            var rig = new Rig { Interior = interior, Door = Door(root, interior, "entry", -1) };
            if (withASideDoor) rig.Side = Door(root, interior, "side_entry", 0);
            return rig;
        }

        private static BoatCabinDoor Door(GameObject root, BoatInterior interior, string id, int additionalIndex)
        {
            var door = new GameObject($"CabinDoor_{id}").AddComponent<BoatCabinDoor>();
            door.transform.SetParent(root.transform, false);
            door.Configure(interior, $"fixture.boat.floors_and_latches_test.{id}", additionalIndex, 1.2f,
                           "Open the door", "Close the door");
            return door;
        }
    }
}
