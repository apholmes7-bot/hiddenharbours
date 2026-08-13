using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>A villager LIVING, over real frames</b> — a real <see cref="VillagerRoutine"/> on a real
    /// transform, driven by a clock this test moves by hand: she waits, sets off on the hour, walks the
    /// lane, arrives, dwells, and goes in through a real <see cref="BuildingInterior"/>'s drawn door.
    ///
    /// <para><b>What only PlayMode can show.</b> The rules are EditMode's — <c>RoutinePlanTests</c> owns the
    /// closed form, the determinism and the threshold arithmetic. What is left is the WIRING, which is
    /// where a seam actually fails: that the component plans itself once the services are up, that it
    /// writes the pose onto the transform every frame (so <see cref="IsoCharacterSprite"/> has motion to
    /// read), that the interior reveal switches the renderer, and that a disabled villager is handed back
    /// visible rather than left hidden inside a house.</para>
    ///
    /// <para><b>⚠️ DRIVEN IN GAME HOURS, NEVER IN FRAMES.</b> A frame count is not time, and least of all
    /// headless, where <c>yield return null</c> spins as fast as the machine allows. The clock here is a
    /// fake this test SETS — so "she leaves at 08:00" is asserted by putting the clock at 08:00, not by
    /// waiting and hoping. <see cref="At"/> then costs two frames (see its remarks for why one is not
    /// enough). The one thing that genuinely needs REAL seconds is the throttled visibility read, and
    /// <see cref="AtSettled"/> is the only place that waits for it — same reason
    /// <c>SternDeckLoopPlayTests</c> waits in seconds rather than frames.</para>
    ///
    /// <para><b>Headless-safe by construction (⚠️ do not relax this).</b> Nothing renders, reads pixels or
    /// calls <c>Camera.Render</c>: CI runs with a null graphics device, where a ReadPixels PlayMode test
    /// does not fail — it KILLS the editor, exit 1, with no results XML at all. Every assertion is on STATE
    /// (a transform position, a renderer's <c>enabled</c> flag, a pose struct).</para>
    ///
    /// <para><b>No region is loaded, on purpose.</b> A resident region reds OTHER files (the "she is
    /// swimming" precedent), and nothing under test needs one: the station table and the lane network are
    /// components, so a synthetic village of three junctions exercises the same code the island does. The
    /// island's own geometry is checked by <c>StPetersRoutineContentTests</c> and by the builder's run.</para>
    /// </summary>
    public class VillagerRoutinePlayTests
    {
        const float SecondsPerGameHour = GameConfig.DefaultSecondsPerDay / 24f;   // 75 s at the pinned day

        /// <summary>A clock this test moves by hand. Only the hour matters to a routine.</summary>
        sealed class DrivenClock : IGameClock
        {
            public float Hour;
            public double TotalSeconds => Hour / 24.0 * GameConfig.DefaultSecondsPerDay;
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => Hour;
            public float DayFraction => Hour / 24f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        sealed class FakeEnvironment : IEnvironmentService
        {
            public int WorldSeed { get; set; } = 4242;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => 0f;
        }

        readonly List<Object> _spawned = new();
        DrivenClock _clock;
        VillagerRoutine _villager;
        SpriteRenderer _renderer;
        Interactable _talkPoint;
        BuildingInterior _home;
        RoutineStations _stations;
        RoutineLanes _lanes;

        // The synthetic village: green(0) ── lane(1), and the cottage's dooryard hanging off the lane.
        static readonly Vector2 Green = new Vector2(0f, 0f);
        static readonly Vector2 Lane = new Vector2(0f, 24f);
        static readonly Vector2 HomeCentre = new Vector2(0f, 31f);

        [SetUp]
        public void SetUp()
        {
            // ⚠️ ONE AUDIO LISTENER, and it is not decoration. Unity logs "There are no audio listeners in
            // the scene" EVERY FRAME of a listener-less play-mode scene, and these tests advance real frames
            // on purpose (see AtSettled). That warning is a pre-existing, suite-wide noise source here — a
            // full PlayMode run writes tens of MB of it — and a listener is the whole of not adding to it.
            Spawn("AudioListener").AddComponent<AudioListener>();

            GameServices.Reset();
            _clock = new DrivenClock { Hour = 6f };
            GameServices.Clock = _clock;
            GameServices.Environment = new FakeEnvironment();

            // --- the lanes.
            var lanesGo = Spawn("RoutineLanes");
            _lanes = lanesGo.AddComponent<RoutineLanes>();
            _lanes.Configure(
                nodePositions: new[] { Green, Lane },
                nodeParents: new[] { RoutineLaneTree.NoParent, 0 },
                nodeNames: new[] { "green", "lane" },
                viaStart: new int[2], viaCount: new int[2], via: System.Array.Empty<Vector2>());
            Assert.That(_lanes.Validate(), Is.Empty);

            // --- the cottage, with a real footprint so its drawn door is a real point.
            var homeGo = Spawn("cottage");
            homeGo.transform.position = new Vector3(HomeCentre.x, HomeCentre.y, 0f);
            _home = homeGo.AddComponent<BuildingInterior>();
            _home.Configure(shell: null, room: null, props: null,
                            widthMetres: 6.6f, lengthMetres: 8.05f, facing: 0, facings: 8,
                            groundDepthScale: 0.6427876f,
                            wallThicknessMetres: 0.3f, doorwayWidthMetres: 1.4f);

            // --- the stations.
            var stationsGo = Spawn("RoutineStations");
            _stations = stationsGo.AddComponent<RoutineStations>();
            _stations.Configure(new[]
            {
                new RoutineStationEntry
                {
                    Id = "s.green", Position = Green, StandHeadingDegrees = 0f,
                    LaneNodeName = "green", Why = "the green",
                },
                new RoutineStationEntry
                {
                    Id = "s.dooryard", Position = Lane, StandHeadingDegrees = 180f,
                    LaneNodeName = "lane", Why = "her own step",
                },
                new RoutineStationEntry
                {
                    // A metre inside the door, which is where the builder puts a home station.
                    Id = "s.home", Position = InsidePoint(_home), StandHeadingDegrees = 0f,
                    Interior = _home, LaneNodeName = "lane", Why = "inside",
                },
            });

            // --- her day: on the green at 08:00, home inside at 18:00. Two blocks, one long walk each way.
            var routine = ScriptableObject.CreateInstance<RoutineDef>();
            _spawned.Add(routine);
            routine.Id = "routine.playtest";
            routine.WalkSpeedMetresPerSecond = 1.2f;
            routine.ScheduleJitterMinutes = 0f;   // ⚠️ zero, so the departure hours ARE the authored ones
            routine.Entries = new[]
            {
                new RoutineEntry { StartHour = 8f, StationId = "s.green", Activity = RoutineActivity.Recreation },
                new RoutineEntry { StartHour = 18f, StationId = "s.home", Activity = RoutineActivity.Home },
            };

            // --- the villager, built in the ORDER THE REGION BUILDER USES: the renderer and the talk point
            //     first (StPetersInhabitants dresses the body and adds the Interactable), the routine last
            //     (StPetersRoutines wires the day). ⚠️ It matters: VillagerRoutine looks its siblings up in
            //     Awake, and a component added AFTER it would never be found. Building it the other way
            //     round in an earlier version of this fixture is what made three of these tests red.
            var go = Spawn("Villager");
            go.transform.position = new Vector3(Green.x, Green.y, 0f);
            _renderer = go.AddComponent<SpriteRenderer>();
            _talkPoint = go.AddComponent<Interactable>();
            _villager = go.AddComponent<VillagerRoutine>();
            _villager.Configure(routine, _stations, _lanes, _renderer);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        static Vector2 InsidePoint(BuildingInterior interior)
        {
            InteriorFootprint f = interior.Footprint;
            return f.ModelToWorld(new Vector2(f.DoorAcrossMetres,
                                              f.DoorSign * (f.LengthMetres * 0.5f - 1.2f)));
        }

        /// <summary>
        /// Put the clock at an hour and let the POSITION land.
        ///
        /// <para>⚠️ TWO frames, and one is not enough. A test coroutine resumes during the <c>Update</c>
        /// phase, in an order relative to other components' <c>Update</c>s that Unity does not define — so
        /// after a single <c>yield return null</c> the villager's own <c>Update</c> may not have run for that
        /// frame, and anything written in a <c>LateUpdate</c> downstream of it (the skin's drawn heading)
        /// certainly has not. Measured: a one-frame settle read
        /// <c>IsoCharacterSprite.HeadingDegrees</c> as its 180° initial value while
        /// <c>IsHeadingHeld</c> was already true — the hold had happened, the LateUpdate that acts on it had
        /// not.</para>
        /// </summary>
        IEnumerator At(float hour)
        {
            _clock.Hour = hour;
            yield return null;
            yield return null;
        }

        /// <summary>
        /// Put the clock at an hour and let the VISIBILITY land too.
        ///
        /// <para>⚠️ This wait is in REAL seconds and it is not optional: the interior/visibility read is
        /// throttled to <see cref="VillagerRoutine.ShelterCheckSeconds"/> of unscaled time (rule 7 — it is
        /// the one thing here that touches another object). A frame-counted settle is ~10 ms and would read
        /// the renderer BEFORE the shelter check was due, so the test would pass or fail on machine speed.
        /// Used only where a renderer or an interactable is actually asserted — every other case waits a
        /// single frame.</para>
        /// </summary>
        IEnumerator AtSettled(float hour)
        {
            _clock.Hour = hour;
            yield return null;   // the position write
            yield return new WaitForSecondsRealtime(VillagerRoutine.ShelterCheckSeconds + 0.05f);
            yield return null;   // the shelter read, now certainly due
        }

        // ---- she plans herself, once the services are up -------------------------------------------

        [UnityTest]
        public IEnumerator SheStartsLiving_OnceTheClockAndTheSeedAreUp()
        {
            yield return At(12f);
            Assert.That(_villager.IsLiving, Is.True,
                        "the component has to plan itself when the services come up — OnEnable runs before " +
                        "the composition root has registered the environment, so a plan built there would " +
                        "have the wrong seed");
            Assert.That(_villager.Plan.BlockCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator WithNoRoutine_SheStandsExactlyWhereSheWasPut_AndNothingThrows()
        {
            var go = Spawn("Anchored");
            go.transform.position = new Vector3(7f, 9f, 0f);
            go.AddComponent<SpriteRenderer>();
            var inert = go.AddComponent<VillagerRoutine>();
            inert.Configure(routine: null, stations: _stations, lanes: _lanes,
                            renderer: go.GetComponent<SpriteRenderer>());

            yield return At(13f);
            yield return At(20f);

            Assert.That(inert.IsLiving, Is.False);
            Assert.That(go.transform.position, Is.EqualTo(new Vector3(7f, 9f, 0f)),
                        "an islander whose routine did not resolve keeps the spot the builder placed them " +
                        "on — which is what an islander without a routine already does");
        }

        // ---- the day, beat by beat ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ATTheDeparture_SheIsStillWhereSheWas_AndAMomentLaterSheIsMoving()
        {
            // Block 1 (18:00) sets off from the green for home.
            yield return At(17.9f);
            Vector2 beforeDeparture = _villager.transform.position;
            Assert.That(Vector2.Distance(beforeDeparture, Green), Is.LessThan(0.05f),
                        "she is on the green until the hour comes round");

            yield return At(18f);
            Assert.That(Vector2.Distance(_villager.transform.position, Green), Is.LessThan(0.05f),
                        "the departure hour is the moment she sets OFF, so she is still at the start of it");

            // A tenth of a game hour later — 9 m at 1.2 m/s — she is up the lane.
            yield return At(18.1f);
            Assert.That(Vector2.Distance(_villager.transform.position, Green), Is.GreaterThan(5f),
                        "she has to actually move, on the clock, without anybody ticking her");
            Assert.That(_villager.Pose.Walking, Is.True);
        }

        [UnityTest]
        public IEnumerator SheWalksTheLANE_NotStraightAtTheDoor()
        {
            // The synthetic village's lane runs due north from the green, and the cottage sits north of the
            // lane node — so a straight line and the lane happen to agree here. What is checked instead is
            // that the route she is on IS the lane's polyline, point for point.
            yield return At(19f);
            RoutinePlan plan = _villager.Plan;
            int block = _villager.Pose.BlockIndex;
            Assert.That(block, Is.EqualTo(1));

            int start = plan.RouteStart[block];
            Assert.That(plan.Waypoints[start], Is.EqualTo(Green), "sets off from the green");
            Assert.That(plan.Waypoints[start + 1], Is.EqualTo(Lane), "up to the lane junction");
            Assert.That(plan.Waypoints[start + plan.RouteCount[block] - 2], Is.EqualTo(_home.DoorWorld),
                        "then to the DRAWN door, which is the threshold the player watches her cross");
        }

        [UnityTest]
        public IEnumerator SheArrives_ThenDwells_WithoutDrifting()
        {
            yield return At(18f);
            RoutinePlan plan = _villager.Plan;
            Assert.That(plan, Is.Not.Null);

            float travel = plan.TravelHours(1, SecondsPerGameHour);
            Assert.That(travel, Is.GreaterThan(0.05f), "the rig's walk has to be long enough to watch");

            yield return At(18f + travel + 0.01f);
            Assert.That(_villager.Pose.Walking, Is.False, "she has arrived");
            Vector3 arrived = _villager.transform.position;

            // Hours of standing still: exactly the same spot, because the position is READ not accumulated.
            yield return At(20f);
            Assert.That(_villager.transform.position, Is.EqualTo(arrived));
            yield return At(23.5f);
            Assert.That(_villager.transform.position, Is.EqualTo(arrived),
                        "nothing integrates, so nothing can drift over a night of standing still");
        }

        [UnityTest]
        public IEnumerator AFrozenClock_FreezesHer()
        {
            yield return At(18.05f);   // mid-walk
            Vector3 frozen = _villager.transform.position;
            Assert.That(_villager.Pose.Walking, Is.True);

            for (int i = 0; i < 30; i++) yield return null;   // frames pass, the clock does not
            Assert.That(_villager.transform.position, Is.EqualTo(frozen),
                        "a paused clock has to stop the whole village — the sabotage control for 'she moves " +
                        "on the clock' rather than 'she moves per frame'");
        }

        /// <summary>
        /// ⭐ MID-LEG ON ARRIVAL. A villager enabled at an arbitrary hour appears ON the leg, mid-stride —
        /// which is exactly what a region loading at 18:05 has to produce, and the thing a
        /// ticked-and-saved routine gets wrong.
        /// </summary>
        [UnityTest]
        public IEnumerator EnabledMidLeg_SheAppearsOnTheLeg_NotAtEitherEnd()
        {
            yield return At(18.06f);

            var fresh = Spawn("LateVillager");
            fresh.transform.position = new Vector3(-500f, -500f, 0f);   // nowhere, on purpose
            var renderer = fresh.AddComponent<SpriteRenderer>();
            var late = fresh.AddComponent<VillagerRoutine>();
            late.Configure(_villager.Routine, _stations, _lanes, renderer);

            yield return null;
            yield return null;

            Assert.That(late.IsLiving, Is.True);
            Assert.That(Vector2.Distance(late.transform.position, _villager.transform.position),
                        Is.LessThan(0.05f),
                        "she must arrive exactly where the villager who has been walking all along is — " +
                        "same clock, same answer, no history needed");
            Assert.That(Vector2.Distance(late.transform.position, Green), Is.GreaterThan(1f));
            Assert.That(late.Pose.Walking, Is.True);
        }

        // ---- the reveal -----------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator OutInTheWorld_SheIsDrawn()
        {
            yield return AtSettled(12f);
            Assert.That(_renderer.enabled, Is.True);
            yield return AtSettled(18.05f);
            Assert.That(_renderer.enabled, Is.True, "and all the way down the lane on her way home");
        }

        /// <summary>
        /// ⭐ THE REVEAL. Once she is past her own doorway she is drawn only if the player shares that
        /// interior — the seamless-interiors rule — and she is drawn again the moment the player steps in
        /// after her.
        /// </summary>
        [UnityTest]
        public IEnumerator InsideAHouse_SheIsHiddenUntilThePlayerIsInThereToo()
        {
            // Park the "player" far away so BuildingInterior reports the player is outside.
            var playerGo = Spawn("Player");
            playerGo.transform.position = new Vector3(0f, -50f, 0f);
            _home.SetOccupant(playerGo.transform);

            yield return AtSettled(23f);   // long since home
            Assert.That(_villager.Pose.Shelter, Is.EqualTo(RoutineShelter.InsideDestination));
            Assert.That(_renderer.enabled, Is.False,
                        "an occupant of an interior the player is not in must not be drawn — she would be " +
                        "standing on the roof of a closed house");

            // The player walks in. BuildingInterior decides that in its own Update, from geometry.
            playerGo.transform.position = new Vector3(_villager.transform.position.x,
                                                      _villager.transform.position.y, 0f);
            yield return AtSettled(23f);
            Assert.That(_home.IsInside, Is.True, "the interior has to agree the player is inside");
            Assert.That(_renderer.enabled, Is.True, "…and then she is in the room with them");
        }

        [UnityTest]
        public IEnumerator LeavingHome_SheIsDrawnAgainTheMomentSheIsPastTheDoor()
        {
            var playerGo = Spawn("Player");
            playerGo.transform.position = new Vector3(0f, -50f, 0f);
            _home.SetOccupant(playerGo.transform);

            yield return AtSettled(23f);
            Assert.That(_renderer.enabled, Is.False);

            // Block 0 departs at 08:00 from home for the green. exitAt is the door.
            RoutinePlan plan = _villager.Plan;
            float exitAt = plan.ExitAtMetres[0];
            Assert.That(exitAt, Is.GreaterThan(0f), "leaving a room starts inside it");

            float insideHours = RoutineSchedule.TravelHours(exitAt * 0.5f,
                                                            plan.WalkSpeedMetresPerSecond,
                                                            SecondsPerGameHour);
            yield return AtSettled(8f + insideHours);
            Assert.That(_villager.Pose.Shelter, Is.EqualTo(RoutineShelter.InsideOrigin));
            Assert.That(_renderer.enabled, Is.False);

            float outHours = RoutineSchedule.TravelHours(exitAt + 1f, plan.WalkSpeedMetresPerSecond,
                                                         SecondsPerGameHour);
            yield return AtSettled(8f + outHours);
            Assert.That(_villager.Pose.Shelter, Is.EqualTo(RoutineShelter.Outdoors));
            Assert.That(_renderer.enabled, Is.True,
                        "out of her own door she is in the world, and the player standing in the lane has " +
                        "to see her come out of it");
        }

        /// <summary>
        /// ⭐ AND SHE STOPS BEING TALKABLE. A home station sits about a metre past a door the player can walk
        /// right up to, and <c>WorldInteractor</c> resolves by plain proximity — so hiding only the sprite
        /// would leave an "E: Talk" prompt on an invisible neighbour through her own wall, and she would
        /// answer. Found while reading the first build's muster; this pins both halves of the fix.
        /// </summary>
        [UnityTest]
        public IEnumerator InsideAHouse_SheIsNotTALKABLEEither()
        {
            Interactable talk = _talkPoint;

            var playerGo = Spawn("Player");
            playerGo.transform.position = new Vector3(0f, -50f, 0f);
            _home.SetOccupant(playerGo.transform);

            yield return AtSettled(12f);   // out on the green
            Assert.That(talk.enabled, Is.True, "out in the world she is a person you can walk up to");

            yield return AtSettled(23f);   // inside, player outside
            Assert.That(_renderer.enabled, Is.False);
            Assert.That(talk.enabled, Is.False,
                        "an invisible neighbour must not answer through her own wall — WorldInteractor " +
                        "skips an Interactable that is switched off, and this is what switches it off");

            // The player steps in after her: both come back.
            playerGo.transform.position = new Vector3(_villager.transform.position.x,
                                                      _villager.transform.position.y, 0f);
            yield return AtSettled(23f);
            Assert.That(_renderer.enabled, Is.True);
            Assert.That(talk.enabled, Is.True, "in the room together, she is a person again");
        }

        [UnityTest]
        public IEnumerator DisablingHerHandsTheRendererBackVISIBLE()
        {
            Interactable talk = _talkPoint;

            var playerGo = Spawn("Player");
            playerGo.transform.position = new Vector3(0f, -50f, 0f);
            _home.SetOccupant(playerGo.transform);

            yield return AtSettled(23f);
            Assert.That(_renderer.enabled, Is.False);
            Assert.That(talk.enabled, Is.False);

            _villager.enabled = false;
            yield return null;
            Assert.That(_renderer.enabled, Is.True,
                        "a villager switched off inside a house would come back INVISIBLE in a scene the " +
                        "player is standing outside of, and the only symptom would be a missing person");
            Assert.That(talk.enabled, Is.True, "…and an un-talkable one, which reads as broken dialogue");
        }

        // ---- the shared character stack ---------------------------------------------------------------

        /// <summary>
        /// She rides the same presenter the player does. No visual def is wired — it draws nothing and
        /// reports a heading, which is exactly the half under test: the facing has to be the ROUTE's while
        /// she walks and the STATION's while she stands, and it must be right on the first frame of a leg
        /// (a mid-leg spawn has no previous position to difference).
        /// </summary>
        [UnityTest]
        public IEnumerator HerFacingIsStatedFromTheRoute_NotGuessedFromTheFirstFrame()
        {
            // A second villager with the SKIN ADDED FIRST — the order the region builder uses, because
            // NpcBodyDresser dresses the body before StPetersRoutines gives it a day.
            var go = Spawn("BodiedVillager");
            go.transform.position = new Vector3(Green.x, Green.y, 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            var iso = go.AddComponent<IsoCharacterSprite>();
            go.AddComponent<Interactable>();
            var bodied = go.AddComponent<VillagerRoutine>();
            bodied.Configure(_villager.Routine, _stations, _lanes, renderer);

            yield return At(18.01f);   // just set off: due north up the lane
            Assert.That(bodied.IsLiving, Is.True);
            Assert.That(iso.IsHeadingHeld, Is.True,
                        "the routine states the facing rather than letting one frame of motion guess it");
            Assert.That(iso.HeadingDegrees, Is.EqualTo(0f).Within(0.5f), "up the lane is due north");

            yield return At(12f);      // standing on the green, whose stance is 0 (looking up at the village)
            Assert.That(bodied.Pose.Walking, Is.False);
            Assert.That(iso.HeadingDegrees, Is.EqualTo(0f).Within(0.5f));

            // …and the STATION's stance is what she keeps while standing, whatever it is.
            yield return At(23f);
            Assert.That(iso.HeadingDegrees, Is.EqualTo(bodied.Pose.HeadingDegrees).Within(0.5f));
        }
    }
}
