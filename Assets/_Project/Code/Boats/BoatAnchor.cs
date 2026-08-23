using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>The ground tackle</b> — drop the hook where the rode reaches the bottom, and lie to it while the
    /// wind and the tide work at you (P1 "the sea has moods" / P5 "cozy, but with teeth").
    ///
    /// <list type="bullet">
    ///   <item><b>Every hull carries one — unless her data says otherwise.</b> The owner's ruling is an
    ///   anchor on every hull, so <see cref="BoatHullDef.HasAnchor"/> is the first gate and the only one
    ///   that is not about the sea: no tackle, nothing to let go, and the helm draws no switch. It is
    ///   asked through <see cref="AnchorMath.CarriesAnchor"/> — the one resolver the switch and the key
    ///   read too, so three controls can never disagree about whether there is a hook.</item>
    ///   <item><b>The drop gate is depth.</b> She anchors only where she is genuinely afloat AND the water
    ///   is no deeper than the rode she carries (<see cref="BoatHullDef.RodeMeters"/>, data — bigger hull,
    ///   longer rode, deeper anchorages, P2). Too deep and the anchor "finds no bottom": a refusal, no
    ///   state change. On the flats you do not anchor, you are aground — the existing grounding sim's
    ///   business, not this component's.</item>
    ///   <item><b>Holding is a position restraint,</b> not a freeze. She lies to a SWING CIRCLE around the
    ///   drop point (<see cref="AnchorMath.SwingRadius"/>) and inside it she bobs, sails about and feels
    ///   every force the sim applies; at the edge the rode goes taut and checks her firmly.</item>
    ///   <item><b>The tide keeps moving.</b> FALLING, the depth shrinks and the existing grounding sim
    ///   takes her if her draught meets the bottom — the anchor never prevented that; a badly chosen
    ///   anchorage did it. RISING, the depth grows, and the moment it passes the rode the hook loses the
    ///   bottom and she <b>DRAGS</b>: no longer held, creeping off downwind/downtide while the tackle
    ///   skips along the seabed (<see cref="AnchorMath.DragBrakeForce"/>). Come the ebb, she brings up
    ///   again where she has fetched to.</item>
    /// </list>
    ///
    /// <para><b>ONE restraint mechanism, two consumers.</b> The rode is checked with the mooring line's own
    /// maths — <see cref="BoatMooring.TetherForce"/> plus the inextensible
    /// <see cref="BoatMooring.ConstrainToRope"/> — with the swing circle standing in for the rope's length.
    /// A boat brought up on her anchor is held exactly the way a boat made fast to a cleat is, because it
    /// is the same code and (by default) the same tuning. This deliberately does not fork a second
    /// "near-rigid tether" implementation.</para>
    ///
    /// <para><b>Depth goes through the existing helpers, never fresh arithmetic</b> (ADR 0014: paint =
    /// sail). Depth over the anchor is <see cref="BoatCrossing.DepthAt"/> — the painted seabed elevation
    /// under the SAME <see cref="ITidalTerrain"/> the water render and the walk sim read, minus the
    /// deterministic tide surface. So the anchor cannot disagree with the sea the player sees, and the
    /// depth is recomputed every tick rather than stored (rule 5).</para>
    ///
    /// <para><b>Nothing here is saved.</b> The drop point is live runtime state, like the mooring's tie
    /// point: no depth, no tide, no swing radius is serialized (rule 5). Reload and the hook is catted.
    /// Persisting an anchored boat across a save is a later item, not this slice.</para>
    ///
    /// <para><b>No magic numbers</b> (rule 6): every constant comes from the owner's
    /// <see cref="GameServices.Anchor"/> policy (<c>GameConfig.Anchor</c>) or from hull data, resolved
    /// per tick so dragging a slider in play changes the next hold. The component is runtime-spawned by
    /// <see cref="BoatController"/> (the <see cref="MastheadTelltale"/>/<see cref="HelmControlRelay"/>
    /// precedent), so every already-built scene grows it with no builder re-run — which is also why it
    /// serializes no tunables of its own: fields on a component that exists in no prefab could never be
    /// edited by the owner.</para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class BoatAnchor : MonoBehaviour
    {
        // Greybox feedback (DevNotice → DevToast) so the owner reads the hook without the Console.
        // Event-time only (a keypress or a tide crossing), never per frame. Public so tests assert the
        // very strings the player is shown rather than a re-typed copy of them.
        public const string NoticeSet = "Anchor down — she's brought up";
        public const string NoticeNoBottom = "No bottom — the rode won't reach here";
        public const string NoticeAground = "She's on the bottom already — no hook needed";
        public const string NoticeDragging = "The anchor's dragging! The tide's over her rode";
        public const string NoticeReBite = "She's found the bottom again";
        public const string NoticeWeighed = "Anchor's aweigh";
        public const string NoticeNoHull = "No hull wired — nothing to anchor (dev)";
        public const string NoticeNoTackle = "She carries no anchor";

        private Rigidbody2D _rb;
        private BoatController _boat;
        private LineRenderer _rode;

        /// <summary>Where the ground tackle stands. Derived runtime state — never saved (rule 5).</summary>
        public AnchorState State { get; private set; } = AnchorState.Stowed;

        /// <summary>Where the hook went down (world). Only meaningful while <see cref="IsDown"/>.</summary>
        public Vector2 DropPoint { get; private set; }

        /// <summary>True while the anchor is over the side at all — holding OR dragging.</summary>
        public bool IsDown => State != AnchorState.Stowed;

        /// <summary>True only while she is actually being held (not dragging).</summary>
        public bool IsHolding => State == AnchorState.Set;

        /// <summary><b>Does this hull carry ground tackle at all?</b> Her own
        /// <see cref="BoatHullDef.HasAnchor"/> data, through the one resolver
        /// (<see cref="AnchorMath.CarriesAnchor"/>) the switch and the key read too. False = there is no
        /// hook: nothing drops, no switch is drawn, and the key does nothing on her.</summary>
        public bool HasAnchor => AnchorMath.CarriesAnchor(Hull);

        /// <summary>The rode this hull carries (m) — her own data, else the shared dinghy-class default.</summary>
        public float RodeMeters => AnchorMath.RodeFor(Hull, GameServices.Anchor);

        /// <summary>What her hook weighs (kg) — her own data, else the shared dinghy grapnel. What a
        /// DRAGGING anchor checks her with (<see cref="AnchorMath.DragBrakeStrengthFor"/>).</summary>
        public float AnchorMassKg => AnchorMath.AnchorMassFor(Hull, GameServices.Anchor);

        /// <summary>Live water depth over the ANCHOR (m), not over the boat: the vertical leg of the rode
        /// is measured where the hook lies, so a boat sailing round her swing circle does not wobble her
        /// own scope. Recomputed every read from terrain + tide, never stored.</summary>
        public float DepthOverAnchor => DepthAt(IsDown ? DropPoint : (Vector2)transform.position);

        /// <summary>The circle she may lie within right now (m) — <c>√(rode² − depth²)</c>, floored.
        /// Shrinks as the tide rises, which is what eventually takes the bottom from her.</summary>
        public float SwingRadius =>
            AnchorMath.SwingRadius(RodeMeters, DepthOverAnchor, GameServices.Anchor.MinSwingMeters);

        private BoatHullDef Hull
        {
            get
            {
                EnsureRefs();
                return _boat != null ? _boat.Hull : null;
            }
        }

        private void Awake()
        {
            EnsureRefs();
            BuildRodeVisual();
        }

        /// <summary>Resolve the siblings lazily so the transitions work even before <see cref="Awake"/> has
        /// run (EditMode, or a rig wired up before the first tick) — the <see cref="BoatController.Stop"/> /
        /// <see cref="BoatMooring"/> precedent. Unity does not call Awake on an AddComponent in edit mode.</summary>
        private void EnsureRefs()
        {
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_boat == null) _boat = GetComponent<BoatController>();
        }

        /// <summary>Wire explicitly (tests / rigs built without Awake-order guarantees).</summary>
        public void Configure(BoatController boat)
        {
            _boat = boat;
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
        }

        // ---- the verbs -------------------------------------------------------------------------------

        /// <summary>
        /// Drop the hook where she floats. Runs the depth gate (<see cref="AnchorMath.EvaluateDrop"/>) over
        /// the live tide and the painted seabed and reports what happened; only <see cref="AnchorDrop.Set"/>
        /// changes any state. Public so a test/tool drives the real path without input.
        /// </summary>
        public AnchorDrop TryDrop()
        {
            EnsureRefs();
            BoatHullDef hull = Hull;
            if (hull == null)
            {
                EventBus.Publish(new DevNotice(NoticeNoHull));
                return AnchorDrop.Aground;
            }

            // The data gate, ahead of the depth gate: a hull the owner has said carries no ground
            // tackle has nothing to let go, wherever she is floating. Reported as "aground" — the
            // refusal answer that changes no state — because AnchorDrop describes what the SEABED
            // said, and there is no fourth answer to invent for a boat that simply has no hook.
            if (!AnchorMath.CarriesAnchor(hull))
            {
                EventBus.Publish(new DevNotice(NoticeNoTackle));
                return AnchorDrop.Aground;
            }

            Vector2 here = _rb != null ? _rb.position : (Vector2)transform.position;
            AnchorSettings settings = GameServices.Anchor;
            float depth = DepthAt(here);
            AnchorDrop result = AnchorMath.EvaluateDrop(depth, AnchorMath.RodeFor(hull, settings),
                                                        hull.DraughtMeters);

            switch (result)
            {
                case AnchorDrop.Set:
                    DropPoint = here;
                    State = AnchorState.Set;
                    EventBus.Publish(new DevNotice(NoticeSet));
                    break;
                case AnchorDrop.NoBottom:
                    EventBus.Publish(new DevNotice(NoticeNoBottom));
                    break;
                default:
                    EventBus.Publish(new DevNotice(NoticeAground));
                    break;
            }

            UpdateRodeVisual();
            return result;
        }

        /// <summary>Weigh anchor — bring the hook home. Safe in any state; only speaks up if it was down.</summary>
        public void Weigh()
        {
            if (State != AnchorState.Stowed) EventBus.Publish(new DevNotice(NoticeWeighed));
            State = AnchorState.Stowed;
            UpdateRodeVisual();
        }

        /// <summary>
        /// The single verb an input binding needs: drop if she is catted, weigh if she is over the side
        /// (holding or dragging alike). Returns the drop result when it dropped, else null.
        /// </summary>
        public AnchorDrop? Toggle()
        {
            if (State == AnchorState.Stowed) return TryDrop();
            Weigh();
            return null;
        }

        // ---- the tick --------------------------------------------------------------------------------

        private void FixedUpdate()
        {
            if (State == AnchorState.Stowed) return;
            EnsureRefs();
            BoatHullDef hull = Hull;
            if (_rb == null || hull == null) return;

            AnchorSettings settings = GameServices.Anchor;
            float rode = AnchorMath.RodeFor(hull, settings);
            float depth = DepthAt(DropPoint);

            // --- Has the tide taken the bottom away, or given it back? (P5 teeth, both directions.) ---
            bool lost = AnchorMath.HasLostBottom(depth, rode);
            if (lost && State == AnchorState.Set)
            {
                State = AnchorState.Dragging;
                EventBus.Publish(new DevNotice(NoticeDragging));
            }
            else if (!lost && State == AnchorState.Dragging)
            {
                // The ebb hands the bottom back and the hook bites again — WHERE SHE HAS FETCHED TO, not
                // where she was dropped. Dragging costs you your berth, not your anchor: cozy, with teeth.
                DropPoint = _rb.position;
                State = AnchorState.Set;
                EventBus.Publish(new DevNotice(NoticeReBite));
                depth = DepthAt(DropPoint);
            }

            if (State == AnchorState.Set) HoldOnTheSwing(rode, depth, in settings);
            else Drag(AnchorMath.AnchorMassFor(hull, in settings), in settings);

            UpdateRodeVisual();
        }

        /// <summary>
        /// She lies to her swing circle: slack inside it (she sails about on wind and tide exactly as the
        /// unanchored sim says she should), a firm near-inextensible check at the edge. The restraint IS
        /// the mooring line's — <see cref="BoatMooring.TetherForce"/> for the force and
        /// <see cref="BoatMooring.ConstrainToRope"/> for the hard positional guarantee — with the swing
        /// circle as the rope's length. One mechanism, two consumers.
        /// </summary>
        private void HoldOnTheSwing(float rode, float depth, in AnchorSettings settings)
        {
            float swing = AnchorMath.SwingRadius(rode, depth, settings.MinSwingMeters);

            Vector2 tether = BoatMooring.TetherForce(_rb.position, DropPoint, swing,
                                                     settings.LimitStiffness, _rb.linearVelocity,
                                                     settings.LimitDamping, settings.RodeGiveMeters);
            if (tether != Vector2.zero)
                _rb.AddForce(tether * BoatController.ForceFeelScale, ForceMode2D.Force);

            // The rode is inextensible: she can NEVER sit past the swing circle plus its tiny give.
            Vector2 clamped = BoatMooring.ConstrainToRope(_rb.position, DropPoint, swing, settings.RodeGiveMeters);
            if (clamped != _rb.position)
            {
                _rb.position = clamped;
                // Kill the outward radial velocity so the clamp doesn't fight the integrator next tick.
                Vector2 outward = clamped - DropPoint;
                if (outward.sqrMagnitude > 1e-6f)
                {
                    outward.Normalize();
                    float outwardSpeed = Vector2.Dot(_rb.linearVelocity, outward);
                    if (outwardSpeed > 0f) _rb.linearVelocity -= outward * outwardSpeed;
                }
            }
        }

        /// <summary>The hook is skipping along the bottom: no restraint at all, only a brake that settles
        /// her onto a slow creep. Wind and tide keep working her (the sim never stopped), so she goes
        /// somewhere — slowly, and downwind/downtide, which is the whole cue.
        ///
        /// <para>How hard it checks her is HER OWN IRON: the shared brake scaled by
        /// <see cref="BoatHullDef.AnchorMassKg"/> against the reference hook it is quoted at
        /// (<see cref="AnchorMath.DragBrakeStrengthFor"/>) — a dory's grapnel skips, a dragger's hook
        /// gouges. A hull with no authored weight takes the reference and drags exactly as she always
        /// did.</para></summary>
        private void Drag(float anchorMassKg, in AnchorSettings settings)
        {
            Vector2 brake = AnchorMath.DragBrakeForce(
                _rb.linearVelocity, settings.DragCreepMetersPerSec,
                AnchorMath.DragBrakeStrengthFor(anchorMassKg, in settings));
            if (brake != Vector2.zero)
                _rb.AddForce(brake * BoatController.ForceFeelScale, ForceMode2D.Force);
        }

        /// <summary>
        /// Water depth (m) at a world position, through the EXISTING elevation helpers — the painted
        /// seabed under the live tide, the same single number the crossing gate, the sounder and the
        /// walkability sim read. Never a second copy of the depth arithmetic (ADR 0014, and the
        /// tide-pacing ruling that tide-fraction elevations must scale with the amplitude while draught
        /// elevations must not — a rule the shared helper already honours and a local subtraction would
        /// not).
        /// </summary>
        private static float DepthAt(Vector2 worldPos)
        {
            IGameClock clock = GameServices.Clock;
            double now = clock != null ? clock.TotalSeconds : 0.0;
            return BoatCrossing.DepthAt(GameServices.TidalTerrain, GameServices.Environment, now, worldPos);
        }

        // ---- greybox rode visual (v1: a straight line to the hook; no bespoke animation) --------------

        private void BuildRodeVisual()
        {
            var go = new GameObject("AnchorRode");
            go.transform.SetParent(transform, false);
            _rode = go.AddComponent<LineRenderer>();
            _rode.useWorldSpace = true;
            _rode.numCapVertices = 2;
            _rode.positionCount = 0;
            _rode.material = new Material(Shader.Find("Sprites/Default"));
            // The rope tier ADR 0032 names by role — the mooring line and the trap-haul line already sit
            // here. A rode drawn over the decor band reads as tackle laid across whatever it crosses, and
            // (unlike a decor sprite) cannot be walked in front of, which is correct for a line in the air.
            _rode.sortingOrder = SortingBands.AboveDecor;
            _rode.enabled = false;
        }

        /// <summary>Width of the greybox rode line (m). Visual only; a windlass + a real rode are the
        /// art-director's later pass (the requirements doc routes the clip there), not this slice.</summary>
        private const float RodeWidth = 0.07f;

        /// <summary>Holding: a dull galvanised line. Dragging: it goes red, which with the toast is the
        /// whole "she's away" cue. Visual only — no sim reads these.</summary>
        private static readonly Color HoldingColor = new Color(0.62f, 0.63f, 0.60f, 1f);
        private static readonly Color DraggingColor = new Color(0.80f, 0.30f, 0.22f, 1f);

        private void UpdateRodeVisual()
        {
            if (_rode == null) return;
            bool show = IsDown;
            if (_rode.enabled != show) _rode.enabled = show;
            if (!show) { _rode.positionCount = 0; return; }

            Color c = State == AnchorState.Dragging ? DraggingColor : HoldingColor;
            _rode.startWidth = RodeWidth; _rode.endWidth = RodeWidth;
            _rode.startColor = c; _rode.endColor = c;
            _rode.positionCount = 2;
            _rode.SetPosition(0, new Vector3(transform.position.x, transform.position.y, 0f));
            _rode.SetPosition(1, new Vector3(DropPoint.x, DropPoint.y, 0f));
        }
    }
}
