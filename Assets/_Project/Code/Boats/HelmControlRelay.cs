using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The Boats-lane producer for the Core <see cref="IHelmControl"/> seam (ADR 0025 S1): which
    /// diegetic control the active hull shows (tiller vs binnacle lever), the live drive/steer the
    /// overlay draws, and the input intents it may send back (detent steps, mouse drag-to-set). The
    /// UI overlay (<c>HiddenHarbours.UI</c>, which references only Core) reads/writes through
    /// <see cref="GameServices.HelmControl"/> — the exact <see cref="ActiveBoatProbe"/> pattern
    /// (rule 4, ADR 0007).
    ///
    /// <para><b>One state, one owner.</b> <see cref="Drive"/> is a pull of
    /// <see cref="BoatController.Throttle"/> — the very <c>_throttle</c> the physics runs — and every
    /// intent lands in <see cref="BoatController.SetControl"/>. This component stores NO drive of its
    /// own; keyboard (DevBoatInput), gamepad and mouse all move the same value through the same detent
    /// maths (<see cref="HelmThrottleStepMath"/>), so the lever the player sees is the throttle the
    /// hull feels, whoever moved it last.</para>
    ///
    /// <para><b>Self-installing — on EVERY hull, which is why it must not self-APPOINT.</b>
    /// <see cref="BoatController.Awake"/> adds one at play time (the MastheadTelltale precedent), so
    /// every already-built scene grows the seam with no builder re-run. That means a skipper's boat, an
    /// ambient hull and a boat for sale all carry one too. So enabling REGISTERS this relay with
    /// <see cref="HelmSlot"/> against the hull it rides — a request, not a claim — and the slot is
    /// granted to whichever registration matches the hull the Player lane says the player is piloting.
    /// <see cref="IsPlayerHelm"/> is that answer; <see cref="HasHelm"/> stayed the ENGINE question it
    /// always was.</para>
    ///
    /// <para><b>Dev F-cycle (owner addition 2026-08-03).</b> <see cref="Style"/> is resolved from the
    /// LIVE <see cref="BoatController.Hull"/> every read, so the dev boat picker's F-swap shows each
    /// hull's control instantly. <see cref="DevIgnoreEquipmentGating"/> is the clearly-marked dev-only
    /// override for the S2+ purchase/equipment gating; it is FALSE unless this is the editor or a
    /// development build, so a shipped build gets the real gating.</para>
    ///
    /// <para><b>S2 (ADR 0025) also makes this the instrument GLASS producer</b>
    /// (<see cref="IHelmInstruments"/>): the piloted hull's effective fit, the live throttled sounding,
    /// and the per-hull sounder preferences. The piloting controls still ship with the hull (a motor
    /// comes with its tiller), so the dev override touches only the INSTRUMENTS — which is the job it was
    /// declared for in S1 and has been a no-op until now.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HelmControlRelay : MonoBehaviour, IHelmControl, IHelmInstruments
    {
        private BoatController _boat;

        // Instrument-seam scratch (ADR 0025 S2). The owned-id list is reused, never re-allocated, so
        // resolving the fit costs nothing per frame (rule 7).
        private readonly System.Collections.Generic.List<string> _ownedScratch = new();
        private float _depthMetres;
        private bool _depthValid;
        private float _nextDepthReadTime = float.NegativeInfinity;

        /// <summary>DEV-ONLY (owner addition 2026-08-03): show each F-cycled hull's control with no
        /// purchase/equipment gating, so the owner can feel tiller vs lever across the fleet NOW.
        /// Defaults ON only in the editor / a development build — a SHIPPED build starts false and
        /// S2+'s real equipment gating takes over. Test seam: settable.
        ///
        /// <para><b>This is the ONE dev predicate in the instrument system.</b>
        /// <see cref="DevInstrumentCycle"/> reads this flag rather than re-deriving
        /// <c>isEditor || isDebugBuild</c> for itself, so the key and the fit override can never drift apart
        /// and a player can never key their way to a free instrument (S3d T2).</para></summary>
        public bool DevIgnoreEquipmentGating { get; set; }

        /// <summary>
        /// DEV-ONLY (ADR 0025 S3d): which instrument the brow cutout is showing right now, as stepped by
        /// <see cref="DevInstrumentCycle"/>. Read only while <see cref="DevIgnoreEquipmentGating"/> is on;
        /// in a shipped build nothing consults it.
        ///
        /// <para><b>Starts at <see cref="SounderKind.Depth"/> — today's picture, unchanged.</b> The S2 dev
        /// bypass granted exactly the depth sounder, so every consoled hull came up showing it. Starting the
        /// ring there means the owner's existing F-cycle looks identical until he presses the cycle key,
        /// which is the "must not regress what already worked" bar.</para>
        ///
        /// <para><b>It lives here BECAUSE it must survive an F-swap.</b> The dev boat picker re-skins the
        /// ONE persistent boat in place, so this component — and the chosen tier with it — rides through
        /// every hull change. That is the deliberate F-swap behaviour: the tier CARRIES rather than
        /// resetting, so the owner sets the finder once and then walks the fleet comparing the same
        /// instrument across four dashes, holding everything constant but the hull — the very comparison
        /// <see cref="DevBoatPicker"/> exists for. (Landing on a console that cannot take the carried unit
        /// shows the nearest one below it without forgetting the request —
        /// <see cref="DevInstrumentCycle.Reachable"/>.)</para>
        /// </summary>
        public SounderKind DevBrowStep { get; set; } = SounderKind.Depth;

        /// <summary>
        /// DEV-ONLY (ADR 0025 S5): which instruments the PILOT DECK's two stations are showing, as
        /// stepped by <see cref="DevInstrumentCycle.StepPilot"/> (SHIFT+K). Read only while
        /// <see cref="DevIgnoreEquipmentGating"/> is on; in a shipped build nothing consults it.
        ///
        /// <para><b>Starts at <see cref="DevInstrumentCycle.DevPilotFit.Owned"/> — today's picture,
        /// exactly.</b> That state does not state the deck at all: it hands the question back to real
        /// ownership, so a plotter the player BOUGHT stays on the brow and the owner's existing F-cycle
        /// is unchanged until he presses SHIFT+K. Same "must not regress what already worked" bar
        /// <see cref="DevBrowStep"/> was held to — and the reason this ring, unlike the brow's, never
        /// narrows.</para>
        ///
        /// <para>It lives here for the same reason the brow's step does: the picker re-skins the ONE
        /// persistent boat in place, so the chosen deck rides through every hull change and the owner can
        /// walk the fleet comparing the same instrument across two wheelhouses.</para>
        /// </summary>
        public DevInstrumentCycle.DevPilotFit DevPilotStep { get; set; }
            = DevInstrumentCycle.DevPilotFit.Owned;

        private void Awake()
        {
            _boat = GetComponent<BoatController>();
            DevIgnoreEquipmentGating = Application.isEditor || Debug.isDebugBuild;
        }

        /// <summary>Wire explicitly (tests / rigs built without Awake order guarantees).</summary>
        public void Configure(BoatController boat) => _boat = boat;

        // ---- the Core slot: this relay REQUESTS, HelmSlot decides ------------------------------
        //
        // ⚠ This used to be `GameServices.HelmControl = this`, and that one line was the seam's whole
        // defect: BoatController installs a relay on EVERY hull, so the slot went to whichever one
        // enabled LAST — a skipper's boat, an ambient hull, a purchased one — and the player's own
        // relay never registered again (its OnDisable had not run; only the winner's had). Now it
        // registers against the hull it rides and nothing more. Registering is not winning: the grant
        // goes to the hull the Player lane declares the player is piloting, in either enable order.
        //
        // EditMode never fires OnEnable, so registration stays PlayMode-only by construction — which is
        // exactly what a presentation seam wants, and why the arbitration rule itself lives in a POCO
        // that EditMode CAN reach (HelmSlot).

        // The token this relay registered under, HELD rather than re-resolved: if the controller were
        // destroyed under us, re-resolving at OnDisable would name something else and leave the entry
        // standing — a dead hull holding a registration open.
        private object _registeredHull;

        private void OnEnable()
        {
            // The hull's identity is her CONTROLLER — the same instance ControlSwitcher holds and
            // declares, so the two sides name the same object without either naming the other's type.
            // ⚠ Unity's == (not ??): a destroyed/absent controller is not a hull. A relay that cannot
            // name one registers under ITSELF, an identity nothing can ever declare piloted — so it is
            // considered, is never granted, and cannot take the helm off a boat that has one.
            BoatController boat = Boat();
            _registeredHull = boat != null ? (object)boat : this;
            GameServices.Helm.Register(_registeredHull, this, this);
        }

        private void OnDisable()
        {
            GameServices.Helm.Unregister(_registeredHull);
            _registeredHull = null;
        }

        // ---- reads (pull — the overlay samples these per frame) --------------------------------

        /// <inheritdoc/>
        /// <remarks>⚠ The ENGINE question, not the PLAYER one — true of any driven motor hull,
        /// whoever is steering her. It is the right gate for the intents and instruments below (a
        /// throttle step means nothing on a dory); presentation wants <see cref="IsPlayerHelm"/>.
        /// </remarks>
        public bool HasHelm
        {
            get
            {
                var boat = Boat();
                return boat != null && boat.isActiveAndEnabled && boat.Hull != null
                       && BoatController.UsesEngineHelm(boat.Hull.Propulsion);
            }
        }

        /// <inheritdoc/>
        /// <remarks>Answered against the arbiter rather than stored, so it cannot go stale behind a
        /// hull swap, a region hop, or a relay that stood down and came back: the grant is recomputed
        /// from occupancy and this simply reads it.</remarks>
        public bool IsPlayerHelm => HasHelm && GameServices.Helm.Holds(this);

        /// <inheritdoc/>
        public HelmControlStyle Style
        {
            get
            {
                if (!HasHelm) return HelmControlStyle.None;
                // S2+ hangs the purchase/equipment gate here; S1's piloting controls ship with the
                // hull (a motor comes with its tiller), so today the dev override changes nothing —
                // it exists as the clearly-marked seam the owner asked for.
                return StyleFor(Boat().Hull);
            }
        }

        /// <inheritdoc/>
        public HelmLeverFinish LeverFinish
        {
            get
            {
                var boat = Boat();
                var helm = boat != null && boat.Hull != null ? boat.Hull.Helm : null;
                return helm != null ? helm.Lever : HelmLeverFinish.Graphite;
            }
        }

        /// <inheritdoc/>
        public HelmWheelRim WheelRim
        {
            get
            {
                var boat = Boat();
                var helm = boat != null && boat.Hull != null ? boat.Hull.Helm : null;
                return helm != null ? helm.Wheel : HelmWheelRim.Rubber;
            }
        }

        /// <inheritdoc/>
        public float Drive { get { var b = Boat(); return b != null ? b.Throttle : 0f; } }

        /// <inheritdoc/>
        public float Steer { get { var b = Boat(); return b != null ? b.Steer : 0f; } }

        // ---- IHelmInstruments: the GLASS (ADR 0025 S2) -----------------------------------------

        /// <inheritdoc/>
        public string HullId
        {
            get
            {
                var boat = Boat();
                return boat != null && boat.Hull != null ? boat.Hull.Id : "";
            }
        }

        /// <summary>
        /// The piloted hull's EFFECTIVE fit, resolved fresh from its console's authored default plus the
        /// instruments the player bought FOR THIS HULL (<see cref="InstrumentLocker"/>) — never stored
        /// (rule 5, <see cref="BoatEquipment"/>'s own discipline).
        ///
        /// <para><b>This is where <see cref="DevIgnoreEquipmentGating"/> starts doing its job</b> (it was
        /// a declared no-op through S1): with it on, the basic instruments read as fitted on every
        /// consoled hull, so the owner can F-cycle the fleet and see each dash without shopping. It is
        /// FALSE in a shipped build, where ownership and the console default are the only gates.</para>
        ///
        /// <para><b>S3d: the dev override is now a CYCLE, not a blanket grant</b>
        /// (<see cref="DevInstrumentCycle.DevFit"/>). Granting every instrument id at once would HIDE the
        /// units it was meant to reveal — the fish finder wins the one brow cutout over the depth sounder
        /// by design — so the override states one brow at a time (<see cref="DevBrowStep"/>) and can
        /// express FEWER instruments than the hull ships as well as more. Either way this method only ever
        /// widens a per-read SCRATCH list and narrows a RESOLVED value: nothing on this path can write an
        /// instrument into the player's save.</para>
        /// </summary>
        public HelmFit Fit
        {
            get
            {
                var boat = Boat();
                HelmConsoleDef console = boat != null && boat.Hull != null ? boat.Hull.Helm : null;
                if (console == null) return HelmFit.None;

                InstrumentLocker.OwnedFor(GameServices.Save?.Current, HullId, _ownedScratch);
                return DevIgnoreEquipmentGating
                    ? DevInstrumentCycle.DevFit(console, _ownedScratch, DevBrowStep, DevPilotStep)
                    : BoatEquipment.EffectiveFit(console, _ownedScratch);
            }
        }

        /// <summary>
        /// The live sounding under the hull — <see cref="DepthSounder.DisplayDepth"/> over the ONE height
        /// map (<see cref="GameServices.TidalTerrain"/>) and the deterministic water level
        /// (<see cref="IEnvironmentService.WaterLevelAt"/>). Recomputed on a THROTTLED tick
        /// (<see cref="DepthSounderSettings.ReadIntervalSec"/> — the tide moves in minutes, rule 7) and
        /// cached only until the next tick; nothing about it is ever saved.
        ///
        /// <para>Returns false — meaning "no transducer read", show nothing — when there is no piloted
        /// hull, no clock/environment service, or no terrain registered for the region (a null terrain is
        /// "open water", which is not a sounding).</para>
        /// </summary>
        public bool TryReadDepth(out float metres)
        {
            float now = Time.time;
            if (now >= _nextDepthReadTime)
            {
                _nextDepthReadTime = now + Mathf.Max(0.01f, GameServices.DepthSounder.ReadIntervalSec);
                _depthValid = Sound(out _depthMetres);
            }
            metres = _depthMetres;
            return _depthValid;
        }

        /// <summary>
        /// Where the instruments are reading: the piloted hull's world position (ADR 0025 S3 — the fish
        /// finder queries <see cref="IFishSchools.MarksAt"/> at a point, and it must be the SAME point the
        /// sounding is taken at). Not throttled — a transform read is free, and the consumer's own repaint
        /// cadence decides how often it asks.
        /// </summary>
        public bool TryReadPosition(out Vector2 worldPos)
        {
            var boat = Boat();
            if (boat == null || !HasHelm) { worldPos = Vector2.zero; return false; }
            Vector3 p = boat.transform.position;
            worldPos = new Vector2(p.x, p.y);
            return true;
        }

        private bool Sound(out float metres)
        {
            metres = 0f;
            var boat = Boat();
            if (boat == null || !HasHelm) return false;
            IEnvironmentService environment = GameServices.Environment;
            IGameClock clock = GameServices.Clock;
            ITidalTerrain terrain = GameServices.TidalTerrain;
            if (environment == null || clock == null || terrain == null) return false;

            Vector3 p = boat.transform.position;
            float waterLevel = environment.WaterLevelAt(clock.TotalSeconds);
            metres = DepthSounder.DisplayDepth(waterLevel, terrain.ElevationAt(new Vector2(p.x, p.y)));
            return true;
        }

        /// <inheritdoc/>
        // ⚠ Property and type deliberately share a name (the interface reads best that way); qualify the
        // static call so nothing rests on C#'s "Color Color" resolution rule.
        public SounderPrefs SounderPrefs
        {
            get
            {
                DepthSounderSettings cfg = GameServices.DepthSounder;
                // ⚠ BOTH settings blocks, not just the sounder's: the one-argument FromDefaults fills the
                // finder's RANGE from FishFinderSettings.Default (the asset-free code default) rather than
                // the owner's tuned GameConfig value, so a freshly fitted finder would silently ignore his
                // dial the moment he moved it off 20 m. ADR 0025 S3.
                return InstrumentLocker.PrefsFor(GameServices.Save?.Current, HullId,
                                                 HiddenHarbours.Core.SounderPrefs.FromDefaults(
                                                     in cfg, GameServices.FishFinder));
            }
        }

        /// <inheritdoc/>
        public void SetSounderPrefs(in SounderPrefs prefs)
        {
            SaveData save = GameServices.Save?.Current;
            if (save == null) return;
            if (InstrumentLocker.SetPrefs(save, HullId, in prefs)) GameServices.Save?.Save();
        }

        /// <summary>
        /// Which control a hull's helm shows — THE tiller-vs-lever decision, data-driven (rule 2):
        /// an Engine hull with a <see cref="HelmConsoleDef"/> composites the binnacle LEVER; an
        /// Engine hull without one is a bare motor and shows the outboard TILLER; an Oars hull shows
        /// nothing. Pure + static so the rule is EditMode-testable without a scene.
        /// </summary>
        public static HelmControlStyle StyleFor(BoatHullDef hull)
        {
            if (hull == null || !BoatController.UsesEngineHelm(hull.Propulsion)) return HelmControlStyle.None;
            return hull.Helm != null ? HelmControlStyle.Lever : HelmControlStyle.Tiller;
        }

        /// <summary>The effective detent counts for a hull: its own override when set, else the
        /// shared GameConfig policy (rule 6). Pure + static.</summary>
        public static void EffectiveNotches(BoatHullDef hull, in HelmThrottleSettings cfg,
                                            out int ahead, out int astern)
        {
            ahead  = hull != null && hull.HelmAheadNotches  > 0 ? hull.HelmAheadNotches  : cfg.AheadNotches;
            astern = hull != null && hull.HelmAsternNotches > 0 ? hull.HelmAsternNotches : cfg.AsternNotches;
            if (ahead  < 1) ahead  = 1;
            if (astern < 1) astern = 1;
        }

        // ---- intents (every one lands in BoatController.SetControl — no second drive) -----------

        /// <inheritdoc/>
        public void StepAhead() => Step(+1);

        /// <inheritdoc/>
        public void StepAstern() => Step(-1);

        private void Step(int dir)
        {
            var boat = Boat();
            if (boat == null || !HasHelm) return;
            HelmThrottleSettings cfg = GameServices.HelmThrottle;
            EffectiveNotches(boat.Hull, in cfg, out int ahead, out int astern);
            boat.SetControl(HelmThrottleStepMath.StepOnce(boat.Throttle, dir, ahead, astern), boat.Steer);
        }

        /// <inheritdoc/>
        public void SetNeutral()
        {
            var boat = Boat();
            if (boat == null || !HasHelm) return;
            boat.SetControl(0f, boat.Steer);
        }

        /// <inheritdoc/>
        public void DragDrive(float sig)
        {
            var boat = Boat();
            if (boat == null || !HasHelm) return;
            boat.SetControl(Mathf.Clamp(sig, -1f, 1f), boat.Steer);
        }

        /// <inheritdoc/>
        public void EndDrag()
        {
            var boat = Boat();
            if (boat == null || !HasHelm) return;
            float snapped = HelmThrottleStepMath.ApplyNeutralSnap(
                boat.Throttle, GameServices.HelmThrottle.NeutralSnapWindow01);
            boat.SetControl(snapped, boat.Steer);
        }

        // ---- the wheel's steer session (S2a) ----------------------------------------------------
        // Arbitration contract (IHelmControl doc): while the session is live, DevBoatInput PRESERVES
        // the held steer when its own momentary read is zero, and a real key press calls
        // EndSteerDrag — keys win with one decisive handover, never a per-frame fight.

        private bool _steerDrag;

        /// <inheritdoc/>
        public bool SteerDragActive => _steerDrag && HasHelm;

        /// <inheritdoc/>
        public void DragSteer(float steer)
        {
            var boat = Boat();
            if (boat == null || !HasHelm) return;
            _steerDrag = true;
            boat.SetControl(boat.Throttle, Mathf.Clamp(steer, -1f, 1f));
        }

        /// <inheritdoc/>
        public void EndSteerDrag() => _steerDrag = false;

        // Resolve lazily (the Stop()/SetHull precedent): the relay can be added + queried in rigs
        // where Awake ordering isn't guaranteed.
        private BoatController Boat()
        {
            if (_boat == null) _boat = GetComponent<BoatController>();
            return _boat;
        }
    }
}
