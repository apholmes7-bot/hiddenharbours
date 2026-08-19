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
    /// <b>A villager stopping to talk, over real frames</b> — the overlay on a real
    /// <see cref="VillagerRoutine"/> with a clock this test moves by hand, plus the whole seam driven
    /// through a real <see cref="WorldInteractor"/>: walk up, talk, walk off, and she gets back to her
    /// day.
    ///
    /// <para><b>What only PlayMode can show.</b> The rules are EditMode's — <c>ConversationHoldTests</c>
    /// owns the overlay's arithmetic and <c>RoutineInterruptibilityTests</c> owns the authored flag and
    /// the determinism guard. What is left is the WIRING, which is where a seam actually fails: that the
    /// interactor finds her routine and asks it to stop, that a held villager stays VISIBLE and TALKABLE
    /// (including indoors, where her own shelter rule would otherwise hide her), that walking away
    /// releases her, and that she rejoins the day the clock says rather than the one she left.</para>
    ///
    /// <para><b>⚠️ No key presses anywhere</b> (the #555 lesson): everything is driven through
    /// <see cref="WorldInteractor.BeginInteract"/> and the presenter's own decision surface, which is
    /// exactly what a press calls.</para>
    ///
    /// <para><b>⚠️ DRIVEN IN GAME HOURS, NEVER IN FRAMES</b>, like <c>VillagerRoutinePlayTests</c> — the
    /// clock is a fake this test SETS. The one thing measured in real seconds is the catch-up walk, which
    /// genuinely happens in real time, and it is waited on by CONDITION rather than by a frame count so
    /// it cannot pass or fail on machine speed.</para>
    ///
    /// <para><b>Headless-safe by construction (⚠️ do not relax).</b> Nothing renders or reads pixels.
    /// Every assertion is on STATE: a transform position, an enabled flag, a phase enum.</para>
    /// </summary>
    public class NpcStopsToTalkPlayTests
    {
        const float Reach = 1.8f;

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
        Interactable _talkPoint;
        SpriteRenderer _renderer;
        IsoCharacterSprite _iso;
        BuildingInterior _home;
        RoutineStations _stations;
        RoutineLanes _lanes;
        RoutineDef _routine;
        Transform _player;
        DialoguePresenter _presenter;
        WorldInteractor _interactor;
        NpcDef _npc;

        // green(0) ── lane(1), with the cottage hanging off the lane.
        static readonly Vector2 Green = new Vector2(0f, 0f);
        static readonly Vector2 Lane = new Vector2(0f, 24f);
        static readonly Vector2 HomeCentre = new Vector2(0f, 31f);

        [SetUp]
        public void SetUp()
        {
            Spawn("AudioListener").AddComponent<AudioListener>();

            GameServices.Reset();
            InteractionGate.Reset();
            // The conversation filter now reads the actor probe; a facing leaked from another suite would
            // decide this one's candidate for it. Unset means "unknown", which passes everything — the
            // proximity-only behaviour these tests were written against.
            InteractActorProbe.Reset();
            InteractOffer.Reset();
            _clock = new DrivenClock { Hour = 6f };
            GameServices.Clock = _clock;
            GameServices.Environment = new FakeEnvironment();

            var lanesGo = Spawn("RoutineLanes");
            _lanes = lanesGo.AddComponent<RoutineLanes>();
            _lanes.Configure(
                nodePositions: new[] { Green, Lane },
                nodeParents: new[] { RoutineLaneTree.NoParent, 0 },
                nodeNames: new[] { "green", "lane" },
                viaStart: new int[2], viaCount: new int[2], via: System.Array.Empty<Vector2>());
            Assert.That(_lanes.Validate(), Is.Empty);

            var homeGo = Spawn("cottage");
            homeGo.transform.position = new Vector3(HomeCentre.x, HomeCentre.y, 0f);
            _home = homeGo.AddComponent<BuildingInterior>();
            _home.Configure(shell: null, room: null, props: null,
                            widthMetres: 6.6f, lengthMetres: 8.05f, facing: 0, facings: 8,
                            groundDepthScale: 0.6427876f,
                            wallThicknessMetres: 0.3f, doorwayWidthMetres: 1.4f);

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
                    Id = "s.home", Position = InsidePoint(_home), StandHeadingDegrees = 0f,
                    Interior = _home, LaneNodeName = "lane", Why = "inside",
                },
            });

            // Her day: on the green at 08:00, home inside at 18:00. She hurries at 4× so a catch-up
            // walk is over in a second of real time rather than five.
            _routine = ScriptableObject.CreateInstance<RoutineDef>();
            _spawned.Add(_routine);
            _routine.Id = "routine.stoptotalk";
            _routine.WalkSpeedMetresPerSecond = 1.2f;
            _routine.ScheduleJitterMinutes = 0f;     // ⚠️ zero: the departure hours ARE the authored ones
            _routine.CatchUpSpeedMultiplier = 4f;
            _routine.Entries = new[]
            {
                new RoutineEntry { StartHour = 8f, StationId = "s.green", Activity = RoutineActivity.Recreation },
                new RoutineEntry { StartHour = 18f, StationId = "s.home", Activity = RoutineActivity.Home },
            };

            _npc = ScriptableObject.CreateInstance<NpcDef>();
            _spawned.Add(_npc);
            _npc.Id = "npc.playtest";
            _npc.DisplayName = "Rose MacIsaac";
            _npc.Dialogue = ScriptableObject.CreateInstance<DialogueDef>();
            _spawned.Add(_npc.Dialogue);
            _npc.Dialogue.Id = "dialogue.playtest";
            _npc.Dialogue.FirstLines = new[] { "Fine morning." };

            // ⚠️ BUILT IN THE ORDER THE REGION BUILDER USES — the renderer and the talk point first, the
            // routine last. VillagerRoutine looks its siblings up when it becomes live, and a component
            // added after it would never be found.
            var go = Spawn("Villager");
            go.transform.position = new Vector3(Green.x, Green.y, 0f);
            _renderer = go.AddComponent<SpriteRenderer>();
            _talkPoint = go.AddComponent<Interactable>();
            _talkPoint.Configure(_npc);
            // The skin the routine hands a facing to. No CharacterVisualDef is wired — it draws nothing
            // and is not asked to; what is under test is the HEADING it is handed.
            _iso = go.AddComponent<IsoCharacterSprite>();
            _villager = go.AddComponent<VillagerRoutine>();
            _villager.Configure(_routine, _stations, _lanes, _renderer);

            _player = Spawn("Player").transform;
            _player.position = new Vector3(Green.x + 1f, Green.y, 0f);

            _presenter = Spawn("DialoguePresenter").AddComponent<DialoguePresenter>();
            _interactor = Spawn("WorldInteractor").AddComponent<WorldInteractor>();
            _interactor.Configure(_player, _presenter, new[] { _talkPoint }, Reach);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
            GameServices.Reset();
            InteractionGate.Reset();
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
        /// Drive the clock to an hour and let the world catch up — INCLUDING the throttled shelter
        /// visibility read (<see cref="VillagerRoutine.ShelterCheckSeconds"/>).
        ///
        /// <para>⚠️ The settle is not optional, and the 4060 run is what proved it (2026-08-17,
        /// coordinator): at SetUp's hour 6 the villager is INSIDE her home (the pre-first-block wrap),
        /// so her <see cref="Interactable"/> starts DISABLED with her renderer. A test that jumps the
        /// clock to an outdoor hour and interacts within two frames finds her interactable still off
        /// from 6:00 — `BeginInteract` returns false and everything downstream reds. Two frames are
        /// not enough; the visibility read runs on its own throttle and must be waited out.</para>
        ///
        /// <para>The two frames themselves are still needed for the reason
        /// <c>VillagerRoutinePlayTests.At</c> documents: a test coroutine resumes during Update in an
        /// order Unity does not define, so a LateUpdate downstream of the villager has certainly not run
        /// after only one.</para>
        /// </summary>
        IEnumerator At(float hour)
        {
            _clock.Hour = hour;
            yield return null;
            yield return new WaitForSecondsRealtime(VillagerRoutine.ShelterCheckSeconds + 0.05f);
            yield return null;
        }

        /// <summary>Kept as an explicit name for the sites where the SHELTER answer itself is what the
        /// test reads next — identical to <see cref="At"/>, which settles the same read.</summary>
        IEnumerator AtSettled(float hour) => At(hour);

        /// <summary>Wait for the catch-up walk to finish — by CONDITION, so the test cannot pass or fail
        /// on how fast the machine renders frames.</summary>
        IEnumerator CatchUpSettles(float timeoutSeconds = 6f)
        {
            float waited = 0f;
            while (_villager.HoldPhase != ConversationHoldPhase.Idle && waited < timeoutSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.That(_villager.HoldPhase, Is.EqualTo(ConversationHoldPhase.Idle),
                        $"she was still catching up after {timeoutSeconds}s — a catch-up that never " +
                        "ends is an overlay that never stops overlaying");
        }

        /// <summary>Where the CLOCK says she is at an hour — sampled with the exact seconds-per-hour the
        /// component itself uses, never a parallel constant, so an exact-equality assertion is about the
        /// overlay and not about two transcriptions of the same number.</summary>
        Vector2 ClockSays(float hour) => _villager.Plan
            .SampleAt(hour, GameServices.SecondsPerDay / RoutineSchedule.HoursPerDay).Position;

        // ---- she stops ----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TalkingToHer_StopsHerWhereSheStands_AndSheStaysThereWhileTheClockRunsOn()
        {
            // 18:06 — six minutes into the walk home, so she is out on the lane and genuinely moving.
            yield return At(18.1f);
            Assert.That(_villager.Pose.Walking, Is.True, "the rig has to catch her mid-walk to mean anything");
            _player.position = _villager.transform.position + new Vector3(0.8f, 0f, 0f);

            Assert.That(_interactor.BeginInteract(), Is.True, "she is in reach and has something to say");
            yield return null;

            Assert.That(_villager.IsTalking, Is.True, "⭐ she stopped to talk");
            Vector2 stoppedAt = _villager.transform.position;

            // Her schedule walks on without her. She does not.
            yield return At(18.4f);
            Assert.That((Vector2)_villager.transform.position, Is.EqualTo(stoppedAt),
                        "a held villager does not drift — the overlay holds a POSITION, it does not " +
                        "slow a walk down");
            Assert.That(Vector2.Distance(ClockSays(18.4f), stoppedAt), Is.GreaterThan(1f),
                        "…and the clock has meanwhile taken her somewhere else entirely, which is the " +
                        "whole point: she is now LATE");
        }

        [UnityTest]
        public IEnumerator SheTurnsToFaceThePlayer()
        {
            yield return At(12f);                       // standing on the green
            // Due EAST of her, and INSIDE reach — the original 2 m stood 0.2 m past the 1.8 m radius,
            // so BeginInteract found nobody and the test failed in every environment (4060 run,
            // 2026-08-17). The bearing only needs the direction; the distance needs to be reachable.
            _player.position = new Vector3(Green.x + Reach * 0.8f, Green.y, 0f);

            Assert.That(_interactor.BeginInteract(), Is.True);
            yield return null;
            yield return null;

            Assert.That(_iso.IsHeadingHeld, Is.True, "a stated stance, not one read off motion she is not in");
            Assert.That(Mathf.DeltaAngle(_iso.HeadingDegrees, 90f), Is.EqualTo(0f).Within(1f),
                        "she faces the player, and due east is 90°. (The DIAGONALS — where a world-XY " +
                        "reading would be up to 12.56° out, ADR 0034 — are ConversationHoldTests' job; " +
                        "this is the wiring, that the bearing reaches the skin at all.)");
        }

        [UnityTest]
        public IEnumerator AnUninterruptibleBlock_RefusesTheStop_AndSheKeepsWalking()
        {
            // ⭐ THE SABOTAGE ARM. Interruptibility is authored per block; the authored "no" must mean no.
            _routine.Entries[1].Uninterruptible = true;
            _villager.enabled = false;                  // force a re-plan with the edited content
            _villager.enabled = true;

            yield return At(18.1f);
            Assert.That(_villager.Pose.Walking, Is.True);
            _player.position = _villager.transform.position + new Vector3(0.8f, 0f, 0f);

            Assert.That(_interactor.BeginInteract(), Is.True, "she still TALKS — she just does not stop");
            yield return null;

            Assert.That(_villager.IsTalking, Is.False);
            Assert.That(_villager.HoldPhase, Is.EqualTo(ConversationHoldPhase.Idle));

            Vector2 was = _villager.transform.position;
            yield return At(18.3f);
            Assert.That(Vector2.Distance(_villager.transform.position, was), Is.GreaterThan(1f),
                        "she is on her way to the wharf and the conversation happens on the move");
            Assert.That((Vector2)_villager.transform.position, Is.EqualTo(ClockSays(18.3f)),
                        "…which is to say she is exactly the pure function of the clock she always was");
        }

        // ---- she resumes --------------------------------------------------------------------

        [UnityTest]
        public IEnumerator OnRelease_SheHurriesBackToWhereTheCLOCKSaysSheShouldBe_NotToWhereSheStopped()
        {
            // ⭐ THE OTHER SABOTAGE ARM.
            yield return At(18.15f);
            _player.position = _villager.transform.position + new Vector3(0.8f, 0f, 0f);
            Assert.That(_interactor.BeginInteract(), Is.True);
            yield return null;
            Assert.That(_villager.IsTalking, Is.True);

            Vector2 stoppedAt = _villager.transform.position;

            // A long chat: her day has moved several metres up the lane without her — and she is still
            // OUTDOORS at 18:15, so what follows is a walk and not the indoors shortcut below.
            yield return At(18.25f);
            Vector2 shouldBe = ClockSays(18.25f);
            Assert.That(Vector2.Distance(shouldBe, stoppedAt), Is.GreaterThan(2f),
                        "the rig has to open a real gap or this proves nothing");

            _presenter.Close();                        // the conversation ends
            Assert.That(_villager.HoldPhase, Is.EqualTo(ConversationHoldPhase.CatchingUp),
                        "she does not teleport back — she hurries");

            yield return CatchUpSettles();

            Assert.That(Vector2.Distance(_villager.transform.position, ClockSays(_clock.Hour)),
                        Is.LessThan(ConversationHold.ArrivedMetres + 0.05f),
                        "she rejoined the day the clock says she should be having");
            Assert.That(Vector2.Distance(_villager.transform.position, stoppedAt), Is.GreaterThan(2f),
                        "and NOT the one she left — resuming to the pre-chat spot would make a " +
                        "conversation rewind her schedule");
        }

        [UnityTest]
        public IEnumerator AfterTheCatchUp_SheIsThePureFunctionAgain_ExactlyAndForever()
        {
            yield return At(18.15f);
            _player.position = _villager.transform.position + new Vector3(0.8f, 0f, 0f);
            Assert.That(_interactor.BeginInteract(), Is.True,
                        "⚠️ ASSERTED, not merely called. Every other claim this test makes is true of a " +
                        "villager who never stopped in the first place — Close() no-ops, the phase is " +
                        "already Idle, and 'she is the pure function again' is trivially true. Without " +
                        "this line the test passes VACUOUSLY, which is exactly how it survived the run " +
                        "that reddened its seven neighbours.");
            yield return null;

            yield return At(18.25f);
            _presenter.Close();
            yield return CatchUpSettles();

            // Hours later, with nothing overlaying anything, she is bit-for-bit the clock's answer.
            yield return At(21f);
            Assert.That((Vector2)_villager.transform.position, Is.EqualTo(ClockSays(21f)),
                        "the overlay ended and left nothing behind — no offset, no drift, no state");
            yield return At(23.5f);
            Assert.That((Vector2)_villager.transform.position, Is.EqualTo(ClockSays(23.5f)));
        }

        [UnityTest]
        public IEnumerator IfTheClockTookHerIndoors_TheCatchUpIsAbandonedRatherThanWalkedThroughAWall()
        {
            yield return At(18.05f);
            _player.position = _villager.transform.position + new Vector3(0.8f, 0f, 0f);
            Assert.That(_interactor.BeginInteract(), Is.True);
            yield return null;
            Assert.That(_villager.IsTalking, Is.True);

            // Talk right past her arrival: the clock now has her a metre inside her own front door.
            yield return At(19f);
            Assert.That(_villager.Pose.Shelter, Is.EqualTo(RoutineShelter.InsideDestination),
                        "the rig has to actually put the clock's answer behind a threshold");

            _presenter.Close();
            yield return null;
            yield return null;

            Assert.That(_villager.HoldPhase, Is.EqualTo(ConversationHoldPhase.Idle),
                        "⭐ a catch-up must never become a walk through a wall — the overlay ends and " +
                        "hands her back to the pure function, so the jump happens behind a door where " +
                        "nobody can see it rather than through one");
            Assert.That((Vector2)_villager.transform.position, Is.EqualTo(ClockSays(19f)));
        }

        // ---- the seam -----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator WalkingAway_ClosesTheBubbleAndReleasesHer_WithNoWedgedGate()
        {
            yield return At(12f);
            _player.position = new Vector3(Green.x + 1f, Green.y, 0f);
            Assert.That(_interactor.BeginInteract(), Is.True);
            yield return null;

            Assert.That(_presenter.IsShowing, Is.True);
            Assert.That(_villager.IsTalking, Is.True);
            Assert.That(InteractionGate.IsBlocked, Is.True);

            _player.position = new Vector3(Green.x + 40f, Green.y, 0f);   // off up the lane
            yield return null;
            yield return null;

            Assert.That(_presenter.IsShowing, Is.False, "walking off ends a conversation");
            Assert.That(_villager.IsTalking, Is.False, "and lets her get back to her day");
            Assert.That(InteractionGate.IsBlocked, Is.False,
                        "⭐ and it must NOT leave the interact key wedged off — that would break every " +
                        "interaction in the game, everywhere, silently");
        }

        [UnityTest]
        public IEnumerator AHalfStepBack_DoesNotHangUpOnYou()
        {
            yield return At(12f);
            _player.position = new Vector3(Green.x + 1f, Green.y, 0f);
            Assert.That(_interactor.BeginInteract(), Is.True);
            yield return null;
            Assert.That(_presenter.IsShowing, Is.True);

            // Just past the prompt's own reach, but inside the disengage slack.
            _player.position = new Vector3(Green.x + Reach * 1.1f, Green.y, 0f);
            yield return null;
            yield return null;

            Assert.That(_presenter.IsShowing, Is.True,
                        "without hysteresis, standing on the edge of reach would open and close the " +
                        "bubble on alternate frames and a half-step back would read as being hung up on");
        }

        [UnityTest]
        public IEnumerator ARegionTornDownMidConversation_LeavesNobodyStandingInOne()
        {
            yield return At(12f);
            _player.position = new Vector3(Green.x + 1f, Green.y, 0f);
            Assert.That(_interactor.BeginInteract(), Is.True);
            yield return null;
            Assert.That(_villager.IsTalking, Is.True);

            _interactor.enabled = false;
            _villager.enabled = false;
            yield return null;

            Assert.That(_villager.HoldPhase, Is.EqualTo(ConversationHoldPhase.Idle),
                        "a villager switched off mid-sentence must not come back still standing in it");
        }

        // ---- indoors ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ATalkINSIDEAHouse_KeepsHerVisibleAndTalkable_WhileTheClockRunsOn()
        {
            // The interior reveal draws an occupant only while the PLAYER shares that interior. Put both
            // of them inside, then hold a conversation across her next departure: her own shelter rule
            // would otherwise hide her mid-sentence, because the clock has meanwhile walked her out of
            // her own front door.
            Vector2 inside = InsidePoint(_home);
            _player.position = new Vector3(inside.x + 0.4f, inside.y, 0f);
            _home.SetOccupant(_player);

            yield return AtSettled(23f);         // long since home and settled indoors
            Assert.That(_home.IsInside, Is.True, "the rig must actually put the player in the room");
            Assert.That(_renderer.enabled, Is.True, "and she must be drawn, or there is nothing to talk to");

            Assert.That(_interactor.BeginInteract(), Is.True);
            yield return null;
            Assert.That(_villager.IsTalking, Is.True, "a conversation indoors works through the same seam");

            // Straight past her morning departure, still talking.
            yield return AtSettled(8.2f);

            Assert.That(_renderer.enabled, Is.True,
                        "⭐ she must not blink out of existence mid-sentence because her SCHEDULE has " +
                        "meanwhile taken her out through the door she is not standing at");
            Assert.That(_talkPoint.enabled, Is.True, "…nor become un-talkable in the middle of talking");
            Assert.That(_presenter.IsShowing, Is.True);
        }
    }
}
