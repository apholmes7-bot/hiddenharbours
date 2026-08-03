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
    /// <para><b>Self-installing.</b> <see cref="BoatController.Awake"/> adds one at play time (the
    /// MastheadTelltale precedent), so every already-built scene grows the seam with no builder
    /// re-run. Registration rides the enable lifetime; <see cref="HasHelm"/> gates on the controller
    /// actually driving, so the overlay naturally hides ashore/moored/rowing.</para>
    ///
    /// <para><b>Dev F-cycle (owner addition 2026-08-03).</b> <see cref="Style"/> is resolved from the
    /// LIVE <see cref="BoatController.Hull"/> every read, so the dev boat picker's F-swap shows each
    /// hull's control instantly. <see cref="DevIgnoreEquipmentGating"/> is the clearly-marked dev-only
    /// override for the S2+ purchase/equipment gating (none exists yet for the piloting controls —
    /// a motor ships with its tiller, a console with its lever); it is FALSE unless this is the editor
    /// or a development build, so a shipped build gets the real gating by default when S2 lands it.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HelmControlRelay : MonoBehaviour, IHelmControl
    {
        private BoatController _boat;

        /// <summary>DEV-ONLY (owner addition 2026-08-03): show each F-cycled hull's control with no
        /// purchase/equipment gating, so the owner can feel tiller vs lever across the fleet NOW.
        /// Defaults ON only in the editor / a development build — a SHIPPED build starts false and
        /// S2+'s real equipment gating takes over. Test seam: settable.</summary>
        public bool DevIgnoreEquipmentGating { get; set; }

        private void Awake()
        {
            _boat = GetComponent<BoatController>();
            DevIgnoreEquipmentGating = Application.isEditor || Debug.isDebugBuild;
        }

        /// <summary>Wire explicitly (tests / rigs built without Awake order guarantees).</summary>
        public void Configure(BoatController boat) => _boat = boat;

        // Register/clear the Core slot with this component's enable lifetime (scene-scoped service,
        // the ActiveBoatProbe pattern). EditMode never fires OnEnable — registration is PlayMode-only
        // by construction, which is exactly what the presentation seam wants.
        private void OnEnable() => GameServices.HelmControl = this;

        private void OnDisable()
        {
            if (ReferenceEquals(GameServices.HelmControl, this))
                GameServices.HelmControl = null;
        }

        // ---- reads (pull — the overlay samples these per frame) --------------------------------

        /// <inheritdoc/>
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
        public HelmFit Fit
        {
            get
            {
                if (!HasHelm) return HelmFit.None;
                // The hull's authored default fit. The owned-per-hull upgrade set is S2's save
                // schema — when it lands, its ids feed this same call (BoatEquipment.EffectiveFit
                // is already the one resolver, tested and waiting).
                return BoatEquipment.EffectiveFit(Boat().Hull, null);
            }
        }

        /// <inheritdoc/>
        public float Drive { get { var b = Boat(); return b != null ? b.Throttle : 0f; } }

        /// <inheritdoc/>
        public float Steer { get { var b = Boat(); return b != null ? b.Steer : 0f; } }

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
