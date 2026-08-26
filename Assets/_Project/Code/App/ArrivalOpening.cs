using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;

namespace HiddenHarbours.App
{
    /// <summary>
    /// <b>THE FIRST TWO MINUTES</b> — a new game does not begin with you standing on a beach. A skipper
    /// runs you in through the reef aboard his cape islander, slows down the dredged channel, comes
    /// alongside the wharf, ties up, and tells you where to find Ginny. You step off onto the planks and
    /// walk up the path. (Owner's ruling, 2026-08-19.)
    ///
    /// <para><b>⭐ WHY THERE IS NO CINEMATIC SYSTEM HERE.</b> Everything this needs already exists and is
    /// already the thing the player is about to be handed: the boat is a real
    /// <see cref="BoatController"/> taking real helm through <see cref="BoatController.SetControl"/>, so
    /// she heels, loses her rudder as she slows and rides the same published wave field every other hull
    /// does; the skipper stands on deck through <see cref="MooredBoat"/>, the same component the ambient
    /// fleets use; the toss and the tie-up are the mooring the game already has. What this class adds is
    /// a STATE MACHINE and nothing else — five states, no coroutines, no timeline asset, no second boat
    /// physics. The opening is the first look anyone gets at how these boats move; it had better be how
    /// they move.</para>
    ///
    /// <para><b>⭐ WHY THE PLAYER IS SEATED BY POSITION AND NOT PARENTED.</b>
    /// <c>ControlSwitcher.ApplyPlayerFor</c> already made this call for the road vehicles and wrote down
    /// why — <i>"seated by position rather than by parenting (see the drive block for why a Unity child
    /// of a despawnable vehicle is not survivable)"</i>. The arrival boat is exactly that: a region
    /// object that goes away, carrying a player who must not. So she rides at a fixed offset, moved each
    /// frame, and the <see cref="ControlSwitcher"/> is not involved at all — the boarding state machine
    /// is the most load-bearing thing in the player module and an opening has no business reaching into
    /// it. What is suppressed is her INPUT (<see cref="PlayerWalkController.enabled"/>), not her.</para>
    ///
    /// <para><b>⭐ GATING, AND ITS RELATIONSHIP TO THE REST ANCHOR (ADR 0037).</b> There are exactly
    /// two things that may decide where a loading player stands, and they must never both act. The
    /// discriminator is the anchor:</para>
    ///
    /// <list type="bullet">
    /// <item><b>Anchor SET</b> → <see cref="RestWakeRestorer"/> wakes them where they slept. The
    /// arrival stands down, unconditionally. Landing a player who went to bed at Ginny's would undo
    /// the whole point of #580.</item>
    /// <item><b>Anchor UNSET</b> → nobody is being woken (<c>Wake()</c> returns
    /// <see cref="RestAnchor.None"/> and "the authored spawn stands"). That is the arrival's path, and
    /// replacing that authored spawn is exactly what it is for.</item>
    /// </list>
    ///
    /// <para><b>⚠ But "no anchor" is NOT the same as "never played", and the second flag is what covers
    /// the difference.</b> ADR 0037 is precise about this: <c>RestRegion == ""</c> means <i>has never
    /// turned in</i>. A player can reach the wharf, walk up to Ginny, buy a rod and quit without ever
    /// having slept — and the save is on disk either way, because roughly a dozen paths write it
    /// (every shop, the licence service, the outfit locker, <c>ShellFlow.QuitToTitle</c>, and
    /// <c>StartingGear</c>, which fires on the first boot before the player has done anything at all).
    /// Continue would then re-land somebody who has been ashore for an hour. So the arrival records
    /// ITSELF, in <see cref="ArrivedFlagKey"/>, which is not a second opinion about freshness — it is
    /// the only witness to a thing that has no other record.</para>
    ///
    /// <para><b>⛔ THE TWO SNAPS ARE GONE (pilotage S1, design/npc-pilotage.md §0).</b> This class used to
    /// finish with two teleports, and they were the design's named anti-goal:</para>
    ///
    /// <list type="number">
    /// <item><c>TieUp()</c> wrote <c>_boatRoot.position = _berth</c> and
    /// <c>_boatRoot.rotation = −_berthHeadingDegrees</c> — the hull snapped to her berth, because the
    /// approach ended <i>near</i> it on <i>some</i> heading and the berth wanted a specific pose.
    /// <b>Replaced by</b> <see cref="BerthingPilot"/>: an approach gate, a parallel come-alongside at the
    /// set rate, astern for the last of the way, and then the LINES — <c>MooringLineMath</c> — taking the
    /// last half-metre. The pose is produced or the phase holds; it is never written.</item>
    /// <item><c>HandOver()</c> wrote <c>_player.position = _stepAshore</c> — the passenger was put on the
    /// planks. <b>Replaced by</b> the owner's Q1 ruling (2026-08-26): <i>"the player is on the boat until
    /// they use the exit key to step onto dock."</i> She rides as long as she likes, presses E, and the
    /// step-ashore MOVE plays. There is no timer anywhere after the tie-up.</item>
    /// </list>
    ///
    /// <para><b>It fails by not happening.</b> Every input is checked and a missing one logs and hands
    /// the player their ordinary spawn. An opening that throws halfway through is worse than an opening
    /// that does not run: rule 10, leave a working build.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArrivalOpening : MonoBehaviour
    {
        /// <summary>
        /// The save flag that says this player has already been brought in. Lives here rather than in
        /// <c>World.OnboardingFlags</c> beside its siblings only because <c>HiddenHarbours.App</c> may
        /// not reference <c>HiddenHarbours.World</c> (rule 4) — it is written through
        /// <see cref="ISaveService.SetFlag"/> into the very same <c>OnboardingFlags</c> list, so the two
        /// are the same store under two names and a reader on either side sees it.
        /// </summary>
        public const string ArrivedFlagKey = "arrived_st_peters";

        /// <summary>The states, in the order they happen. Public so a PlayMode test can assert on the
        /// STATE rather than on a frame count (frames are not time). The finer pilotage machine sits
        /// underneath — see <see cref="Pilotage"/> for the mapping.</summary>
        public enum Phase
        {
            /// <summary>Not started, or decided against — the ordinary spawn.</summary>
            Dormant = 0,
            /// <summary>Under way down the route, the skipper at the helm (pilotage Passage/Approach).</summary>
            Approaching,
            /// <summary>Presenting at the gate and coming alongside (pilotage Gate/Alongside): square on
            /// the berth heading, closing at the set rate, way off astern.</summary>
            Docking,
            /// <summary>Tied up — <b>the lines hold her</b>. The player is still aboard, and stays aboard
            /// until she says otherwise.</summary>
            Moored,
            /// <summary>The player has the controls, ashore on the planks. Done.</summary>
            HandedOver,
        }

        [Header("Who brings you in")]
        [Tooltip("The skipper and his boat, as ONE asset — his hull, his paint and his own figure on the " +
                 "deck all come off it, exactly as the ambient fleets' do. Content, not code (rule 2).")]
        [SerializeField] private BoatOwnerDef _skipper;

        [Header("The route (authored by the region, on its own channel's line)")]
        [Tooltip("The run in, seaward FIRST. Pushed by the region builder off its dredged channel rather " +
                 "than typed here, so re-cutting the channel re-routes the arrival that uses it.")]
        [SerializeField] private Vector2[] _route = new Vector2[0];

        [Tooltip("Where she ends up lying, and on what compass heading — the berth alongside the wharf.")]
        [SerializeField] private Vector2 _berth;
        [SerializeField] private float _berthHeadingDegrees = 90f;

        [Tooltip("Where the player is put down when control is handed over — the dock planks. The " +
                 "region's own disembark point, pushed, never a second opinion about where the dock is.")]
        [SerializeField] private Vector2 _stepAshore;

        [Tooltip("The bed of the dredged channel she runs, m above chart datum — the region's own cut, " +
                 "pushed. ⚠ It is what stops her reading AGROUND for the whole passage: BoatController's " +
                 "grounding is a flat per-hull depth, and its 3 m default against a −2.2 m spring low " +
                 "leaves 0.8 m under a hull that draws 1.4.")]
        [SerializeField] private float _channelBedElevation = -4f;

        [Header("The skipper's hand on the helm")]
        [SerializeField] private ArrivalPilot.Settings _pilot = ArrivalPilot.Settings.Default;

        [Tooltip("The COME-ALONGSIDE (design/npc-pilotage.md §2.2): the approach gate, the set rate, the " +
                 "pose tolerances, and harbour speed inside the wharf line. The fairway cruise stays on " +
                 "the pilot above. ⚠ Read through OrDefault(): the committed scene was serialized before " +
                 "this field existed, so its key is absent — and a YAML-omitted struct deserialises to " +
                 "C# defaults, which here would be a set rate of nothing and a gate on top of the berth.")]
        [SerializeField] private BerthPilot.Settings _alongside = BerthPilot.Settings.Default;

        [Header("Riding in")]
        [Tooltip("Where the player stands on his deck while he brings her in, in metres from the hull's " +
                 "own centre, in HER frame (x across, y along, bow positive). Forward of amidships and " +
                 "off to one side, so she is not standing in the skipper's lap.")]
        [SerializeField] private Vector2 _passengerDeckOffset = new Vector2(-1.2f, 1.0f);

        [Tooltip("How long she rides the tied-up boat before the step ashore is OFFERED (real seconds). " +
                 "Short: a beat while the lines come up taut and she settles, not a cutscene. ⚠ It is no " +
                 "longer a countdown to anything — the owner ruled (Q1, 2026-08-26) that the player is " +
                 "aboard until SHE presses the exit key, so nothing after this beat happens on a timer.")]
        [Min(0f)] [SerializeField] private float _mooredBeatSeconds = 2.5f;

        [Tooltip("The fallback. Alongside, the pilot goes astern and she is tied up the moment she reads " +
                 "STOPPED IN HER BERTH POSE — but a boat held off her stop (her bow on the wharf's " +
                 "collider, a chop, a current) can sit a hair above the threshold forever, and the " +
                 "passenger sits with her. After this many seconds docking she is tied up regardless. " +
                 "⚠ Tied up WHERE SHE IS: there is no snap to the berth on this path either, so a " +
                 "fallback tie-up is visible in the pose rather than hidden by it. 0 disables it (then " +
                 "only an honest stop ties her up). Timed from the FIRST moment she came in to dock and " +
                 "never restarted, so a re-presented approach cannot renew its own grace.")]
        [Min(0f)] [SerializeField] private float _dockingSettleSeconds = 12f;

        [Header("Stepping ashore — the owner's Q1 ruling (2026-08-26)")]
        [Tooltip("How long the step off her rail onto the planks takes, real seconds. The shape and the " +
                 "number are ControlSwitcher's own disembark vault, because this IS that move — a hull " +
                 "the switcher does not own is the only reason it is spelled out again here.")]
        [Min(0.05f)] [SerializeField] private float _stepAshoreSeconds = 0.55f;

        [Tooltip("How high the step arcs over the gap between her rail and the planks, m. Zero is a " +
                 "slide; the arc is what makes it read as a step rather than a drift.")]
        [Min(0f)] [SerializeField] private float _stepAshoreHopMetres = 0.35f;

        [Header("What he says")]
        [Tooltip("The one line. The path and the line ARE the guidance — no quest, no marker (the " +
                 "diegetic-UI law). ⚠ Copy is the owner's to veto.")]
        [SerializeField] private string _skipperLine = "Ginny's up the path — she's expecting you.";

        [Header("Dev")]
        [Tooltip("Run the arrival even on a save that has already had one. Editor-only convenience for " +
                 "looking at the opening without wiping a save; never true in a build.")]
        [SerializeField] private bool _alwaysRunInEditor;

        /// <summary>The id the arrival's deck registers under in <c>StandableSurfaces</c>.</summary>
        private const string DeckSurfaceId = "boat.arrival_deck";

        /// <summary>The id the step ashore offers itself under in <c>Interactables</c> — unique among live
        /// registrants, in the <c>IMooringCleat.Id</c> convention the seam asks for.</summary>
        internal const string StepAshoreId = "arrival.step_ashore";

        /// <summary>
        /// Her beam as a fraction of her length — the ONE place the arrival guesses at a hull's width.
        ///
        /// <para>⚠ <b>Why a guess at all.</b> <c>BoatHullDef</c> carries <c>LengthMeters</c> and
        /// <c>DraughtMeters</c> and <b>no beam</b> (design/npc-pilotage.md §3's caveat says so and names
        /// the clean fix: a real <c>BeamMetres</c> on the def). For the cape islander this lands on
        /// 2.39 m against the region's own measured 2.40 — which is the check that says the ratio is a
        /// fair stand-in rather than a fudge. It is where her mooring cleat sits on her rail.</para>
        ///
        /// <para>⛔ <b>It is NOT her collider any more.</b> See <see cref="GreyboxHullCapsule"/>.</para>
        /// </summary>
        private const float HullBeamFraction = 0.37f;

        /// <summary>
        /// 🔴 <b>THE HULL CAPSULE EVERY BOAT IN THIS GAME CARRIES — and the arrival used to be the one
        /// exception, which is why she could not turn.</b>
        ///
        /// <para><c>PersistentCoreBuilder</c> gives the player's hull a fixed <b>1.7 × 4.0 m</b> capsule
        /// and <c>BoatController.SetHull</c> never resizes it — it re-derives her MASS from the
        /// displacement and nothing else. So a cape islander under the player's hand has a 1.7 × 4.0
        /// collider. This class used to size the arrival's collider to the hull's REAL dimensions
        /// (<c>LengthMeters × HullBeamFraction</c> = 4.77 × 12.9 m), which reads like the more honest
        /// choice and is a trap: <b>Unity derives a rigidbody's moment of inertia from its collider</b>,
        /// and inertia goes as the square of the dimensions.</para>
        ///
        /// <para><b>⚠ The measurement, because it is a factor of ten and nobody would believe it
        /// otherwise.</b> At <c>MassKg/100 = 60 kg</c>, full helm gives
        /// <c>RudderAuthority(5150) × RudderFeelScale(0.01) = 51.5 N·m</c> against
        /// <c>angularDamping = 2.5</c>, so her steady turn rate is <c>T / (I · d)</c>:</para>
        ///
        /// <list type="bullet">
        ///   <item>hull-sized capsule → <c>I ≈ 946</c> → <b>1.25 °/s</b> → a <b>177 m</b> turning radius
        ///   at cruise. She is a barge; she cannot round anything.</item>
        ///   <item>the shipping capsule → <c>I ≈ 94</c> → <b>12.5 °/s</b> → a <b>17.7 m</b> radius —
        ///   which is how this hull turns for the player.</item>
        /// </list>
        ///
        /// <para>⛔ <b>What that hid, and for how long.</b> St Peters' fairway turns 65° at its landfall
        /// mark and 67° back at the channel mouth; rounding those needs about 11 m of tangent, which the
        /// 27 m leg between them affords easily at 17.7 m and never at 177 m. So the arrival never
        /// navigated the fairway at all — she ran straight through both corners, passed the berth about
        /// 22 m off, took the way off, and was TELEPORTED onto her berth by the snap this slice deletes.
        /// The green test measured the teleport. Deleting the snap is what made her tell the truth.</para>
        ///
        /// <para>So she carries the same capsule as the boat the player is about to be handed, which is
        /// this class's own founding law — <i>"the opening is the first look anyone gets at how these
        /// boats move; it had better be how they move"</i> — stated in the one place it was not being
        /// kept.</para>
        /// </summary>
        private static readonly Vector2 GreyboxHullCapsule = new Vector2(1.7f, 4.0f);

        /// <summary>How high the arrival hull's deck rides over her own waterline, metres. A working
        /// boat's washboard — enough that a figure on it is clearly out of the water, which is the only
        /// thing this number decides here (nothing walks up to it; the passenger is placed).</summary>
        private const float DeckFreeboardMetres = 0.9f;

        // --- live state -------------------------------------------------------------------------------
        private Phase _phase = Phase.Dormant;
        private BoatController _boat;
        private Transform _boatRoot;
        private Transform _player;
        private PlayerWalkController _walk;
        private IsoCharacterSprite _skin;
        private BoatHullPresenterHost _presenterHost;
        private ArrivalDeck _deck;
        private Rigidbody2D _playerBody;
        private bool _playerWasSimulated = true;
        private bool _holding;
        private float _mooredTimer;
        private float _dockingTimer;
        private bool _subscribed;

        // --- the pilotage layer (S1) ------------------------------------------------------------------
        private BerthingPilot _pilotage;
        private HelmedBoat _helm;

        // --- her lines --------------------------------------------------------------------------------
        private BoatMooring _mooring;
        private SkipperCleat _boatCleat;
        private bool _linesFast;               // did WE make them fast?
        private bool _tiedUpHonestly;          // did the HULL get herself there, or the stopwatch?
        private bool _scopeEased;

        // --- the step ashore --------------------------------------------------------------------------
        private StepAshoreOffer _offer;
        private bool _stepping;
        private float _stepElapsed;
        private Vector3 _stepFrom;
        private CharacterClipPlayer _clipPlayer;
        private bool _clipResolved;
        private bool _clipPlaying;

        /// <summary>Which state the arrival is in — the thing a test asserts on.</summary>
        public Phase Current => _phase;

        /// <summary>
        /// Which PILOTAGE phase her skipper is in (design/npc-pilotage.md §2.1) — the finer machine
        /// underneath <see cref="Current"/>. The two are one mapping and not two opinions:
        /// <c>Passage</c>/<c>Approach</c> read as <see cref="Phase.Approaching"/>, <c>Gate</c>/
        /// <c>Alongside</c> as <see cref="Phase.Docking"/>, <c>Moored</c> as <see cref="Phase.Moored"/>.
        /// </summary>
        public PilotagePhase Pilotage => _pilotage != null ? _pilotage.Phase : PilotagePhase.Passage;

        /// <summary>The approach gate she presents from — one hull-length astern of the berth, a standoff
        /// off its line. Zero before she is spawned.</summary>
        public Vector2 ApproachGate => _pilotage != null ? _pilotage.GatePosition : Vector2.zero;

        /// <summary>How many times she has gone round and re-presented at the gate (§2.1's abort path).
        /// A number a failing test prints: an arrival that never converged looks very different from one
        /// that converged on the second attempt.</summary>
        public int Aborts => _pilotage != null ? _pilotage.Aborts : 0;

        /// <summary>⭐ <b>Is the skipper's line made fast?</b> This is what holds her alongside — the
        /// replacement for the snap, and the thing a test asserts instead of a written pose.</summary>
        public bool LinesAreFast => _mooring != null && _mooring.IsMadeFast;

        /// <summary>
        /// ⭐ <b>Did the HULL get herself there?</b> True only when the lines went over because
        /// <see cref="BerthingPilot.ReadyForLines"/> said yes — alongside, stopped, <i>and in the pose</i>
        /// — and false when the settle fallback tied her up where she happened to be lying.
        ///
        /// <para>This exists because the two paths are no longer distinguishable by their outcome. The
        /// snap used to end both of them, so "she is moored" meant nothing about how she got there; now
        /// the lines go over where she is either way, and a fallback tie-up shows up only as a worse pose.
        /// A test that asserts a pose is asserting the tolerance; a test that asserts THIS is asserting
        /// that the come-alongside converged — which is §2.2's actual claim.</para>
        /// </summary>
        public bool TiedUpHonestly => _tiedUpHonestly;

        /// <summary>True while the step-ashore move is in the air. She is neither aboard nor ashore.</summary>
        public bool IsSteppingAshore => _stepping;

        /// <summary>The hull she came in on, once spawned. Null before and after.</summary>
        public BoatController Boat => _boat;

        /// <summary>The line the skipper will say (or said). For the test that holds the copy against
        /// the region rather than against a literal in two places.</summary>
        public string SkipperLine => _skipperLine;

        /// <summary>The authored route, seaward first. Copy-free read for tests and tooling.</summary>
        public Vector2[] Route => _route;

        /// <summary>Wire the whole thing in one call — the region builder's seam, and the test's.</summary>
        public void Configure(BoatOwnerDef skipper, Vector2[] route, Vector2 berth,
                              float berthHeadingDegrees, Vector2 stepAshore,
                              float channelBedElevation)
        {
            _skipper = skipper;
            _route = route ?? new Vector2[0];
            _berth = berth;
            _berthHeadingDegrees = berthHeadingDegrees;
            _stepAshore = stepAshore;
            _channelBedElevation = channelBedElevation;
        }

        /// <summary>Tune the approach (the owner's pacing verdict lands here).</summary>
        public void ConfigurePilot(ArrivalPilot.Settings pilot) => _pilot = pilot;

        /// <summary>Tune the come-alongside — the gate, the set rate, the tolerances, harbour speed. The
        /// twin of <see cref="ConfigurePilot"/>, and there for the same two callers: the region builder,
        /// and a fixture that wants the SEQUENCE proved over twenty metres rather than over the region's
        /// hundred and fifty.</summary>
        public void ConfigureAlongside(BerthPilot.Settings alongside) => _alongside = alongside;

        // =================================================================================================
        //  the decision
        // =================================================================================================

        /// <summary>
        /// <b>Does this save get an arrival?</b> Pure and static, so the whole gating rule is one
        /// expression an EditMode test can drive through every combination without a scene, a save file
        /// or a boat — and so the mutual exclusion with the wake path is a thing you can read rather
        /// than a thing you have to trace.
        /// </summary>
        /// <param name="hasRestAnchor"><c>RestLocker.Anchor(save).IsSet</c> — ADR 0037. True means
        /// <see cref="RestWakeRestorer"/> is about to put this player where they slept, and the arrival
        /// must not also move them.</param>
        /// <param name="alreadyArrived">The <see cref="ArrivedFlagKey"/> flag — this player has been
        /// landed before, whether or not they have ever slept since.</param>
        public static bool ShouldPlay(bool hasRestAnchor, bool alreadyArrived) =>
            !hasRestAnchor && !alreadyArrived;

        private void OnEnable()
        {
            if (_subscribed) return;
            EventBus.Subscribe<ShellPhaseChanged>(OnShellPhase);
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (_subscribed) EventBus.Unsubscribe<ShellPhaseChanged>(OnShellPhase);
            _subscribed = false;
            // Never leave the player frozen, or a dead deck or a dead offer registered, because a region
            // unloaded mid-arrival. All three are global state that would outlive the scene that made
            // them — and each is torn down under the same law the player release keeps: everything here
            // is state only THIS component created, so relinquishing it can never stomp somebody else's.
            if (_deck != null) { StandableSurfaces.Unregister(_deck); _deck = null; }
            WithdrawTheStepAshore();
            StopTheStepClip();
            _stepping = false;
            ReleaseThePlayer();
        }

        /// <summary>
        /// The world has just been entered (New Game or Continue — <c>ShellFlow.EnterWorld</c> publishes
        /// this either way, which is why the decision is made HERE rather than on a New Game hook: one
        /// path, and the save itself says which kind of entry it was).
        /// </summary>
        private void OnShellPhase(ShellPhaseChanged e)
        {
            if (e.Phase != ShellPhase.Playing || _phase != Phase.Dormant) return;
            TryBegin();
        }

        /// <summary>Start the arrival if this save has one coming. Public so a PlayMode test can drive
        /// the sequence through its API — a virtual keypress is undeliverable, and an opening that can
        /// only be started by the shell can only be tested by booting the shell.</summary>
        public bool TryBegin()
        {
            var save = GameServices.Save;
            bool already = save != null && save.GetFlag(ArrivedFlagKey);

            // The anchor, off the live service — RestAnchor.None (and so IsSet false) when there is no
            // save at all, which is the greybox posture every sibling locker keeps and reads correctly
            // here as "nobody is being woken".
            RestAnchor anchor = RestLocker.Anchor();

            bool run = ShouldPlay(anchor.IsSet, already) || (_alwaysRunInEditor && Application.isEditor);
            if (!run)
            {
                Debug.Log("[ArrivalOpening] not landing this player — " +
                          (anchor.IsSet
                               ? $"they turned in at {anchor} and RestWakeRestorer is waking them there"
                               : $"they have made landfall before ({ArrivedFlagKey}), so the ordinary " +
                                 "spawn stands"));
                return false;
            }

            if (!Spawn()) return false;

            _phase = Phase.Approaching;
            _dockingTimer = 0f;
            Debug.Log($"[ArrivalOpening] making the approach — {_route.Length} marks, " +
                      $"{_pilotage.MetresToGate(_route[0]):F0} m to run to the gate at " +
                      $"({_pilotage.GatePosition.x:F1}, {_pilotage.GatePosition.y:F1}), and " +
                      $"{_pilotage.Berth.HullLengthMetres:F1} m of come-alongside after it.");
            return true;
        }

        // =================================================================================================
        //  the setup
        // =================================================================================================

        private bool Spawn()
        {
            if (_skipper == null || _skipper.Boat == null || _skipper.Boat.Visual == null)
            {
                Debug.LogError("[ArrivalOpening] no skipper Def (or she keeps no boat with art) — there " +
                               "is nobody to bring the player in, so the ordinary spawn stands. This is " +
                               "an authoring gap in Data/Boats/Owners, not a code one.");
                return false;
            }
            if (_route == null || _route.Length < 2)
            {
                Debug.LogError("[ArrivalOpening] the route needs at least a start and a berth; the " +
                               "region has authored " + (_route?.Length ?? 0) + ". Ordinary spawn.");
                return false;
            }

            _player = GameServices.PlayerTransform;
            if (_player == null)
            {
                Debug.LogError("[ArrivalOpening] no player transform is published — nobody to bring in.");
                return false;
            }
            _walk = _player.GetComponentInParent<PlayerWalkController>();

            // Her DRAWER — the one authority for which cell the fisher is shown in. Resolved the same
            // way and in the same place as her walking, because for the length of this passage the two
            // are the same problem: she is neither steering herself nor drawing herself.
            _skin = _player.GetComponentInParent<IsoCharacterSprite>();

            // Her root, built the way every other hull in a region is: the builder PLACES, the runtime
            // DRAWS. MooredBoat is the drawer — it skins her, stands her skipper on the deck through the
            // measured occupant slots, and installs the wave motion that keeps her on the same sea as
            // everything else. She is not moored yet; her heading is her inbound one, and the controller
            // below turns the root from here on, which both presentation paths read off transform.up.
            var go = new GameObject($"ArrivalBoat_{_skipper.Id}");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.position = new Vector3(_route[0].x, _route[0].y, 0f);
            go.SetActive(false);                                  // configure BEFORE anything wakes

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            var hull = go.AddComponent<CapsuleCollider2D>();
            hull.direction = CapsuleDirection2D.Vertical;
            hull.size = GreyboxHullCapsule;

            _boat = go.AddComponent<BoatController>();
            _boat.SetHull(_skipper.Boat);

            // 🔴 THE PASSENGER IS NOT AT THE HELM — and this hull now offers her none BY CONSTRUCTION.
            // She used to need a workaround here (a HelmControlRelay added disabled, pre-empting the one
            // BoatController installs) because the Core helm slot was last-writer-wins: her relay's
            // OnEnable simply took it, and the owner's 2026-08-21 playtest saw the result — the helm
            // card, wheel and gauges drawn for a boat somebody else was steering. The slot is arbitrated
            // by OCCUPANCY now (HelmSlot): her relay registers like every other hull's and is never
            // granted, because nobody has declared the passenger to be piloting her. Pinned by
            // ArrivalOpeningPlayTests.ThePassengerIsShownNoHelm_AndTheHelmSlotIsNotTaken, which asserts
            // exactly that — an ENABLED relay aboard her, and an empty slot.

            // ⚠ The hull's own idea of the water under her. Left at the component default (a flat 3 m)
            // she reads AGROUND for the whole passage at spring low — 3 − 2.2 = 0.8 m against a 1.4 m
            // draught — and drags herself in through the grounded-slowdown force for no reason. The
            // dredged channel she is actually running is cut to ApproachBedElevation, so its depth at
            // chart datum is exactly that, negated. Measured: aground=True for the entire spring-low
            // passage before this line existed.
            _boat.SetLocalSeabedDepth(-_channelBedElevation);

            // ⭐ THE LINES THAT WILL HOLD HER — the M2-38 mooring, which every hull already carries:
            // BoatController declares [RequireComponent(typeof(BoatMooring))], so adding the controller
            // above installed one. ⚠ FOUND, not added — a second AddComponent would put TWO on the hull
            // (BoatMooring is not [DisallowMultipleComponent]) and she would be moored twice, with two
            // ropes drawn and two tethers fighting. The AddComponent arm is only for a hull that somehow
            // arrived without one. It is INERT until MakeFast either way: BoatMooring's FixedUpdate
            // returns immediately while the state is Stowed, so a boat under way carries it for free.
            // This is the component that replaces the snap — see TieUp.
            _mooring = go.GetComponent<BoatMooring>();
            if (_mooring == null) _mooring = go.AddComponent<BoatMooring>();

            // ⭐ THE DECK SHE STANDS ON, registered where the whole game asks about it (see ArrivalDeck).
            _deck = new ArrivalDeck(_skipper.Boat.LengthMeters, DeckFreeboardMetres);
            StandableSurfaces.Register(_deck);

            float inbound = _route.Length > 1
                ? ArrivalPilot.CompassOf(_route[1] - _route[0])
                : _berthHeadingDegrees;

            // ⚠ SET HERE, not left to MooredBoat. It sets the same rotation when it draws her, but
            // it returns EARLY when the hull art has not baked — and where she points is not a
            // drawing decision. A boat pointed north because a mesh was missing runs out to sea.
            go.transform.rotation = Quaternion.Euler(0f, 0f, -inbound);
            go.AddComponent<MooredBoat>().Configure(_skipper, inbound);

            go.SetActive(true);
            _boatRoot = go.transform;

            // ⭐ THE PILOTAGE LAYER (design/npc-pilotage.md §2). The berth is a POSE, and every part of it
            // is DERIVED rather than authored twice: the region's berth and heading, the hull's own
            // length, and — the one that used to be missing — which side the WATER is on, read off the
            // ratified step-ashore point, which is on the planks by construction. Move the pier and the
            // side she presents from turns with it.
            var berth = BerthPilot.Berth.FromShorePoint(_berth, _berthHeadingDegrees, _stepAshore,
                                                        _skipper.Boat.LengthMeters);
            _helm = new HelmedBoat(_boat, _boatRoot);
            _pilotage = new BerthingPilot(_route, berth, _pilot, _alongside);

            // ⭐ SHE IS ALREADY RUNNING WHEN THE GAME OPENS, and that is both true and necessary.
            // True: a boat bringing you in has been at sea for an hour, not sitting stopped on the
            // horizon waiting for a new game. Necessary: this hull takes twenty seconds to reach
            // 63% of her speed, so a standing start would open the game on a minute of a boat
            // slowly gathering way. She enters at the cruise the pilot is about to hold her at.
            rb.linearVelocity = new Vector2(go.transform.up.x, go.transform.up.y)
                                * _pilot.CruiseSpeedMetresPerSecond;

            HoldThePlayer();
            return true;
        }

        // =================================================================================================
        //  the run
        // =================================================================================================

        /// <summary>
        /// ⭐ <b>ONE step of the phase machine, and this component no longer holds a control law at all.</b>
        /// It used to run <see cref="ArrivalPilot"/> itself with two arms — a route arm and a
        /// "docking = the same law asked for zero" arm — which was right as far as it went and could not
        /// reach a POSE: a berth has a heading and a side, and steering to it as a point is how she
        /// finished with her bow on the planks. <see cref="BerthingPilot"/> is that shape widened by the
        /// three additions §2.2 names, and it still runs <see cref="ArrivalPilot"/> underneath for
        /// everything the approach was already right about — including taking the last of the way off
        /// ASTERN, which on a hull with a twenty-second time constant is the only thing that stops her.
        /// </summary>
        private void FixedUpdate()
        {
            // ⚠ IsAlive, not `??` or a null-coalesce: a destroyed UnityEngine.Object is FAKE-null, which
            // the coalescing operators cannot see, and a region unloading under a passage destroys the
            // hull before this component's OnDisable gets to it.
            if (_pilotage == null || _helm == null || !_helm.IsAlive) return;
            if (_phase != Phase.Approaching && _phase != Phase.Docking) return;

            _pilotage.Step(_helm);
            FollowThePilotagePhase();
        }

        /// <summary>Keep the coarse sequence state in step with the pilot's own. One mapping, both ways —
        /// an aborted approach that falls back out of the gate reads as APPROACHING again, because that
        /// is what she is doing.</summary>
        private void FollowThePilotagePhase()
        {
            Phase want = _pilotage.Phase == PilotagePhase.Passage
                         || _pilotage.Phase == PilotagePhase.Approach
                             ? Phase.Approaching
                             : Phase.Docking;
            if (want == _phase) return;

            if (want == Phase.Docking)
                Debug.Log($"[ArrivalOpening] docking at {_boat.Velocity.magnitude:F2} m/s, " +
                          $"{Vector2.Distance(_boatRoot.position, _berth):F1} m off the berth — " +
                          "presenting at the gate.");
            _phase = want;
        }

        private void Update()
        {
            // ⭐ THE POSE IS STATED HERE, in Update, and NOT beside the seating in LateUpdate. The split
            // is not tidiness: IsoCharacterSprite consumes the holds in its own LateUpdate at execution
            // order 0 — the order this component also runs at — so which of the two LateUpdates runs
            // first is undefined, and a hold written there would be read a frame late about half the
            // time. DeckRiderVisual learned this on a turning hull and wrote the rule down: inputs
            // early, picture late.
            PoseThePassenger();

            if (_stepping) { TickStepAshore(); return; }
            if (_phase != Phase.Docking && _phase != Phase.Moored) return;

            if (_phase == Phase.Docking) { TickDocking(); return; }

            // MOORED. ⚠ Nothing after this beat happens on a clock. The owner ruled (Q1, 2026-08-26) that
            // "the player is on the boat until they use the exit key to step onto dock" — so the beat is
            // only how long she rides the tied-up boat before the step ashore is OFFERED, and the offer
            // then waits for her as long as she likes. There is no auto-handover to be found here, and
            // that is the whole of the ruling.
            _mooredTimer += Time.deltaTime;
            if (_offer == null && _mooredTimer >= _mooredBeatSeconds) OfferTheStepAshore();
        }

        /// <summary>
        /// She is coming alongside, taking the last of her way off astern. Wait for her to actually STOP
        /// <b>in her berth's pose</b> rather than for a stopwatch: a boat still carrying way has not
        /// finished arriving, and a stopwatch would tie her up mid-glide on a fast machine and leave her
        /// drifting past the wharf on a slow one.
        ///
        /// <para>⚠ The pose is part of the question now, and it was not before. The old test was
        /// "velocity under a threshold", full stop — which is true of a boat stopped anywhere, including
        /// a boat stopped ten metres off on the wrong heading, and the snap that followed made that
        /// indistinguishable from an arrival. <see cref="BerthingPilot.ReadyForLines"/> asks the whole
        /// question: alongside, stopped, and in the pose.</para>
        /// </summary>
        private void TickDocking()
        {
            // IsAlive, not a null-coalesce: a destroyed UnityEngine.Object is fake-null (see FixedUpdate).
            if (_pilotage == null || _helm == null || !_helm.IsAlive) return;

            if (_pilotage.ReadyForLines(_helm.Position, _helm.HeadingDegrees, _helm.Velocity))
            {
                TieUp(honest: true);
                return;
            }

            // 🔴 THE FALLBACK — measured on the owner's machine 2026-08-21, St Peters, bow-on berth:
            // "alongside … taking the last of it off astern" logged, and "tied up" never did. The stop
            // above is a RIGIDBODY reading, and a hull with her bow on the pier's collider is pushed back
            // a hair every physics step — never under 0.1 m/s, never moored, a passenger pinned aboard
            // with no exit by design. The time is a BOUND on waiting, not a replacement for the stop: a
            // boat that settles still ties up the moment she settles.
            //
            // ⚠ AND IT NO LONGER HIDES ANYTHING. It used to end in the same snap as the good path, so a
            // boat tied up by the stopwatch was indistinguishable on screen from one that had actually
            // arrived. Now the lines go over WHERE SHE IS: a fallback tie-up is visible in her pose, the
            // warning says so in metres and degrees, and the PlayMode pose assertions are what hold it
            // honest.
            _dockingTimer += Time.deltaTime;
            float bound = SettleBoundSeconds();
            if (bound > 0f && _dockingTimer >= bound)
            {
                Debug.LogWarning($"[ArrivalOpening] still making {_helm.Velocity.magnitude:F2} m/s " +
                                 $"{_dockingTimer:F1} s after coming in to dock, " +
                                 $"{Vector2.Distance(_helm.Position, _berth):F1} m off the berth and " +
                                 $"{ArrivalPilot.Wrap180(_helm.HeadingDegrees - _berthHeadingDegrees):F0}° " +
                                 $"off her heading ({_pilotage.Phase}, {_pilotage.Aborts} aborts) — " +
                                 "something is holding her off her stop. Tying up WHERE SHE IS.");
                TieUp(honest: false);
            }
        }

        /// <summary>
        /// ⚠ <b>The settle fallback's real bound: the owner's number, or the manoeuvre's own budget,
        /// whichever is LONGER.</b>
        ///
        /// <para>The serialized 12 s was measured against the old docking, which was "point at the berth
        /// and ask for zero" — a few seconds of astern. A come-alongside is a longer thing by
        /// construction: she runs the gate's capture range and then her own length at the berthing speed,
        /// closing the standoff at the set rate the whole way. At the shipped tuning that is about
        /// twenty-seven seconds, so a 12 s stopwatch would tie her up in the middle of the manoeuvre it
        /// is supposed to be a bound ON — and the fallback would become the normal path.</para>
        ///
        /// <para>So the bound is floored on the budget, and every term of the budget is a tunable rather
        /// than a number typed here (rule 6): the capture range and the hull's length at the berthing
        /// speed, the stop from that speed at the approach deceleration, and the standoff at the set rate.
        /// Raise <see cref="_dockingSettleSeconds"/> past it and the owner's number wins again, which is
        /// what a floor is for.</para>
        /// </summary>
        private float SettleBoundSeconds()
        {
            if (_dockingSettleSeconds <= 0f) return 0f;      // 0 still means "no fallback at all"
            if (_pilotage == null) return _dockingSettleSeconds;

            BerthPilot.Settings s = _pilotage.Alongside;
            float berthing = Mathf.Max(0.05f, s.BerthingSpeedMetresPerSecond);
            float budget = s.GateCaptureMetres / berthing                                   // in to the gate
                           + _pilotage.Berth.HullLengthMetres / berthing                    // her own length
                           + berthing / Mathf.Max(0.01f, _pilot.ApproachDecelMetresPerSecondSquared)
                           + s.GateStandoffMetres / Mathf.Max(0.01f, s.SetRateMetresPerSecond);
            return Mathf.Max(_dockingSettleSeconds, budget);
        }

        /// <summary>
        /// ⛔ <b>TIED UP — AND NOTHING HERE WRITES A POSE.</b> This method used to end with
        /// <c>_boatRoot.position = _berth</c> and <c>_boatRoot.rotation = −_berthHeadingDegrees</c>: the
        /// hull snapped to her berth because the approach ended <i>near</i> it on <i>some</i> heading and
        /// the berth wanted a specific one. That snap is the anti-goal design/npc-pilotage.md §0 names,
        /// and deleting it is this slice's whole point. The pose is produced by
        /// <see cref="BerthingPilot"/> — held heading, closed line, way off astern — or it is not
        /// produced, and then the log above says so in metres.
        ///
        /// <para>⭐ <b>What holds her instead: the LINES.</b> The heaving line goes over to the wharf's
        /// nearest bollard and <c>MooringLineMath</c> takes the last half-metre — the constraint the snap
        /// was faking, and one that M2-38 already shipped. She stays a DYNAMIC body on her lines rather
        /// than being frozen kinematic, because that is what the mooring is: <i>the sim keeps computing
        /// and the rope is a RESTRAINT on the result, never a freeze</i> (BoatMooring's own note, rule
        /// 5). Her engine is what stops — <see cref="BoatController.enabled"/> off — not her physics.</para>
        /// </summary>
        private void TieUp(bool honest)
        {
            _boat.Stop();
            _boat.enabled = false;               // the helm is dead; the sea and the rope are not

            MakeTheLinesFast();
            _pilotage.Moor(_helm);

            _tiedUpHonestly = honest;
            _phase = Phase.Moored;
            _mooredTimer = 0f;
            Debug.Log($"[ArrivalOpening] tied up at ({_helm.Position.x:F1}, {_helm.Position.y:F1}) " +
                      $"heading {_helm.HeadingDegrees:F0}° — the berth is ({_berth.x:F1}, {_berth.y:F1}) " +
                      $"on {_berthHeadingDegrees:F0}°, so she lies " +
                      $"{Vector2.Distance(_helm.Position, _berth):F2} m and " +
                      $"{Mathf.Abs(ArrivalPilot.Wrap180(_helm.HeadingDegrees - _berthHeadingDegrees)):F1}° " +
                      $"off it. Lines fast: {_linesFast}" + (honest ? "." : " (by the settle fallback)."));
        }

        // =================================================================================================
        //  her lines — the snap's honest replacement
        // =================================================================================================

        /// <summary>Her half-beam, metres — where a cleat on her rail stands out from her keel line. See
        /// <see cref="HullBeamFraction"/> for why this is a ratio and not a def field.</summary>
        private float HalfBeamMetres()
            => _skipper != null && _skipper.Boat != null
                   ? Mathf.Max(0.25f, _skipper.Boat.LengthMeters * HullBeamFraction * 0.5f)
                   : 1f;

        /// <summary>
        /// ⭐ <b>The line goes over.</b> One line, from a cleat on her inboard rail to the nearest bollard
        /// on the wharf she is lying at, with the scope that puts her ALONGSIDE.
        ///
        /// <para><b>⚠ Why the scope is measured at the BERTH and not at where she actually stopped.</b> A
        /// line made fast at the length she happens to be lying at holds that gap forever — it would
        /// merely freeze whatever the approach left, which is the snap again wearing rope. Measured at the
        /// berth pose it is SHORTER than the span she has, so the tether hauls her the last of the way in
        /// and holds her there. That is §2.2's "the last half-metre is the LINES, not the hull",
        /// arithmetically.</para>
        ///
        /// <para><b>⚠ Her end of the line is deliberately NOT registered in <c>MooringCleats</c>.</b>
        /// <c>MooredBoat.StripTheTieOffs</c> takes a stranger's boat OUT of that registry on purpose — a
        /// player must not be able to make her own painter fast to somebody else's hull — and publishing a
        /// cleat here would undo that ruling for the one boat the player is standing on. The skipper's
        /// own line needs two ends, not two registrations, and <see cref="BoatMooring.MakeFast"/> takes
        /// the handles directly.</para>
        ///
        /// <para>No shore cleat in reach is <b>data, not a fault</b>: she lies where her engine left her,
        /// the arrival still finishes, and the warning names the authoring gap (a wharf places its own
        /// <c>ShoreCleat</c>s) rather than blaming the pilot.</para>
        /// </summary>
        private void MakeTheLinesFast()
        {
            if (_mooring == null || _pilotage == null) return;

            BerthPilot.Berth berth = _pilotage.Berth;
            float reach = _pilotage.Alongside.LineReachMetres;
            if (!MooringCleats.TryFindNearestNow(_berth, CleatSide.Shore, reach, out IMooringCleat shore))
            {
                Debug.LogWarning($"[ArrivalOpening] no shore cleat within {reach:F0} m of the berth — " +
                                 "there is nothing to make fast to, so she simply lies where her engine " +
                                 "left her. That is an AUTHORING gap (a wharf places its own ShoreCleats) " +
                                 "rather than a pilot one, and the arrival still finishes.");
                return;
            }

            // Her cleat: on the rail she is tied ALONGSIDE by, amidships. The side is read off the berth
            // (which side the wharf is on), never assumed, and stored hull-local so the fitting swings
            // with her the way a fitting does.
            float halfBeam = HalfBeamMetres();
            float shoreSign =
                Vector2.Dot(-berth.Seaward, BerthPilot.Starboard(_berthHeadingDegrees)) >= 0f ? 1f : -1f;
            _boatCleat = new SkipperCleat(_boatRoot, new Vector2(shoreSign * halfBeam, 0f),
                                          DeckFreeboardMetres);

            Vector2 cleatAtTheBerth = berth.Position - berth.Seaward * halfBeam;
            float scope = MooringLineMath.Span(cleatAtTheBerth, _boatCleat.ElevationMeters,
                                               shore.WorldPosition, shore.ElevationMeters);

            _linesFast = _mooring.MakeFast(_boatCleat, shore, scope);
            if (!_linesFast)
            {
                Debug.LogWarning("[ArrivalOpening] the line would not go fast — she lies on her engine's " +
                                 "own work. (MakeFast refuses anything but a boat end and a shore end.)");
                return;
            }

            Debug.Log($"[ArrivalOpening] her line is fast to '{shore.Id}' on " +
                      $"{_mooring.ScopeMetres:F2} m of scope (asked for {scope:F2}, clamped to the " +
                      $"config's limits) — {_mooring.HorizontalReachMetres:F2} m of horizontal reach " +
                      $"against a " +
                      $"{_mooring.VerticalDropMetres:F2} m drop. From here the LINE holds her, not a " +
                      "written pose.");
        }

        /// <summary>
        /// ⭐ <b>And then the skipper eases his scope for the tide</b> — the second half of tying up, and
        /// the half that keeps this from being a boat lost to the ebb.
        ///
        /// <para>The line was made fast SHORT on purpose, so it would haul her alongside. A short line at
        /// a 5.35 m wharf on a 4.4 m tide is precisely M2-38's cozy failure: the water falls, the drop
        /// eats the whole scope, and the loop surrenders (<c>MooringLineMath.Slips</c>). So once she is up
        /// on her lines the scope is paid out to the shortest length that still reaches ACROSS at the
        /// LOWEST water this region's tide ever reaches — <c>√(worstDrop² + room²)</c>,
        /// <see cref="MooringLineMath.ScopeForFall"/> asked with no further fall to allow for.</para>
        ///
        /// <para>That is the whole of P1's lesson said by an NPC who knows it: <i>leave scope for the
        /// tide you expect</i>. The player will learn it the other way.</para>
        /// </summary>
        private void EaseTheScopeForTheTide()
        {
            if (_scopeEased || !_linesFast || _mooring == null || _boatCleat == null) return;
            IMooringCleat shore = _mooring.ShoreCleat;
            if (shore == null) return;

            float lowest = LowestWaterLevel();
            float worstDrop = MooringLineMath.VerticalDrop(
                MooringLineMath.BoatCleatElevation(lowest, DeckFreeboardMetres), shore.ElevationMeters);
            float room = Vector2.Distance(_boatCleat.WorldPosition, shore.WorldPosition);

            _scopeEased = true;
            float scope = _mooring.SetScope(MooringLineMath.ScopeForFall(worstDrop, 0f, room));
            Debug.Log($"[ArrivalOpening] scope eased to {scope:F2} m — enough that a fall to " +
                      $"{lowest:F2} m still leaves her {room:F2} m of reach across the water, so the ebb " +
                      "cannot hang her.");
        }

        /// <summary>The lowest water this region will ever be asked for: the tide profile's own spring
        /// low, or the level it is being held at right now if that is lower (a fixture may hold a tide
        /// its profile does not describe, and the line has to survive the sea it is actually in).</summary>
        private static float LowestWaterLevel()
        {
            IEnvironmentService env = GameServices.Environment;
            if (env == null) return 0f;
            IGameClock clock = GameServices.Clock;
            float now = env.WaterLevelAt(clock != null ? clock.TotalSeconds : 0.0);
            TideProfile tide = env.ActiveTideProfile;
            return Mathf.Min(now, tide.MeanLevel - Mathf.Abs(tide.Amplitude));
        }

        // 🔴 AND THERE IS DELIBERATELY NO ReleaseTheLines(). The "did I hold?" law — state you did not
        // create is not yours to overwrite — is why the player release, the deck registration, the step
        // ashore and the move clip are each guarded by a flag this component owns. The LINE needs no such
        // guard because it needs no release at all: both its ends and the hull between them are children
        // of this component's own transform, so a region unload takes them together. Casting off on
        // teardown would be the opposite of the law — it would set a boat adrift on the way out.

        // =================================================================================================
        //  the step ashore — E, and only E (the owner's Q1 ruling)
        // =================================================================================================

        /// <summary>True while the wharf is there to be stepped onto: she is tied up, the settling beat is
        /// done, and she is not already in the air.</summary>
        public bool CanStepAshore =>
            _phase == Phase.Moored && !_stepping && _mooredTimer >= _mooredBeatSeconds && _player != null;

        /// <summary>
        /// Offer the step ashore on the ONE interact verb (M2-39), rather than binding a key of its own.
        /// The candidate is the wharf: it sits at the region's ratified disembark point, reaches the whole
        /// length of the hull she is standing on (you can step off a boat lying alongside from anywhere on
        /// her deck), and answers in the words the popup shows.
        /// </summary>
        private void OfferTheStepAshore()
        {
            if (_offer != null) return;
            float deck = _skipper != null && _skipper.Boat != null ? _skipper.Boat.LengthMeters : 0f;
            _offer = new StepAshoreOffer(this, _stepAshore, Mathf.Max(4f, deck));
            Interactables.Register(_offer);
            Debug.Log("[ArrivalOpening] she is tied up and the planks are alongside — the step ashore is " +
                      "offered. She goes when she says so.");
            EaseTheScopeForTheTide();
        }

        /// <summary>Take the offer back off the registry (idempotent). A candidate that outlived its own
        /// actionability teaches the player a lie.</summary>
        private void WithdrawTheStepAshore()
        {
            if (_offer == null) return;
            Interactables.Unregister(_offer);
            _offer = null;
        }

        /// <summary>
        /// ⭐ <b>THE EXIT KEY.</b> Public so a PlayMode test can drive the verb rather than synthesise a
        /// key press — a virtual key is undeliverable to the New Input System from a test, and this is the
        /// same call <c>InteractVerb</c> makes when the player presses E on the candidate above.
        /// </summary>
        public bool StepAshore()
        {
            if (!CanStepAshore) return false;
            WithdrawTheStepAshore();
            _stepping = true;
            _stepElapsed = 0f;
            _stepFrom = _player.position;
            return true;
        }

        /// <summary>
        /// The move itself: off her rail, over the gap, onto the planks — eased out so she plants rather
        /// than arrives at speed, and lifted on a parabola that is zero at both ends so the arc joins her
        /// deck to the wharf with no step in position. The shape, the timing and the clip are
        /// <c>ControlSwitcher</c>'s own disembark vault; they are spelled out again only because that
        /// machine is bound to the player's OWN boat and this is a hull it does not own (see the class
        /// note on why an opening has no business reaching into the boarding state machine).
        ///
        /// <para>⚠ This is a MOVE and not a relocated teleport. The hand-over's
        /// <c>_player.position = _stepAshore</c> is gone; what puts her on the planks is an arc she starts
        /// herself, and the landing is where it ends.</para>
        /// </summary>
        private void TickStepAshore()
        {
            if (_player == null) { _stepping = false; StopTheStepClip(); HandOver(); return; }

            float seconds = Mathf.Max(0.05f, _stepAshoreSeconds);
            _stepElapsed += Time.deltaTime;
            float v = Mathf.Clamp01(_stepElapsed / seconds);

            var landing = new Vector3(_stepAshore.x, _stepAshore.y, _player.position.z);
            Vector3 pos = Vector3.Lerp(_stepFrom, landing, Mathf.SmoothStep(0f, 1f, v));
            pos.y += 4f * Mathf.Max(0f, _stepAshoreHopMetres) * v * (1f - v);
            _player.position = pos;

            PlayTheStepClip(ArrivalPilot.CompassOf(landing - _stepFrom), seconds);

            if (v < 1f) return;

            _stepping = false;
            StopTheStepClip();
            HandOver();
        }

        /// <summary>Ask for the disembark clip once — a character with no boarding art must not re-walk
        /// its sheet on every one of the arc's frames to be told "no" each time. Absence is data:
        /// <see cref="CharacterClipPlayer.Play"/> returns false and changes nothing, and the move is the
        /// arc either way.</summary>
        private void PlayTheStepClip(float headingDegrees, float seconds)
        {
            if (!_clipResolved)
            {
                _clipResolved = true;
                _clipPlayer = _player != null
                    ? _player.GetComponentInParent<CharacterClipPlayer>()
                    : null;
                if (_clipPlayer != null && _clipPlayer.Play(CharacterClip.BoardDown, headingDegrees,
                                                            seconds, holdOnFinish: true))
                    _clipPlaying = true;
                return;
            }

            if (_clipPlaying && _clipPlayer != null) _clipPlayer.SetHeading(headingDegrees);
        }

        /// <summary>Hand the renderer back (idempotent) — the held last frame has to come off, or she
        /// walks up the path mid-vault.</summary>
        private void StopTheStepClip()
        {
            _clipResolved = false;
            if (!_clipPlaying) return;
            _clipPlaying = false;
            if (_clipPlayer != null) _clipPlayer.Stop();
        }

        /// <summary>
        /// She has landed: the controls come back and the skipper says the one thing that points her up
        /// the path. No quest is opened and no marker is drawn — the path and the line ARE the guidance
        /// (the diegetic-UI law).
        ///
        /// <para>⛔ <b>The player teleport that used to open this method is DELETED.</b> It read
        /// <c>_player.position = _stepAshore</c>, and it is the second of design/npc-pilotage.md §0's two
        /// snaps: the passenger was put on the planks rather than walking there. She walks there now, on a
        /// press of her own, and this method is only what happens AFTER she lands. Nothing was
        /// relocated — there is no assignment of her position anywhere in the hand-over.</para>
        /// </summary>
        private void HandOver()
        {
            ReleaseThePlayer();

            var save = GameServices.Save;
            if (save != null)
            {
                save.SetFlag(ArrivedFlagKey, true);
                save.Save();                       // the arrival is not something to have to sit through twice
            }

            if (_deck != null) { StandableSurfaces.Unregister(_deck); _deck = null; }
            WithdrawTheStepAshore();

            _phase = Phase.HandedOver;
            EventBus.Publish(new ArrivalCompleted(_skipperLine, SkipperTransform(),
                                                 _skipper != null ? _skipper.DisplayName : null));
            Debug.Log($"[ArrivalOpening] ashore. The skipper: \"{_skipperLine}\"");
        }

        // =================================================================================================
        //  the passenger
        // =================================================================================================

        /// <summary>
        /// 🔴 <b>Take the passenger out of the physics world for the passage.</b> Disabling her input was
        /// not enough, and the shortfall is the defect the owner watched: she keeps a
        /// <see cref="Rigidbody2D"/> and a foot collider, and this component plants her INSIDE the hull's
        /// own capsule every frame. Two overlapping dynamic bodies are a contact the solver must resolve,
        /// so it shoves them apart, every fixed step, for the whole passage — a sustained impulse on a
        /// 60 kg boat from a passenger who is put straight back. The boat is thrown off her track and the
        /// passenger reads as pinned, which is exactly the pair of symptoms reported.
        ///
        /// <para><c>simulated = false</c> is the whole fix and it is the honest statement: while she is
        /// being carried she is cargo, not a body. It removes her from contacts, from queries and from
        /// the solver in one line, without touching her collider's authored size or her rigidbody's
        /// authored type — so putting her back is restoring one flag rather than reconstructing a
        /// state.</para>
        ///
        /// <para>And <see cref="ControlModeChanged"/> to <see cref="ControlMode.OnDeck"/>, because that
        /// is what the game already means by "she is standing on planking": <c>PlayerSubmergeVisual</c>
        /// gates the waterline on it and forces a dry body on a deck however deep the sea under the hull.
        /// The <c>ControlSwitcher</c>'s own mode is deliberately NOT touched — she is not aboard HER
        /// boat, and she must come off this one on foot.</para>
        /// </summary>
        private void HoldThePlayer()
        {
            if (_walk != null) _walk.enabled = false;

            _playerBody = _player != null ? _player.GetComponentInParent<Rigidbody2D>() : null;
            if (_playerBody != null)
            {
                _playerWasSimulated = _playerBody.simulated;
                _playerBody.linearVelocity = Vector2.zero;
                _playerBody.simulated = false;
            }

            _holding = true;

            // ⭐ THE PASSAGE IS FRAMED FOR THE BOAT SHE IS ON. Two Core signals, in the order the camera
            // has always wanted them: the hull's own authored framing first (the same
            // <see cref="ActiveBoatChanged"/> a helm-take sends, carrying the same three facts), then
            // the one bit that tells the camera to USE it for somebody who is only a passenger here.
            // Nothing about the arrival crosses the seam — a ferry or a tow would say exactly this.
            BoatHullDef hull = _skipper != null ? _skipper.Boat : null;
            if (hull != null)
                EventBus.Publish(new ActiveBoatChanged(hull.Id, hull.CameraWorldHeightMeters,
                                                       hull.LengthMeters));
            EventBus.Publish(new CarriedAboardChanged(true));
            EventBus.Publish(new ControlModeChanged(ControlMode.OnDeck));

            // Both halves of "she is aboard" on the very first frame: where she stands, and how she is
            // drawn standing there. The spawn happens inside TryBegin, which may run before this
            // component's first Update, and one frame of a sprinting passenger is one frame too many.
            SeatThePlayer();
            PoseThePassenger();
        }

        /// <summary>
        /// 🔴 <b>GIVE HER BACK — but only if this component is the one holding her.</b> The
        /// <see cref="_holding"/> guard is not defensive tidiness, it is the difference between a
        /// correct release and a stomped control mode on the standard play path. This component lives
        /// in the StPeters scene until the region UNLOADS, and the region unloads at the exact moment
        /// the player sails away — i.e. while she is <see cref="ControlMode.Aboard"/>. Without the
        /// guard, <see cref="OnDisable"/> would publish <see cref="ControlMode.OnFoot"/> over the top
        /// of that, and <c>PlayerSubmergeVisual</c> gates the waterline on the mode: she would read as
        /// a wading body while standing on her own deck at sea.
        ///
        /// <para>The mid-arrival unload — the case <see cref="OnDisable"/> is actually here for — still
        /// releases, because the flag is still set then. Everything this method touches is state only
        /// <see cref="HoldThePlayer"/> creates, so "did I hold?" is exactly the right question: an
        /// un-held release has nothing of its own to undo and no standing to speak about the mode.</para>
        /// </summary>
        private void ReleaseThePlayer()
        {
            if (!_holding) return;
            _holding = false;

            if (_walk != null) _walk.enabled = true;
            if (_playerBody != null) _playerBody.simulated = _playerWasSimulated;
            _playerBody = null;

            // Her own motion is honest again the moment she is standing on ground that does not move,
            // so the drawer takes both reads back. ReleaseHeading KEEPS the direction she was last
            // facing rather than snapping her north — she steps off looking where the boat was looking.
            if (_skin != null)
            {
                _skin.Stance = CharacterStance.Free;
                _skin.ReleaseHeading();
                _skin.ReleaseSpeed();
            }

            EventBus.Publish(new CarriedAboardChanged(false));
            EventBus.Publish(new ControlModeChanged(ControlMode.OnFoot));
        }

        /// <summary>Ride her deck — by POSITION, never by parenting. See the class note.
        ///
        /// <para>⚠ <b>The seating stands down while she is in the air.</b> The step ashore is a move that
        /// owns her position for its half-second, and a seat re-asserted every LateUpdate would drag her
        /// back onto the deck she is stepping off — the "something is putting her back where it wants her
        /// every frame" defect, re-created by the fix for it. The DECK still follows the hull: the
        /// registered surface is about where the planking is, not about who is standing on it.</para></summary>
        private void LateUpdate()
        {
            if (_phase != Phase.Approaching && _phase != Phase.Docking && _phase != Phase.Moored) return;
            FollowTheDeck();
            if (!_stepping) SeatThePlayer();
        }

        /// <summary>Keep the registered deck under her as she moves.</summary>
        private void FollowTheDeck()
        {
            if (_deck == null || _boatRoot == null) return;
            _deck.MoveTo(_boatRoot.position);
        }

        /// <summary>
        /// The figure the bubble hangs over. <see cref="MooredBoat"/> draws him under a child of the
        /// hull's VISUAL — not of the root — so this walks the tree by his own published name rather
        /// than assuming a depth. Falls back to the boat herself, which is a bubble over the right
        /// boat rather than no bubble at all.
        /// </summary>
        private Transform SkipperTransform()
        {
            if (_boatRoot == null) return null;
            foreach (Transform t in _boatRoot.GetComponentsInChildren<Transform>(true))
                if (t.name == MooredBoat.SkipperChildName) return t;
            return _boatRoot;
        }

        /// <summary>
        /// 🔴 <b>SHE IS BEING CARRIED, SO SHE IS NOT WALKING.</b> The defect the owner watched on his
        /// first sail in: the passenger played a walk cycle, on the spot, for the whole passage.
        ///
        /// <para>Nothing was lying. <see cref="IsoCharacterSprite"/> picks the fisher's cell by
        /// MEASURING her own step — which is the right reading in both frames she normally lives in,
        /// ashore and parented to a deck — and <see cref="SeatThePlayer"/> moves her by writing world
        /// position every LateUpdate. So a passenger standing perfectly still on a hull doing five
        /// knots was measured at five knots and drawn at a dead run. The drawer was asked the wrong
        /// question, and its own documentation says who is supposed to answer it: <i>"a character
        /// standing on something that MOVES has no honest motion of its own to read, so whoever owns
        /// that frame supplies both the facing and the speed."</i> For this passage that is this
        /// component, so it says both.</para>
        ///
        /// <para><b>Zero, stated, not measured.</b> Her honest travelling speed is metres of DECK per
        /// second, and a passenger who is placed rather than steered crosses no deck at all. The facing
        /// is the hull's — she is looking where the boat is going, which is the harbour she is arriving
        /// at — and it is the DRAWN heading where the hull publishes one, so the figure faces along the
        /// boat that is actually on screen rather than along a physics angle the sprite compass has
        /// quantised away (<c>DeckRiderVisual</c>'s rule, for <c>DeckRiderVisual</c>'s reason).</para>
        ///
        /// <para>And the <see cref="CharacterStance.Balance"/> brace, because that is what the game
        /// already means by standing on a working deck. A def with no brace art falls back to the free
        /// idle on its own, so this costs a boat whose skipper has no such sheets exactly nothing.</para>
        /// </summary>
        private void PoseThePassenger()
        {
            if (!_holding || _skin == null || _boatRoot == null) return;
            _skin.Stance = CharacterStance.Balance;
            _skin.HoldHeading(DrawnHeadingDegrees());
            _skin.HoldSpeed(0f);
        }

        /// <summary>
        /// The compass heading of the hull PICTURE she is standing on — the presenter's own drawn
        /// heading where one is installed (quantised on a sprite compass, continuous on a mesh hull),
        /// else the physics heading this class steers by. Resolved once and then only null-checked:
        /// <c>MooredBoat</c> installs the host when it skins her, which can be after the spawn.
        /// </summary>
        private float DrawnHeadingDegrees()
        {
            if (_presenterHost == null && _boatRoot != null)
                _presenterHost = _boatRoot.GetComponent<BoatHullPresenterHost>();

            IBoatHullPresenter hull = _presenterHost != null ? _presenterHost.Presenter : null;
            return hull != null ? hull.DrawnHeadingDegrees() : ArrivalPilot.HeadingOf(_boatRoot);
        }

        private void SeatThePlayer()
        {
            if (_player == null || _boatRoot == null) return;
            Vector3 offset = _boatRoot.rotation *
                             new Vector3(_passengerDeckOffset.x, _passengerDeckOffset.y, 0f);
            _player.position = new Vector3(_boatRoot.position.x + offset.x,
                                           _boatRoot.position.y + offset.y,
                                           _player.position.z);
        }
    }

    /// <summary>
    /// ⭐ <b>THE SKIPPER'S OWN END OF HIS OWN LINE</b> — one <see cref="IMooringCleat"/> on the arrival
    /// hull's inboard rail, amidships, so <c>MooringLineMath</c> has two ends to work between and the
    /// constraint the snap was faking becomes a real one.
    ///
    /// <para><b>⚠ It is NOT registered in <c>MooringCleats</c>, and that is the whole reason it exists
    /// rather than the hull's own <c>BoatCleats</c>.</b> <c>MooredBoat.StripTheTieOffs</c> deliberately
    /// takes a stranger's boat OUT of that global registry — <i>"a rope attached to a hull nothing
    /// simulates, on a wharf where the whole point of #451 is that the thing you can see is the thing you
    /// can use"</i> — and the arrival hull is the one stranger's boat the player is standing on. Putting a
    /// cleat back on the registry would let her make her own painter fast to the skipper's boat, which is
    /// exactly the ruling being kept. <see cref="BoatMooring.MakeFast"/> takes handles directly, so the
    /// skipper's line needs two ENDS, not two registrations.</para>
    ///
    /// <para><b>A live view, not a baked point.</b> Position is read fresh through the hull's rotation
    /// every time it is asked (a fitting swings with the boat) and elevation off the deterministic water
    /// level (she floats, so her fittings ride the tide while the wharf's do not) — the same split
    /// <see cref="MooringLineMath.BoatCleatElevation"/> exists to state.</para>
    /// </summary>
    internal sealed class SkipperCleat : IMooringCleat
    {
        private readonly Transform _hull;
        private readonly Vector2 _local;              // hull-local metres: +x starboard, +y bow
        private readonly float _heightAboveWaterline;

        public SkipperCleat(Transform hull, Vector2 localOffset, float heightAboveWaterline)
        {
            _hull = hull;
            _local = localOffset;
            _heightAboveWaterline = heightAboveWaterline;
        }

        /// <summary>One line, one boat, one arrival.</summary>
        public string Id => "boat.arrival.rail_cleat";

        /// <inheritdoc/>
        public CleatSide Side => CleatSide.Boat;

        /// <inheritdoc/>
        public Vector2 WorldPosition
        {
            get
            {
                if (_hull == null) return Vector2.zero;
                Vector3 offset = _hull.rotation * new Vector3(_local.x, _local.y, 0f);
                return new Vector2(_hull.position.x + offset.x, _hull.position.y + offset.y);
            }
        }

        /// <inheritdoc/>
        public float ElevationMeters
        {
            get
            {
                IEnvironmentService env = GameServices.Environment;
                IGameClock clock = GameServices.Clock;
                float water = env != null
                    ? env.WaterLevelAt(clock != null ? clock.TotalSeconds : 0.0)
                    : 0f;
                return MooringLineMath.BoatCleatElevation(water, _heightAboveWaterline);
            }
        }
    }

    /// <summary>
    /// ⭐ <b>THE WHARF, OFFERED</b> — the step ashore on the one interact verb (M2-39), so the passenger's
    /// exit is a candidate the resolver arbitrates rather than a key this opening binds for itself.
    ///
    /// <para>The owner's Q1 ruling (2026-08-26) is the whole of its behaviour: <i>"the player is on the
    /// boat until they use the exit key to step onto dock."</i> So the offer simply stands, indefinitely,
    /// from the moment she is tied up and settled, and nothing takes it back but the press.</para>
    ///
    /// <para><b>It reaches the whole deck</b>, which is the same rule <c>ControlSwitcher.CanStepAshore</c>
    /// already keeps for the player's own boat: you can step off a hull lying alongside from anywhere on
    /// her, not only from one plank. The reach is measured off the hull's own length, so a longer boat
    /// offers a longer deck rather than a number needing a re-edit.</para>
    ///
    /// <para><b>Both contexts, on purpose.</b> The arrival publishes <see cref="ControlMode.OnDeck"/> on
    /// the bus (that is what keeps her drawn dry on planking) but deliberately does NOT touch the
    /// <c>ControlSwitcher</c>'s own mode — she is not aboard HER boat. So the actor the resolver is handed
    /// may honestly report either, and a candidate that insisted on one of them would be unreachable
    /// exactly half the time.</para>
    /// </summary>
    internal sealed class StepAshoreOffer : IInteractable
    {
        private readonly ArrivalOpening _arrival;
        private readonly Vector2 _landing;
        private readonly float _reach;

        public StepAshoreOffer(ArrivalOpening arrival, Vector2 landing, float reachMetres)
        {
            _arrival = arrival;
            _landing = landing;
            _reach = reachMetres;
        }

        /// <inheritdoc/>
        public string Id => ArrivalOpening.StepAshoreId;

        /// <inheritdoc/>
        public string VerbLabel => "Step ashore";

        /// <inheritdoc/>
        public Vector2 WorldPosition => _landing;

        /// <inheritdoc/>
        public float ReachMeters => _reach;

        /// <inheritdoc/>
        public int Priority => InteractPriority.Fixture;

        /// <inheritdoc/>
        public InteractContext Contexts => InteractContext.OnFoot | InteractContext.OnDeck;

        /// <inheritdoc/>
        public bool RequiresFacing => false;

        /// <inheritdoc/>
        public bool IsAvailable => _arrival != null && _arrival.CanStepAshore;

        /// <inheritdoc/>
        public void Interact(in InteractActor actor)
        {
            if (_arrival != null) _arrival.StepAshore();
        }
    }

    /// <summary>
    /// <b>THE ARRIVAL BOAT'S DECK, as the on-foot sim sees it</b> — a moving
    /// <see cref="IStandableSurface"/>, so the passenger being carried reads as standing on planking
    /// rather than as standing on the seabed four metres under her.
    ///
    /// <para><b>Why this and not <c>World.FloatingPlatform</c>, which is exactly this for a dock.</b>
    /// <c>HiddenHarbours.App</c> may not reference <c>HiddenHarbours.World</c> (rule 4), and the
    /// interface it would be reached through is Core anyway — so the two-member contract is implemented
    /// here rather than the module boundary being widened for one ride. What is NOT duplicated is the
    /// float's real content: going aground on the ebb, the gangway seam, the cleats. A boat under way
    /// has none of those, and a copy of them would be three behaviours nobody drives.</para>
    ///
    /// <para><b>Her deck height is the WATER's, not the seabed's</b> — the same law #594 established for
    /// the harbour floats, and the reason a fixed platform would be wrong here: the tide swings 4.4 m at
    /// St Peters, so a deck pinned to a number puts the passenger in the air at low water and under it
    /// at high.</para>
    ///
    /// <para>A SQUARE envelope about her centre rather than her rotated oblong: the contract's footprint
    /// is an axis-aligned <see cref="Rect"/>, and the envelope errs the forgiving way — a passenger a
    /// foot over the side still reads as aboard rather than as suddenly swimming. Nothing walks to this
    /// edge (the passenger is placed, not steered), so the slack costs nothing.</para>
    /// </summary>
    internal sealed class ArrivalDeck : IStandableSurface
    {
        private readonly float _halfLength;
        private readonly float _freeboardMetres;
        private Rect _footprint;

        public ArrivalDeck(float hullLengthMetres, float freeboardMetres)
        {
            _halfLength = Mathf.Max(0.5f, hullLengthMetres * 0.5f);
            _freeboardMetres = freeboardMetres;
        }

        /// <summary>Her id in the registry — one deck, one arrival.</summary>
        public string Id => "boat.arrival_deck";

        /// <summary>Where she is now (for tests and tooling).</summary>
        public Rect Footprint => _footprint;

        /// <summary>Put the deck under the hull's current position.</summary>
        public void MoveTo(Vector2 centre) =>
            _footprint = new Rect(centre.x - _halfLength, centre.y - _halfLength,
                                  _halfLength * 2f, _halfLength * 2f);

        /// <inheritdoc/>
        public bool TryGetDeckElevation(Vector2 worldPos, out float deckElevation)
        {
            deckElevation = WaterLevelNow() + _freeboardMetres;
            return _footprint.Contains(worldPos);
        }

        /// <summary>The deterministic water level right now, off the same Core services the water render
        /// and the wade model read — never a second tide.</summary>
        private static float WaterLevelNow()
        {
            var env = GameServices.Environment;
            if (env == null) return 0f;
            var clock = GameServices.Clock;
            return env.WaterLevelAt(clock != null ? clock.TotalSeconds : 0d);
        }
    }
}
