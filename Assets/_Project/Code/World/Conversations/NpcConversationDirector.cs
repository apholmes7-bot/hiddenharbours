using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>TWO VILLAGERS TALK, AND YOU CAN WATCH</b> — the owner's 2026-09-06 ruling: <i>"I also want npcs
    /// to engage in conversation with each other and the conversations to be visible bubbles as they
    /// talk, the player can still walk and run as normal."</i>
    ///
    /// <para><b>It observes routines; it never drives them.</b> Nobody is scheduled here, nobody is moved
    /// here, nothing is written down and nothing is saved. The director watches for the moment two
    /// authored participants are BOTH standing near an authored station inside an authored window, and if
    /// they are, it speaks the exchange through the ambient bubble. A routine is the same pure function
    /// of the clock during a chat as before one (rule 5) — the stop is
    /// <see cref="ConversationHold"/>'s overlay, exactly as it is when the player talks to somebody.</para>
    ///
    /// <para><b>Sequencing is on the BEAT, not on a timer.</b> Line 2 waits for
    /// <see cref="AmbientSpeechEnded"/> with <see cref="AmbientSpeechEndReason.Dwelled"/> on line 1 —
    /// i.e. for the line to have been readable — so the exchange runs at the speakers' own cadences and a
    /// re-tuned voice cannot desynchronise it. Any other ending (displaced by a busier moment, cancelled
    /// because the player pressed Talk on one of them, the speaker despawned) <b>aborts the exchange
    /// cleanly</b> rather than speaking the next line into a bubble that is not there.</para>
    ///
    /// <para><b>The player is never held.</b> Not once, not for a frame. She can walk through the middle
    /// of it; the bubbles are ambient and take nothing (see <see cref="AmbientSpeechPresenter"/>). If she
    /// presses Talk on one of the speakers, the modal wins and the pair's exchange ends — one person
    /// never has two bubbles.</para>
    ///
    /// <para><b>Self-installing</b> (the <c>DayNightController</c> / <see cref="InnerVoiceDirector"/>
    /// idiom): one hidden persistent host, so the exchanges work in every region with no scene wiring and
    /// no builder change.</para>
    ///
    /// <para><b>Rule 4 clean.</b> Reads <see cref="GameServices.Clock"/>,
    /// <see cref="GameServices.Environment"/> and <see cref="GameServices.CurrentRegionId"/>; publishes
    /// <see cref="AmbientSpeechRequested"/>; subscribes <see cref="AmbientSpeechEnded"/>. It names no
    /// feature module and none names it.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NpcConversationDirector : MonoBehaviour
    {
        /// <summary>How often the village is checked for a due exchange, in seconds. A conversation is
        /// not a physics query: twice a second is imperceptible against a walking pace and costs a
        /// handful of distance compares (rule 7, the slow tick).</summary>
        public const float TickSeconds = 0.5f;

        /// <summary>How often the villager roster is rebuilt, in seconds. Villagers appear when a region
        /// loads and vanish when it unloads, so this only has to be faster than a player can notice — and
        /// a <c>FindObjectsByType</c> every tick would be a scene walk for an answer that changes twice an
        /// hour.</summary>
        public const float RosterRescanSeconds = 3f;

        /// <summary>Where a villager's bubble hangs when they carry no <see cref="Interactable"/> of their
        /// own to ask — the same head height an un-anchored one falls back to.</summary>
        public const float BubbleHeightMetres = Interactable.DefaultBubbleHeightMetres;

        private static bool _installed;

        /// <summary>The one director, or null before the first scene. The singleton accessor rather than a
        /// scene query, for the reason <see cref="AmbientSpeechPresenter.Instance"/> gives.</summary>
        public static NpcConversationDirector Instance { get; private set; }

        private ConversationLibrary _library;
        private readonly List<ConversationDef> _conversations = new List<ConversationDef>();

        /// <summary>npc id → the villager living it, rebuilt on the roster tick.</summary>
        private readonly Dictionary<string, VillagerRoutine> _villagers =
            new Dictionary<string, VillagerRoutine>(16, System.StringComparer.Ordinal);

        /// <summary>Conversation id → the game day it last ran on, so a once-a-day exchange is once a
        /// day. Session state, never saved: chatter is not history.</summary>
        private readonly Dictionary<string, int> _ranOnDay =
            new Dictionary<string, int>(8, System.StringComparer.Ordinal);

        /// <summary>Conversation id → the game time it last ran at, for a repeatable exchange's gap.</summary>
        private readonly Dictionary<string, double> _ranAt =
            new Dictionary<string, double>(8, System.StringComparer.Ordinal);

        private RoutineStations _stations;
        private float _tickClock;
        private float _rosterClock;
        private bool _subscribed;

        // ---- the exchange in flight ------------------------------------------------------------

        private ConversationDef _running;
        private VillagerRoutine _speakerA;
        private VillagerRoutine _speakerB;
        private int _lineIndex = -1;
        private int _pendingSpeechId;

        /// <summary>How many exchanges were loaded. Zero is a working state — no library, no chatter.</summary>
        public int ConversationCount => _conversations.Count;

        /// <summary>The exchange being spoken right now, or null. <b>One at a time, on purpose</b>: two
        /// pairs talking at once would put four bubbles on a screen the pool caps at three, and the coast
        /// would read as a crowd rather than as neighbours.</summary>
        public ConversationDef Running => _running;

        /// <summary>Which line of <see cref="Running"/> is on screen, or −1.</summary>
        public int RunningLineIndex => _lineIndex;

        /// <summary>The id of the last exchange that actually began. What a test asserts on.</summary>
        public string LastStartedId { get; private set; }

        /// <summary>
        /// Why the last DUE exchange did not start, or null when none was refused since the last one ran.
        ///
        /// <para>It exists for the owner's playtest. An exchange is authored against two people's days,
        /// and the honest answer to "why didn't they talk?" is usually mundane — one of them was still
        /// walking, or was four metres too far along the green. Without this the only way to find out is
        /// to read six assets and do the arithmetic; with it, a dev readout or a test can just say so.
        /// It is a string rather than an enum because it names the PARTICIPANT, which is the half that
        /// makes it useful.</para>
        /// </summary>
        public string LastRefusalReason { get; private set; }

        /// <summary>Why the last exchange ended, or null if none has. <c>"finished"</c> when it was spoken
        /// to the end; otherwise the <see cref="AmbientSpeechEndReason"/> that stopped it.</summary>
        public string LastEndReason { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("NpcConversationDirector") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<NpcConversationDirector>();
        }

        private void Awake()
        {
            Instance = this;
            LoadLibrary(Resources.Load<ConversationLibrary>(ConversationLibrary.ResourcesPath));
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        /// <summary>Take a library (the boot path passes the Resources one; a fixture passes its own).
        /// Keeps only the runnable defs and registers every voice they name, so the presenter can resolve
        /// the id that crosses Core.</summary>
        public void LoadLibrary(ConversationLibrary library)
        {
            _library = library;
            _conversations.Clear();
            Abort(AmbientSpeechEndReason.Cancelled);
            if (library == null || library.Conversations == null) return;

            foreach (ConversationDef def in library.Conversations)
            {
                if (def == null || !def.IsRunnable) continue;

                foreach (ConversationLine line in def.Lines)
                    DialogueVoiceCatalog.Register(line.Voice);
                foreach (NpcDef npc in def.Participants)
                    if (npc != null) DialogueVoiceCatalog.Register(npc.Voice);

                _conversations.Add(def);
            }
        }

        private void OnEnable()
        {
            if (_subscribed) return;
            EventBus.Subscribe<AmbientSpeechEnded>(OnSpeechEnded);
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (_subscribed) EventBus.Unsubscribe<AmbientSpeechEnded>(OnSpeechEnded);
            _subscribed = false;
            Abort(AmbientSpeechEndReason.Cancelled);
        }

        // ---- the tick ---------------------------------------------------------------------------

        private void Update()
        {
            if (_conversations.Count == 0) return;

            float dt = Time.unscaledDeltaTime;

            _rosterClock -= dt;
            if (_rosterClock <= 0f) { _rosterClock = RosterRescanSeconds; RebuildRoster(); }

            // An exchange in flight is driven entirely by the end-of-line signal; nothing to poll. But a
            // speaker who was destroyed mid-sentence has to end it, or the pair stays held forever.
            if (_running != null)
            {
                if (_speakerA == null || _speakerB == null) Abort(AmbientSpeechEndReason.Cancelled);
                return;
            }

            _tickClock -= dt;
            if (_tickClock > 0f) return;
            _tickClock = TickSeconds;

            TryStartOne();
        }

        private void RebuildRoster()
        {
            _villagers.Clear();
            foreach (VillagerRoutine v in FindObjectsByType<VillagerRoutine>(FindObjectsSortMode.None))
            {
                RoutineDef routine = v != null ? v.Routine : null;
                string id = routine != null && routine.Npc != null ? routine.Npc.Id : null;
                if (!string.IsNullOrEmpty(id)) _villagers[id] = v;
            }

            if (_stations == null) _stations = FindAnyObjectByType<RoutineStations>(FindObjectsInactive.Exclude);
        }

        /// <summary>
        /// Start the first exchange that is due and possible. Refusals, in order of cost: wrong region,
        /// already run today (or still inside a repeatable gap), not yet its seeded minute or past its
        /// window, a participant not in this region, a participant still walking, a participant too far
        /// from the meeting station, a participant already talking to somebody.
        /// </summary>
        private void TryStartOne()
        {
            IGameClock clock = GameServices.Clock;
            IEnvironmentService env = GameServices.Environment;
            if (clock == null || env == null || _stations == null) return;

            string region = GameServices.CurrentRegionId;
            if (string.IsNullOrEmpty(region)) return;

            float hour = clock.HourOfDay;
            int day = clock.DayIndex;

            for (int i = 0; i < _conversations.Count; i++)
            {
                ConversationDef def = _conversations[i];
                if (!string.Equals(def.RegionId, region, System.StringComparison.Ordinal)) continue;

                if (_ranOnDay.TryGetValue(def.Id, out int lastDay) && lastDay == day)
                {
                    if (!def.Repeatable) continue;
                    if (_ranAt.TryGetValue(def.Id, out double lastAt) &&
                        clock.TotalSeconds - lastAt < def.GapSeconds) continue;
                }

                float start = ConversationSchedule.StartHourFor(env.WorldSeed, day, def.Id,
                                                                def.EarliestHour, def.LatestHour);
                if (!ConversationSchedule.IsDue(hour, start, def.LatestHour)) continue;

                if (!_stations.TryFind(def.StationId, out RoutineStationEntry station))
                { Refuse(def, $"no station '{def.StationId}' in this region"); continue; }

                VillagerRoutine a = Villager(def.ParticipantId(0));
                VillagerRoutine b = Villager(def.ParticipantId(1));
                if (a == null) { Refuse(def, $"{def.ParticipantId(0)} is not in this region"); continue; }
                if (b == null) { Refuse(def, $"{def.ParticipantId(1)} is not in this region"); continue; }

                if (!StandingNear(a, station.Position, def.RadiusMetres))
                { Refuse(def, Why(a, station.Position, def.RadiusMetres)); continue; }
                if (!StandingNear(b, station.Position, def.RadiusMetres))
                { Refuse(def, Why(b, station.Position, def.RadiusMetres)); continue; }

                if (a.IsTalking || b.IsTalking)
                { Refuse(def, "the player is already talking to one of them"); continue; }

                Begin(def, a, b, day, clock.TotalSeconds);
                return;
            }
        }

        /// <summary>Record why a due exchange did not start. Overwrites: the newest answer is the one
        /// worth having, and a list would be a log nobody reads.</summary>
        private void Refuse(ConversationDef def, string why) => LastRefusalReason = $"{def.Id}: {why}";

        /// <summary>The mundane reason somebody is not at the meeting point, in words.</summary>
        private static string Why(VillagerRoutine villager, Vector2 station, float radius)
        {
            string who = villager.Routine != null && villager.Routine.Npc != null
                ? villager.Routine.Npc.Id : villager.name;
            if (!villager.IsLiving) return $"{who} has no routine yet";
            if (villager.Pose.Walking) return $"{who} is still walking";
            float d = Vector2.Distance((Vector2)villager.transform.position, station);
            return $"{who} is {d:0.#} m from the station (radius {radius:0.#} m)";
        }

        private VillagerRoutine Villager(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return null;
            // ⚠ Explicit != null on the value: a destroyed component is fake-null and would otherwise be
            // handed out as a live villager.
            return _villagers.TryGetValue(npcId, out VillagerRoutine v) && v != null ? v : null;
        }

        /// <summary>Standing (not mid-walk) and inside the meeting radius. Both halves matter: somebody
        /// crossing the green on their way elsewhere is not at the green.</summary>
        private static bool StandingNear(VillagerRoutine villager, Vector2 station, float radius)
        {
            if (!villager.IsLiving || villager.Pose.Walking) return false;
            Vector2 here = villager.transform.position;
            return (here - station).sqrMagnitude <= radius * radius;
        }

        // ---- running one exchange -----------------------------------------------------------------

        private void Begin(ConversationDef def, VillagerRoutine a, VillagerRoutine b, int day, double at)
        {
            _running = def;
            _speakerA = a;
            _speakerB = b;
            _lineIndex = -1;
            _ranOnDay[def.Id] = day;
            _ranAt[def.Id] = at;
            LastStartedId = def.Id;
            LastEndReason = null;
            LastRefusalReason = null;

            // Each of them stops and turns to the other — the same overlay the player's Talk uses, and
            // refusable the same way. A refused hold is NOT a refused conversation: an uninterruptible
            // block means they carry on walking and the bubble travels with them, which reads exactly as
            // two people talking on their way somewhere.
            a.TryHoldForConversation(b.transform);
            b.TryHoldForConversation(a.transform);

            SpeakNext();
        }

        private void SpeakNext()
        {
            if (_running == null) return;

            _lineIndex++;
            if (_lineIndex >= _running.Lines.Length) { Finish("finished"); return; }

            NpcDef speaker = _running.SpeakerOf(_lineIndex);
            VillagerRoutine body = SpeakerBody(_lineIndex);

            // A line whose speaker index names nobody is skipped rather than spoken by the wrong person.
            // Content validation fails on it; at runtime the exchange simply carries on.
            if (speaker == null || body == null) { SpeakNext(); return; }

            string text = _running.Lines[_lineIndex].Text;
            if (string.IsNullOrWhiteSpace(text)) { SpeakNext(); return; }

            _pendingSpeechId = AmbientSpeechId.Next();
            EventBus.Publish(new AmbientSpeechRequested(
                _pendingSpeechId,
                body.transform,
                AnchorOffsetFor(body),
                speaker.Id,
                speaker.DisplayName,
                text,
                _running.VoiceIdFor(_lineIndex),
                AmbientSpeechKind.NpcToNpc));
        }

        /// <summary>Which of the two bodies speaks line <paramref name="lineIndex"/>.</summary>
        private VillagerRoutine SpeakerBody(int lineIndex)
        {
            int who = _running.Lines[lineIndex].SpeakerIndex;
            if (who == 0) return _speakerA;
            if (who == 1) return _speakerB;
            return null;
        }

        /// <summary>Where this villager's bubble hangs — their own <see cref="Interactable"/>'s anchor
        /// when they carry one, so an overheard line sits exactly where their spoken one would, else the
        /// default head height.</summary>
        private static Vector3 AnchorOffsetFor(VillagerRoutine villager)
        {
            var interactable = villager.GetComponent<Interactable>();
            return interactable != null
                ? interactable.BubbleAnchorOffset
                : new Vector3(0f, BubbleHeightMetres, 0f);
        }

        private void OnSpeechEnded(AmbientSpeechEnded ended)
        {
            if (_running == null || ended.Id != _pendingSpeechId) return;

            // Delivered means the line was filled AND stood long enough to read. Anything else — the pool
            // displaced it, the player pressed Talk on the speaker, the speaker was destroyed — means the
            // exchange is over, because the next line would be an answer to something nobody heard.
            if (ended.Delivered) SpeakNext();
            else Abort(ended.Reason);
        }

        private void Abort(AmbientSpeechEndReason reason)
        {
            if (_running == null) return;
            Finish(reason.ToString().ToLowerInvariant());
        }

        /// <summary>End the exchange and hand both speakers back their day. Releasing a villager who was
        /// never held is a no-op, and releasing one the PLAYER is now talking to is harmless — her own
        /// Talk re-holds them on the frame it opens.</summary>
        private void Finish(string reason)
        {
            if (_speakerA != null) _speakerA.ReleaseFromConversation();
            if (_speakerB != null) _speakerB.ReleaseFromConversation();

            _running = null;
            _speakerA = null;
            _speakerB = null;
            _lineIndex = -1;
            _pendingSpeechId = 0;
            LastEndReason = reason;
        }

        /// <summary>Forget which exchanges have run today (fixtures, and a new game). Does not touch the
        /// library.</summary>
        public void ForgetSessionState()
        {
            Abort(AmbientSpeechEndReason.Cancelled);
            _ranOnDay.Clear();
            _ranAt.Clear();
            LastStartedId = null;
            LastEndReason = null;
        }
    }
}
