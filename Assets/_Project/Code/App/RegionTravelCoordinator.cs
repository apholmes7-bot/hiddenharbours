using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;
using HiddenHarbours.World;

namespace HiddenHarbours.App
{
    /// <summary>
    /// The persistent-core half of VS-22 travel: it carries the player/boat/hold/wallet across an additive
    /// region hop and re-binds them to whichever region just became active. Lives on the persistent core
    /// (DontDestroyOnLoad) and reacts to <see cref="SceneManager.activeSceneChanged"/> (raised by the
    /// <c>RegionSceneLoader</c> when it loads or re-activates a region): it deactivates the region we left,
    /// activates the one we arrived in, silences the arrived region's stray camera/listener (the core owns
    /// those), points the hold proxies at the live hold, and repositions the rig + re-points the control
    /// switcher's dock to the new region's <see cref="RegionAnchor"/>.
    ///
    /// PRAGMATIC SLICE — flagged for lead-architect. Region scenes are TOGGLED (their roots SetActive),
    /// not unloaded/reloaded, so nothing duplicates the persistent core and no scene re-runs its Awakes;
    /// the loader re-activates an already-loaded region instead of loading a second copy. The reposition +
    /// dock re-point is a pure static helper (<see cref="ApplyArrival"/>) so it's unit-testable without
    /// scene loading; the toggling/camera glue is play-mode only.
    /// </summary>
    public sealed class RegionTravelCoordinator : MonoBehaviour, IWorldWhereabouts
    {
        [Tooltip("The persistent player (repositioned to a region's disembark point on arrival).")]
        [SerializeField] private Transform _player;
        [Tooltip("The persistent boat (repositioned to a region's arrival point on arrival).")]
        [SerializeField] private Transform _boat;
        [Tooltip("The persistent control switcher (its dock zone is re-pointed to the arrived region).")]
        [SerializeField] private ControlSwitcher _switcher;
        [Tooltip("The persistent hold (injected into each region's wharf hold proxy so selling hits the real hold).")]
        [SerializeField] private ShipHold _hold;

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            PublishPlayer();
            GameServices.Whereabouts = this;
            EnsureWorldFrame();
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            // A rig switched off mid-fade must never leave the screen black: drop the fade and clear the cover.
            _seamFade.Reset();
            GameServices.SeamCoverAlpha = 0f;
        }

        /// <summary>
        /// Step the seam fade and hand its alpha to the one screen fade (<see cref="GameServices.SeamCoverAlpha"/>).
        /// Scaled time, so a paused world pauses the fade with the mover. The arrival frame is not stepped
        /// (it renders black), and the fade drops the step after it, which carries the hop frame's cost.
        /// </summary>
        private void Update()
        {
            _seamFade.Configure(GameServices.RegionSeamFadeOutSeconds, GameServices.RegionSeamFadeInSeconds,
                                GameServices.RegionSeamMaxBlackSeconds);
            if (Time.frameCount != _seamArrivedFrame) _seamFade.Tick(Time.deltaTime);
            GameServices.SeamCoverAlpha = _seamFade.Alpha;
        }

        /// <summary>
        /// Take the player registration back — <b>on DESTROY, deliberately not on disable</b>, which is
        /// the lifetime hook <c>GameRoot.OnDestroy</c> uses for exactly the same "whoever registers,
        /// unregisters" symmetry.
        ///
        /// <para><b>Why the distinction is load-bearing rather than stylistic.</b> Being disabled is not
        /// the same as being gone, and this component in particular gets switched off by things that
        /// are not teardown — it is a root object like any other, and root toggling is how a region hop
        /// works. Clearing on disable made a coordinator that was momentarily deactivated wipe a
        /// registration that was still true, and the symptom was every door in the game silently
        /// sealing: the interiors fall back to this relay, so a null here is indistinguishable from "no
        /// player exists". Caught by <c>InteriorTravelPlayTests</c>; pinned by
        /// <c>ADisabledCoordinator_KeepsTheRelay</c>.</para>
        ///
        /// <para>Clear only if we STILL OWN it: a shell restart stands a second core up before the old
        /// one finishes tearing down, and that new core has already published its own player, so an
        /// unconditional clear would wipe a live registration on the way out.</para>
        ///
        /// <para><b>⚠ `==` here, NOT the `ReferenceEquals` GameRoot uses</b> — because unlike those
        /// slots, this accessor LAUNDERS fake-null. Read back on a destroyed player it returns a real
        /// null, so `ReferenceEquals` would be false against our own dead registration and we would
        /// never clean it up; Unity's overload sees through that. Its one divergence (two different
        /// destroyed transforms comparing equal) is harmless — a destroyed registration already reads
        /// as null to every consumer, so there is nothing there to protect.</para>
        /// </summary>
        private void OnDestroy()
        {
            if (GameServices.PlayerTransform == _player) GameServices.PlayerTransform = null;
            if (ReferenceEquals(GameServices.Whereabouts, this)) GameServices.Whereabouts = null;
            // A neighbour this core warmed and never crossed into dies with it (PR 1223): left parked, it outlived
            // the core that loaded it and read as "loaded" to everything after (the next test's whole session).
            if (!_quitting) ReleaseWarmNeighbours();
        }

        bool _quitting;

        private void OnApplicationQuit() => _quitting = true;   // the engine unloads everything itself

        /// <summary>
        /// Publish the persistent player on the Core relay (<see cref="GameServices.PlayerTransform"/>),
        /// so REGION content can find the person walking the world without referencing App (rule 4).
        ///
        /// <para><b>Published once, on enable — not per arrival — and that is the whole point.</b> The
        /// player this rig carries is <c>DontDestroyOnLoad</c>: it is the SAME transform before, during
        /// and after every hop, so there is nothing for a travel event to re-bind. What actually changes
        /// across a hop is which region's content is asking, and that content resolves the relay itself
        /// when it needs it. One publication at boot therefore covers boot, every crossing, and every
        /// return trip.</para>
        /// </summary>
        private void PublishPlayer()
        {
            // ⚠️ Explicit `!= null` (UnityEngine.Object's overload), never `??`: publishing a destroyed
            // transform would hand every consumer a fake-null that their own null checks would pass.
            if (_player != null) GameServices.PlayerTransform = _player;
        }

        // ---- pure logic (unit-testable) -----------------------------------------------------

        /// <summary>
        /// Re-bind the persistent rig to a region: park the boat at the region's arrival point, place the
        /// (hidden, aboard) player at its disembark spot, re-point the control switcher's dock, and
        /// RE-ASSERT the control mode — WITHOUT resetting which mode you're in (you arrive still aboard /
        /// still on foot). Null-safe throughout.
        ///
        /// <para>The re-assert (<see cref="ControlSwitcher.ReassertControlMode"/>) is the fix for "boat
        /// controls stop after returning to a scene": the persistent switcher carries the mode across the
        /// hop, but the scene toggle never re-enabled the active boat's controller + input to match it, so
        /// a re-activated region (especially on a RETURN trip) could leave the helm dead. Re-asserting on
        /// every arrival makes boat OR foot control reliably live after EVERY return, for both the rowed
        /// Dory and the engine Punt; it also re-raises the camera signals so the view follows the right
        /// target. Idempotent.</para>
        /// </summary>
        public static void ApplyArrival(Transform player, Transform boat, ControlSwitcher switcher, RegionAnchor anchor)
            => ApplyArrival(player, boat, switcher, anchor, null);

        /// <summary>
        /// <inheritdoc cref="ApplyArrival(Transform, Transform, ControlSwitcher, RegionAnchor)"/>
        ///
        /// <para><paramref name="arrivalKey"/> names WHICH WAY IN the player took — the key the
        /// <c>RegionPassage</c> they crossed published. It selects among the region's
        /// <see cref="NamedArrival"/>s; null, empty or unmatched means the region's own single arrival,
        /// which is every region built before Nine Mile Creek's mainland.</para>
        ///
        /// <para><b>⚠ The DOCK keeps the region's own step-off spot, never the arrival's.</b>
        /// <c>SetDock</c> says where you land when you get OFF THE BOAT, and that is a property of the
        /// wharf — it is true for the whole visit, not just for the moment you turned up. Passing the
        /// per-passage point in here would mean walking in over the tidal bar re-pointed every later
        /// disembark at the bar landing, so a player who moored at the wharf would step off it onto a
        /// beach 400 m away. Where you ARRIVE and where you STEP ASHORE are two different questions and
        /// only the first one is the passage's to answer.</para>
        /// </summary>
        /// <returns>True when she was laid at the region's alongside berth (see
        /// <see cref="LayAlongside"/>), which the coordinator then holds against a change of hull.</returns>
        public static bool ApplyArrival(Transform player, Transform boat, ControlSwitcher switcher,
                                        RegionAnchor anchor, string arrivalKey)
        {
            if (anchor == null) return false;
            bool alongside = false;

            // ⭐ A FOOT ARRIVAL NEVER MOVES THE BOAT (owner ruling 2026-11-15: "doors do not teleport";
            // fault D1). A named way in that authors no boat point is a walk-in: the boat was not brought,
            // so she stays exactly where she was left, in the region she was left in. Before this, the
            // empty point fell back to the region's default berth and the one persistent boat was carried
            // across the world with the walker — 930 m from St Peters to the creek wharf, free.
            if (!ArrivalMovesBoat(anchor, arrivalKey)) boat = null;

            Transform arrivalPoint = anchor.ArrivalPointFor(arrivalKey);
            Transform landingPoint = anchor.DisembarkPointFor(arrivalKey);

            // ⭐ HER BERTH BEATS THE STANDOFF (owner ruling, 2026-09-08). Where the region's own scene
            // laid her, she returns to — position AND heading. Where it did not (a region first entered
            // by sea), this is false and the arrival point answers exactly as it always has.
            //
            // ⚠ THE DEFECT THIS CLOSES, measured on a slot. St Peters' ArrivalPos is the alongside berth
            // plus a 2 m standoff, on the moored cape islander's own berth line — and she is 12.9 m long,
            // so her outline spans x 205.05..217.95 and simply CONTAINS the parked dory. Measured with
            // HullFootprint: a signed gap of −3.25 m, the dory wholly inside her, and on screen the dory
            // is invisible behind the cape's sprite (both roots at y = −5.80, so the Y-sort is a tie).
            // The standoff was sized against a POINT, not against a HULL. The owner had said it three
            // times; every answer before this one moved the BANKED berth, which this method overwrote on
            // the way in — #677, #707 and #790 all measured a position the game does not use.
            if (boat != null && anchor.HasAuthoredBoatBerth)
            {
                boat.position = anchor.AuthoredBoatBerthPosition;
                boat.rotation = anchor.AuthoredBoatBerthRotation;
            }
            // An ALONGSIDE berth is laid for a design hull: a hull of another beam lies with her side on the
            // same fender line, so her centre is the hull's to say, not the point's. A named way in keeps
            // its own point.
            else if (boat != null && arrivalPoint != null && anchor.AlongsideBerth && arrivalPoint == anchor.ArrivalPoint)
            {
                LayAlongside(boat, anchor);
                alongside = true;
            }
            else if (boat != null && arrivalPoint != null) boat.position = arrivalPoint.position;
            if (player != null && landingPoint != null) player.position = landingPoint.position;
            if (switcher != null)
            {
                switcher.SetDock(anchor.DockZone, anchor.DisembarkPoint);   // the WHARF's step-off — see above
                switcher.ReassertControlMode();   // re-enable the active boat/foot controller after the toggle
            }
            return alongside;
        }

        /// <summary>
        /// Is this activation the region opening under her (read the scene-laid berth), rather than a hop
        /// from another region? Only the first activation comes from somewhere with no anchor (the shell).
        /// <paramref name="leftAnchor"/> MUST be read before the region being left is deactivated:
        /// deactivating it unlists its anchor, which is the defect this guards. PURE.
        /// </summary>
        public static bool ShouldRememberBoatBerth(RegionAnchor arrived, Transform boat, RegionAnchor leftAnchor) =>
            arrived != null && boat != null && leftAnchor == null;

        /// <summary>True unless <paramref name="arrivalKey"/> names a way in that sets no BOAT point — a
        /// walk-in, which leaves the boat where she was. PURE over the anchor's table.</summary>
        public static bool ArrivalMovesBoat(RegionAnchor anchor, string arrivalKey) =>
            anchor == null ||
            !(RegionAnchor.TryFindArrival(anchor.Arrivals, arrivalKey, out NamedArrival named) &&
              named.ArrivalPoint == null);

        /// <summary>
        /// ⭐ <b>A WORLD-FRAME CROSSING</b> (ADR 0057): carry whoever crossed into the neighbour's local
        /// frame by a pure translation, <paramref name="localShift"/> = <c>from.Origin − to.Origin</c>. The
        /// world point is unchanged, so the distance across the seam is exactly the distance travelled —
        /// no landing point, no inset, no clearance jump — and heading and velocity are untouched (nothing
        /// here rotates or stops anything).
        ///
        /// <para>The boat moves only when she came (<paramref name="boatCame"/>: the player was aboard). A
        /// foot crossing leaves her where she lies, in the region she was left in. The player moves either
        /// way, unless he is parented under the boat (aboard), in which case moving her carried him.</para>
        /// PURE over the transforms, so EditMode asserts it.
        /// </summary>
        public static void ApplyCrossing(Transform player, Transform boat, Vector2 localShift, bool boatCame)
        {
            var shift = new Vector3(localShift.x, localShift.y, 0f);
            if (boatCame && boat != null) Shift(boat, shift);
            if (player != null && !(boatCame && boat != null && player.IsChildOf(boat))) Shift(player, shift);
        }

        static void Shift(Transform t, Vector3 shift)
        {
            t.position += shift;
            var body = t.GetComponent<Rigidbody2D>();
            if (body != null) body.position = t.position;   // velocity is NOT touched: speed carries across
        }

        /// <summary>How far she may move from where she was laid alongside and still be re-laid when her
        /// hull changes there. Past it she has been sailed off and the berth no longer holds her.</summary>
        public const float AlongsideHoldMetres = 0.5f;

        /// <summary>Lay <paramref name="boat"/> at <paramref name="anchor"/>'s alongside berth, sized to
        /// her live hull (<see cref="RegionAnchor.AlongsideCentre"/>), on the berth's heading. Returns the
        /// half beam she was laid for.</summary>
        public static float LayAlongside(Transform boat, RegionAnchor anchor)
        {
            Transform point = anchor.ArrivalPoint;
            float design = anchor.AlongsideHalfBeamMetres;
            float live = LiveHalfBeam(boat, design);
            boat.SetPositionAndRotation(RegionAnchor.AlongsideCentre(point.position, point.rotation, design, live),
                                        point.rotation);
            return live;
        }

        /// <summary>Her hull's half beam, or <paramref name="design"/> while no hull reports one.</summary>
        static float LiveHalfBeam(Transform boat, float design)
        {
            var hull = boat.GetComponent<IHullPresence>();
            float live = hull != null ? hull.Footprint.HalfBeam : 0f;
            return live > 0f ? live : design;
        }

        // The alongside berth she was last laid at, held while she lies there: a hull configured after the
        // arrival (a reload, the earned dory) is re-laid on the same fender line.
        RegionAnchor _alongside;
        Vector3 _alongsideAt;
        float _alongsideHalfBeam;

        private void LateUpdate()
        {
            if (_alongside == null) return;
            if (_boat == null || !_alongside.isActiveAndEnabled ||
                (_boat.position - _alongsideAt).sqrMagnitude > AlongsideHoldMetres * AlongsideHoldMetres)
            {
                _alongside = null;
                return;
            }
            if (LiveHalfBeam(_boat, _alongside.AlongsideHalfBeamMetres) == _alongsideHalfBeam) return;
            _alongsideHalfBeam = LayAlongside(_boat, _alongside);
            _alongsideAt = _boat.position;
        }

        // ---- play-mode glue (scene toggling — flagged for Unity verification) ---------------

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            // WHICH WAY IN, taken once. Consumed here whether or not the arrived region has named
            // arrivals, so a key can never survive its own crossing and steer the next one.
            string arrivalKey = GameServices.ConsumePendingArrivalKey();

            // A world-frame crossing in flight (ADR 0057) is taken once too, whatever happens next.
            PendingCrossing crossing = _pending;
            _pending = null;
            if (crossing != null && _seamFade.Arrived()) _seamArrivedFrame = Time.frameCount;   // clears from here

            // Every arrival places the mover (a door, a passage, a crossing's carry): that jump is not speed, and
            // the heading toward a neighbour is counted afresh in the new region. A warmed neighbour the player
            // has now entered is a visited region, toggled like any other, never released as a warm one.
            _hasLastMoverPos = false;
            _warmGate.Reset();
            _warmScenes.Remove(next.name);

            if (crossing == null) ArrivalOpening.CaptureSeaCrossing(previous, next, arrivalKey);

            // The region being left, read while its anchor is still live (deactivating its roots unlists it).
            RegionAnchor leftAnchor = RegionAnchor.ForScene(previous);
            string leftRegionId = leftAnchor != null ? leftAnchor.RegionId : null;

            SetSceneRootsActive(previous, false);  // the region we left (persistents already DDOL'd out)
            SetSceneRootsActive(next, true);
            SilenceRegionCamera(next);              // the persistent core's camera/listener are the live ones
            BindHoldProxies(next);

            var anchor = RegionAnchor.ForScene(next);

            if (crossing != null && anchor != null &&
                string.Equals(anchor.RegionId, crossing.To, System.StringComparison.Ordinal))
            {
                ArriveAcrossTheSeam(anchor, crossing);
                return;
            }

            bool passengerArrived = ArrivalOpening.ReceiveSeaJourney(next, arrivalKey, anchor);

            // ⭐ DID SHE SAIL IN, or is this the region opening under her? A hop comes FROM a region; the
            // first activation comes from the shell, which authors no anchor. Only on that first
            // activation is the boat still standing where the region's scene laid her — one moment, and
            // it is the only chance anything has to read the berth, because she carries PersistentObject
            // and Awake has already promoted her out of the scene for good.
            //
            // ⚠ Asked of `leftAnchor`, read ABOVE while the region we left was still live. Asking
            // `RegionAnchor.ForScene(previous)` here is always null: SetSceneRootsActive(previous, false)
            // has already run, and RegionAnchor.OnDisable unlists the anchor, so every hop read as a first
            // activation and the berth could be re-read from wherever she happened to be.
            if (ShouldRememberBoatBerth(anchor, _boat, leftAnchor))
                anchor.RememberBoatBerth(_boat, next.name);
            // A key the region does not answer to is a MIS-WIRE, not a style: the player lands at the
            // default and everything looks fine, which is exactly how a passage pointed at a renamed
            // arrival stays broken. Say so once, then fall back.
            if (anchor != null && !string.IsNullOrEmpty(arrivalKey) && !anchor.HasArrival(arrivalKey))
                Debug.LogWarning($"[RegionTravelCoordinator] Passage asked for arrival '{arrivalKey}' in " +
                                 $"'{anchor.RegionId}', which authors no such way in — landing at the " +
                                 "region's default instead.", anchor);
            // A walk-in leaves the boat where she was (D1): she stays in her own region, out of sight here.
            bool boatMoves = passengerArrived || ArrivalMovesBoat(anchor, arrivalKey);
            if (boatMoves) ShowBoat(true);   // she is placed here, so she must be live to be placed
            _alongside = ApplyArrival(passengerArrived ? null : _player, _boat,
                passengerArrived ? null : _switcher, anchor, arrivalKey) ? anchor : null;
            if (_alongside != null)
            {
                _alongsideAt = _boat.position;
                _alongsideHalfBeam = LiveHalfBeam(_boat, anchor.AlongsideHalfBeamMetres);
            }
            if (anchor != null)
            {
                _boatRegionId = BoatRegionAfter(_boatRegionId, leftRegionId, anchor.RegionId, boatMoves);
                UpdateBoatPresence(anchor.RegionId);
            }

            // The boat was just teleported to the arrival point — bring it to rest there so a residual
            // velocity from the previous region doesn't carry it off the mark once the controller re-enables.
            if (_boat != null && boatMoves)
            {
                var boatController = _boat.GetComponent<BoatController>();
                if (boatController != null) boatController.Stop();
            }
        }

        // ---- the world frame (ADR 0057): the whole wall is a door ------------------------------------

        /// <summary>A crossing begun at an edge and not yet arrived: where to, by how much, and whether the
        /// boat came.</summary>
        sealed class PendingCrossing
        {
            public string From, To;
            public Vector2 Shift;
            public bool BoatCame;
        }

        PendingCrossing _pending;
        WorldMap _map;
        // The ONE region the player's boat is in. Empty = not yet known (a rig before its first arrival).
        string _boatRegionId = "";

        /// <summary>True while a crossing has been started and its scene has not yet become active.</summary>
        public bool CrossingInFlight => _pending != null;

        // The momentary fade over every seam crossing (owner ruling on PR 1223). Pure; stepped in Update.
        readonly SeamCrossingFade _seamFade = new SeamCrossingFade();
        int _seamArrivedFrame = -1;
        // The mover's last physics position, for its velocity (which seam it heads for). Reset on a hop.
        Transform _lastMover;
        Vector2 _lastMoverPos;
        bool _hasLastMoverPos;
        // When to warm a neighbour and when to let it go, and the scenes warmed and not yet entered (PR 1223).
        readonly NeighbourWarmGate _warmGate = new NeighbourWarmGate();
        readonly List<string> _warmScenes = new List<string>();

        /// <summary>Where the seam fade is (read-only, for probes and tests).</summary>
        public SeamFadePhase SeamFadePhase => _seamFade.Phase;

        /// <summary>How black the seam fade has the screen (0 clear .. 1 black).</summary>
        public float SeamFadeAlpha => _seamFade.Alpha;

        /// <summary>The region the player's boat was last left in ("" until the first arrival).</summary>
        public string BoatRegionId => _boatRegionId;

        /// <summary>Make sure the shared frame is published (built from the shipped map, data only).</summary>
        void EnsureWorldFrame()
        {
            if (_map == null) _map = WorldMap.LoadShipped();
            if (GameServices.WorldFrame == null && _map != null) GameServices.WorldFrame = _map.BuildFrame();
        }

        private void FixedUpdate()
        {
            if (_pending != null) return;                       // one crossing at a time
            if (GameServices.OpeningCinematicRunning) { _seamFade.Cancel(); return; }   // it sails its own door
            EnsureWorldFrame();
            WorldFrame frame = GameServices.WorldFrame;
            string here = GameServices.CurrentRegionId;
            if (frame == null || !frame.Contains(here)) return; // no frame, or a region off the map
            OpenSharedEdgesOnce(frame, here);                   // before anyone can reach a seam
            if (!TryGetMover(out Transform mover, out bool boatIsMover))
            {
                _hasLastMoverPos = false;
                _seamFade.Cancel();
                TendWarmNeighbour(null, null);   // no one walking or sailing heads for a neighbour
                return;
            }
            Vector2 velocity = MoverVelocity(mover);

            float band = GameServices.RegionCrossingHysteresisMetres;
            CrossingDecision d = frame.Resolve(here, mover.position, band);
            // Inside the region the mover may be heading for a neighbour; past an edge it is crossing or held.
            if (d.Kind == CrossingKind.Stay) WarmNeighbour(frame, here, mover.position, velocity);
            else TendWarmNeighbour(null, null);
            if (d.Kind != CrossingKind.Boundary && !(d.Kind == CrossingKind.Cross && _seamFade.GaveUp))
            {
                // ⭐ The hop is made only under a black screen (owner ruling on PR 1223), and the black follows
                // how near the mover's leading edge is to the edge of the screen (SteerSeamFade). A mover
                // already past the band (a teleport, a shove) is a full-black approach with nothing to carry.
                float carry = 0f;
                Vector2 outward = Vector2.zero;
                if (d.Kind == CrossingKind.Stay)
                {
                    if (!SteerSeamFade(frame, here, mover, velocity, boatIsMover, band, out d, out carry, out outward))
                        return;
                }
                else _seamFade.Approach(1f);
                if (!_seamFade.ReadyToHop) return;
                _seamFade.Hopped();
                // Carried on across under the black, so the first clear frame shows all of it inside the new
                // screen: its trailing edge lands the band inside the neighbour.
                if (carry > 0f) CarryOn(mover, boatIsMover, outward * carry);
                _hasLastMoverPos = false;
                if (BeginCrossing(here, d, boatIsMover)) return;
                if (carry > 0f) CarryOn(mover, boatIsMover, -outward * carry);   // nothing to cross into: undo
            }
            _seamFade.Cancel();

            // A world boundary, or a neighbour whose scene would not load: hold the mover on the edge.
            frame.TryGet(here, out RegionFrame f);
            Rect r = f.LocalRect;
            Vector2 p = mover.position;
            HoldAtEdge(mover, new Vector2(Mathf.Clamp(p.x, r.xMin, r.xMax), Mathf.Clamp(p.y, r.yMin, r.yMax)), d.Edge);
        }

        // The region scene whose shared-edge solids have been stood down (its handle), so the scan runs once
        // per scene, not per step. A revisited scene keeps its colliders as they were left: off.
        Scene _edgesOpenedInScene;
        readonly List<Collider2D> _solidScratch = new List<Collider2D>();

        /// <summary>Stand down the current region scene's solids that wall a SHARED edge (once per scene).
        /// Found through the live anchor that reports <paramref name="here"/>, so a region played direct and
        /// one travelled into are treated alike.</summary>
        void OpenSharedEdgesOnce(WorldFrame frame, string here)
        {
            RegionAnchor anchor = null;
            IReadOnlyList<RegionAnchor> live = RegionAnchor.Live;
            for (int i = 0; i < live.Count && anchor == null; i++)
                if (live[i] != null && string.Equals(live[i].RegionId, here, System.StringComparison.Ordinal)) anchor = live[i];
            if (anchor == null) return;
            Scene scene = anchor.gameObject.scene;
            if (!scene.IsValid() || scene == _edgesOpenedInScene) return;
            _edgesOpenedInScene = scene;
            int opened = OpenSharedEdges(scene, frame, here, _solidScratch);
            if (opened > 0)
                Debug.Log($"[RegionTravelCoordinator] {here}: stood down {opened} edge solid(s) on shared edges " +
                          "(the whole wall is a door; world boundaries keep theirs).");
        }

        /// <summary>
        /// ⭐ <b>THE WHOLE WALL IS A DOOR, physically</b> (owner ruling 2026-11-15; ADR 0057). Disable every
        /// static, non-trigger collider in <paramref name="scene"/> that lies wholly beyond an edge of
        /// <paramref name="regionId"/> that has a neighbour (<see cref="WorldFrame.StandsDownOnSharedEdge"/>).
        /// The land template closes each region with a solid laid flush outside every edge
        /// (<c>ClosedEdges</c>); left on, it held a walker 0.35 m (his foot radius) and a dory 2 m (half her
        /// hull) short of the seam, so the crossing, which fires past the edge plus the hysteresis, never
        /// could. A boundary edge keeps its solid. Moving bodies and triggers are never touched. Returns
        /// how many were disabled.
        /// </summary>
        public static int OpenSharedEdges(Scene scene, WorldFrame frame, string regionId,
                                          List<Collider2D> scratch)
        {
            if (frame == null || !scene.IsValid() || !scene.isLoaded || !frame.Contains(regionId)) return 0;
            scratch ??= new List<Collider2D>();
            int opened = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                root.GetComponentsInChildren(false, scratch);
                foreach (Collider2D c in scratch)
                {
                    if (c == null || !c.enabled || c.isTrigger) continue;
                    Rigidbody2D body = c.attachedRigidbody;
                    if (body != null && body.bodyType != RigidbodyType2D.Static) continue;
                    Bounds b = c.bounds;
                    var solid = Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
                    if (!frame.StandsDownOnSharedEdge(regionId, solid)) continue;
                    c.enabled = false;
                    opened++;
                }
            }
            scratch.Clear();
            return opened;
        }

        /// <summary>Who is moving through the world: the walker on foot, or the boat with him aboard.
        /// Driving is not handled here (a vehicle crossing is its own seam, not built).</summary>
        bool TryGetMover(out Transform mover, out bool boatIsMover)
        {
            mover = null;
            boatIsMover = false;
            if (_switcher == null) return false;
            switch (_switcher.Mode)
            {
                case ControlMode.OnFoot:
                    mover = _player;
                    break;
                case ControlMode.Aboard:
                case ControlMode.OnDeck:
                    mover = _boat;
                    boatIsMover = true;
                    break;
                default:
                    return false;
            }
            return mover != null && mover.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// The mover's velocity from its physics position one step ago (m/s). Zero on the first step after a
        /// hop or a change of mover, which only delays the fade by one step. The body's position, not
        /// the transform's, so render interpolation never reads as speed.
        /// </summary>
        Vector2 MoverVelocity(Transform mover)
        {
            var body = mover.GetComponent<Rigidbody2D>();
            Vector2 p = body != null ? body.position : (Vector2)mover.position;
            Vector2 v = Vector2.zero;
            if (_hasLastMoverPos && mover == _lastMover)
            {
                // A jump faster than anything moves is a teleport (a staging, a placement), not a heading.
                v = SeamCrossingFade.PlausibleVelocity(_lastMoverPos, p, Time.fixedDeltaTime,
                        GameServices.RegionMoverMaxPlausibleSpeed, out bool jumped);
                if (jumped) _warmGate.Interrupt();
            }
            else _warmGate.Interrupt();   // a new mover's heading is counted from its own first steps
            _lastMover = mover;
            _lastMoverPos = p;
            _hasLastMoverPos = true;
            return v;
        }

        /// <summary>
        /// ⭐ <b>Fade by distance as the mover runs up to the edge</b> (owner ruling on PR 1223). The camera stops
        /// at the region edge, so the mover's leading edge (a walker's body, a boat's bow) meets the edge of the
        /// screen exactly when it meets the edge of the region. The screen is driven toward
        /// <see cref="SeamCrossingFade.ApproachAlpha"/> of that gap, so it is black as the leading edge arrives,
        /// at any speed, and nothing is cut by the screen edge while it is still clear. Standing, turning back
        /// or heading for a world boundary clears it. Returns true, with the crossing to make and how far to
        /// carry the mover on across, while it heads for a seam.
        /// </summary>
        bool SteerSeamFade(WorldFrame frame, string here, Transform mover, Vector2 velocity, bool boatIsMover,
                           float band, out CrossingDecision crossing, out float carry, out Vector2 outward)
        {
            crossing = CrossingDecision.Stay;
            carry = 0f;
            outward = Vector2.zero;
            float margin = boatIsMover ? GameServices.RegionSeamBoatDrawnMarginMetres : GameServices.RegionSeamFootDrawnMarginMetres;
            float fadeDistance = boatIsMover ? GameServices.RegionSeamBoatFadeDistanceMetres : GameServices.RegionSeamFootFadeDistanceMetres;
            frame.TryGet(here, out RegionFrame f);
            if (!SeamCrossingFade.TryApproach(f.LocalRect, MoverExtent(mover, margin), velocity,
                    GameServices.RegionSeamMinApproachSpeed, band, out SeamApproach a))
            {
                _seamFade.NoApproach();
                return false;
            }
            CrossingDecision ahead = frame.Resolve(here, a.Probe, 0f);
            if (ahead.Kind != CrossingKind.Cross)
            {
                _seamFade.NoApproach();   // a world boundary ahead: no fade, the edge holds the mover
                return false;
            }
            _seamFade.Approach(SeamCrossingFade.ApproachAlpha(a.GapMetres,
                SeamCrossingFade.FadeDistanceFor(fadeDistance, a.OutSpeed, GameServices.RegionSeamFadeOutSeconds)));
            crossing = ahead;
            carry = a.CarryMetres;
            outward = a.Outward;
            return true;
        }

        readonly List<Collider2D> _extentScratch = new List<Collider2D>();

        /// <summary>The mover's local extent: the solid colliders on its own body, grown by the drawn
        /// <paramref name="margin"/> (a hull's bow and oars, a figure's arms reach past them). A mover with no
        /// collider is its position. No allocation per step (rule 7).</summary>
        Rect MoverExtent(Transform mover, float margin)
        {
            Vector2 p = mover.position;
            var body = mover.GetComponent<Rigidbody2D>();
            bool any = false;
            Bounds b = default;
            if (body != null)
            {
                body.GetAttachedColliders(_extentScratch);
                foreach (Collider2D c in _extentScratch)
                {
                    if (c == null || !c.enabled || c.isTrigger) continue;
                    if (!any) { b = c.bounds; any = true; }
                    else b.Encapsulate(c.bounds);
                }
                _extentScratch.Clear();
            }
            Rect r = any ? Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y) : new Rect(p, Vector2.zero);
            float m = Mathf.Max(0f, margin);
            return Rect.MinMaxRect(r.xMin - m, r.yMin - m, r.xMax + m, r.yMax + m);
        }

        /// <summary>Move the mover (and, for a boat, a rider she does not already carry) by
        /// <paramref name="offset"/>, body and transform together; velocity untouched.</summary>
        void CarryOn(Transform mover, bool boatIsMover, Vector2 offset)
        {
            var d = new Vector3(offset.x, offset.y, 0f);
            Shift(mover, d);
            if (boatIsMover && _player != null && !_player.IsChildOf(mover)) Shift(_player, d);
        }

        RegionSceneLoader _loader;

        RegionSceneLoader Loader
        {
            get
            {
                if (_loader == null) _loader = Object.FindAnyObjectByType<RegionSceneLoader>();
                return _loader;
            }
        }

        /// <summary>
        /// ⭐ <b>Warm the neighbour the mover is heading for</b> (PR 1223, the first-entry freeze): within
        /// <see cref="GameServices.RegionPreloadDistanceMetres"/> of a shared edge, start reading the
        /// neighbour's scene in the background and park it (<see cref="RegionSceneLoader.Preload"/>), so the
        /// crossing only re-activates it. A region already loaded (visited) asks nothing, and so does a mover
        /// standing near a seam or moving along or away from it: only HEADING for the edge (faster than
        /// <see cref="GameServices.RegionPreloadMinHeadingSpeed"/>, held for
        /// <see cref="GameServices.RegionPreloadHeadingSeconds"/>, a teleport never counting as speed) warms it,
        /// and a warmed neighbour never entered is let go when the approach ends, so an idle player does not hold
        /// a whole second scene.
        /// </summary>
        // A least speed of zero would let a standing mover (velocity exactly zero) count as heading out. Not a
        // tunable: it only keeps "standing" meaning standing when the tuned least speed is 0.
        const float MinHeadingSpeed = 1e-3f;

        void WarmNeighbour(WorldFrame frame, string here, Vector2 moverLocal, Vector2 velocity)
        {
            // The pure question first, so a rig with no loader never searches for one per step.
            RegionDef target = null;
            bool heading = _map != null && frame.TryGetApproachingNeighbour(here, moverLocal, velocity,
                    Mathf.Max(GameServices.RegionPreloadMinHeadingSpeed, MinHeadingSpeed),
                    GameServices.RegionPreloadDistanceMetres, out RegionFrame next)
                && _map.TryGet(next.RegionId, out target) && target != null;
            TendWarmNeighbour(heading ? target.Id : null, heading ? target : null);
        }

        /// <summary>
        /// One physics step of the warm-up (PR 1223): <paramref name="headingFor"/> is the neighbour the mover is
        /// heading for now (null for none). Its scene is preloaded once the heading has held for
        /// <see cref="GameServices.RegionPreloadHeadingSeconds"/>, and every neighbour warmed and never entered
        /// is unloaded once no neighbour has been headed for during <see cref="GameServices.RegionPreloadReleaseSeconds"/>.
        /// </summary>
        void TendWarmNeighbour(string headingFor, RegionDef target)
        {
            bool warm = _warmGate.Step(headingFor, Time.fixedDeltaTime, GameServices.RegionPreloadHeadingSeconds);
            if (_warmScenes.Count > 0 && _warmGate.ApproachEnded(GameServices.RegionPreloadReleaseSeconds))
                ReleaseWarmNeighbours();
            if (!warm || target == null) return;
            if (_loader != null && _loader.PreloadingSceneName != null) return;
            RegionSceneLoader loader = Loader;
            if (loader != null && loader.Preload(target) && !_warmScenes.Contains(target.SceneName))
                _warmScenes.Add(target.SceneName);
        }

        /// <summary>
        /// Unload every neighbour this core warmed and the player never entered (PR 1223: the approach ended, or
        /// the core is going). One still on its way in is left to land and park, and is let go on a later step;
        /// one that is gone, or that became the active scene, is simply forgotten.
        /// </summary>
        void ReleaseWarmNeighbours()
        {
            Scene active = SceneManager.GetActiveScene();
            for (int i = _warmScenes.Count - 1; i >= 0; --i)
            {
                Scene s = SceneManager.GetSceneByName(_warmScenes[i]);
                if (s.IsValid() && !s.isLoaded) continue;   // still loading: parked when it lands, released then
                _warmScenes.RemoveAt(i);
                if (!s.IsValid() || s == active) continue;
                SceneManager.UnloadSceneAsync(s);
            }
        }

        bool BeginCrossing(string here, CrossingDecision d, bool boatIsMover)
        {
            if (_map == null || !_map.TryGet(d.To.RegionId, out RegionDef target)) return false;
            var loader = Loader;
            if (loader == null) return false;

            _pending = new PendingCrossing { From = here, To = d.To.RegionId, Shift = d.LocalShift, BoatCame = boatIsMover };
            GameServices.PendingArrivalKey = null;   // an edge crossing names no way in: it is the way in
            if (loader.Travel(target)) return true;  // (a re-visited region arrives inside this call)
            _pending = null;
            return false;
        }

        /// <summary>Hold a mover on the region's edge: put it back on the nearest inside point and take
        /// away only the part of its velocity that points out of the world.</summary>
        static void HoldAtEdge(Transform mover, Vector2 inside, RegionEdge edge)
        {
            Vector2 outward = edge switch
            {
                RegionEdge.West => Vector2.left,
                RegionEdge.East => Vector2.right,
                RegionEdge.South => Vector2.down,
                _ => Vector2.up,
            };
            var body = mover.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = inside;
                Vector2 v = body.linearVelocity;
                float o = Vector2.Dot(v, outward);
                if (o > 0f) body.linearVelocity = v - outward * o;
            }
            mover.position = new Vector3(inside.x, inside.y, mover.position.z);
        }

        /// <summary>The arrival half of a world-frame crossing: same world point, same heading, same speed;
        /// the boat only if she came; the dock re-pointed and control re-asserted as on any arrival.</summary>
        void ArriveAcrossTheSeam(RegionAnchor anchor, PendingCrossing crossing)
        {
            _alongside = null;
            // Walking out of the region with her line in hand: the line is made fast where he let go of
            // it (still in the old frame, before the shift), so she is not left tethered to someone who
            // is no longer in her world.
            if (!crossing.BoatCame && _boat != null && _player != null)
            {
                var mooring = _boat.GetComponent<BoatMooring>();
                if (mooring != null && mooring.State == MooringState.HeldByPlayer) mooring.Root(_player.position);
            }
            ApplyCrossing(_player, _boat, crossing.Shift, crossing.BoatCame);
            _hasLastMoverPos = false;   // the 400 m local jump is not speed
            _boatRegionId = BoatRegionAfter(_boatRegionId, crossing.From, crossing.To, crossing.BoatCame);
            UpdateBoatPresence(anchor.RegionId);

            // The camera is carried by the same translation, follow state and all, and settled into the new
            // region's clamp while the seam fade holds the screen black: the first clear frame shows the view
            // already stopped at the near edge, with the mover just inside it (owner ruling on PR 1223).
            var follow = Object.FindFirstObjectByType<CameraFollow>();
            if (follow != null) follow.CarryAcross(crossing.Shift);

            if (_switcher != null)
            {
                _switcher.SetDock(anchor.DockZone, anchor.DisembarkPoint);
                // A boat crossing carried him on his seat: keep it (the rider pop, PR 1223). A foot
                // crossing re-asserts exactly as before.
                _switcher.ReassertControlMode(keepSeat: crossing.BoatCame);
            }
        }

        /// <summary>
        /// The region the boat is in after a hop from <paramref name="from"/> to <paramref name="to"/>. PURE.
        /// She came: <paramref name="to"/>. She was left and her region was known: unchanged. She was left
        /// and her region was NOT yet known: <paramref name="from"/>, because "unknown" is exactly the state
        /// <see cref="UpdateBoatPresence"/> shows as "here", so she was standing in the region being left.
        ///
        /// <para>⚠ The defect this closes: a region played direct (or any rig whose first arrival the
        /// coordinator never saw, because the core came up after the region's scene was already active)
        /// never set her region, so a walk out left it empty and she stayed SHOWN in the neighbour at her
        /// old local coordinates, floating in the wrong place (RicePointCrossingPlayTests, the strand).</para>
        /// </summary>
        public static string BoatRegionAfter(string boatRegionId, string from, string to, bool boatCame)
        {
            if (boatCame) return to ?? "";
            if (!string.IsNullOrEmpty(boatRegionId)) return boatRegionId;
            return from ?? "";
        }

        void ShowBoat(bool present)
        {
            if (_boat != null && _boat.gameObject.activeSelf != present) _boat.gameObject.SetActive(present);
        }

        /// <summary>The boat is present only in her own region (or while her region is not yet known).</summary>
        void UpdateBoatPresence(string currentRegionId) =>
            ShowBoat(string.IsNullOrEmpty(_boatRegionId) ||
                     string.Equals(_boatRegionId, currentRegionId, System.StringComparison.Ordinal));

        // ---- IWorldWhereabouts ------------------------------------------------------------------

        Whereabouts IWorldWhereabouts.Player => _player != null
            ? new Whereabouts(GameServices.CurrentRegionId, _player.position, _player.eulerAngles.z)
            : default;

        Whereabouts IWorldWhereabouts.Boat => _boat != null
            ? new Whereabouts(_boatRegionId, _boat.position, _boat.eulerAngles.z)
            : default;

        public void RestoreBoat(Whereabouts boat)
        {
            if (_boat == null || !boat.IsKnown) return;
            _alongside = null;
            _boatRegionId = boat.RegionId;
            _boat.SetPositionAndRotation(new Vector3(boat.X, boat.Y, _boat.position.z),
                                         Quaternion.Euler(0f, 0f, boat.HeadingDegrees));
            var body = _boat.GetComponent<Rigidbody2D>();
            if (body != null) { body.position = _boat.position; body.rotation = boat.HeadingDegrees; }
            UpdateBoatPresence(GameServices.CurrentRegionId);
        }

        private static void SetSceneRootsActive(Scene scene, bool active)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (var go in scene.GetRootGameObjects()) go.SetActive(active);
        }

        // A region scene authored for standalone review carries its own Camera + AudioListener; the
        // persistent core already has them, so quiet the region's to avoid a double render / listener warning.
        private static void SilenceRegionCamera(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (var go in scene.GetRootGameObjects())
            {
                foreach (var cam in go.GetComponentsInChildren<Camera>(true)) cam.enabled = false;
                foreach (var listener in go.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            }
        }

        private void BindHoldProxies(Scene scene)
        {
            if (_hold == null || !scene.IsValid() || !scene.isLoaded) return;
            foreach (var go in scene.GetRootGameObjects())
                foreach (var proxy in go.GetComponentsInChildren<PersistentHoldProxy>(true))
                    proxy.Bind(_hold);
        }

        /// <summary>Wire the coordinator in one call (tests / editor builder).</summary>
        public void Configure(Transform player, Transform boat, ControlSwitcher switcher, ShipHold hold)
        {
            _player = player; _boat = boat; _switcher = switcher; _hold = hold;

            // Re-publish when wired AFTER we were already enabled. The builder and the test rigs both
            // configure while inactive and enable afterwards (so OnEnable does it), but a caller that
            // configures a live coordinator would otherwise leave the relay holding the previous player.
            if (isActiveAndEnabled) PublishPlayer();
        }
    }
}
