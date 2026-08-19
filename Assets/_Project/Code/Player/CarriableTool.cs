using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>A tool lying in the world that you pick up, carry and put back down</b> — the owner's ruling
    /// made physical: "currently the player always has a rod available to use; this should be a carried
    /// item like any item", and "they should need a shovel to dig clams".
    ///
    /// <para><b>This is the fuel can's twin, and that is the whole design.</b> It implements the same two
    /// Core contracts (<see cref="ICarriable"/> to be held, <see cref="IInteractable"/> to answer the
    /// press), reports the same two rungs (<see cref="InteractPriority.Portable"/> on the ground,
    /// <see cref="InteractPriority.Held"/> in your hands), and closes the same loop with no mode flag: the
    /// resolver picks the held thing over the loose thing, so E means "put it down" while carrying and
    /// "pick it up" while not. Nothing here is a new mechanism — a second implementor is exactly what the
    /// carry seam was extracted for.</para>
    ///
    /// <para><b>What a tool does that a fuel can does not: it makes a gate somewhere else true.</b>
    /// Digging asks "is <c>tool.shovel</c> in the fisher's hands"; casting asks the same of
    /// <c>tool.rod</c>. Both askers live in <c>HiddenHarbours.Fishing</c>, which cannot see this class at
    /// all — they ask <see cref="CarriedItem.InHand"/> through Core (rule 4). <b>This component therefore
    /// has no idea what it enables</b>, and must not learn: a shovel that knew about clams would be the
    /// coupling the seam exists to prevent.</para>
    ///
    /// <para><b>⚠️ The priority inversion this created, and where it is answered.</b> A held tool reports
    /// <c>Held</c> (20) and a clam hole is a fixture (0) — so with the shovel in hand E would mean "put
    /// the shovel down" while standing on the very hole you meant to dig, and digging would be
    /// IMPOSSIBLE. The answer is not here and not a special case: the hole reports
    /// <see cref="InteractPriority.ToolTarget"/> (30), because using the thing you are holding on the
    /// thing in front of you is more specific than putting it down. Put-down is what you get by stepping
    /// away, which is legible and is what a person does.</para>
    ///
    /// <para><b>Non-directional art, deliberately, for now.</b> <see cref="BakedFacings"/> is 0 — the
    /// documented "no directional art" answer — so <see cref="ShowFacing"/> is a no-op and the tool draws
    /// the one sprite the builder wired at every heading. The rod and shovel each have a committed 32 px
    /// gear sprite; the DIRECTIONAL kits (<c>shovelIsoRig.js</c>, a carried-rod stance) are art-pipeline's
    /// and land later without touching this file: this class already asks the Def and the carrier for
    /// everything a directional version would need.</para>
    ///
    /// <para><b>No persistence.</b> A tool set down is SESSION-LOCAL, the same honest half-measure the
    /// fuel can ships with — world-placed persistence is net-new save schema and its own ADR
    /// (<c>diegetic-ui-and-inventory.md</c> §4.3). A tool put down therefore lives until the scene does.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hidden Harbours/Carriable Tool")]
    public sealed class CarriableTool : MonoBehaviour, IInteractable, ICarriable, ICarryAnchored
    {
        [Header("What it is (content is data — rule 2)")]
        [Tooltip("The tool this object IS. Null makes the object inert rather than universally " +
                 "liftable — an unbuilt thing should do nothing, never everything.")]
        [SerializeField] private ToolDef _tool;

        [Header("Wiring (auto-resolved off this object if empty)")]
        [Tooltip("What it looks like on the ground and in the hand. Wired by the builder that stands the " +
                 "tool in the world (the clam hole's arrangement), not carried on the Def.")]
        [SerializeField] private SpriteRenderer _renderer;

        [Header("Reach (rule 6 — a tunable, not a literal)")]
        [Tooltip("How close (m) the on-foot fisher must be to pick this up. Matches the fuel can's reach: " +
                 "you bend for a tool you are standing over, not one across the wharf.")]
        [SerializeField, Min(0f)] private float _reachMeters = 1.5f;

        [Tooltip("Stable INSTANCE id — the resolver's last tie-break and what the art highlight matches " +
                 "on. ⚠️ Must be unique among live registrants; left empty a unique one is derived from " +
                 "the tool id and this instance. NOT the same thing as the tool id (see DefId).")]
        [SerializeField] private string _id;

        // In your hands is in reach BY DEFINITION — the fuel can's constant, for the fuel can's reason: no
        // hip-offset tuning may ever push a held tool outside its own pick-up reach and strand it there.
        private const float HeldReachMeters = 4f;

        private CarryHands _hands;      // cached — Priority reads IsCarried EVERY FRAME (rule 7)
        private YSortSprite _ySort;
        private string _resolvedId;

        /// <summary>The Def this tool draws its identity from.</summary>
        public ToolDef Tool => _tool;

        // ---- ICarriable -----------------------------------------------------------------------------

        /// <summary>The KIND — <c>tool.shovel</c>. What a gate matches on, shared by every shovel there
        /// will ever be. Deliberately not <see cref="Id"/>; see <see cref="ICarriable.DefId"/>.</summary>
        public string DefId => _tool != null ? _tool.Id : null;

        /// <summary>Every tool is light enough to lift — that is what makes it a tool rather than a
        /// fixture. A missing Def is NOT carriable: an unbuilt object should be inert.</summary>
        public bool IsCarriable => _tool != null;

        // ---- ICarryAnchored -------------------------------------------------------------------------

        /// <summary>
        /// Which hand-prop row holds this tool — <b>the Def's, never this class's</b>. The rod says
        /// <c>rodTrail</c>; the shovel says nothing, because the rig bakes no spade row and an empty key
        /// is the documented "keep the carrier's own offset" answer.
        ///
        /// <para>It comes off <see cref="ToolDef"/> rather than being switched on <see cref="DefId"/>
        /// here for the reason rule 2 exists: the day the art lane bakes a shovel row, the owner fills a
        /// field in an asset and nothing recompiles. A <c>switch</c> on the tool id would have made that
        /// a code change, and would have put a fact about an art drop inside a gameplay component.</para>
        /// </summary>
        public string HandPropKey => _tool != null ? _tool.HandPropKey : null;

        /// <inheritdoc/>
        public Transform Transform => transform;

        /// <summary>0 — no directional art yet (see the class remarks). The documented null-safe answer,
        /// which <c>CarryMath.BakedFacingIndex</c> resolves to cell 0 rather than dividing by.</summary>
        public int BakedFacings => 0;

        /// <summary>A no-op while the art is non-directional. Kept honest rather than removed: when the
        /// tool rigs bake, this is the one method that changes and the carrier does not.</summary>
        public void ShowFacing(int facingIndex) { }

        /// <summary>Draw at the sorting slot the CARRIER computed, inside the band it already occupies
        /// (ADR 0032). No order is authored here and none is stored — the deck rider's arrangement.</summary>
        public void RideSortingBand(int sortingLayerId, int sortingOrder)
        {
            ResolveRenderer();
            if (_renderer == null) return;
            _renderer.sortingLayerID = sortingLayerId;
            _renderer.sortingOrder = sortingOrder;
        }

        /// <summary>Lifted: remember whose hands, and stand this object's own Y-sort down so the two do
        /// not fight over the renderer.</summary>
        public void OnLifted(ICarrier carrier)
        {
            // Narrowed for the press path's TryPickUp/TryPlace, which are the carrier's own verbs and not
            // on the read-only Core contract — the same arrangement, for the same reason, as the fuel can.
            _hands = carrier as CarryHands;
            ResolveYSort();
            if (_ySort != null) _ySort.enabled = false;
        }

        /// <summary>Back on the ground at its final position: hand Y-sorting back, which is what re-sorts
        /// it for where it now stands.</summary>
        public void OnPlaced()
        {
            ResolveYSort();
            if (_ySort != null) _ySort.enabled = true;
        }

        // ---- IInteractable --------------------------------------------------------------------------

        /// <inheritdoc/>
        public string Id => _resolvedId ??= ResolveId();

        /// <inheritdoc/>
        public Vector2 WorldPosition => transform.position;

        /// <inheritdoc/>
        public float ReachMeters => IsCarried ? HeldReachMeters : _reachMeters;

        /// <summary>Held outranks loose — the two rungs that close the pick-up/put-down loop with no mode
        /// flag. ⚠️ It does NOT outrank a work-site the held tool operates; see the class remarks on the
        /// inversion and <see cref="InteractPriority.ToolTarget"/>.</summary>
        public int Priority => IsCarried ? InteractPriority.Held : InteractPriority.Portable;

        /// <summary>On your own two feet. A tool set down on a deck would have to RIDE that hull (the
        /// deck-occupant seam's twelve measured slots, not a re-parent), so the deck is not offered:
        /// board while carrying and you keep hold of it, which is what a person does.</summary>
        public InteractContext Contexts => InteractContext.OnFoot;

        /// <inheritdoc/>
        public bool RequiresFacing => false;

        /// <summary>Always available — a refusal is something the press EARNS a sentence for (P5), not a
        /// reason to vanish from the resolver.</summary>
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
                string what = _tool != null && !string.IsNullOrEmpty(_tool.DisplayName) ? _tool.DisplayName : "it";
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

        /// <summary>True while this is the thing in the fisher's hands. Derived from the hands, never
        /// stored — a second copy of "am I carried" is a copy that can disagree.</summary>
        public bool IsCarried => _hands != null && ReferenceEquals(_hands.Carried, this);

        /// <summary>The press: lift it if it is on the ground, set it down if it is in your hands.</summary>
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

            string what = _tool != null && !string.IsNullOrEmpty(_tool.DisplayName) ? _tool.DisplayName : "it";
            EventBus.Publish(new DevNotice(wasCarried ? $"You set the {what} down." : $"You pick up the {what}."));
        }

        // ---- registration ---------------------------------------------------------------------------

        private void OnEnable() => RefreshRegistration();

        private void OnDisable() => Interactables.Unregister(this);

        /// <summary>
        /// Register or relinquish according to what the Def now says. <b>Call this if the Def arrives
        /// AFTER the component does</b> — registration is decided from <see cref="IsCarriable"/>, which is
        /// a fact about the Def, so a component added before its Def would decide "not carriable" and stay
        /// silently out of the registry. The builder sets the Def first; this is the way back for anything
        /// that cannot. (EditMode never fires <c>OnEnable</c> on a plain MonoBehaviour, so a test must
        /// call this explicitly — the fuel can's tests carry the same load-bearing line.)
        /// </summary>
        public void RefreshRegistration()
        {
            if (IsCarriable) Interactables.Register(this);
            else Interactables.Unregister(this);
        }

        /// <summary>Wire the tool in one call (tests / editor builder), mirroring the Configure pattern
        /// used across the lane. A negative <paramref name="reachMeters"/> leaves the serialized reach
        /// untouched.</summary>
        public void Configure(ToolDef tool, SpriteRenderer renderer, string id, float reachMeters = -1f)
        {
            _tool = tool;
            if (renderer != null) _renderer = renderer;
            _id = id;
            _resolvedId = null;
            if (reachMeters >= 0f) _reachMeters = reachMeters;
        }

        private void ResolveHands()
        {
            if (_hands != null) return;
            if (GameServices.Hands is CarryHands published) _hands = published;
            if (_hands == null) _hands = FindAnyObjectByType<CarryHands>();
        }

        private void ResolveRenderer()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        }

        private void ResolveYSort()
        {
            if (_ySort == null) _ySort = GetComponent<YSortSprite>();
        }

        private string ResolveId()
        {
            if (!string.IsNullOrEmpty(_id)) return _id;
            string def = _tool != null && !string.IsNullOrEmpty(_tool.Id) ? _tool.Id : "tool.unknown";
            // Instance-scoped, so two spawned shovels cannot collide. Readable ids are the builder's job;
            // this is the safety net. GetEntityId, not the deprecated GetInstanceID — 6000.5 marks that
            // obsolete-as-ERROR, which compiles locally and reds CI.
            return $"{def}#{GetEntityId()}";
        }
    }
}
