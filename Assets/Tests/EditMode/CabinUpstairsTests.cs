using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Guards the storey above Ginny's cottage: the level swap itself, the fixtures that drive it, the
    /// manual-save seam behind the player's bed, and the plan's geometry.
    ///
    /// <para>None of this needs a baked room on disk. The swap is three references and an int, the
    /// fixtures are pure components, and the plan is a table of metres that can be checked against the
    /// footprint it was authored for — so the whole contract is provable before the owner's next bake.
    /// What is NOT provable here is whether the upstairs LOOKS right, which is the PR's play checklist.</para>
    /// </summary>
    public class CabinUpstairsTests
    {
        // =============================================================================
        //  fixtures
        // =============================================================================

        readonly List<Object> _spawned = new List<Object>();

        /// <summary>The cottage's own numbers, restated so the geometry assertions below are anchored to
        /// something and not to themselves. sageCottage is 6.6 x 8.05 m (StPetersInteriors' own table
        /// says so in prose); the walls are StPetersInteriors.WallThicknessMetres thick.</summary>
        const float CottageWidth = 6.6f;
        const float CottageLength = 8.05f;

        /// <summary>A save service that behaves the way the real one does in the one respect the rest
        /// service contract cares about: LoadedExistingSave goes true once a write has landed. A fake
        /// that left it false would make every rest report a refusal.</summary>
        sealed class FakeSaveService : ISaveService
        {
            public int Saves;
            public SaveData Current { get; } = new SaveData();
            public bool LoadedExistingSave { get; private set; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { Saves++; LoadedExistingSave = true; }
        }

        /// <summary>A save service that refuses every write — the shape of the real one at the title.</summary>
        sealed class RefusingSaveService : ISaveService
        {
            public int Saves;
            public SaveData Current { get; } = new SaveData();
            public bool LoadedExistingSave => false;
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { Saves++; }
        }

        T Spawn<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go.AddComponent<T>();
        }

        readonly List<DevNotice> _notices = new List<DevNotice>();
        readonly List<GameSaved> _saved = new List<GameSaved>();
        readonly List<WardrobeRequested> _wardrobe = new List<WardrobeRequested>();

        [SetUp]
        public void SetUp()
        {
            Interactables.Clear();
            _notices.Clear();
            _saved.Clear();
            _wardrobe.Clear();

            EventBus.Clear<DevNotice>();
            EventBus.Clear<GameSaved>();
            EventBus.Clear<WardrobeRequested>();
            EventBus.Clear<RestSaveRequested>();
            EventBus.Clear<PlayerWokeAt>();
            GameServices.CurrentRegionId = null;

            EventBus.Subscribe<DevNotice>(_notices.Add);
            EventBus.Subscribe<GameSaved>(_saved.Add);
            EventBus.Subscribe<WardrobeRequested>(_wardrobe.Add);
        }

        [TearDown]
        public void TearDown()
        {
            RestSaveResponder.Uninstall();
            RestWakeRestorer.Uninstall();
            GameServices.Save = null;
            GameServices.CurrentRegionId = null;
            GameServices.PlayerTransform = null;

            EventBus.Clear<DevNotice>();
            EventBus.Clear<GameSaved>();
            EventBus.Clear<WardrobeRequested>();
            EventBus.Clear<RestSaveRequested>();
            EventBus.Clear<PlayerWokeAt>();

            Interactables.Clear();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        /// <summary>A configured interior with a ground floor and a storey above, standing at the
        /// origin. Renderers and roots are real objects so the swap can be observed the way the game
        /// observes it — through what is enabled.</summary>
        BuildingInterior StandTwoStorey(out SpriteRenderer shell, out SpriteRenderer ground,
                                        out GameObject groundProps, out SpriteRenderer upper,
                                        out GameObject upperProps, out GameObject upperWalls)
        {
            var buildingGo = new GameObject("cottage");
            _spawned.Add(buildingGo);

            shell = new GameObject("Shell").AddComponent<SpriteRenderer>();
            ground = new GameObject("Interior").AddComponent<SpriteRenderer>();
            upper = new GameObject("InteriorUpper").AddComponent<SpriteRenderer>();
            groundProps = new GameObject("Furniture");
            upperProps = new GameObject("FurnitureUpper");
            upperWalls = new GameObject("WallsUpper");

            foreach (var t in new[] { shell.transform, ground.transform, upper.transform,
                                      groundProps.transform, upperProps.transform, upperWalls.transform })
                t.SetParent(buildingGo.transform, worldPositionStays: false);

            var interior = buildingGo.AddComponent<BuildingInterior>();
            interior.Configure(shell, ground, groundProps.transform,
                               CottageWidth, CottageLength, facing: 0, facings: 8,
                               groundDepthScale: 0.6427876f,
                               wallThicknessMetres: StPetersInteriors.WallThicknessMetres,
                               doorwayWidthMetres: StPetersInteriors.DoorwayWidthMetres);
            interior.ConfigureUpperLevel(upper, upperProps.transform, upperWalls.transform);
            return interior;
        }

        /// <summary>Drive one frame of the component. EditMode runs no game loop, so <c>Update</c> is
        /// invoked directly — via reflection and NOT <c>SendMessage</c>, which trips the engine's
        /// edit-mode <c>ShouldRunBehaviour</c> assert (the idiom <c>StPetersInteriorsTests</c>
        /// established, and the reason it is written this way there too).</summary>
        static void Tick(BuildingInterior interior) =>
            typeof(BuildingInterior)
                .GetMethod("Update", System.Reflection.BindingFlags.Instance |
                                     System.Reflection.BindingFlags.NonPublic)
                .Invoke(interior, null);

        /// <summary>Walk an occupant to the middle of the room and tick, so the interior is genuinely
        /// occupied rather than merely told that it is.</summary>
        Transform WalkInside(BuildingInterior interior)
        {
            var occupant = new GameObject("occupant");
            _spawned.Add(occupant);
            interior.SetOccupant(occupant.transform);
            occupant.transform.position = interior.transform.position;
            Tick(interior);
            Assert.IsTrue(interior.IsInside, "precondition: the occupant is in the room");
            return occupant.transform;
        }

        /// <summary>
        /// ⚠️ REGISTER EXPLICITLY, AND DO NOT "TIDY THIS AWAY". EditMode fires no <c>OnEnable</c> on a
        /// plain MonoBehaviour — <c>AddComponent</c> only wakes an <c>[ExecuteAlways]</c> one — so a
        /// fixture that relied on the components registering themselves would run against an EMPTY
        /// registry. That is not merely missing coverage: every "nothing resolves here" assertion would
        /// then pass for the wrong reason. This does by hand exactly what the runtime does by hook, and
        /// the hook itself is proved in <c>CabinUpstairsPlayTests</c>, where OnEnable/OnDisable run.
        /// </summary>
        static void Register(IInteractable candidate) => Interactables.Register(candidate);

        /// <summary>Walk them back out into the dooryard and tick.</summary>
        static void WalkOutside(BuildingInterior interior, Transform occupant)
        {
            occupant.position = interior.transform.position + new Vector3(0f, -40f, 0f);
            Tick(interior);
            Assert.IsFalse(interior.IsInside, "precondition: the occupant is outside again");
        }

        // =============================================================================
        //  the level swap
        // =============================================================================

        [Test]
        public void A_two_storey_building_starts_outside_on_the_ground_floor_with_nothing_showing()
        {
            var interior = StandTwoStorey(out var shell, out var ground, out var groundProps,
                                          out var upper, out var upperProps, out var upperWalls);

            Assert.IsFalse(interior.IsInside, "a building nobody has entered is not occupied");
            Assert.AreEqual(0, interior.Level, "and it opens on its ground floor, never upstairs");
            Assert.IsTrue(interior.HasUpperLevel, "this one does have a storey above");
            Assert.AreEqual(1, interior.TopLevel);

            Assert.IsTrue(shell.enabled, "the shell is what you see from the dooryard");
            Assert.IsFalse(ground.enabled, "and neither room is drawn");
            Assert.IsFalse(upper.enabled);
            Assert.IsFalse(groundProps.activeSelf, "nor is either storey's furniture");
            Assert.IsFalse(upperProps.activeSelf);
            Assert.IsFalse(upperWalls.activeSelf,
                           "and the partitions upstairs are not standing in the dooryard either");
        }

        [Test]
        public void A_building_with_no_upper_level_reports_none_and_cannot_be_sent_upstairs()
        {
            var buildingGo = new GameObject("village cottage");
            _spawned.Add(buildingGo);
            var interior = buildingGo.AddComponent<BuildingInterior>();
            interior.Configure(null, null, null, CottageWidth, CottageLength, 0, 8, 0.6427876f,
                               StPetersInteriors.WallThicknessMetres,
                               StPetersInteriors.DoorwayWidthMetres);

            Assert.IsFalse(interior.HasUpperLevel,
                           "every building but one has no storey above, and says so");
            Assert.AreEqual(0, interior.TopLevel);
            Assert.IsFalse(interior.TryGoToLevel(1),
                           "and a stair wired into one by mistake is inert, not wrong");
            Assert.AreEqual(0, interior.Level);
        }

        [Test]
        public void You_cannot_climb_a_stair_from_outside_the_house()
        {
            var interior = StandTwoStorey(out _, out _, out _, out _, out _, out _);

            Assert.IsFalse(interior.IsInside, "precondition: the occupant is in the dooryard");
            Assert.IsFalse(interior.TryGoToLevel(1),
                           "and a stair is not reachable through a wall");
            Assert.AreEqual(0, interior.Level);
        }

        [Test]
        public void Going_upstairs_hides_the_ground_floor_and_shows_the_storey_above()
        {
            var interior = StandTwoStorey(out var shell, out var ground, out var groundProps,
                                          out var upper, out var upperProps, out var upperWalls);
            WalkInside(interior);

            // INSIDE, ground floor.
            Assert.IsFalse(shell.enabled, "inside, the shell yields");
            Assert.IsTrue(ground.enabled);
            Assert.IsTrue(groundProps.activeSelf);
            Assert.IsFalse(upper.enabled, "and the storey above is not drawn over it");
            Assert.IsFalse(upperProps.activeSelf);
            Assert.IsFalse(upperWalls.activeSelf,
                           "nor are its partitions standing in the middle of the kitchen");

            // UP.
            Assert.IsTrue(interior.TryGoToLevel(1));
            Assert.AreEqual(1, interior.Level);
            Assert.IsFalse(shell.enabled, "you are still indoors");
            Assert.IsFalse(ground.enabled, "the ground floor is switched OFF, not sorted behind");
            Assert.IsFalse(groundProps.activeSelf,
                           "which is what keeps the two storeys out of the Y-sort argument entirely");
            Assert.IsTrue(upper.enabled);
            Assert.IsTrue(upperProps.activeSelf);
            Assert.IsTrue(upperWalls.activeSelf, "and the partitions divide the bedrooms only up here");

            // DOWN.
            Assert.IsTrue(interior.TryGoToLevel(0));
            Assert.AreEqual(0, interior.Level);
            Assert.IsTrue(ground.enabled);
            Assert.IsFalse(upper.enabled);
            Assert.IsFalse(upperWalls.activeSelf);
        }

        [Test]
        public void Leaving_the_house_puts_you_back_on_the_ground_floor()
        {
            var interior = StandTwoStorey(out var shell, out var ground, out _,
                                          out var upper, out _, out var upperWalls);
            var occupant = WalkInside(interior);
            Assert.IsTrue(interior.TryGoToLevel(1));

            WalkOutside(interior, occupant);

            Assert.AreEqual(0, interior.Level,
                            "a house that remembered 'upstairs' would open on its bedroom next time");
            Assert.IsTrue(shell.enabled);
            Assert.IsFalse(ground.enabled);
            Assert.IsFalse(upper.enabled, "and nothing of the upstairs is left drawn over the dooryard");
            Assert.IsFalse(upperWalls.activeSelf);
        }

        [Test]
        public void Walking_back_in_after_a_visit_upstairs_opens_on_the_ground_floor()
        {
            var interior = StandTwoStorey(out _, out var ground, out _, out var upper, out _, out _);
            var occupant = WalkInside(interior);
            interior.TryGoToLevel(1);
            WalkOutside(interior, occupant);

            occupant.position = interior.transform.position;
            Tick(interior);

            Assert.IsTrue(interior.IsInside);
            Assert.AreEqual(0, interior.Level, "you come in through the front door, not the bedroom");
            Assert.IsTrue(ground.enabled);
            Assert.IsFalse(upper.enabled);
        }

        [Test]
        public void Both_halves_of_the_stairwell_work_from_the_storey_they_serve()
        {
            var interior = StandTwoStorey(out _, out _, out _, out _, out _, out _);
            WalkInside(interior);

            var up = Spawn<InteriorStair>("stair up");
            up.Configure(interior, "fixture.test.stair_up", 0, 1, 1.2f, "up");
            var down = Spawn<InteriorStair>("stair down");
            down.Configure(interior, "fixture.test.stair_down", 1, 0, 1.2f, "down");

            Assert.IsTrue(up.IsAvailable, "standing downstairs, the way UP is the one on offer");
            Assert.IsFalse(down.IsAvailable, "and the way down is not a thing to act on");

            Assert.IsTrue(up.Climb());
            Assert.AreEqual(1, interior.Level);

            Assert.IsFalse(up.IsAvailable, "upstairs, it is the other way round");
            Assert.IsTrue(down.IsAvailable);

            Assert.IsTrue(down.Climb());
            Assert.AreEqual(0, interior.Level);
        }

        [Test]
        public void The_stair_the_press_resolves_to_is_never_the_wrong_direction()
        {
            var interior = StandTwoStorey(out _, out _, out _, out _, out _, out _);
            WalkInside(interior);

            var up = Spawn<InteriorStair>("stair up");
            up.Configure(interior, "fixture.test.stair_up", 0, 1, 1.2f, "up");
            var down = Spawn<InteriorStair>("stair down");
            down.Configure(interior, "fixture.test.stair_down", 1, 0, 1.2f, "down");

            // Both stand on the same point — which is the whole design — so the resolver has two
            // candidates at identical distance and identical priority. Only availability separates them.
            up.transform.position = interior.transform.position;
            down.transform.position = interior.transform.position;
            Register(up);
            Register(down);

            var actor = new InteractActor(interior.transform.position, Vector2.zero,
                                          InteractContext.OnFoot);

            Assert.IsTrue(InteractResolver.TryResolve(Interactables.Active, actor, 120f, out var best));
            Assert.AreSame(up, best,
                           "downstairs the press must reach the way up, even though the way down is " +
                           "the same distance away — two stairs on one point is the design, not a tie");

            up.Climb();
            Assert.IsTrue(InteractResolver.TryResolve(Interactables.Active, actor, 120f, out best));
            Assert.AreSame(down, best, "and upstairs it reaches the way down");
        }

        [Test]
        public void The_stair_refuses_a_level_that_does_not_exist_and_the_one_already_occupied()
        {
            var interior = StandTwoStorey(out _, out _, out _, out _, out _, out _);

            Assert.IsFalse(interior.TryGoToLevel(-1), "there is no cellar");
            Assert.IsFalse(interior.TryGoToLevel(2), "and no attic above the storey above");
            Assert.IsFalse(interior.TryGoToLevel(0), "and no work in going where you already are");
        }

        // =============================================================================
        //  the stair fixture
        // =============================================================================

        [Test]
        public void A_stair_is_unavailable_while_its_own_storey_is_not_the_occupied_one()
        {
            var interior = StandTwoStorey(out _, out _, out _, out _, out _, out _);

            var up = Spawn<InteriorStair>("stair up");
            up.Configure(interior, "fixture.test.stair_up", fromLevel: 0, toLevel: 1,
                         reachMeters: 1.2f, arrivalText: "up");
            var down = Spawn<InteriorStair>("stair down");
            down.Configure(interior, "fixture.test.stair_down", fromLevel: 1, toLevel: 0,
                           reachMeters: 1.2f, arrivalText: "down");

            Assert.IsFalse(up.IsAvailable,
                           "outside the house neither half of the stairwell is a thing to act on");
            Assert.IsFalse(down.IsAvailable);
        }

        [Test]
        public void A_registered_stair_is_the_candidate_the_verb_finds_rather_than_a_key_of_its_own()
        {
            var interior = StandTwoStorey(out _, out _, out _, out _, out _, out _);
            WalkInside(interior);

            var up = Spawn<InteriorStair>("stair up");
            up.Configure(interior, "fixture.test.stair_up", 0, 1, 1.2f, "up");
            Register(up);

            var actor = new InteractActor(interior.transform.position, Vector2.zero,
                                          InteractContext.OnFoot);
            Assert.IsTrue(InteractResolver.TryResolve(Interactables.Active, actor, 120f, out var best));
            Assert.AreSame(up, best,
                           "a stair reaches the player through the one interact verb — the dev-key " +
                           "ledger is spent A-Z and nothing here binds a key");
        }

        [Test]
        public void A_stair_asks_for_the_verb_on_foot_only_and_does_not_require_facing()
        {
            var up = Spawn<InteriorStair>("stair up");
            Assert.AreEqual(InteractContext.OnFoot, up.Contexts,
                            "you cannot take a stair from a boat's deck");
            Assert.IsFalse(up.RequiresFacing, "you climb a stair by standing at it");
            Assert.AreEqual(InteractPriority.Fixture, up.Priority);
        }

        // =============================================================================
        //  the beds
        // =============================================================================

        [Test]
        public void The_players_bed_asks_for_a_rest_and_names_where_they_are_turning_in()
        {
            var requests = new List<RestSaveRequested>();
            EventBus.Subscribe<RestSaveRequested>(requests.Add);

            var bed = Spawn<InteriorBed>("player bed");
            bed.Configure("fixture.test.bed_player", isPlayerBed: true, ownerName: "",
                          placeName: StPetersInteriors.PlayerBedPlaceName, reachMeters: 1.4f);

            Assert.IsTrue(bed.Rest(), "your own bed offers to keep the day");
            Assert.AreEqual(1, requests.Count, "exactly one request per press");
            Assert.AreEqual(StPetersInteriors.PlayerBedPlaceName, requests[0].Place);
        }

        [Test]
        public void Ginnys_bed_refuses_politely_and_never_asks_for_a_save()
        {
            var requests = new List<RestSaveRequested>();
            EventBus.Subscribe<RestSaveRequested>(requests.Add);

            var bed = Spawn<InteriorBed>("ginny bed");
            bed.Configure("fixture.test.bed_owner", isPlayerBed: false,
                          ownerName: StPetersInteriors.BedOwnerName, placeName: "", reachMeters: 1.4f);

            Assert.IsFalse(bed.Rest(), "somebody else's bed is not yours to sleep in");
            Assert.AreEqual(0, requests.Count, "and it must never reach the save system");
            Assert.AreEqual(1, _notices.Count, "it says so rather than doing nothing");
            StringAssert.Contains(StPetersInteriors.BedOwnerName, _notices[0].Text,
                                  "and the refusal names whose bed it is");
        }

        [Test]
        public void A_bed_is_on_foot_only_which_is_what_keeps_a_save_out_of_a_deck_work_cycle()
        {
            var bed = Spawn<InteriorBed>("player bed");
            bed.Configure("fixture.test.bed_player", true, "", "here", 1.4f);

            Assert.AreEqual(InteractContext.OnFoot, bed.Contexts,
                            "the resolver will not offer this to an actor on a deck (#193)");

            // ...and the component refuses one anyway, rather than trusting a seam two files away.
            var requests = new List<RestSaveRequested>();
            EventBus.Subscribe<RestSaveRequested>(requests.Add);
            bed.Interact(new InteractActor(Vector2.zero, Vector2.zero, InteractContext.OnDeck));
            Assert.AreEqual(0, requests.Count,
                            "a rest driven from a deck is refused at the bed, not only at the resolver");
        }

        // =============================================================================
        //  waking up where you slept (ADR 0037)
        // =============================================================================

        [Test]
        public void The_players_bed_names_the_spot_the_actor_is_standing_on_not_its_own()
        {
            var requests = new List<RestSaveRequested>();
            EventBus.Subscribe<RestSaveRequested>(requests.Add);

            var bed = Spawn<InteriorBed>("player bed");
            bed.transform.position = new Vector3(-2.1f, -2.3f, 0f);        // the middle of the mattress
            bed.Configure("fixture.test.bed_player", isPlayerBed: true, ownerName: "",
                          placeName: StPetersInteriors.PlayerBedPlaceName, reachMeters: 1.4f);

            var bedside = new Vector2(-1.35f, -2.3f);                       // where you actually stand
            bed.Interact(new InteractActor(bedside, Vector2.zero, InteractContext.OnFoot));

            Assert.AreEqual(1, requests.Count);
            Assert.AreEqual(bedside, requests[0].WakePosition,
                            "you wake at the bedside you walked to, not inside the mattress — and it " +
                            "needs no tuned offset, because the reach test already put you there");
        }

        [Test]
        public void A_bed_upstairs_reports_the_storey_it_is_standing_on()
        {
            BuildingInterior interior = StandTwoStorey(out _, out _, out _, out _,
                                                       out GameObject upperProps, out _);
            Transform occupant = WalkInside(interior);

            var bed = Spawn<InteriorBed>("player bed");
            bed.transform.SetParent(upperProps.transform, worldPositionStays: false);
            bed.Configure("fixture.test.bed_player", true, "", "here", 1.4f, interior);

            Assert.AreEqual(0, bed.Level, "on the ground floor it is a ground-floor bed");

            interior.TryGoToLevel(1);
            Assert.AreEqual(1, bed.Level,
                            "and upstairs it is an upstairs bed — read LIVE off the building, so the " +
                            "component and the builder cannot disagree about which floor it is on");

            var requests = new List<RestSaveRequested>();
            EventBus.Subscribe<RestSaveRequested>(requests.Add);
            bed.Interact(new InteractActor(occupant.position, Vector2.zero, InteractContext.OnFoot));

            Assert.AreEqual(1, requests[0].WakeLevel,
                            "which is what stops the wake landing in the room BELOW the bed");
        }

        [Test]
        public void A_bed_with_no_building_reports_the_ground_floor_rather_than_throwing()
        {
            var bed = Spawn<InteriorBed>("bed in a field");
            bed.Configure("fixture.test.bed_player", true, "", "here", 1.4f);

            Assert.AreEqual(0, bed.Level,
                            "outdoors, in a single-storey building, or in no building at all");
        }

        /// <summary>
        /// The whole point of the storey riding in the anchor: a player who slept upstairs walks back
        /// into their own bedroom, not into the room underneath it.
        /// </summary>
        [Test]
        public void A_house_opens_on_the_storey_you_slept_on_and_only_the_once()
        {
            BuildingInterior interior = StandTwoStorey(out _, out SpriteRenderer ground, out _,
                                                       out SpriteRenderer upper, out _, out _);

            var occupant = new GameObject("occupant");
            _spawned.Add(occupant);
            interior.SetOccupant(occupant.transform);
            occupant.transform.position = interior.transform.position + new Vector3(0f, -40f, 0f);
            Tick(interior);
            Assert.IsFalse(interior.IsInside, "precondition: the load has not put them inside yet");

            // ⚠️ DRIVEN DIRECTLY, AND DO NOT "TIDY THIS AWAY" — the same rule the Register helper above
            // carries. EditMode fires no OnEnable on a plain MonoBehaviour, so this interior is NOT
            // subscribed to PlayerWokeAt here; publishing the signal would assert against a room that
            // could not have heard it, and the "nothing happens" cases below would go green for the
            // wrong reason. The RULE is proved here; the SUBSCRIPTION is proved in CabinUpstairsPlayTests.
            //
            // The wake lands BEFORE the interior has seen anyone inside — which is exactly the ordering
            // the real load has, and the reason the storey is remembered rather than set.
            interior.WakeAt(new RestAnchor("region.st_peters", interior.transform.position, 1));
            Assert.AreEqual(0, interior.Level, "nothing has changed storey yet; nobody is in the house");

            occupant.transform.position = interior.transform.position;
            Tick(interior);

            Assert.IsTrue(interior.IsInside);
            Assert.AreEqual(1, interior.Level, "you wake in your own bedroom");
            Assert.IsTrue(upper.enabled, "and the storey above is what is drawn");
            Assert.IsFalse(ground.enabled, "the one you are not on is switched OFF, never sorted behind");

            // ...and it is spent. Walk out, walk back in: the front door lands on the ground floor.
            WalkOutside(interior, occupant.transform);
            occupant.transform.position = interior.transform.position;
            Tick(interior);

            Assert.AreEqual(0, interior.Level,
                            "a one-shot, or every later visit would open on the bedroom — the very bug " +
                            "ADR 0036's reset rule exists to prevent");
            Assert.IsTrue(ground.enabled);
        }

        /// <summary>
        /// The defensive branch, which is worth a test precisely because it is hard to reach: a wake
        /// that lands while the occupant is ALREADY indoors has no inside-transition coming to spend
        /// the one-shot on, so it has to be spent immediately — and through the path that REDRAWS.
        /// Setting the storey without redrawing would change what the interact registry reports while
        /// leaving the ground floor on screen, which is a bug you would only ever see in a screenshot.
        /// </summary>
        [Test]
        public void A_wake_that_arrives_while_you_are_already_indoors_redraws_as_well_as_swaps()
        {
            BuildingInterior interior = StandTwoStorey(out _, out SpriteRenderer ground, out _,
                                                       out SpriteRenderer upper, out _, out _);
            WalkInside(interior);
            Assert.AreEqual(0, interior.Level, "precondition: indoors, on the ground floor");

            interior.WakeAt(new RestAnchor("region.st_peters",                      // driven — see above
                                           interior.transform.position, 1));

            Assert.AreEqual(1, interior.Level, "the storey changes");
            Assert.IsTrue(upper.enabled, "AND the storey above is actually drawn");
            Assert.IsFalse(ground.enabled, "and the one below it is switched off");
        }

        [Test]
        public void A_wake_somewhere_else_leaves_this_house_alone()
        {
            BuildingInterior interior = StandTwoStorey(out _, out _, out _, out _, out _, out _);

            interior.WakeAt(new RestAnchor("region.st_peters",                      // driven — see above
                                           (Vector2)interior.transform.position + new Vector2(0f, 60f), 1));

            Transform occupant = WalkInside(interior);

            Assert.AreEqual(0, interior.Level,
                            "somebody sleeping in a different building is not a reason to open this " +
                            "one on its bedroom");
            Assert.IsNotNull(occupant);
        }

        [Test]
        public void A_wake_on_the_ground_floor_changes_nothing()
        {
            BuildingInterior interior = StandTwoStorey(out _, out SpriteRenderer ground, out _,
                                                       out _, out _, out _);

            interior.WakeAt(new RestAnchor("region.st_peters",                      // driven — see above
                                           interior.transform.position, 0));

            WalkInside(interior);

            Assert.AreEqual(0, interior.Level);
            Assert.IsTrue(ground.enabled, "which is what walking in through the front door has always done");
        }

        // =============================================================================
        //  the wardrobe
        // =============================================================================

        [Test]
        public void The_wardrobe_raises_its_signal_and_says_there_is_nothing_to_change_into()
        {
            var wardrobe = Spawn<InteriorWardrobe>("wardrobe");
            wardrobe.Configure("fixture.test.wardrobe", 1.2f);

            wardrobe.Open();

            Assert.AreEqual(1, _wardrobe.Count, "the customization lane's seam fires");
            Assert.AreEqual("fixture.test.wardrobe", _wardrobe[0].FixtureId,
                            "and names which wardrobe, so a later consumer can tell yours from a shop's");
            Assert.AreEqual(1, _notices.Count);
            Assert.AreEqual(InteriorWardrobe.NothingYetText, _notices[0].Text);
        }

        // =============================================================================
        //  the save entry point
        // =============================================================================

        [Test]
        public void A_rest_request_writes_through_the_save_service_and_reports_that_it_landed()
        {
            var fake = new FakeSaveService();
            GameServices.Save = fake;
            RestSaveResponder.Install();

            EventBus.Publish(new RestSaveRequested("your bed at Ginny's"));

            Assert.AreEqual(1, fake.Saves, "the bed's request reaches the one save service");
            Assert.AreEqual(1, _saved.Count, "and the outcome is reported for a surface to present");
            Assert.IsTrue(_saved[0].Ok);
            Assert.AreEqual(RestSaveResponder.SavedText, _saved[0].Reason);
        }

        [Test]
        public void A_rest_with_no_save_service_answers_kindly_instead_of_throwing()
        {
            GameServices.Save = null;
            RestSaveResponder.Install();

            EventBus.Publish(new RestSaveRequested(""));

            Assert.AreEqual(1, _saved.Count, "silence would be the one unacceptable answer");
            Assert.IsFalse(_saved[0].Ok);
            Assert.AreEqual(RestSaveResponder.NoServiceText, _saved[0].Reason);
        }

        [Test]
        public void A_refused_write_is_reported_as_a_refusal_not_as_a_save()
        {
            var refusing = new RefusingSaveService();
            GameServices.Save = refusing;
            RestSaveResponder.Install();

            EventBus.Publish(new RestSaveRequested(""));

            Assert.AreEqual(1, refusing.Saves, "the service was asked");
            Assert.AreEqual(1, _saved.Count);
            Assert.IsFalse(_saved[0].Ok, "but nothing reached the disk, and the player is not told it did");
        }

        [Test]
        public void Installing_the_responder_twice_does_not_save_twice_for_one_press()
        {
            var fake = new FakeSaveService();
            GameServices.Save = fake;
            RestSaveResponder.Install();
            RestSaveResponder.Install();

            EventBus.Publish(new RestSaveRequested(""));

            Assert.AreEqual(1, fake.Saves,
                            "a service that survives quit-to-title can be re-awoken; one press is one save");
            Assert.AreEqual(1, _saved.Count);
        }

        [Test]
        public void An_uninstalled_responder_does_not_answer()
        {
            var fake = new FakeSaveService();
            GameServices.Save = fake;
            RestSaveResponder.Install();
            RestSaveResponder.Uninstall();

            EventBus.Publish(new RestSaveRequested(""));

            Assert.AreEqual(0, fake.Saves);
            Assert.AreEqual(0, _saved.Count);
            Assert.IsFalse(RestSaveResponder.Installed);
        }

        [Test]
        public void The_bed_and_the_responder_meet_end_to_end_over_the_bus()
        {
            var fake = new FakeSaveService();
            GameServices.Save = fake;
            RestSaveResponder.Install();

            var bed = Spawn<InteriorBed>("player bed");
            bed.Configure("fixture.test.bed_player", true, "", "your bed at Ginny's", 1.4f);
            bed.Interact(new InteractActor(Vector2.zero, Vector2.zero, InteractContext.OnFoot));

            Assert.AreEqual(1, fake.Saves,
                            "pressing the player's bed is the manual save, all the way through");
            Assert.IsTrue(_saved[0].Ok);
        }

        // =============================================================================
        //  the plan
        // =============================================================================

        [Test]
        public void Only_Ginnys_cottage_has_a_storey_above_and_the_village_cottage_does_not()
        {
            Assert.IsTrue(StPetersInteriors.HasUpperLevel(StPetersInteriors.GinnyCottagePlanKey),
                          "the ruling gave HER cottage the upstairs");

            Assert.IsFalse(StPetersInteriors.HasUpperLevel("sageCottage"),
                           "and the plan is keyed by SITE, not by build — the village's pilot cottage " +
                           "is the same sageCottage build and must NOT gain a bedroom you can save in");
            Assert.IsFalse(StPetersInteriors.HasUpperLevel("school"));
            Assert.IsFalse(StPetersInteriors.HasUpperLevel(null));
            Assert.IsFalse(StPetersInteriors.HasUpperLevel(""));
        }

        [Test]
        public void The_plan_furnishes_two_bedrooms_a_wardrobe_and_both_halves_of_one_stairwell()
        {
            var plan = StPetersInteriors.UpperLevelFor(StPetersInteriors.GinnyCottagePlanKey);

            int players = 0, owners = 0, wardrobes = 0, down = 0, up = 0;
            foreach (var f in plan.Furnishings)
            {
                if (f.Fixture == StPetersInteriors.InteriorFixture.PlayerBed) players++;
                if (f.Fixture == StPetersInteriors.InteriorFixture.OwnerBed) owners++;
                if (f.Fixture == StPetersInteriors.InteriorFixture.Wardrobe) wardrobes++;
                if (f.Fixture == StPetersInteriors.InteriorFixture.StairDown) down++;
                if (f.Fixture == StPetersInteriors.InteriorFixture.StairUp) up++;
            }
            foreach (var f in plan.GroundAdditions)
                if (f.Fixture == StPetersInteriors.InteriorFixture.StairUp) up++;

            Assert.AreEqual(1, players, "exactly one bed is the player's");
            Assert.AreEqual(1, owners, "and exactly one is Ginny's");
            Assert.AreEqual(1, wardrobes, "the wardrobe stands in the player's room");
            Assert.AreEqual(1, up, "one foot of the stairs, downstairs");
            Assert.AreEqual(1, down, "and one head of them, upstairs");
        }

        [Test]
        public void Both_halves_of_the_stairwell_stand_on_the_same_point_which_is_why_nothing_teleports()
        {
            var plan = StPetersInteriors.UpperLevelFor(StPetersInteriors.GinnyCottagePlanKey);

            Vector2 foot = Vector2.positiveInfinity, head = Vector2.positiveInfinity;
            foreach (var f in plan.GroundAdditions)
                if (f.Fixture == StPetersInteriors.InteriorFixture.StairUp) foot = f.RoomMetres;
            foreach (var f in plan.Furnishings)
                if (f.Fixture == StPetersInteriors.InteriorFixture.StairDown) head = f.RoomMetres;

            Assert.AreEqual(foot.x, head.x, 1e-4f,
                            "the foot and the head of one stairwell are the same place on the plan");
            Assert.AreEqual(foot.y, head.y, 1e-4f,
                            "and that is precisely what lets the level swap move nobody");
        }

        [Test]
        public void Every_piece_of_upstairs_furniture_stands_inside_the_walls()
        {
            var plan = StPetersInteriors.UpperLevelFor(StPetersInteriors.GinnyCottagePlanKey);
            float hw = CottageWidth * 0.5f - StPetersInteriors.WallThicknessMetres;
            float hl = CottageLength * 0.5f - StPetersInteriors.WallThicknessMetres;

            foreach (var f in plan.Furnishings)
            {
                Assert.LessOrEqual(Mathf.Abs(f.RoomMetres.x), hw,
                    $"'{f.PropKey}' at {f.RoomMetres} is through the side wall of a " +
                    $"{CottageWidth}x{CottageLength} m cottage");
                Assert.LessOrEqual(Mathf.Abs(f.RoomMetres.y), hl,
                    $"'{f.PropKey}' at {f.RoomMetres} is through the end wall");
            }
        }

        [Test]
        public void The_two_bedrooms_are_on_opposite_sides_of_the_partition_that_divides_them()
        {
            var plan = StPetersInteriors.UpperLevelFor(StPetersInteriors.GinnyCottagePlanKey);

            // The east-west partition is the widest rect in the plan; it is what separates the rooms.
            float divide = float.NaN, widest = 0f;
            foreach (var w in plan.Partitions)
            {
                float span = Mathf.Abs(w.X1 - w.X0);
                if (span > widest) { widest = span; divide = 0.5f * (w.Y0 + w.Y1); }
            }
            Assert.IsFalse(float.IsNaN(divide), "the plan has a partition between the bedrooms");

            Vector2 player = Vector2.zero, ginny = Vector2.zero;
            foreach (var f in plan.Furnishings)
            {
                if (f.Fixture == StPetersInteriors.InteriorFixture.PlayerBed) player = f.RoomMetres;
                if (f.Fixture == StPetersInteriors.InteriorFixture.OwnerBed) ginny = f.RoomMetres;
            }

            Assert.Less(player.y, divide, "the player's bed is on the door side of the partition");
            Assert.Greater(ginny.y, divide,
                           "and Ginny's is on the hearth side — two rooms, not one room with two beds");
        }

        [Test]
        public void The_wardrobe_is_in_the_players_room_and_not_in_Ginnys()
        {
            var plan = StPetersInteriors.UpperLevelFor(StPetersInteriors.GinnyCottagePlanKey);

            float divide = float.NaN, widest = 0f;
            foreach (var w in plan.Partitions)
            {
                float span = Mathf.Abs(w.X1 - w.X0);
                if (span > widest) { widest = span; divide = 0.5f * (w.Y0 + w.Y1); }
            }

            foreach (var f in plan.Furnishings)
                if (f.Fixture == StPetersInteriors.InteriorFixture.Wardrobe)
                    Assert.Less(f.RoomMetres.y, divide,
                                "the owner ruled the wardrobe stands in the PLAYER's room");
        }

        // =============================================================================
        //  the doorway plug
        // =============================================================================

        [Test]
        public void The_plug_covers_exactly_the_gap_the_walls_leave_for_the_front_door()
        {
            var footprint = new InteriorFootprint(Vector2.zero, CottageWidth, CottageLength,
                                                  facing: 0, facings: 8, depthScale: 0.6427876f,
                                                  doorSign: -1f, doorAcrossMetres: 0f);

            Vector2[] plug = footprint.DoorwayPlugQuad(StPetersInteriors.WallThicknessMetres,
                                                       StPetersInteriors.DoorwayWidthMetres);
            Assert.AreEqual(4, plug.Length);

            // The doorway's own point must be covered by the plug's span — this is the whole contract:
            // the piece the walls leave out is the piece the upper storey puts back.
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in plug)
            {
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }

            Vector2 door = footprint.DoorWorld;
            Assert.GreaterOrEqual(door.x, minX - 1e-3f, "the threshold is inside the plug across");
            Assert.LessOrEqual(door.x, maxX + 1e-3f);
            Assert.GreaterOrEqual(door.y, minY - 1e-3f, "and through the wall's thickness");
            Assert.LessOrEqual(door.y, maxY + 1e-3f);

            Assert.AreEqual(StPetersInteriors.DoorwayWidthMetres, maxX - minX, 1e-3f,
                            "the plug is exactly as wide as the gap, not a wall's width more or less");
        }

        [Test]
        public void The_plug_follows_an_off_centre_door_rather_than_the_middle_of_the_wall()
        {
            var offset = new InteriorFootprint(Vector2.zero, CottageWidth, CottageLength,
                                               0, 8, 0.6427876f, -1f, doorAcrossMetres: -1.68f);

            Vector2[] plug = offset.DoorwayPlugQuad(StPetersInteriors.WallThicknessMetres,
                                                    StPetersInteriors.DoorwayWidthMetres);
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (var p in plug) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); }

            Assert.AreEqual(-1.68f, 0.5f * (minX + maxX), 1e-3f,
                            "the shops' doors are measured and off centre; a plug that assumed the " +
                            "middle would seal a wall and leave the door open");
        }
    }
}
