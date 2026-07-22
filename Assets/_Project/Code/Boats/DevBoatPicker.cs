using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>DEV-ONLY: cycle the piloted hull IN PLACE, at the helm.</b> Press <c>F</c> while driving and the
    /// boat re-skins under you — same spot, same heading, same wave — so two hulls can be felt back-to-back
    /// in the same water, in seconds. Dory → fishing boat → punt → punt (upgraded) → console skiff → sport
    /// single → sport twin → wrap: roughly the speed ladder, so walking the key walks up it.
    ///
    /// <para><b>What this is NOT.</b> It is not the fleet. Being on this roster sells nothing, owns nothing
    /// and costs nothing: the M2 boat ladder and its economy are a later phase (rule 8) and this deliberately
    /// does not build a scrap of them. It is a workbench — a way to answer "how does she feel?" before
    /// anyone designs what she costs.</para>
    ///
    /// <para><b>…which does not mean nothing on it is real.</b> Most rungs are dev-only, but
    /// <c>boat.punt</c> is a genuine purchasable M1 boat (<c>PuntOffer</c>, ₲1800) that
    /// <see cref="OwnedFleet"/>'s purchase registry knows about — she rides the picker so her new iso skin
    /// can be felt beside the others, not because the picker owns her. So do not reason "it is on the picker,
    /// therefore it is disposable": check for an offer. (Her upgraded engine, <c>boat.punt_upgraded</c>, IS
    /// picker-only — no offer exists for it, on purpose.)</para>
    ///
    /// <para><b>Why it swaps FOUR things.</b> A hull is feel (<see cref="BoatController.SetHull"/>) AND hold
    /// (<see cref="ShipHold.SetHull"/>) AND camera (the Core <see cref="ActiveBoatChanged"/> signal) AND
    /// picture (<see cref="BoatHullSkinner.ApplyHull"/>). These CAN silently diverge — that was the #208
    /// bug, where a bought boat changed its feel, hold and camera while the picture stayed the dory, because
    /// the swap wrote a sprite onto a renderer the skin had disabled. Any picker that moved fewer than four
    /// would ship that bug again, so <see cref="Show"/> is the one path and it always moves all four.</para>
    ///
    /// <para><b>The roster is DATA</b> (rule 2) — a serialized <see cref="BoatHullDef"/> array the builder
    /// fills, not a list of ids in C#. Adding a boat to the picker is adding an asset to the array.</para>
    ///
    /// <para><b>Only at the helm.</b> Gated on the controller actually driving, exactly as
    /// <see cref="OutboardMotorLayer"/> gates its helm read: on foot, <c>F</c> does nothing. Re-skinning a boat
    /// the player is standing next to (or worse, standing ON, mid-deck-walk) is not the affordance asked
    /// for, and it would fight the ControlSwitcher for the player's parent.</para>
    ///
    /// <para>New Input System only (<c>Keyboard.current</c>) — legacy <c>UnityEngine.Input</c> compiles and
    /// then THROWS at runtime in this project. The key is a serialized field, so the owner can rebind it
    /// without touching code.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class DevBoatPicker : MonoBehaviour
    {
        [Header("The roster (data, not code — order IS the cycle order)")]
        [Tooltip("Every hull F cycles through, in order. Add a boat by dropping its BoatHullDef in here. " +
                 "Null/empty = the picker is inert. Nulls in the middle are skipped, so a half-filled " +
                 "array never swaps you into a dead boat.")]
        [SerializeField] private BoatHullDef[] _roster = System.Array.Empty<BoatHullDef>();

        [Header("Keys (owner-editable)")]
        [Tooltip("Cycle to the next hull in the roster. F for Fleet. Free of every other binding in the " +
                 "project (WASD/arrows helm, Space brace/haul, E interact, Q mooring, P buy, B sell, " +
                 "T trap-drop, G grant, H haul, Y auto-yaw, Esc close).")]
        [SerializeField] private Key _nextHullKey = Key.F;

        [Tooltip("DEV A/B (ADR 0022 phase 4): flip the CURRENT hull between its real-time MESH and its " +
                 "baked SPRITE presentation, in place, same water — only works on a hull that carries " +
                 "both (the lobster boat). V for Variant; free of every other binding (see above; L is " +
                 "the spotlight). The comparison is the point: same heading, same wave, two render paths.")]
        [SerializeField] private Key _variantToggleKey = Key.V;

        [Header("Wiring (the builder sets these)")]
        [Tooltip("The boat whose hull is swapped. Also the gate: F only works while this is actually driving.")]
        [SerializeField] private BoatController _boat;
        [Tooltip("The hold whose capacity must follow the hull — or the new boat's picture lies about what it carries.")]
        [SerializeField] private ShipHold _hold;
        [Tooltip("The boat root's own plain hull renderer — the fallback picture the skinner brings back for " +
                 "a hull with no directional Visual. Handed to BoatHullSkinner, never written directly.")]
        [SerializeField] private SpriteRenderer _hullRenderer;

        private int _index;

        // The A/B override for the hull currently worn (null = the asset's own Variant). Reset on every
        // roster hop, so F always shows a boat as her data says and V is an explicit, per-hull act.
        private BoatHullVariant? _variantOverride;

        /// <summary>The hull currently shown, or null before the first swap / on an empty roster.</summary>
        public BoatHullDef Current => Valid(_index) ? _roster[_index] : null;

        /// <summary>The presentation override currently forced by <see cref="ToggleHullVariant"/> —
        /// null when the hull is shown as its data says (diagnostics / tests).</summary>
        public BoatHullVariant? VariantOverride => _variantOverride;

        /// <summary>How many hulls the picker will cycle through (nulls included — see <see cref="Next"/>).</summary>
        public int RosterCount => _roster != null ? _roster.Length : 0;

        /// <summary>True while the player is actually driving this boat. F is a no-op otherwise.</summary>
        public bool IsAtHelm => _boat != null && _boat.isActiveAndEnabled;

        /// <summary>
        /// Wire the picker in one call — the builder's path, mirroring <see cref="OwnedFleet.Configure"/>
        /// so the owner never needs the Inspector to get a working rig. Also used by tests.
        /// </summary>
        public void Configure(BoatHullDef[] roster, BoatController boat, ShipHold hold,
                              SpriteRenderer hullRenderer)
        {
            _roster = roster ?? System.Array.Empty<BoatHullDef>();
            _boat = boat;
            _hold = hold;
            _hullRenderer = hullRenderer;
            _index = StartIndex();
        }

        private void Awake() => _index = StartIndex();

        /// <summary>
        /// Start the cycle ON the hull the boat is already wearing, so the FIRST press of F moves to the
        /// NEXT boat rather than re-applying the one under you (which would look like a dropped keypress).
        /// A hull that isn't in the roster — or no hull at all — starts before the beginning, so the first
        /// press lands on entry 0.
        /// </summary>
        private int StartIndex()
        {
            if (_boat == null || _boat.Hull == null || _roster == null) return -1;
            for (int i = 0; i < _roster.Length; i++)
                if (_roster[i] == _boat.Hull) return i;
            return -1;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !IsAtHelm) return;
            if (kb[_nextHullKey].wasPressedThisFrame) Next();
            if (kb[_variantToggleKey].wasPressedThisFrame) ToggleHullVariant();
        }

        /// <summary>
        /// Flip the CURRENT hull between mesh and sprite presentation in place (ADR 0022 phase 4's
        /// owner comparison). Only meaningful on a hull whose visual carries BOTH a full sprite
        /// compass and a usable mesh def — anything else gets a toast and no change. Public so tests
        /// can drive the flip without the input loop.
        /// </summary>
        public void ToggleHullVariant()
        {
            var hull = _boat != null ? _boat.Hull : null;
            var visual = hull != null ? hull.Visual : null;
            if (visual == null || !visual.HasFullCompass() || !visual.HasHullMesh())
            {
                EventBus.Publish(new DevNotice("This hull has only one look — nothing to A/B."));
                return;
            }

            var effective = _variantOverride ?? visual.Variant;
            _variantOverride = effective == BoatHullVariant.Mesh
                ? BoatHullVariant.Sprite
                : BoatHullVariant.Mesh;

            // Picture only: feel, hold and camera are untouched — the whole point is the SAME boat,
            // same spot, same wave, drawn the other way.
            BoatHullSkinner.ApplyHull(gameObject, _hullRenderer, hull, _boat,
                                      new BoatHullSkinner.Options { VariantOverride = _variantOverride });
            EventBus.Publish(new DevNotice($"Hull presentation → {_variantOverride}"));
            Debug.Log($"[DevBoatPicker] {hull.Id} presentation → {_variantOverride} " +
                      $"(asset says {visual.Variant}). Press {_variantToggleKey} to flip back.");
        }

        /// <summary>
        /// Advance to the next hull in the roster and show it, wrapping at the end. Nulls are skipped, and
        /// a roster with no usable hull at all is a no-op rather than a null-swap into a dead boat. Public
        /// so tests can drive the cycle without the input loop.
        /// </summary>
        public void Next()
        {
            if (_roster == null || _roster.Length == 0) return;

            // Walk at most one full lap: that terminates on an all-null roster instead of spinning, and it
            // means a single valid entry re-shows itself rather than the picker appearing to jam.
            for (int step = 1; step <= _roster.Length; step++)
            {
                int candidate = (((_index + step) % _roster.Length) + _roster.Length) % _roster.Length;
                if (_roster[candidate] == null) continue;
                _index = candidate;
                Show(_roster[candidate]);
                return;
            }
        }

        /// <summary>
        /// Put the player in this hull, in place. THE one swap path — feel, hold, camera and picture move
        /// together or not at all (see the class note on #208). Public so tests can assert the lockstep
        /// without the input loop. A null hull is a no-op.
        /// </summary>
        public void Show(BoatHullDef hull)
        {
            if (hull == null) return;

            _variantOverride = null;   // a roster hop always shows the hull as her DATA says (V re-forces)

            if (_boat != null) _boat.SetHull(hull);   // FEEL: mass, thrust, drag, propulsion branch
            if (_hold != null) _hold.SetHull(hull);   // HOLD: capacity follows the boat

            // PICTURE: through the data-driven skin seam, never by poking a renderer. Handles both
            // directions — a hull with a Visual installs/refreshes the compass (and its rock grid, oars or
            // outboard); a hull without one tears the compass down and brings the base renderer back. The
            // boat root does not move, so the swap happens under the player in the same water.
            BoatHullSkinner.ApplyHull(gameObject, _hullRenderer, hull, _boat);

            // CAMERA: via Core, so Boats never references the App's CameraFollow (rule 4). Unconditional
            // here — unlike OwnedFleet, which keys framing on being aboard because a purchase happens on
            // foot at the wharf. The picker only ever runs AT THE HELM, so the player is aboard by
            // construction and the reframe is always wanted.
            EventBus.Publish(new ActiveBoatChanged(hull.Id, hull.CameraWorldHeightMeters));

            // The owner's read that the swap landed. Toasts go through the Core DevNotice signal the
            // greybox DevToast already listens to — no HUD reach-in (ui-ux owns the HUD, rule 4), no new
            // on-screen widget of my own.
            EventBus.Publish(new DevNotice($"Now piloting: {hull.DisplayName}"));
            Debug.Log($"[DevBoatPicker] Hull → {hull.Id} ({hull.DisplayName}) — " +
                      $"{hull.LengthMeters} m, {hull.MassKg} kg, hold {hull.HoldUnits}. Press {_nextHullKey} for the next.");
        }

        private bool Valid(int i) => _roster != null && i >= 0 && i < _roster.Length && _roster[i] != null;
    }
}
