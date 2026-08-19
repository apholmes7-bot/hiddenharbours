using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>A fuel container you can pick up and put down</b> — the first real consumer of the one interact
    /// verb (M2-39), and the thing that finally reads <c>FuelContainerDef.Carriable</c>.
    ///
    /// <para><b>Why this is the first consumer, deliberately.</b> The fuel kit shipped 84 Defs with
    /// <c>Carriable</c> set and its own tooltip conceding that "NOTHING reads this yet — carrying needs an
    /// interact verb that does not exist". The verb now exists, it binds no key, and this is what it was
    /// built for: a feature that wants a button registers a candidate. Not one letter was spent to add
    /// carrying to the game.</para>
    ///
    /// <para><b>One object, two rungs of the ladder.</b> The same component is the candidate whether it is
    /// on the ground or in your hands — it simply reports a different <see cref="Priority"/>:
    /// <see cref="InteractPriority.Portable"/> lying about, <see cref="InteractPriority.Held"/> once
    /// lifted. That is exactly the distinction <see cref="InteractPriority"/> was written for (its
    /// <c>Portable</c> rung names a fuel can outright), and it is what makes the loop close with no state
    /// machine of its own: the resolver picks the held thing over the loose thing over the fixture, so E
    /// means "put it down" while carrying and "pick it up" while not, with nothing consulting a mode.</para>
    ///
    /// <para><b>A container that cannot be lifted never registers at all.</b> A bulk tank is not a
    /// candidate — not an unavailable one, not a refusing one, simply absent. Registering it would put a
    /// 10,000 L tank into the resolver at the same rung as the jerry can beside it, where distance alone
    /// decides, and it could take the press meant for the can. An unliftable tank has nothing to say about
    /// a press, so it does not enter the conversation. (<see cref="CarryMath.CanPickUp"/> still checks, as
    /// belt to this braces.)</para>
    ///
    /// <para><b>What is NOT here.</b> No key (the verb), no fuel consumed, no refuelling, no price — all
    /// phase-gated. No persistence: a container set down is session-local, see <see cref="CarryHands"/>.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FuelLevelPresenter))]
    [AddComponentMenu("Hidden Harbours/Carriable Fuel Container")]
    public sealed class CarriableFuelContainer : MonoBehaviour, IInteractable, ICarriable
    {
        [Tooltip("Stable id — the art highlight matches on it, and it is the resolver's LAST tie-break. " +
                 "⚠️ Must be unique among live registrants: two cans sharing an id make the resolver's " +
                 "order non-total and registration order decides between them. Left empty, a unique one " +
                 "is derived from the Def id and the instance, which is safe but not readable.")]
        [SerializeField] private string _id;

        [Tooltip("How close (m) the on-foot fisher must be to pick this up. Smaller than a fixture's " +
                 "step-up range: you reach for a can you are standing over, not one across the wharf.")]
        [SerializeField, Min(0f)] private float _reachMeters = 1.5f;

        [Tooltip("Require the fisher to be FACING it. Off, matching every interaction on this seam so " +
                 "far — a thing at your feet should not need you to look down at it.")]
        [SerializeField] private bool _requiresFacing;

        // In your hands is in reach BY DEFINITION. Stated as its own number so no hip-offset tuning can
        // ever push the held can outside its own pick-up reach and strand it there, unable to be set down.
        private const float HeldReachMeters = 4f;

        private FuelLevelPresenter _presenter;
        private SpriteRenderer _renderer;
        private YSortSprite _ySort;
        private CarryHands _hands;          // cached — Priority reads IsCarried EVERY FRAME (rule 7)
        private string _resolvedId;

        // ---- what the Def says --------------------------------------------------------------------

        /// <summary>The Def this container draws from — read off the presenter, which already owns it, so
        /// there is ONE reference to a container's identity on the object rather than two that can drift.</summary>
        public FuelContainerDef Container => Presenter != null ? Presenter.Container : null;

        /// <summary>True when the Def says this vessel is light enough to lift. A missing Def is NOT
        /// carriable — an unbuilt object should be inert, never universally liftable.</summary>
        public bool IsCarriable => Container != null && Container.Carriable;

        /// <summary>How many facings the Def baked — the modulus <see cref="CarryMath.BakedFacingIndex"/>
        /// resolves into. 0 with no Def, which that function answers null-safely.</summary>
        public int BakedFacings => Container != null ? Container.Facings : 0;

        /// <summary>The Def id of WHAT this is (<c>fuelstore.gas_jerry_s20</c>) — the TYPE id a gate
        /// matches on, and deliberately not <see cref="Id"/>, which is this INSTANCE's unique
        /// registry key. See <see cref="ICarriable.DefId"/> on why the two must differ.</summary>
        public string DefId => Container != null ? Container.Id : null;

        /// <summary>The transform the hands re-parent and pose (<see cref="ICarriable.Transform"/>).</summary>
        public Transform Transform => transform;

        /// <summary>True while this is the thing in the fisher's hands. <b>Derived from the hands, not
        /// stored here</b> — a second copy of "am I carried" is a copy that can disagree with the hands
        /// that actually hold it. <c>ReferenceEquals</c>, not <c>==</c>: the hands now report an
        /// <see cref="ICarriable"/>, and comparing an interface reference against a concrete
        /// <see cref="UnityEngine.Object"/> with <c>==</c> is exactly the mixed-operand case whose
        /// resolution is worth nobody's time to reason about. Identity is what is meant, so identity is
        /// what is asked.</summary>
        public bool IsCarried => _hands != null && ReferenceEquals(_hands.Carried, this);

        private FuelLevelPresenter Presenter
        {
            get
            {
                if (_presenter == null) _presenter = GetComponent<FuelLevelPresenter>();
                return _presenter;
            }
        }

        // ---- the interact seam (M2-39) ------------------------------------------------------------

        /// <inheritdoc/>
        public string Id => _resolvedId ??= ResolveId();

        /// <summary>Where the thing actually is — including, while carried, out at the fisher's hip rather
        /// than at her feet. Reporting the truth keeps <see cref="WorldPosition"/> meaning one thing for
        /// every candidate (it is what the art highlight will draw at); <see cref="HeldReachMeters"/> is
        /// what stops that honesty costing anything.</summary>
        public Vector2 WorldPosition => transform.position;

        /// <inheritdoc/>
        public float ReachMeters => IsCarried ? HeldReachMeters : _reachMeters;

        /// <summary>Held outranks loose (see the class remarks) — the two rungs that close the loop with
        /// no mode flag anywhere.</summary>
        public int Priority => IsCarried ? InteractPriority.Held : InteractPriority.Portable;

        /// <summary>On your own two feet only.
        ///
        /// <para><b>Deck carrying is out of scope on purpose.</b> A can set down on a deck has to RIDE that
        /// hull — rock, heave and all — which is the deck-occupant seam's twelve measured slots, not a
        /// re-parent. So the deck is not offered: board while carrying and you keep hold of it (which is
        /// what a person does), and you set it down when you step ashore.</para></summary>
        public InteractContext Contexts => InteractContext.OnFoot;

        /// <inheritdoc/>
        public bool RequiresFacing => _requiresFacing;

        /// <summary>Always available. A refusal is something the press EARNS a sentence for (P5), not a
        /// reason to vanish from the resolver — the same reading <c>WetBucketPoint</c> takes.</summary>
        public bool IsAvailable => true;

        /// <summary>
        /// "Pick up the shovel" / "Set the shovel down" — the same two states <see cref="Priority"/> and
        /// <see cref="Interact"/> are derived from, so the words, the rung and the action can never
        /// disagree.
        ///
        /// <para><b>⚠ Cached, because this is read on the offer path, not on the press.</b>
        /// <c>InteractVerb</c> asks for a label every frame it has a candidate, and an interpolated string
        /// built there would be one allocation per frame for as long as the player stands near a pail —
        /// exactly the per-frame garbage rule 7 rules out. The two lines are built ONCE and rebuilt only if
        /// the display name they were made from changes (a def swapped in by <c>Configure</c> after
        /// Awake), which is the only way they can go stale.</para>
        /// </summary>
        public string VerbLabel
        {
            get
            {
                string what = Container != null ? Container.DisplayName : "it";
                if (!string.Equals(what, _labelledWhat, System.StringComparison.Ordinal))
                {
                    _labelledWhat = what;
                    _pickUpLabel = "Pick up the " + what;
                    _setDownLabel = "Set the " + what + " down";
                }
                return IsCarried ? _setDownLabel : _pickUpLabel;
            }
        }

        // The label pair above, and the display name they were built from. Never read outside VerbLabel.
        private string _labelledWhat, _pickUpLabel, _setDownLabel;

        /// <summary>
        /// The press: lift it if it is on the ground, set it down if it is in your hands.
        ///
        /// <para>Which of the two is decided by <see cref="IsCarried"/> — the same fact
        /// <see cref="Priority"/> is derived from — so the action taken and the rung it won on can never
        /// disagree.</para>
        /// </summary>
        public void Interact(in InteractActor actor)
        {
            ResolveHands();
            if (_hands == null)
            {
                EventBus.Publish(new DevNotice("No one here to pick that up."));
                return;
            }

            bool wasCarried = IsCarried;
            CarryRefusal refusal = wasCarried ? _hands.TryPlace() : _hands.TryPickUp(this);

            if (refusal != CarryRefusal.None)
            {
                EventBus.Publish(new DevNotice(CarryMath.Message(refusal)));
                return;
            }

            string what = Container != null ? Container.DisplayName : "it";
            EventBus.Publish(new DevNotice(wasCarried ? $"You set the {what} down." : $"You pick up the {what}."));
        }

        // ---- registration -------------------------------------------------------------------------

        private void OnEnable() => RefreshRegistration();

        private void OnDisable() => Interactables.Unregister(this);

        /// <summary>
        /// Register or relinquish according to what the Def now says — an unliftable vessel is not a
        /// candidate at all (see the class remarks).
        ///
        /// <para><b>Call this if the Def arrives AFTER the component does.</b> Registration is decided from
        /// <see cref="IsCarriable"/>, which is a fact about the Def, so a component added to an object whose
        /// <see cref="FuelLevelPresenter"/> is still empty would decide "not carriable" and stay silently
        /// out of the registry. Builders avoid it by setting the Def first (the dev spawner does); this is
        /// the way back for anything that cannot.</para>
        /// </summary>
        public void RefreshRegistration()
        {
            if (IsCarriable) Interactables.Register(this);
            else Interactables.Unregister(this);
        }

        // ---- what the hands drive -----------------------------------------------------------------

        /// <summary>Draw a given baked facing. The presenter picks the frame from fill + facing exactly as
        /// it always has; this only hands it the facing the hands derived.</summary>
        public void ShowFacing(int facingIndex)
        {
            if (Presenter != null) Presenter.Facing = facingIndex;
        }

        /// <summary>
        /// Draw at a sorting slot the CARRIER computed, inside the band it already occupies (ADR 0032).
        ///
        /// <para>No order is authored here and none is stored: the number arrives derived from the body's
        /// own live <c>YSortSprite</c> output every frame. That is the deck rider's arrangement, for the
        /// deck rider's reason — one Y-sort policy in the scene, not a second one parked on a prop.</para>
        /// </summary>
        public void RideSortingBand(int sortingLayerId, int sortingOrder)
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null) return;
            _renderer.sortingLayerID = sortingLayerId;
            _renderer.sortingOrder = sortingOrder;
        }

        /// <summary>Called by <see cref="CarryHands"/> as it lifts this: remember WHOSE hands these are —
        /// handed in rather than searched for, so <see cref="IsCarried"/> can never bind to a different
        /// pair — and stand the object's own Y-sort down so the two do not fight over the renderer.</summary>
        public void OnLifted(ICarrier carrier)
        {
            // Narrowed deliberately, and it costs nothing: the press path below needs TryPickUp/TryPlace,
            // which are the CARRIER's own verbs and are not on the Core contract (ICarrier is read-only by
            // design — what crosses a module boundary is the QUESTION "what is held", never the act of
            // holding). Player→Player, so the concrete type is free here. There is exactly one pair of
            // hands in the game; a carrier that is not CarryHands would leave this null and IsCarried
            // false, which is the correct answer to "am I in the fisher's hands" when it is not.
            _hands = carrier as CarryHands;
            if (_ySort == null) _ySort = GetComponent<YSortSprite>();
            if (_ySort != null) _ySort.enabled = false;
        }

        /// <summary>Called by <see cref="CarryHands"/> once this is back on the ground at its final
        /// position: hand Y-sorting back. Re-enabling is what re-sorts it — <c>YSortSprite.OnEnable</c>
        /// applies the order for where it now stands and then parks its own dispatch again if it is
        /// static, which is that component's documented one-shot re-sort.</summary>
        public void OnPlaced()
        {
            if (_ySort == null) _ySort = GetComponent<YSortSprite>();
            if (_ySort != null) _ySort.enabled = true;
        }

        /// <summary>
        /// Give this instance its stable id — for a spawner, which is the only thing that knows how many
        /// cans it has made and can therefore make them READABLE as well as unique.
        ///
        /// <para>Without it the id falls back to the instance-scoped form, which is unique but says
        /// nothing. Safe to call after registration: the registry is keyed on the component, and
        /// <see cref="Id"/> is re-derived here rather than left stale.</para>
        /// </summary>
        public void Configure(string id)
        {
            _id = id;
            _resolvedId = null;
        }

        /// <summary>
        /// Find the fisher's hands: the Core relay first (<see cref="GameServices.Hands"/>, which is where
        /// a live <see cref="CarryHands"/> publishes itself and is correct across a region hop), then a
        /// scene scan as the fallback.
        ///
        /// <para>The fallback is not dead weight — it is what keeps EditMode working. A plain
        /// MonoBehaviour's <c>OnEnable</c> does not fire there, so nothing publishes the relay and a test
        /// that builds hands with <c>AddComponent</c> would otherwise never be found.</para>
        /// </summary>
        private void ResolveHands()
        {
            if (_hands != null) return;
            if (GameServices.Hands is CarryHands published) _hands = published;
            if (_hands == null) _hands = FindAnyObjectByType<CarryHands>();
        }

        private string ResolveId()
        {
            if (!string.IsNullOrEmpty(_id)) return _id;
            string def = Container != null && !string.IsNullOrEmpty(Container.Id) ? Container.Id : "fuelstore.unknown";
            // Instance-scoped, so two spawned cans of the same Def cannot collide. Readable ids are the
            // spawner's job (see the dev menu); this is the safety net, not the intent.
            // GetEntityId, not the deprecated GetInstanceID — 6000.5 marks that obsolete-as-ERROR, which
            // compiles locally and reds CI (the repo already moved: see IsoFacetHullFeature).
            return $"{def}#{GetEntityId()}";
        }
    }
}
