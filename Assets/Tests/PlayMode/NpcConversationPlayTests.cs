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
    /// <b>TWO VILLAGERS TALKING, OVER REAL FRAMES</b> — the wiring of the owner's 2026-09-06 ruling, on
    /// two real <see cref="VillagerRoutine"/>s with a clock this test moves by hand.
    ///
    /// <para><b>What only PlayMode can show.</b> The rules are EditMode's — <c>ConversationScheduleTests</c>
    /// owns the seeded minute and <c>ConversationDefTests</c> owns the def. What is left is the SEAM, and
    /// it is where this can actually fail: that the director finds two villagers by their npc ids, that it
    /// refuses when one of them is absent or still walking, that line 2 waits for line 1 to have been
    /// READ rather than for a timer, that each line hangs over the right speaker — and, the law the whole
    /// feature exists for, <b>that the player is never held for a second of it</b>.</para>
    ///
    /// <para><b>⚠ DRIVEN IN GAME HOURS, NEVER IN FRAMES</b> (the <c>NpcStopsToTalkPlayTests</c>
    /// convention): the clock is a fake this test SETS. Waits on the exchange itself are by CONDITION with
    /// a real-seconds deadline, never a frame budget — headless batchmode runs uncapped, so a frame count
    /// is not a duration.</para>
    ///
    /// <para><b>Headless-safe by construction (⚠ do not relax).</b> Nothing renders or reads pixels. Every
    /// assertion is on STATE: a published signal, a visible string, a transform, a flag.</para>
    /// </summary>
    public class NpcConversationPlayTests
    {
        sealed class DrivenClock : IGameClock
        {
            public float Hour;
            public int Day;
            public double TotalSeconds => (Day * 24.0 + Hour) / 24.0 * GameConfig.DefaultSecondsPerDay;
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => Day;
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

        const string RegionId = "region.playtest";
        const string StationId = "s.green";
        const string ConversationId = "conversation.playtest.pair";

        // The green, and two places on it a stride apart — the shape the real village uses.
        static readonly Vector2 Green = new Vector2(0f, 0f);
        static readonly Vector2 SpotA = new Vector2(-1.4f, 0f);
        static readonly Vector2 SpotB = new Vector2(1.4f, 0f);
        static readonly Vector2 Away = new Vector2(0f, 40f);

        readonly List<Object> _spawned = new();
        readonly List<AmbientSpeechRequested> _spoken = new();

        DrivenClock _clock;
        FakeEnvironment _env;
        RoutineStations _stations;
        RoutineLanes _lanes;
        VillagerRoutine _ada, _ben;
        NpcDef _adaNpc, _benNpc;
        ConversationDef _def;
        NpcConversationDirector _director;
        AmbientSpeechPresenter _presenter;

        /// <summary>A cadence fast enough that a two-line exchange is over in a second of real time,
        /// with a read pause short enough to wait out.</summary>
        const string VoiceId = "voice.playtest_quick";
        static DialogueVoice QuickVoice => new DialogueVoice
        {
            CharactersPerSecond = 80f,
            CharactersPerTick = 1,
            PunctuationPauseSeconds = 0f,
            ReadPauseSeconds = 0.15f,
            TimbreId = "timbre.playtest",
        };

        [SetUp]
        public void SetUp()
        {
            Spawn("AudioListener").AddComponent<AudioListener>();

            GameServices.Reset();
            InteractionGate.Reset();
            DialogueVoiceCatalog.Clear();
            DialogueVoiceCatalog.Register(VoiceId, QuickVoice);

            _clock = new DrivenClock { Hour = 12.5f, Day = 0 };
            GameServices.Clock = _clock;
            _env = new FakeEnvironment();
            GameServices.Environment = _env;
            GameServices.CurrentRegionId = RegionId;

            var camGo = Spawn("TestCamera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 12f;
            cam.transform.position = new Vector3(0f, 0f, -10f);

            var lanesGo = Spawn("RoutineLanes");
            _lanes = lanesGo.AddComponent<RoutineLanes>();
            _lanes.Configure(
                nodePositions: new[] { Green, Away },
                nodeParents: new[] { RoutineLaneTree.NoParent, 0 },
                nodeNames: new[] { "green", "away" },
                viaStart: new int[2], viaCount: new int[2], via: System.Array.Empty<Vector2>());

            var stationsGo = Spawn("RoutineStations");
            _stations = stationsGo.AddComponent<RoutineStations>();
            _stations.Configure(new[]
            {
                Station(StationId, Green, "green"),
                Station("s.a", SpotA, "green"),
                Station("s.b", SpotB, "green"),
                Station("s.away", Away, "away"),
            });

            _adaNpc = Npc("npc.playtest_ada", "Ada");
            _benNpc = Npc("npc.playtest_ben", "Ben");

            // Both of them stand on the green all day, a stride apart — so the ONLY thing deciding
            // whether they talk is the director, which is what is under test.
            _ada = Villager("Ada", SpotA, _adaNpc, "s.a");
            _ben = Villager("Ben", SpotB, _benNpc, "s.b");

            _def = Conversation(
                new ConversationLine { SpeakerIndex = 0, Text = "Some weather" },
                new ConversationLine { SpeakerIndex = 1, Text = "It is that" });

            _presenter = AmbientSpeechPresenter.Instance;
            Assert.IsNotNull(_presenter, "the ambient presenter did not install itself");
            _presenter.SetCamera(cam);
            _presenter.CancelAll();

            _director = NpcConversationDirector.Instance;
            Assert.IsNotNull(_director, "the conversation director did not install itself");
            _director.ForgetSessionState();
            _director.LoadLibrary(Library(_def));

            EventBus.Subscribe<AmbientSpeechRequested>(OnSpoken);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<AmbientSpeechRequested>(OnSpoken);
            _spoken.Clear();

            if (_director != null) { _director.ForgetSessionState(); _director.LoadLibrary(null); }
            if (_presenter != null) { _presenter.CancelAll(); _presenter.SetCamera(null); }

            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();

            DialogueVoiceCatalog.Clear();
            GameServices.Reset();
            InteractionGate.Reset();
        }

        void OnSpoken(AmbientSpeechRequested r) => _spoken.Add(r);

        GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        static RoutineStationEntry Station(string id, Vector2 at, string lane)
            => new RoutineStationEntry { Id = id, Position = at, StandHeadingDegrees = 0f,
                                         LaneNodeName = lane, Why = "playtest" };

        NpcDef Npc(string id, string name)
        {
            var n = ScriptableObject.CreateInstance<NpcDef>();
            _spawned.Add(n);
            n.Id = id;
            n.DisplayName = name;
            return n;
        }

        /// <summary>A villager who stands at one spot all day — two blocks at the SAME station, so she is
        /// never walking and the fixture is about the director rather than about pathing.</summary>
        VillagerRoutine Villager(string name, Vector2 at, NpcDef npc, string stationId)
        {
            var routine = ScriptableObject.CreateInstance<RoutineDef>();
            _spawned.Add(routine);
            routine.Id = "routine.playtest_" + name.ToLowerInvariant();
            routine.Npc = npc;
            routine.WalkSpeedMetresPerSecond = 1.2f;
            routine.ScheduleJitterMinutes = 0f;   // ⚠ zero: the departure hours ARE the authored ones
            routine.Entries = new[]
            {
                new RoutineEntry { StartHour = 6f, StationId = stationId, Activity = RoutineActivity.Recreation },
                new RoutineEntry { StartHour = 18f, StationId = stationId, Activity = RoutineActivity.Recreation },
            };

            var go = Spawn(name);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            var talkPoint = go.AddComponent<Interactable>();
            talkPoint.Configure(npc);
            var villager = go.AddComponent<VillagerRoutine>();
            villager.Configure(routine, _stations, _lanes, renderer);
            return villager;
        }

        ConversationDef Conversation(params ConversationLine[] lines)
        {
            var d = ScriptableObject.CreateInstance<ConversationDef>();
            _spawned.Add(d);
            d.Id = ConversationId;
            d.Participants = new[] { _adaNpc, _benNpc };
            for (int i = 0; i < lines.Length; i++) lines[i].Voice = null;
            d.Lines = lines;
            d.RegionId = RegionId;
            d.StationId = StationId;
            d.RadiusMetres = 3f;
            d.EarliestHour = 12f;
            d.LatestHour = 13f;
            d.Repeatable = false;
            return d;
        }

        ConversationLibrary Library(params ConversationDef[] defs)
        {
            var lib = ScriptableObject.CreateInstance<ConversationLibrary>();
            _spawned.Add(lib);
            lib.Conversations = defs;
            return lib;
        }

        /// <summary>
        /// The game hour today's run of the authored exchange is actually due at.
        ///
        /// <para>⚠ <b>Ask for the seeded minute; never guess at it.</b> The start is a hash of
        /// <c>(worldSeed, dayIndex, conversationId)</c> somewhere inside the authored window — that is
        /// the whole determinism design — so a fixture that parks the clock at the window's midpoint
        /// fires or does not fire depending on which side of the midpoint this seed happens to land.
        /// Mine landed above it, and four of these tests reported "the exchange never started" for a
        /// director that was behaving perfectly.</para>
        /// </summary>
        float DueHour() => ConversationSchedule.StartHourFor(
            _env.WorldSeed, _clock.Day, ConversationId, _def.EarliestHour, _def.LatestHour);

        /// <summary>Put the clock at <paramref name="hour"/> and let the villagers settle onto it.</summary>
        IEnumerator At(float hour)
        {
            _clock.Hour = hour;
            yield return null;
            yield return new WaitForSecondsRealtime(VillagerRoutine.ShelterCheckSeconds + 0.05f);
        }

        /// <summary>Wait, in REAL seconds with a deadline, until <paramref name="until"/> or the time is
        /// up. Never a frame budget — headless renders uncapped.</summary>
        IEnumerator Until(System.Func<bool> until, float seconds = 6f)
        {
            float t = 0f;
            while (!until() && t < seconds) { yield return null; t += Time.unscaledDeltaTime; }
        }

        // ---- the exchange -------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TwoVillagersOnTheGreen_HaveTheirExchange_LineByLine()
        {
            yield return At(DueHour());
            yield return Until(() => _spoken.Count >= 2);

            Assert.That(_director.LastStartedId, Is.EqualTo(ConversationId),
                        "the exchange never started with both of them standing on the green in the window");
            Assert.That(_spoken.Count, Is.EqualTo(2), "both lines should have been spoken, in order");

            Assert.That(_spoken[0].Line, Is.EqualTo("Some weather"));
            Assert.That(_spoken[0].SpeakerId, Is.EqualTo(_adaNpc.Id));
            Assert.That(_spoken[0].Anchor, Is.SameAs(_ada.transform),
                        "line 1 must hang over the person who says it");

            Assert.That(_spoken[1].Line, Is.EqualTo("It is that"));
            Assert.That(_spoken[1].SpeakerId, Is.EqualTo(_benNpc.Id));
            Assert.That(_spoken[1].Anchor, Is.SameAs(_ben.transform),
                        "the ANSWER must hang over the other one — a two-hander with both bubbles on one " +
                        "head is a monologue");

            Assert.That(_spoken[0].Kind, Is.EqualTo(AmbientSpeechKind.NpcToNpc));

            yield return Until(() => _director.Running == null);
            Assert.That(_director.LastEndReason, Is.EqualTo("finished"));
        }

        /// <summary>
        /// ⭐ THE LAW, at the conversation level. She can walk through the middle of two neighbours
        /// talking and nothing touches her: the gate is never raised on any frame of the exchange.
        /// </summary>
        [UnityTest]
        public IEnumerator ThePlayerIsNeverHeldForASecondOfIt()
        {
            yield return At(DueHour());

            float t = 0f;
            int frames = 0;
            while (t < 4f && (_director.Running != null || _spoken.Count < 2))
            {
                Assert.IsFalse(InteractionGate.IsBlocked,
                    $"frame {frames}: two NPCs talking to each other raised InteractionGate — the " +
                    "owner's law is that the player can still walk and run as normal");
                yield return null;
                t += Time.unscaledDeltaTime;
                frames++;
            }

            Assert.Greater(frames, 1, "no frames ran, so nothing was tested");
            Assert.That(_spoken.Count, Is.EqualTo(2), "the exchange did not happen");
            Assert.IsFalse(InteractionGate.IsBlocked);
        }

        [UnityTest]
        public IEnumerator TheSecondLineWaitsForTheFirstToBeREAD_NotForAFrame()
        {
            yield return At(DueHour());
            yield return Until(() => _spoken.Count >= 1);

            Assert.That(_spoken.Count, Is.EqualTo(1),
                        "the second line arrived before the first had been read — the exchange is " +
                        "running on something other than the end-of-line beat");

            // It only arrives once line 1 has dwelled.
            yield return Until(() => _spoken.Count >= 2);
            Assert.That(_spoken.Count, Is.EqualTo(2));
        }

        // ---- the refusals -------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ItNeverFiresWithAParticipantAbsent()
        {
            // ⚠ Take the GameObject FIRST. Reading `_ben.gameObject` after destroying it throws
            // MissingReferenceException — a destroyed component is fake-null for `==`, but touching a
            // MEMBER of it is still an access to a dead object.
            GameObject benGo = _ben.gameObject;
            _spawned.Remove(benGo);
            Object.DestroyImmediate(benGo);

            yield return At(DueHour());
            yield return Until(() => _spoken.Count > 0, seconds: 5f);

            Assert.That(_spoken, Is.Empty,
                        "an exchange fired with one of its two participants not in the region — the " +
                        "other one would be talking to nobody");
            Assert.IsNull(_director.Running);
        }

        [UnityTest]
        public IEnumerator ItNeverFiresWhenOneOfThemIsNowhereNearTheStation()
        {
            // ⚠ A villager cannot be moved by writing her transform: VillagerRoutine rewrites it from
            // her plan every frame, so the move is gone before the director's next tick. Her ROUTINE has
            // to put her elsewhere — which is also the only version of this that tests anything, since
            // it is where the clock puts her that the radius is measured against.
            GameObject oldBen = _ben.gameObject;
            _spawned.Remove(oldBen);
            Object.DestroyImmediate(oldBen);
            _ben = Villager("BenAway", Away, _benNpc, "s.away");

            yield return At(DueHour());
            yield return Until(() => _spoken.Count > 0, seconds: 5f);

            Assert.That(_spoken, Is.Empty,
                        "they talked across forty metres — the meeting radius is not being applied");
        }

        [UnityTest]
        public IEnumerator ItNeverFiresOutsideTheAuthoredWindow()
        {
            yield return At(9f);      // hours before the earliest
            yield return Until(() => _spoken.Count > 0, seconds: 3f);
            Assert.That(_spoken, Is.Empty, "it fired before its window");

            yield return At(16f);     // hours after the latest
            yield return Until(() => _spoken.Count > 0, seconds: 3f);
            Assert.That(_spoken, Is.Empty,
                        "it fired after its window — an exchange that could not start must not turn up " +
                        "hours later at a moment the author never pictured");
        }

        [UnityTest]
        public IEnumerator ItHappensOnceADay_NotOnEveryTick()
        {
            yield return At(DueHour());
            yield return Until(() => _director.Running == null && _spoken.Count >= 2);

            int after = _spoken.Count;
            // Later in the window, but still inside it — and still at or past the seeded start.
            yield return At(Mathf.Min(DueHour() + 0.25f, _def.LatestHour));
            yield return Until(() => _spoken.Count > after, seconds: 3f);

            Assert.That(_spoken.Count, Is.EqualTo(after),
                        "the exchange came round again the same day — a once-a-day chat on a half-second " +
                        "tick would be a loop, not a village");
        }

        [UnityTest]
        public IEnumerator ANewDay_LetsThemTalkAgain()
        {
            yield return At(DueHour());
            yield return Until(() => _director.Running == null && _spoken.Count >= 2);
            int after = _spoken.Count;

            _clock.Day = 1;                   // DueHour() re-seeds off the new day
            yield return At(DueHour());
            yield return Until(() => _spoken.Count > after);

            Assert.That(_spoken.Count, Is.GreaterThan(after),
                        "tomorrow they said nothing — the day is not reaching the schedule");
        }

        // ---- handover -----------------------------------------------------------------------------

        /// <summary>
        /// The player presses Talk on one of them mid-exchange. The pair's chat ends cleanly, both are
        /// released back onto their day, and the modal is what is left — one person never has two bubbles.
        /// </summary>
        [UnityTest]
        public IEnumerator TalkingToOneOfThem_EndsThePairsExchange_AndReleasesBoth()
        {
            var modal = Spawn("DialoguePresenter").AddComponent<DialoguePresenter>();

            yield return At(DueHour());
            yield return Until(() => _director.Running != null && _spoken.Count >= 1);
            Assert.IsNotNull(_director.Running, "the exchange never started, so there is nothing to end");

            modal.Play(new DialogueRequest(
                new[] { new DialogueLine("Ada", "There you are.") },
                _ada.transform, new Vector3(0f, 2.1f, 0f), QuickVoice, null,
                "dialogue.playtest", _adaNpc.Id));

            yield return Until(() => _director.Running == null, seconds: 4f);

            Assert.IsNull(_director.Running, "the pair carried on talking under the player's conversation");
            Assert.That(_director.LastEndReason, Is.EqualTo("cancelled"),
                        "it must end as CANCELLED, so nothing speaks the next line into a bubble that is " +
                        "no longer there");
            Assert.IsTrue(modal.IsShowing, "the conversation the player chose is what is left");

            modal.Close();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ASpeakerDestroyedMidExchange_EndsItRatherThanHangingBothOfThem()
        {
            yield return At(DueHour());
            yield return Until(() => _director.Running != null);

            // ⚠ Take the GameObject FIRST. Reading `_ben.gameObject` after destroying it throws
            // MissingReferenceException — a destroyed component is fake-null for `==`, but touching a
            // MEMBER of it is still an access to a dead object.
            GameObject benGo = _ben.gameObject;
            _spawned.Remove(benGo);
            Object.DestroyImmediate(benGo);

            yield return Until(() => _director.Running == null, seconds: 4f);
            Assert.IsNull(_director.Running);
            Assert.IsFalse(_ada.IsTalking, "the survivor was left standing in a conversation with nobody");
        }
    }
}
