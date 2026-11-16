using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>A rod rack (or any stowage fixture) you walk up to and put a tool in, or take one out of</b> —
    /// owner ask 2026-11-15: "rods also need storage on boats, only fitting in a rod rack or appropriate
    /// locker".
    ///
    /// <para><b>What it takes is the Def's, never this class's.</b> <see cref="StowageDef.Accepts"/> and
    /// <see cref="StowageDef.Capacity"/> go straight into <see cref="StowageRule.Check"/> with the held
    /// tool's <see cref="ToolDef.Stowage"/>. A rack refuses a shovel because its Def takes Rods only; a fish
    /// hold refuses a rod because every hold takes <see cref="StowageRule.ExistingContainer"/> (and a hold's
    /// <c>TryAdd</c> takes a catch, not a tool, so no path even reaches it).</para>
    ///
    /// <para><b>Nothing is ever deleted or stranded.</b> Every refusal is decided before anything moves, so
    /// a rod the rack will not take stays in the hand. A stowed tool is the SAME object, re-parented under
    /// the rack's holder and switched off (the hands' sling does the same), and taking it back switches
    /// it on and lifts it; if the lift is refused it is switched off again and stays in the rack.</para>
    ///
    /// <para><b>The press.</b> With a tool the rack takes in either hand it claims
    /// <see cref="InteractPriority.ToolTarget"/> (put it in the rack, rather than on the ground beside it);
    /// otherwise it is a <see cref="InteractPriority.Fixture"/> and the press takes one out. The ToolTarget
    /// rung is gated on that tool actually being held, as the rung requires.</para>
    ///
    /// <para><b>No persistence, on purpose.</b> What is in a rack is SESSION-LOCAL, the same honest
    /// half-measure a tool set down on the ground ships with (<see cref="CarriableTool"/>): the rod lives
    /// until the scene does. Saving rack contents is new save schema (ADR 0020's placed-object
    /// persistence) and is not done here.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hidden Harbours/Stowage Rack")]
    public sealed class StowageRack : MonoBehaviour, IInteractable
    {
        [Header("What it is (content is data — rule 2)")]
        [Tooltip("What this rack takes and how many. Null makes the rack inert rather than universal.")]
        [SerializeField] private StowageDef _def;

        [Header("Wiring")]
        [Tooltip("Where a stowed tool is parented while it is in the rack. This object if empty. On a boat, " +
                 "a child of the hull, so the rods ride with her.")]
        [SerializeField] private Transform _holder;

        [Header("Reach (rule 6 — a tunable, not a literal)")]
        [Tooltip("How close (m) the fisher must stand to use the rack.")]
        [SerializeField, Min(0f)] private float _reachMeters = 1.2f;

        [Tooltip("Where the rack can be used from. On foot ashore and on a boat's deck by default.")]
        [SerializeField] private InteractContext _contexts = InteractContext.OnFoot | InteractContext.OnDeck;

        [Tooltip("Stable INSTANCE id (unique among live registrants). Left empty, one is derived from the " +
                 "Def id and this instance.")]
        [SerializeField] private string _id;

        private readonly List<CarriableTool> _stowed = new List<CarriableTool>();
        private CarryHands _hands;
        private string _resolvedId;
        private string _labelledWhat, _stowLabel, _takeLabel;

        /// <summary>The Def this rack draws what it takes from.</summary>
        public StowageDef Def => _def;

        /// <summary>How many tools are in the rack now.</summary>
        public int Count
        {
            get { Prune(); return _stowed.Count; }
        }

        /// <summary>How many it holds at most (the Def's; 0 without one).</summary>
        public int Capacity => _def != null ? _def.Capacity : 0;

        /// <summary>What it takes (the Def's; nothing without one).</summary>
        public StowageAccepts Accepts => _def != null ? _def.Accepts : StowageAccepts.None;

        private Transform HolderTransform => _holder != null ? _holder : transform;

        // ---- the two verbs, testable without the resolver ---------------------------------------------

        /// <summary>
        /// May this tool go in the rack right now? Changes nothing. Asks the one shared rule.
        /// </summary>
        public StowRefusal CanStow(CarriableTool tool)
        {
            if (tool == null || tool.Tool == null) return StowRefusal.NothingToStow;
            if (_def == null) return StowRefusal.NoContainer;
            return StowageRule.Check(tool.Tool.Stowage, Accepts, Count, Capacity);
        }

        /// <summary>
        /// Put <paramref name="tool"/> from <paramref name="hands"/> into the rack. On any refusal nothing
        /// has moved: the tool is still in the hand.
        /// </summary>
        public StowRefusal TryStow(CarryHands hands, CarriableTool tool)
        {
            if (hands == null) return StowRefusal.NothingToStow;
            StowRefusal refusal = CanStow(tool);
            if (refusal != StowRefusal.None) return refusal;
            if (!hands.TryStowInto(tool, HolderTransform)) return StowRefusal.NothingToStow;

            tool.gameObject.SetActive(false);   // in the rack, out of the interact registry
            _stowed.Add(tool);
            return StowRefusal.None;
        }

        /// <summary>
        /// Take the most recently stowed tool into <paramref name="hands"/>. Returns the hands' own refusal
        /// when they cannot take it (the tool stays in the rack), and
        /// <see cref="CarryRefusal.NotCarriable"/> when the rack is empty.
        /// </summary>
        public CarryRefusal TryTake(CarryHands hands, out CarriableTool taken)
        {
            taken = null;
            if (hands == null || Count == 0) return CarryRefusal.NotCarriable;

            int last = _stowed.Count - 1;
            CarriableTool tool = _stowed[last];

            // Out of the rack to the scene root BEFORE the lift, because the hands remember the parent a
            // thing was lifted from and set it back down under that parent. Lifted straight off a boat's
            // rack, a rod later set down on the wharf would be parented to the boat and sail away with her.
            tool.transform.SetParent(null, worldPositionStays: true);
            tool.gameObject.SetActive(true);
            CarryRefusal refusal = hands.TryPickUp(tool);
            if (refusal != CarryRefusal.None)
            {
                // Refused: back in the rack exactly as it was.
                tool.transform.SetParent(HolderTransform, worldPositionStays: false);
                tool.transform.localPosition = Vector3.zero;
                tool.gameObject.SetActive(false);
                return refusal;
            }

            _stowed.RemoveAt(last);
            taken = tool;
            return CarryRefusal.None;
        }

        /// <summary>The first tool in either hand that this rack takes, or null. Read every frame by
        /// <see cref="Priority"/>: two slot reads, no allocation (rule 7).</summary>
        public CarriableTool HeldStowable(CarryHands hands)
        {
            if (hands == null || _def == null) return null;
            CarriableTool right = hands.Slots.Right as CarriableTool;
            if (right != null && right.Tool != null && StowageRule.Accepts(Accepts, right.Tool.Stowage)) return right;
            CarriableTool left = hands.Slots.Left as CarriableTool;
            if (left != null && left.Tool != null && StowageRule.Accepts(Accepts, left.Tool.Stowage)) return left;
            return null;
        }

        // ---- IInteractable ------------------------------------------------------------------------------

        /// <inheritdoc/>
        public string Id => _resolvedId ??= ResolveId();

        /// <inheritdoc/>
        public Vector2 WorldPosition => transform.position;

        /// <inheritdoc/>
        public float ReachMeters => _reachMeters;

        /// <summary>ToolTarget while a tool it takes is in hand (so E stows it rather than dropping it
        /// beside the rack); a plain fixture otherwise.</summary>
        public int Priority => HeldStowable(PublishedHands()) != null
                               ? InteractPriority.ToolTarget
                               : InteractPriority.Fixture;

        /// <inheritdoc/>
        public InteractContext Contexts => _contexts;

        /// <inheritdoc/>
        public bool RequiresFacing => false;

        /// <summary>Available whenever it has a Def: a full or empty rack earns a sentence, not silence.
        /// </summary>
        public bool IsAvailable => _def != null;

        /// <summary>"Stow it in the rod rack" / "Take one from the rod rack", cached (read every frame on the
        /// offer path, rule 7).</summary>
        public string VerbLabel
        {
            get
            {
                string what = RackName();
                if (!string.Equals(what, _labelledWhat, System.StringComparison.Ordinal))
                {
                    _labelledWhat = what;
                    _stowLabel = "Stow it in the " + what;
                    _takeLabel = "Take one from the " + what;
                }
                return HeldStowable(PublishedHands()) != null ? _stowLabel : _takeLabel;
            }
        }

        /// <summary>The press: stow the held tool if the rack takes it, else take one out.</summary>
        public void Interact(in InteractActor actor)
        {
            CarryHands hands = ResolveHands();
            if (hands == null)
            {
                EventBus.Publish(new DevNotice("No one here to use that."));
                return;
            }

            string rack = RackName();
            CarriableTool held = HeldStowable(hands);
            if (held != null)
            {
                StowRefusal refusal = TryStow(hands, held);
                EventBus.Publish(new DevNotice(refusal switch
                {
                    StowRefusal.None => $"You stow the {ToolName(held)} in the {rack}.",
                    StowRefusal.Full => $"The {rack} is full.",
                    StowRefusal.WrongContainer => $"That does not go in the {rack}.",
                    _ => $"You cannot stow that in the {rack}.",
                }));
                return;
            }

            if (Count == 0)
            {
                EventBus.Publish(new DevNotice($"The {rack} is empty."));
                return;
            }

            CarryRefusal take = TryTake(hands, out CarriableTool taken);
            EventBus.Publish(new DevNotice(take == CarryRefusal.None
                ? $"You take the {ToolName(taken)} from the {rack}."
                : CarryMath.Message(take)));
        }

        // ---- registration -------------------------------------------------------------------------------

        private void OnEnable() => RefreshRegistration();

        private void OnDisable() => Interactables.Unregister(this);

        /// <summary>Register or relinquish according to whether a Def is wired. Call after a late
        /// <see cref="Configure"/> (and explicitly in EditMode, where <c>OnEnable</c> does not fire).</summary>
        public void RefreshRegistration()
        {
            if (_def != null) Interactables.Register(this);
            else Interactables.Unregister(this);
        }

        /// <summary>Wire the rack in one call (tests / editor builder). A negative
        /// <paramref name="reachMeters"/> leaves the serialized reach untouched.</summary>
        public void Configure(StowageDef def, Transform holder, string id, float reachMeters = -1f)
        {
            _def = def;
            _holder = holder;
            _id = id;
            _resolvedId = null;
            if (reachMeters >= 0f) _reachMeters = reachMeters;
        }

        // ---- helpers --------------------------------------------------------------------------------------

        private void Prune()
        {
            for (int i = _stowed.Count - 1; i >= 0; i--)
                if (_stowed[i] == null) _stowed.RemoveAt(i);
        }

        private static CarryHands PublishedHands() => GameServices.Hands as CarryHands;

        private CarryHands ResolveHands()
        {
            if (_hands != null) return _hands;
            _hands = PublishedHands();
            if (_hands == null) _hands = FindAnyObjectByType<CarryHands>();
            return _hands;
        }

        private string RackName()
            => _def != null && !string.IsNullOrEmpty(_def.DisplayName) ? _def.DisplayName : "rack";

        private static string ToolName(CarriableTool tool)
            => tool != null && tool.Tool != null && !string.IsNullOrEmpty(tool.Tool.DisplayName)
               ? tool.Tool.DisplayName
               : "it";

        private string ResolveId()
        {
            if (!string.IsNullOrEmpty(_id)) return _id;
            string def = _def != null && !string.IsNullOrEmpty(_def.Id) ? _def.Id : "stowage.unknown";
            return $"{def}#{GetEntityId()}";
        }
    }
}
