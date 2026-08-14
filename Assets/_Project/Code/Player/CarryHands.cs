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
    public sealed class CarryHands : MonoBehaviour, ICarrier, ICatchHands
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
        private ICarriable _carried;
        private bool _resolved;

        /// <summary>
        /// What is in the fisher's hands right now, or null. The single source of truth for "am I carrying
        /// something" — <see cref="CarriableFuelContainer"/> reads it, nothing writes it but
        /// <see cref="TryPickUp"/> and <see cref="TryPlace"/>.
        ///
        /// <para><b>⚠ The getter launders Unity's fake-null and the cast is load-bearing.</b> The backing
        /// field is INTERFACE-typed, and an interface reference does not carry
        /// <see cref="UnityEngine.Object"/>'s <c>==</c> overload — so a carried object destroyed out from
        /// under the hands (a region unloaded, a test tearing down) would read here as a live
        /// <see cref="ICarriable"/> and hand every consumer a corpse whose <c>!= null</c> passes. Casting
        /// back to <c>Object</c> re-enters the Unity-aware comparison. Same reason, same shape, as
        /// <see cref="GameServices.Hands"/>'s own getter.</para>
        /// </summary>
        public ICarriable Carried
            => _carried is Object o && o == null ? null : _carried;

        /// <summary>True when something is held. Sugar over <see cref="Carried"/>, for readability at the
        /// call sites that only care whether the hands are free.</summary>
        public bool IsCarrying => Carried != null;

        // ---- the Core relay ------------------------------------------------------------------------

        /// <summary>
        /// Publish these hands so lanes that cannot reference Player can still ask what is held
        /// (<see cref="GameServices.Hands"/> — the seam <c>ClamDig</c> reads across the Fishing boundary).
        /// </summary>
        private void OnEnable()
        {
            GameServices.Hands = this;
            GameServices.CatchHands = this;
        }

        /// <summary>
        /// Relinquish the relay — <b>in <c>OnDestroy</c>, and deliberately NOT in <c>OnDisable</c>.</b>
        ///
        /// <para>⚠️ The house law, learned the expensive way (fix/interior-reveal-travel): root-toggling
        /// IS how a region hop works, so "disabled" happens constantly and does not mean "gone". A service
        /// cleared on disable is a service wiped mid-crossing, and the symptom is silent and total — every
        /// consumer reads the null as "the thing does not exist". Whoever registers, unregisters, on
        /// destroy, guarded on still owning the slot (<c>GameRoot.OnDestroy</c> is the reference
        /// implementation).</para>
        /// </summary>
        private void OnDestroy()
        {
            if (ReferenceEquals(GameServices.Hands, this)) GameServices.Hands = null;
            if (ReferenceEquals(GameServices.CatchHands, this)) GameServices.CatchHands = null;
        }

        // ---- the catch seam: a landed clam goes in your hand, and the tool tucks under your arm -------

        /// <summary>
        /// What is SLUNG — the tool tucked away to free the hands for a landed catch, or null.
        ///
        /// <para><b>Why a second slot exists when the design says "one tool at a time".</b> It is not a
        /// second carry slot and it cannot be used as one: nothing may put anything here deliberately.
        /// It exists because the dig requires the shovel IN HAND and the dig's own product must also go in
        /// hand, so without somewhere for the shovel to go the very first clam has nowhere to land and the
        /// loop cannot close. That is the owner's stated default — "the rod auto-stows to slung on a
        /// landed catch, fish in hand" — and the sling is strictly a consequence of the catch: it fills
        /// only in <see cref="TryPutInHand"/> and empties the moment the hands are free.</para>
        /// </summary>
        public ICarriable Slung => _slung is Object o && o == null ? null : _slung;

        private ICarriable _slung;

        /// <summary>
        /// Put a landed catch in her hands (<see cref="ICatchHands.TryPutInHand"/>).
        ///
        /// <para>Refuses — returning false, so the caller lands it in its own hold instead — when the
        /// hands already hold a CATCH. That is the ruling's over-encumbrance rule in its cheapest honest
        /// form: full is full, and you deal with the clam you have before you dig another
        /// (diegetic-ui-and-inventory.md §4.2, "no room = you cannot pick it up", no weight meter and no
        /// slow-crawl).</para>
        ///
        /// <para>A TOOL in the hands is not a refusal — it is slung, and taken back the moment the catch
        /// leaves. A tool already slung is, though: that would mean a catch is in hand, which the first
        /// check has already covered, so it is belt-and-braces against a state nothing can currently
        /// reach.</para>
        /// </summary>
        public bool TryPutInHand(in CatchItem item)
        {
            if (Carried is CarriableCatch) return false;      // one catch at a time — full is full
            if (Slung != null) return false;                  // unreachable today; see the remarks

            ICarriable tool = Carried;
            if (tool != null)
            {
                // Tuck it under the arm: it stays parented to the fisher and stays HERS, it simply stops
                // being posed at the hip. Deliberately not TryPlace() — setting the shovel on the sand
                // every time a clam comes up would be a different, worse game.
                _slung = tool;
                _carried = null;
                if (tool.Transform != null) tool.Transform.gameObject.SetActive(false);
            }

            _carried = CarriableCatch.Create(item, transform);
            ApplyCarriedPose();
            EventBus.Publish(new CatchLanded(item));
            return true;
        }

        /// <summary>
        /// Hand the held catch to a container. Returns false when there is no catch to give or the
        /// container would not take it, and NOTHING changes in that case — the catch stays in her hands
        /// rather than evaporating between the two.
        ///
        /// <para>The item is moved, never copied and never re-stamped: the same <see cref="CatchItem"/>
        /// that landed is the one that stacks, freshness clock and all.</para>
        /// </summary>
        public bool TryGiveCatchTo(IHold hold)
        {
            if (hold == null) return false;
            if (Carried is not CarriableCatch held || !held.HasItem) return false;
            if (hold.UsedUnits >= hold.CapacityUnits) return false;

            CatchItem item = held.Item;
            if (!hold.TryAdd(item)) return false;

            // ⚠️ THIS is where FishCaught belongs on the in-hand path, and it is easy to leave out — the
            // catch source no longer publishes it (it did not put anything in a hold), so if this line is
            // missing, nothing does. The event means "a catch entered a hold": the hold-fill renderers
            // re-read the container on it, the onboarding director counts clams with it, the deck
            // presenters re-stack. Without it a clam dug the new way would be in the pail and invisible to
            // every one of them.
            EventBus.Publish(new FishCaught(item));

            held.Take();
            _carried = null;

            // ⚠️ Destroy() THROWS in edit mode ("Destroy may not be called from edit mode"), and this path
            // is reached by every EditMode test of the stack. Object.Destroy is also DEFERRED to end of
            // frame in play mode, which would leave a taken-from catch parented to the fisher for the rest
            // of the frame — harmless, but the immediate form is the honest one here since the object has
            // already given up its item.
            if (Application.isPlaying) Destroy(held.gameObject);
            else DestroyImmediate(held.gameObject);

            TakeBackSlung();
            return true;
        }

        /// <summary>
        /// Put the slung tool back in her hands, if the hands are free. Idempotent and silent when there
        /// is nothing slung — this is called on every path that empties the hands, so "nothing to do" is
        /// the ordinary case.
        /// </summary>
        public void TakeBackSlung()
        {
            ICarriable slung = Slung;
            if (slung == null || IsCarrying) return;

            _slung = null;
            if (slung.Transform != null) slung.Transform.gameObject.SetActive(true);
            _carried = slung;
            ApplyCarriedPose();
        }

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
        public CarryRefusal TryPickUp(ICarriable container)
        {
            // The Unity-aware leg of the check is the one that matters: `container == null` on an
            // interface reference misses a destroyed component entirely (see the Carried remarks), and a
            // destroyed one has no transform to re-parent.
            if (container == null || container.Transform == null) return CarryRefusal.NotCarriable;

            CarryRefusal refusal = CarryMath.CanPickUp(container.IsCarriable, IsCarrying);
            if (refusal != CarryRefusal.None) return refusal;

            _placedParent = container.Transform.parent;
            _carried = container;
            container.OnLifted(this);

            container.Transform.SetParent(transform, worldPositionStays: false);
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

            ICarriable container = Carried;
            _carried = null;

            // Back to the region it came from when that parent is still alive AND still on screen; the
            // scene root otherwise (the region was unloaded or toggled out under it, which is not a
            // reason to refuse the press).
            //
            // ⚠️ The activeInHierarchy leg is not belt-and-braces — it is a real defect the moment
            // anything carriable is standing in a REGION rather than spawned by a dev menu. A region hop
            // does not unload the region you left; it SetActive(false)s its roots. So carrying a tool
            // from St Peters to Nine Mile Creek and setting it down would re-parent it under St Peters'
            // sleeping root: the object survives, inactive — invisible, unregistered from the interact
            // registry, and unrecoverable without going back and re-activating a scene. Dropping to the
            // scene root instead leaves it where the fisher actually stood, which is what she just did.
            Transform parent = _placedParent != null && _placedParent.gameObject.activeInHierarchy
                               ? _placedParent
                               : null;
            container.Transform.SetParent(parent, worldPositionStays: false);
            container.Transform.position = feet;
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
            ICarriable carried = Carried;      // read ONCE — the getter does the fake-null laundering
            if (carried == null) return;
            Resolve();

            carried.Transform.localPosition = _hipOffsetMeters;

            float heading = DrawnHeadingDegrees;
            carried.ShowFacing(CarryMath.BakedFacingIndex(heading, carried.BakedFacings));

            if (_bodyRenderer != null)
                carried.RideSortingBand(_bodyRenderer.sortingLayerID,
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
