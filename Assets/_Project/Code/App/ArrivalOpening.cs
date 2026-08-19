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

        // --- live state -------------------------------------------------------------------------------
        private Phase _phase = Phase.Dormant;
        private BoatController _boat;
        private Transform _boatRoot;
        private Transform _player;
        private PlayerWalkController _walk;
        private int _leg;
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
                              float berthHeadingDegrees, Vector2 stepAshore)
        {
            _skipper = skipper;
            _route = route ?? new Vector2[0];
            _berth = berth;
            _berthHeadingDegrees = berthHeadingDegrees;
            _stepAshore = stepAshore;
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
            // Never leave the player frozen because a region unloaded mid-arrival.
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

            // Her WAY — speed along the bow, signed — is what the throttle closes its loop on.
            float way = Vector2.Dot(_boat.Velocity, new Vector2(_boatRoot.up.x, _boatRoot.up.y));

            // ⭐ ONE control law all the way in, and DOCKING is not an exception to it — it is the same
            // law asked for zero. The tempting shape is "alongside → helm amidships, let her run out of
            // way", and it does not work on this hull: her time constant is twenty seconds, so a boat
            // set adrift at half a knot is still doing half a knot half a minute later, and the arrival
            // hangs a few metres off the wharf forever. A skipper takes the last of the way off ASTERN.
            // Asking the pilot for a target speed of zero is exactly that, in the units it already
            // speaks.
            ArrivalPilot.Helm helm = _phase == Phase.Approaching
                ? ArrivalPilot.Command(here, ArrivalPilot.HeadingOf(_boatRoot), way, _route[_leg],
                                       ArrivalPilot.MetresToBerth(here, _route, _leg), _pilot)
                : ArrivalPilot.Command(here, ArrivalPilot.HeadingOf(_boatRoot), way, _berth,
                                       0f, _pilot);
            _boat.SetControl(helm.Throttle, helm.Steer);

            if (_phase == Phase.Approaching &&
                Vector2.Distance(here, _berth) <= _pilot.ArriveRadiusMetres) BeginDocking();
        }

        private void Update()
        {
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

        /// <summary>Alongside: from here the pilot is asked for a standstill, which means astern.</summary>
        private void BeginDocking()
        {
            _phase = Phase.Docking;
            Debug.Log($"[ArrivalOpening] alongside at {_boat.Velocity.magnitude:F2} m/s — " +
                      "taking the last of it off astern.");
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

            _phase = Phase.HandedOver;
            EventBus.Publish(new ArrivalCompleted(_skipperLine, SkipperTransform(),
                                                 _skipper != null ? _skipper.DisplayName : null));
            Debug.Log($"[ArrivalOpening] ashore. The skipper: \"{_skipperLine}\"");
        }

        // =================================================================================================
        //  the passenger
        // =================================================================================================

        private void HoldThePlayer()
        {
            if (_walk != null) _walk.enabled = false;
            SeatThePlayer();
        }

        private void ReleaseThePlayer()
        {
            if (_walk != null) _walk.enabled = true;
        }

        /// <summary>Ride her deck — by POSITION, never by parenting. See the class note.</summary>
        private void LateUpdate()
        {
            if (_phase == Phase.Approaching || _phase == Phase.Docking || _phase == Phase.Moored)
                SeatThePlayer();
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

}
