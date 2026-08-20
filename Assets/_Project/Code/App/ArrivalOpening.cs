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
        /// STATE rather than on a frame count (frames are not time).</summary>
        public enum Phase
        {
            /// <summary>Not started, or decided against — the ordinary spawn.</summary>
            Dormant = 0,
            /// <summary>Under way down the route, the skipper at the helm.</summary>
            Approaching,
            /// <summary>Alongside: way off, lines away, coming to rest.</summary>
            Docking,
            /// <summary>Tied up. The skipper has said his piece.</summary>
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

        [Header("Riding in")]
        [Tooltip("Where the player stands on his deck while he brings her in, in metres from the hull's " +
                 "own centre, in HER frame (x across, y along, bow positive). Forward of amidships and " +
                 "off to one side, so she is not standing in the skipper's lap.")]
        [SerializeField] private Vector2 _passengerDeckOffset = new Vector2(-1.2f, 1.0f);

        [Tooltip("How long she stands on the planks with the boat tied up before the line is spoken and " +
                 "control comes back (real seconds). Short: this is a beat, not a cutscene.")]
        [Min(0f)] [SerializeField] private float _mooredBeatSeconds = 2.5f;

        [Header("What he says")]
        [Tooltip("The one line. The path and the line ARE the guidance — no quest, no marker (the " +
                 "diegetic-UI law). ⚠ Copy is the owner's to veto.")]
        [SerializeField] private string _skipperLine = "Ginny's up the path — she's expecting you.";

        [Header("Dev")]
        [Tooltip("Run the arrival even on a save that has already had one. Editor-only convenience for " +
                 "looking at the opening without wiping a save; never true in a build.")]
        [SerializeField] private bool _alwaysRunInEditor;

        /// <summary>She is STOPPED below this speed, in (m/s)². 0.1 m/s — slower than a walk, and well
        /// inside the drift the mooring lines would take up anyway.</summary>
        private const float StoppedSpeedSquared = 0.01f;

        /// <summary>Inside this range of the berth (m) she is COMMITTED, and the closest-approach arm of
        /// <see cref="Alongside"/> starts watching. Three boat-lengths of the arrival hull — far enough
        /// out that a wide pass still latches, close enough in that the turn onto the channel does
        /// not.</summary>
        private const float CommittedRangeMetres = 40f;

        /// <summary>How far the range must OPEN again before the closest approach is called (m). A metre
        /// of hysteresis, so a boat holding station in a chop does not latch on measurement noise.</summary>
        private const float RangeOpeningMetres = 1f;

        /// <summary>The id the arrival's deck registers under in <c>StandableSurfaces</c>.</summary>
        private const string DeckSurfaceId = "boat.arrival_deck";

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
        private int _leg;
        private float _closestSoFar = float.MaxValue;
        private float _mooredTimer;
        private bool _subscribed;

        /// <summary>Which state the arrival is in — the thing a test asserts on.</summary>
        public Phase Current => _phase;

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
            // Never leave the player frozen, or a dead deck registered, because a region unloaded
            // mid-arrival. Both are global state that would outlive the scene that made them.
            if (_deck != null) { StandableSurfaces.Unregister(_deck); _deck = null; }
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

            _leg = 0;
            _closestSoFar = float.MaxValue;
            _phase = Phase.Approaching;
            Debug.Log($"[ArrivalOpening] making the approach — {_route.Length} marks, " +
                      $"{ArrivalPilot.MetresToBerth(_route[0], _route, 0):F0} m to run.");
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
            hull.size = new Vector2(Mathf.Max(1f, _skipper.Boat.LengthMeters * 0.37f),
                                    Mathf.Max(1f, _skipper.Boat.LengthMeters));

            _boat = go.AddComponent<BoatController>();
            _boat.SetHull(_skipper.Boat);

            // ⚠ The hull's own idea of the water under her. Left at the component default (a flat 3 m)
            // she reads AGROUND for the whole passage at spring low — 3 − 2.2 = 0.8 m against a 1.4 m
            // draught — and drags herself in through the grounded-slowdown force for no reason. The
            // dredged channel she is actually running is cut to ApproachBedElevation, so its depth at
            // chart datum is exactly that, negated. Measured: aground=True for the entire spring-low
            // passage before this line existed.
            _boat.SetLocalSeabedDepth(-_channelBedElevation);

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

        private void FixedUpdate()
        {
            if (_boat == null || _boatRoot == null) return;
            if (_phase != Phase.Approaching && _phase != Phase.Docking) return;

            Vector2 here = _boatRoot.position;

            // Advance the mark when she is inside its radius — every mark but the last, which is the
            // berth and is where she STOPS rather than turns.
            while (_leg < _route.Length - 1 &&
                   Vector2.Distance(here, _route[_leg]) <= _pilot.ArriveRadiusMetres)
                _leg++;

            // ⭐ ONE control law all the way in, and DOCKING is not an exception to it — it is the same
            // law asked for zero. The tempting shape is "alongside → helm amidships, let her run out of
            // way", and it does not work on this hull: her time constant is twenty seconds, so a boat
            // set adrift at half a knot is still doing half a knot half a minute later, and the arrival
            // hangs a few metres off the wharf forever. A skipper takes the last of the way off ASTERN.
            // Asking the pilot for a target speed of zero is exactly that, in the units it already
            // speaks.
            ArrivalPilot.Helm helm = _phase == Phase.Approaching
                ? ArrivalPilot.Command(here, ArrivalPilot.HeadingOf(_boatRoot), _boat.Velocity,
                                       _route[_leg],
                                       ArrivalPilot.MetresToBerth(here, _route, _leg), _pilot)
                : ArrivalPilot.Command(here, ArrivalPilot.HeadingOf(_boatRoot), _boat.Velocity,
                                       _berth, 0f, _pilot);
            _boat.SetControl(helm.Throttle, helm.Steer);

            if (_phase == Phase.Approaching && Alongside(here)) BeginDocking();
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

            if (_phase != Phase.Docking && _phase != Phase.Moored) return;

            if (_phase == Phase.Docking)
            {
                // She is alongside taking the last of her way off astern. Wait for her to actually STOP
                // rather than for a stopwatch: a boat still carrying way has not finished arriving, and
                // a stopwatch would tie her up mid-glide on a fast machine and leave her drifting past
                // the wharf on a slow one. Read through the controller's own Velocity, which is the
                // number the grounding and the wake already read.
                if (_boat.Velocity.sqrMagnitude < StoppedSpeedSquared) TieUp();
                return;
            }

            _mooredTimer += Time.deltaTime;
            if (_mooredTimer >= _mooredBeatSeconds) HandOver();
        }

        /// <summary>
        /// 🔴 <b>Is she alongside?</b> Inside the arrive radius — <i>or past her closest approach to it</i>,
        /// which is the arm that keeps a docking from becoming an ORBIT.
        ///
        /// <para>A radius alone assumes she can always be steered inside it. She cannot: her turning
        /// circle at approach speed is wider than four metres, so a track that misses by five and a half
        /// sails on, comes round, and misses again — forever. Measured on the real fairway: closest
        /// approach 5.5 m, then a 50 m loop south, helm hard over the whole way. The boat was doing
        /// everything it was told; what it was told had no way to end.</para>
        ///
        /// <para>So the second arm is the one a skipper actually uses: <b>the range stopped
        /// shortening</b>. Once she is inside a few boat-lengths and the distance to the berth starts
        /// opening again, this is as close as this pass gets — take the way off here and let her settle
        /// rather than going round for another try. <see cref="_closestSoFar"/> is reset on every entry
        /// so a re-run cannot inherit a stale minimum.</para>
        /// </summary>
        private bool Alongside(Vector2 here)
        {
            float range = Vector2.Distance(here, _berth);
            if (range <= _pilot.ArriveRadiusMetres) return true;

            // Only once she is committed — otherwise the first metre of the passage, where the range to a
            // berth 150 m away momentarily grows on the turn, would read as "as close as she gets".
            if (range > CommittedRangeMetres) { _closestSoFar = float.MaxValue; return false; }

            if (range < _closestSoFar) { _closestSoFar = range; return false; }
            return range > _closestSoFar + RangeOpeningMetres;
        }

        /// <summary>Alongside: from here the pilot is asked for a standstill, which means astern.</summary>
        private void BeginDocking()
        {
            _phase = Phase.Docking;
            Debug.Log($"[ArrivalOpening] alongside at {_boat.Velocity.magnitude:F2} m/s, " +
                      $"{Vector2.Distance(_boatRoot.position, _berth):F1} m off the berth — taking the " +
                      "last of it off astern.");
        }

        /// <summary>
        /// Tied up. The lines go over (the toss is the skipper's own clip on his rig), she settles onto
        /// the berth heading, and the helm goes dead — she is a moored boat from here, which is exactly
        /// the state <see cref="MooredBoat"/> was drawing her in all along.
        /// </summary>
        private void TieUp()
        {
            _boat.Stop();
            _boat.enabled = false;

            var rb = _boatRoot.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;   // tied up: the sea moves her, her engine does not
            }
            _boatRoot.position = new Vector3(_berth.x, _berth.y, _boatRoot.position.z);
            _boatRoot.rotation = Quaternion.Euler(0f, 0f, -_berthHeadingDegrees);

            _phase = Phase.Moored;
            _mooredTimer = 0f;
            Debug.Log($"[ArrivalOpening] tied up at ({_berth.x:F1}, {_berth.y:F1}), heading " +
                      $"{_berthHeadingDegrees:F0}°.");
        }

        /// <summary>
        /// The player gets the controls back, standing on the planks, and the skipper says the one thing
        /// that points her up the path. No quest is opened and no marker is drawn: the path and the line
        /// ARE the guidance (the diegetic-UI law).
        /// </summary>
        private void HandOver()
        {
            ReleaseThePlayer();
            if (_player != null)
                _player.position = new Vector3(_stepAshore.x, _stepAshore.y, _player.position.z);

            var save = GameServices.Save;
            if (save != null)
            {
                save.SetFlag(ArrivedFlagKey, true);
                save.Save();                       // the arrival is not something to have to sit through twice
            }

            if (_deck != null) { StandableSurfaces.Unregister(_deck); _deck = null; }

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

        /// <summary>Ride her deck — by POSITION, never by parenting. See the class note.</summary>
        private void LateUpdate()
        {
            if (_phase != Phase.Approaching && _phase != Phase.Docking && _phase != Phase.Moored) return;
            FollowTheDeck();
            SeatThePlayer();
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
