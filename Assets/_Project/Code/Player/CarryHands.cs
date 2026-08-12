using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The fisher's hands</b> — the one place that knows what is being carried, and the component that
    /// draws it there.
    ///
    /// <para><b>Why the state lives HERE and not on the thing carried.</b> "You may carry one object" is a
    /// single-occupancy invariant, and an invariant enforced by each of N objects individually is not
    /// enforced at all — two containers both believing they were picked up is a state no test would catch
    /// until the second one drew inside the first. The hands are the resource, so the hands own the slot,
    /// and <see cref="CarriableFuelContainer"/> asks them. It is also why this is a component and not a
    /// field on <see cref="FuelLevelPresenter"/>: a presenter draws what it is handed and must never be the
    /// thing that remembers (the lesson of the six private ride smoothers — state parked in a presenter is
    /// state nothing else can read or test).</para>
    ///
    /// <para><b>What is NOT here.</b> No key is read: the press arrives through <see cref="InteractVerb"/>
    /// (M2-39), which is the entire point of that seam — the dev key ledger is spent A–Z and a new feature
    /// registers a candidate rather than binding a letter. No fuel is consumed, nothing is refuelled and
    /// nothing is priced; that is phase-gated. No save is written: see the class remarks on persistence
    /// below.</para>
    ///
    /// <para><b>Persistence is deliberately absent.</b> A set-down container is SESSION-LOCAL — it exists
    /// until the scene does. Nothing is placed in a shipped region yet, so there is nothing to reload, and
    /// the world-placed wiring (the <c>PlacedTrap</c> / ADR 0020 precedent) belongs to the PR that actually
    /// stands containers on a wharf. Parenting is restored on set-down so a container put back down lands
    /// in the region it came from rather than leaking into the persistent core, which is the honest
    /// half-measure until that PR exists.</para>
    ///
    /// <para><b>Rules.</b> Visual + state only; the sim is untouched (rule 5). Every offset and magnitude is
    /// a serialized tunable (rule 6). The per-frame path does no allocation and no <c>Find</c> — the
    /// renderer and the character skin are resolved once (rule 7).</para>
    /// </summary>
    [DisallowMultipleComponent]
    // Runs AFTER YSortSprite (default 0) has written the body's order for this frame, so the carried
    // sprite mirrors the order the player ACTUALLY drew at rather than last frame's. Same reason, and the
    // same number, as DeckRiderVisual.
    [DefaultExecutionOrder(100)]
    public sealed class CarryHands : MonoBehaviour
    {
        [Header("Wiring (auto-resolved off this object if empty)")]
        [Tooltip("The player's own renderer — the SORTING DATUM the carried sprite rides. Null = the " +
                 "carried sprite keeps whatever order it had, which is wrong but never invisible.")]
        [SerializeField] private SpriteRenderer _bodyRenderer;

        [Tooltip("The 8-direction iso skin — the source of the DRAWN heading the carried object's facing " +
                 "is derived from. Absent (no iso art built) falls back to the four-way walk facing below.")]
        [SerializeField] private IsoCharacterSprite _character;

        [Tooltip("The four-way walk controller — the FALLBACK heading source when the iso skin is absent, " +
                 "matching the fallback the player's own picture uses.")]
        [SerializeField] private PlayerWalkController _walk;

        [Header("How it is held (greybox tunables, rule 6)")]
        [Tooltip("Where the carried object sits relative to the fisher's feet, in metres — the hip. " +
                 "⚠️ ONE offset for every facing: the fuel kit bakes no hand anchors, so there is nothing " +
                 "to read a per-facing grip from. A per-facing hand anchor is art-lane work (the boats " +
                 "carry exactly that in their anchor sidecars); until then this is the owner's knob.")]
        [SerializeField] private Vector2 _hipOffsetMeters = new Vector2(0.28f, 0.34f);

        [Tooltip("How many sorting orders the carried sprite draws ahead of the body when the fisher " +
                 "faces the camera (and behind it when she faces away) — see CarryMath.AheadOrdersFor. " +
                 "1 is one step of the decor band; 0 makes it tie with the body and sort by draw order, " +
                 "which flickers.")]
        [SerializeField, Min(0)] private int _aheadOrders = 1;

        private Transform _placedParent;      // where the carried thing hung before it was lifted
        private bool _resolved;

        /// <summary>What is in the fisher's hands right now, or null. The single source of truth for
        /// "am I carrying something" — <see cref="CarriableFuelContainer"/> reads it, nothing writes it
        /// but <see cref="TryPickUp"/> and <see cref="TryPlace"/>.</summary>
        public CarriableFuelContainer Carried { get; private set; }

        /// <summary>True when something is held. Sugar over <see cref="Carried"/>, for readability at the
        /// call sites that only care whether the hands are free.</summary>
        public bool IsCarrying => Carried != null;

        /// <summary>The heading the BODY is drawn at right now (compass degrees, 0 = N, CW) — the one
        /// quantity a carried object's facing is derived from. Prefers the iso skin's drawn heading (the
        /// picture on screen); falls back to the four-way walk facing, which is what draws the fisher when
        /// the iso art is missing. Neither present = 0 (north), the null-safe default.</summary>
        public float DrawnHeadingDegrees
        {
            get
            {
                Resolve();
                if (_character != null) return _character.HeadingDegrees;
                if (_walk != null)
                    return CarryMath.HeadingFor(PlayerWalkController.FacingUnitVector(_walk.CurrentFacing));
                return 0f;
            }
        }

        /// <summary>
        /// Lift a container into the hands. Returns <see cref="CarryRefusal.None"/> on success; any other
        /// value means nothing changed and the caller should say so.
        ///
        /// <para>The GameObject is re-parented, never rebuilt — which is exactly why the fill survives a
        /// carry with no code to carry it: the <see cref="FuelLevelPresenter"/> that holds the level is the
        /// same component on the same object throughout. Its previous parent is remembered so
        /// <see cref="TryPlace"/> can put it back in the region it came from.</para>
        /// </summary>
        public CarryRefusal TryPickUp(CarriableFuelContainer container)
        {
            if (container == null) return CarryRefusal.NotCarriable;

            CarryRefusal refusal = CarryMath.CanPickUp(container.IsCarriable, IsCarrying);
            if (refusal != CarryRefusal.None) return refusal;

            _placedParent = container.transform.parent;
            Carried = container;
            container.OnLifted(this);

            container.transform.SetParent(transform, worldPositionStays: false);
            ApplyCarriedPose();
            return CarryRefusal.None;
        }

        /// <summary>
        /// Set what is in the hands down at the fisher's feet. Returns <see cref="CarryRefusal.None"/> on
        /// success.
        ///
        /// <para>Valid ground is <see cref="TidalWalkability.IsWalkableNow"/> at the fisher's own position —
        /// the same read the walker gates on, so "somewhere I can stand" and "somewhere I can put this
        /// down" can never disagree. It fails open in a region with no tide gate, which is the right way
        /// round: a can that cannot be put down because a service is not installed is a soft-lock.</para>
        /// </summary>
        public CarryRefusal TryPlace()
        {
            Vector2 feet = transform.position;
            CarryRefusal refusal = CarryMath.CanPlace(IsCarrying, TidalWalkability.IsWalkableNow(feet));
            if (refusal != CarryRefusal.None) return refusal;

            CarriableFuelContainer container = Carried;
            Carried = null;

            // Back to the region it came from when that parent is still alive; the scene root otherwise
            // (the region was unloaded under it, which is not a reason to refuse the press).
            container.transform.SetParent(_placedParent != null ? _placedParent : null,
                                          worldPositionStays: false);
            container.transform.position = feet;
            _placedParent = null;

            container.OnPlaced();
            return CarryRefusal.None;
        }

        private void LateUpdate()
        {
            if (IsCarrying) ApplyCarriedPose();
        }

        /// <summary>
        /// State the carried object's pose from the body, every frame: where it hangs, which baked facing
        /// it shows, and which order it draws at. <b>Stated, never accumulated</b> — the deck rider's rule,
        /// for the deck rider's reason. Public so a test can settle the pose without waiting a frame.
        /// </summary>
        public void ApplyCarriedPose()
        {
            if (!IsCarrying) return;
            Resolve();

            Carried.transform.localPosition = _hipOffsetMeters;

            float heading = DrawnHeadingDegrees;
            Carried.ShowFacing(CarryMath.BakedFacingIndex(heading, Carried.BakedFacings));

            if (_bodyRenderer != null)
                Carried.RideSortingBand(_bodyRenderer.sortingLayerID,
                                        _bodyRenderer.sortingOrder
                                        + CarryMath.AheadOrdersFor(heading, _aheadOrders));
        }

        private void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            if (_bodyRenderer == null) _bodyRenderer = GetComponent<SpriteRenderer>();
            if (_character == null) _character = GetComponent<IsoCharacterSprite>();
            if (_walk == null) _walk = GetComponent<PlayerWalkController>();
        }
    }
}
