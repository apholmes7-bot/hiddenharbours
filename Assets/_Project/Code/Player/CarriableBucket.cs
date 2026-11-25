using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>A pail standing in the world that you pick up, carry, fill and put back down</b> — the container
    /// half of the two-hand ruling: "a bucket in one hand and the rod in the other".
    ///
    /// <para><b>It is its own hold.</b> Unlike <see cref="ClamBucket"/>, which rides the player's belt and
    /// is always in reach, this pail has a transform, a picture and contents of its own — so what you can
    /// see is what you are carrying, and where you left it is where it is. That is exactly the change
    /// <see cref="ClamBucket"/>'s own remarks predicted ("this component gets a transform of its own and
    /// NOTHING else here has to move"), taken one object at a time rather than by rewriting the opening.
    /// While a pail is in her hands the belt pail stands down (<see cref="ClamBucket.IsAvailable"/>): what
    /// is in your hands beats what is on your belt.</para>
    ///
    /// <para><b>Three verbs, one press, no mode flag</b> — the <see cref="CarriableTool"/> arrangement with
    /// one rung added. With a catch in her hands the pail is a <see cref="InteractPriority.ToolTarget"/>
    /// and E stows the fish; otherwise it is <see cref="InteractPriority.Held"/> while carried and
    /// <see cref="InteractPriority.Portable"/> on the ground, so E means "set it down" or "pick it up".
    /// One consequence worth knowing: you cannot put the pail down while holding a fish, because stowing
    /// the fish is the more specific act — and it is also the one you were about to do.</para>
    ///
    /// <para><b>Its catch is saved, its place is not</b> (owner 2026-11-24: clams left in the carried pail
    /// survive a relaunch). A pail with an authored id keeps its catch in the v25 storage rows under that id
    /// (<see cref="StoredContainers.WriteCatch"/>, the <see cref="CamperFreezer"/> pattern), written on every
    /// change and read back on enable and on <see cref="GameLoaded"/>. Where it stands and whether it is in
    /// his hands have no saved home, so after a relaunch it is back at its authored spot with its catch and
    /// his hands are empty; world-placed persistence is net-new save schema and its own ADR
    /// (<c>diegetic-ui-and-inventory.md</c> §4.3). No ownership gate:
    /// this is a physical object you are holding, so owning <c>gear.bucket</c> has nothing to say about
    /// it — the gate belongs to the belt pail, which is a capability rather than a thing.</para>
    ///
    /// <para><b>The picture is not here.</b> Contents → art goes through the Core bridge like every other
    /// container (<see cref="HoldCatchFillSource"/> → <see cref="BucketFillPresenter"/>): this class owns
    /// the items and nothing about how they are drawn, which is what lets the same pail wear the baked
    /// bucket states today and anything else later.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hidden Harbours/Carriable Bucket")]
    public sealed class CarriableBucket : MonoBehaviour,
                                          IInteractable, ICarriable, ICarryAnchored, IHandLoad, IHold
    {
        [Header("What it holds (rule 6 — a tunable, not a literal)")]
        [Tooltip("How many catch units the pail takes. 20 is the design's clam pail, matched to the belt " +
                 "bucket so the two do not disagree about what a pail is.")]
        [SerializeField, Min(1)] private int _capacity = 20;

        [Header("What it is (content is data — rule 2)")]
        [Tooltip("The KIND id a gate matches on — shared by every pail there will ever be. Not the " +
                 "instance id below.")]
        [SerializeField] private string _defId = "container.bucket";

        [Tooltip("Stable INSTANCE id for the interact registry — the resolver's last tie-break. ⚠️ Must " +
                 "be unique among live registrants; left empty a unique one is derived from this " +
                 "instance.")]
        [SerializeField] private string _id;

        [Header("Wiring (auto-resolved off this object if empty)")]
        [Tooltip("The pail's own renderer — what the carrier re-sorts while it is held.")]
        [SerializeField] private SpriteRenderer _renderer;

        [Header("Reach (rule 6)")]
        [Tooltip("How close (m) the on-foot fisher must be to pick it up. The carried tool's reach: you " +
                 "bend for a pail you are standing over, not one across the wharf.")]
        [SerializeField, Min(0f)] private float _reachMeters = 1.5f;

        // In your hands is in reach BY DEFINITION — the carried tool's constant, for its reason: no
        // hip-offset tuning may ever push a held pail outside its own reach and strand it there.
        private const float HeldReachMeters = 4f;

        private readonly List<CatchItem> _items = new List<CatchItem>();
        private CarryHands _hands;      // cached — Priority reads IsCarried EVERY FRAME (rule 7)
        private YSortSprite _ySort;
        private BucketFillPresenter _presenter;
        private string _resolvedId;

        // ---- IHold ------------------------------------------------------------------------------------

        /// <inheritdoc/>
        public int CapacityUnits => _capacity;

        /// <inheritdoc/>
        public int UsedUnits => _items.Count;

        /// <inheritdoc/>
        public IReadOnlyList<CatchItem> Items => _items;

        /// <inheritdoc/>
        public bool TryAdd(CatchItem item)
        {
            if (!CatchKeep.MayKeep(in item)) return false;   // protected: release-only, never held
            if (!IsBound()) return false;                    // the saved catch has not been read yet
            if (_items.Count >= CapacityUnits) return false;
            _items.Add(item);
            SyncToSave();
            return true;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            if (!IsBound()) return;   // never write an empty view over catch it has not read
            _items.Clear();
            SyncToSave();
        }

        // ---- the save (v26, the v25 storage rows) ------------------------------------------------------

        /// <summary>
        /// The key its catch is kept under in <see cref="StoredContainers"/>: its authored instance id
        /// (<c>container.st_peters.bucket</c>), or null when none was authored. A derived id changes from
        /// run to run, so such a pail keeps nothing over a relaunch (session-local, as before).
        /// </summary>
        public string SaveKey => string.IsNullOrEmpty(_id) ? null : _id;

        /// <summary>Read its catch back from the save it has not read yet (or <paramref name="force"/>).
        /// Public for EditMode, where <c>OnEnable</c> does not fire. The <see cref="CamperFreezer"/> bind:
        /// with no <see cref="GameServices.CatchFactory"/> a saved row cannot be rebuilt, so a pail whose
        /// row holds catch stays unbound (every change refused) and the row is left exactly as it is.</summary>
        public void BindToSave(bool force = false)
        {
            SaveData data = GameServices.Save?.Current;
            if (data == null || SaveKey == null || (!force && ReferenceEquals(data, _boundTo))) return;
            IReadOnlyList<HeldCatchDto> saved = StoredContainers.CatchIn(data, SaveKey);
            ICatchItemFactory factory = GameServices.CatchFactory;
            if (factory == null && saved != null && saved.Count > 0)
            {
                _items.Clear();   // a view of an older save must not show; the row itself is untouched
                _boundTo = null;
                return;
            }
            _boundTo = data;
            HoldCatchCodec.Restore(saved, factory, _items);
        }

        /// <summary>True when a change may be written: no save or no key to guard, or the current save has
        /// been read (binding now if it can).</summary>
        public bool IsBound()
        {
            SaveData data = GameServices.Save?.Current;
            if (data == null || SaveKey == null) return true;
            if (!ReferenceEquals(data, _boundTo)) BindToSave();
            return ReferenceEquals(data, _boundTo);
        }

        // Every change writes the whole catch (an emptied pail drops its row, so no stale clams remain).
        private void SyncToSave()
        {
            if (SaveKey == null) return;
            StoredContainers.WriteCatch(GameServices.Save?.Current, SaveKey, _defId, _items);
        }

        private SaveData _boundTo;   // the save blob it last restored from

        private void OnGameLoaded(GameLoaded _) => BindToSave(force: true);

        // ---- ICarriable / ICarryAnchored / IHandLoad ---------------------------------------------------

        /// <summary>The KIND — <c>container.bucket</c>, shared by every pail. Deliberately not
        /// <see cref="Id"/>; see <see cref="ICarriable.DefId"/>.</summary>
        public string DefId => _defId;

        /// <summary>A pail is always liftable — that is what a pail is for.</summary>
        public bool IsCarriable => true;

        /// <summary>
        /// A <see cref="HandLoad.Container"/>. Stated rather than inferred even though
        /// <see cref="HandSlots.LoadOf"/> would reach the same answer from the <see cref="IHold"/> —
        /// because "this is the thing you stack into" is the fact the sharing rule turns on, and a class
        /// that stops implementing <see cref="IHold"/> some day should fail loudly rather than quietly
        /// become a tool.
        /// </summary>
        // Fully qualified deliberately: the property and the enum share a name, so the bare
        // form leans on C#'s "Color Color" resolution — legal, and one rename away from being
        // a confusing error message instead.
        public HandLoad HandLoad => HiddenHarbours.Core.HandLoad.Container;

        /// <summary>
        /// No hand-prop row — the carrier keeps its own fallback offset.
        ///
        /// <para>⚠️ A DECLARED ART DEBT, not an oversight, and it must not be pointed at another row. The
        /// character rig bakes seven prop rows (rod, sling, fish, clam, knife, gaff, rope) and none of
        /// them is a pail; <c>bucketRig.js</c> does publish a CARRY pivot at the bail apex, so the row is
        /// cheap to bake when the art lane gets to it, and the day it exists this returns its key and
        /// nothing else in this file changes. Borrowing the rope's row instead would hang a 0.28 m pail
        /// off a half-scale deck coil's grip — a tuned constant re-used on a new lever, which is the
        /// mistake this project has already paid for.</para>
        /// </summary>
        public string HandPropKey => null;

        /// <inheritdoc/>
        public Transform Transform => transform;

        /// <summary>
        /// 8 — the bucket rig bakes a full turntable, so a carried pail turns with her.
        ///
        /// <para>The presenter owns which cell is drawn (it picks the sprite for band × group × facing),
        /// so <see cref="ShowFacing"/> forwards rather than touching the renderer: two writers on one
        /// sprite is the flicker the deck rider was rebuilt to remove.</para>
        /// </summary>
        public int BakedFacings => BucketFillPresenter.Facings;

        /// <inheritdoc/>
        public void ShowFacing(int facingIndex)
        {
            ResolvePresenter();
            if (_presenter != null) _presenter.SetDirection(facingIndex);
        }

        /// <inheritdoc/>
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
            SetDrawnElsewhere(false);   // a pail on the ground is drawn: nothing else is drawing it
        }

        // ---- the pail as a mesh in his hand (ADR 0053) ---------------------------------------------------

        /// <summary>
        /// The bucket rig's held mesh for this pail at its fill and catch, as tier <paramref name="tierId"/>
        /// ('pail'), or null: a container that does not show its fill as baked bucket states (the clam hod
        /// stacks a heap between two sprites) has no mesh and keeps its sprite.
        /// </summary>
        public HeldPropMeshDef HeldMeshFor(string tierId)
        {
            ResolvePresenter();
            return _presenter != null ? _presenter.HeldMeshFor(tierId) : null;
        }

        /// <summary>
        /// <b>Held out of the picture while the mesh figure draws this pail in his hand</b>, as
        /// <see cref="CarriableTool.SetDrawnElsewhere"/> is for a tool: the carrier states it every frame and
        /// never latches it. By <see cref="Renderer.forceRenderingOff"/>, never <c>enabled</c>, and the sprite's
        /// sun shadow goes quiet with it; given back on <see cref="OnPlaced"/>.
        /// </summary>
        public void SetDrawnElsewhere(bool drawnElsewhere)
        {
            ResolveRenderer();
            if (_renderer == null) return;
            if (_renderer.forceRenderingOff != drawnElsewhere)
                _renderer.forceRenderingOff = drawnElsewhere;
            if (_shadow == null) _shadow = _renderer.GetComponent<SpriteShadow>();
            if (_shadow != null && _shadow.Quiet != drawnElsewhere) _shadow.Quiet = drawnElsewhere;
        }

        /// <summary>True while the sprite is held out of the picture (<see cref="SetDrawnElsewhere"/>).</summary>
        public bool DrawnElsewhere => _renderer != null && _renderer.forceRenderingOff;

        private SpriteShadow _shadow;

        // ---- IInteractable -----------------------------------------------------------------------------

        /// <inheritdoc/>
        public string Id => _resolvedId ??= string.IsNullOrEmpty(_id) ? $"{_defId}#{GetEntityId()}" : _id;

        /// <inheritdoc/>
        public Vector2 WorldPosition => transform.position;

        /// <inheritdoc/>
        public float ReachMeters => IsCarried ? HeldReachMeters : _reachMeters;

        /// <summary>
        /// Three rungs, decided by what is in her hands. <see cref="InteractPriority.ToolTarget"/> with a
        /// catch held — using the thing you are holding on the thing in front of you is the most specific
        /// act, and it is the rung the belt pail already claims for the same reason. Otherwise the carried
        /// tool's two rungs, which close the pick-up/put-down loop with no mode flag.
        /// </summary>
        public int Priority => HeldCatch() != null
            ? InteractPriority.ToolTarget
            : (IsCarried ? InteractPriority.Held : InteractPriority.Portable);

        /// <summary>On your own two feet. A pail set down on a deck would have to RIDE that hull (the
        /// deck-occupant seam's measured slots, not a re-parent), so the deck is not offered: board while
        /// carrying and you keep hold of it, which is what a person does.</summary>
        public InteractContext Contexts => InteractContext.OnFoot;

        /// <inheritdoc/>
        public bool RequiresFacing => false;

        /// <summary>Always — a refusal is something the press EARNS a sentence for (P5), not a reason to
        /// vanish from the resolver. A FULL pail still answers, and says it is full.</summary>
        public bool IsAvailable => true;

        /// <summary>
        /// "Put it in the bucket" / "Pick up the bucket" / "Set the bucket down" — derived from the same
        /// two facts <see cref="Priority"/> and <see cref="Interact"/> are, so the words, the rung and the
        /// action can never disagree. Constant strings: this is read on the OFFER path, every frame there
        /// is a candidate, so nothing may be interpolated here (rule 7).
        /// </summary>
        public string VerbLabel => HeldCatch() != null
            ? "Put it in the bucket"
            : (IsCarried ? "Set the bucket down" : "Pick up the bucket");

        /// <summary>True while this pail is in EITHER hand. Derived from the hands, never stored — a
        /// second copy of "am I carried" is a copy that can disagree.</summary>
        public bool IsCarried => CarriedItem.IsHeld(_hands, this);

        /// <summary>The press: stow the catch if there is one, else lift or set down.</summary>
        public void Interact(in InteractActor actor)
        {
            ResolveHands();
            if (_hands == null)
            {
                EventBus.Publish(new DevNotice("No one here to pick that up."));
                return;
            }

            if (HeldCatch() != null)
            {
                Stow();
                return;
            }

            bool wasCarried = IsCarried;
            CarryRefusal refusal = wasCarried ? _hands.TryPlace(this) : _hands.TryPickUp(this);
            if (refusal != CarryRefusal.None)
            {
                EventBus.Publish(new DevNotice(CarryMath.Message(refusal)));
                return;
            }

            EventBus.Publish(new DevNotice(wasCarried ? "You set the bucket down."
                                                      : "You pick up the bucket."));
        }

        /// <summary>Move the catch from her hand into this pail, or say why it did not go.</summary>
        private void Stow()
        {
            CarriableCatch held = HeldCatch();
            string what = held != null && held.HasItem ? held.Item.DisplayName : "catch";

            if (!_hands.TryGiveCatchTo(this))
            {
                EventBus.Publish(new DevNotice(BucketStrings.Full));
                return;
            }

            EventBus.Publish(new DevNotice($"You drop the {what} in the bucket. " +
                                           $"({UsedUnits}/{CapacityUnits})"));
        }

        /// <summary>The catch in the fisher's hands right now, or null — in EITHER hand, which is the
        /// whole point: she can be holding the pail as well.</summary>
        private CarriableCatch HeldCatch()
        {
            ResolveHands();
            return CarriedItem.Held(_hands, HandLoad.Catch) as CarriableCatch;
        }

        // ---- registration -------------------------------------------------------------------------------

        private void OnEnable()
        {
            Interactables.Register(this);
            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
            BindToSave();   // a region enabled after the load edge reads the save it has not read
        }

        private void OnDisable()
        {
            Interactables.Unregister(this);
            EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
        }

        /// <summary>Wire the pail in one call (the region builders / tests) — the Configure seam used
        /// across the lane, because EditMode never runs a builder. A non-positive
        /// <paramref name="capacity"/> leaves the serialized value alone.</summary>
        public void Configure(int capacity, SpriteRenderer renderer, string id, string defId = null)
        {
            if (capacity > 0) _capacity = capacity;
            if (renderer != null) _renderer = renderer;
            if (!string.IsNullOrEmpty(defId)) _defId = defId;
            _id = id;
            _resolvedId = null;
            _boundTo = null;   // a new key reads its own row on the next change
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

        private void ResolvePresenter()
        {
            if (_presenter == null) _presenter = GetComponent<BucketFillPresenter>();
        }

        private void ResolveYSort()
        {
            if (_ySort == null) _ySort = GetComponent<YSortSprite>();
        }
    }
}
