using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>A rod rack (or any stowage fixture) you walk up to and put a tool in, or take one out of</b> —
    /// owner ask 2026-11-15: "rods also need storage on boats, only fitting in a rod rack or appropriate
    /// locker"; and 2026-11-17: storage WITH MEMORY at Ginny's, more of it in the camper and the shed.
    ///
    /// <para><b>What it takes is the Def's, never this class's.</b> <see cref="StowageDef.Accepts"/> and
    /// <see cref="StowageDef.Capacity"/> go straight into <see cref="StowageRule.Check"/> with the held
    /// tool's <see cref="ToolDef.Stowage"/>. A rack refuses a shovel because its Def takes Rods only; a fish
    /// hold refuses a rod because every hold takes <see cref="StowageRule.ExistingContainer"/> (and a hold's
    /// <c>TryAdd</c> takes a catch, not a tool, so no path even reaches it). A gear locker is the same class
    /// with a Def that takes General.</para>
    ///
    /// <para><b>Who may use it is the Def's too.</b> <see cref="StowageDef.DeedHomeId"/> names the home whose
    /// deed opens it (the camper's and the shed's stores); until that deed is his, a stow is refused
    /// <see cref="StowRefusal.NotYours"/> and nothing comes out (<see cref="StorageAccess"/>).</para>
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
    /// <para><b>Memory (save v25).</b> A rack whose id is a <c>storage.*</c> id (<see cref="StorageIds"/>)
    /// is REMEMBERED: it is one of the player's containers in <see cref="PlayerStorage"/>, and every stow and
    /// take writes the tool's stable instance id into its row on the save (<see cref="StoredContainers"/>,
    /// keyed by this id). The save is the truth and the rack a view of it: it binds to its row on Start, on
    /// <see cref="GameLoaded"/>, and on the first frame after an enable that finds a save it has not read, so
    /// a scene loaded after the load still finds its rods. Binding moves the SAME scene tool back into the
    /// rack, found by its id; nothing is ever spawned, so the starter rod can never be two rods. A row whose
    /// tool is not in any loaded scene keeps its slot until that tool's scene loads, when the tool walks
    /// back in itself (<see cref="ReturnIfStored"/>). Only tools with an authored id are remembered; any
    /// other rack (a boat's, a test's) stays session-local as before.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hidden Harbours/Stowage Rack")]
    public sealed class StowageRack : MonoBehaviour, IInteractable, IStorageContainer, IRoomFixtureReach
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

        [Tooltip("Stable INSTANCE id (unique among live registrants). A storage.* id (storage.stpeters." +
                 "ginny_rod_rack) makes the rack REMEMBERED: what is in it is kept on the save under this id, " +
                 "so it is append-only. Left empty, a session-only id is derived from the Def id and this " +
                 "instance.")]
        [SerializeField] private string _id;

        private readonly List<CarriableTool> _stowed = new List<CarriableTool>();
        private CarryHands _hands;
        private string _resolvedId;
        private string _labelledWhat, _stowLabel, _takeLabel;
        private SaveData _boundTo;      // the save blob this rack last bound to
        private bool _bindPending;      // an enable found a save it has not read: bind next frame
        private bool _listed;           // in PlayerStorage

        // Scratch for the save reads (press-time and bind-time only, never per frame).
        private static readonly List<string> _scratchIds = new List<string>(8);
        private static CarriableTool[] _sceneTools;

        /// <summary>The Def this rack draws what it takes from.</summary>
        public StowageDef Def => _def;

        /// <summary>How many tools are in the rack now (in this scene).</summary>
        public int Count
        {
            get { Prune(); return _stowed.Count; }
        }

        /// <summary>
        /// How many of its slots are taken: the tools in it here, plus any its save row lists whose tool is
        /// in a scene not loaded now (that rod is still in the rack). What the capacity check reads.
        /// </summary>
        public int UsedSlots
        {
            get
            {
                int used = Count;
                SaveData data = RememberedSave();
                if (data == null) return used;
                _scratchIds.Clear();
                StoredContainers.InstancesIn(data, StorageId, _scratchIds);
                for (int i = 0; i < _scratchIds.Count; i++)
                    if (IndexOfStowed(_scratchIds[i]) < 0) used++;
                _scratchIds.Clear();
                return used;
            }
        }

        /// <summary>How many it holds at most (the Def's; 0 without one).</summary>
        public int Capacity => _def != null ? _def.Capacity : 0;

        /// <summary>What it takes (the Def's; nothing without one).</summary>
        public StowageAccepts Accepts => _def != null ? _def.Accepts : StowageAccepts.None;

        /// <summary>True when the player may use it: no deed named, or that deed is his.</summary>
        public bool IsOpen => _def == null || StorageAccess.IsOpen(_def.DeedHomeId);

        /// <summary>True when what is in it is kept on the save (its id is a <c>storage.*</c> id).</summary>
        public bool IsRemembered => StorageIds.IsStorageId(_id);

        /// <summary>True when <paramref name="tool"/> is in this rack now.</summary>
        public bool Holds(CarriableTool tool) => tool != null && _stowed.Contains(tool);

        private Transform HolderTransform => _holder != null ? _holder : transform;

        // ---- IStorageContainer --------------------------------------------------------------------------

        /// <summary>The rack's id: the save key when it is remembered.</summary>
        public string StorageId => Id;

        /// <summary>A placed fixture (<see cref="PlayerStorage.PriorityFixture"/>).</summary>
        public int StoragePriority => PlayerStorage.PriorityFixture;

        // ---- the two verbs, testable without the resolver ---------------------------------------------

        /// <summary>
        /// May this tool go in the rack right now? Changes nothing. Asks the deed, then the one shared rule.
        /// </summary>
        public StowRefusal CanStow(CarriableTool tool)
        {
            if (tool == null || tool.Tool == null) return StowRefusal.NothingToStow;
            if (_def == null) return StowRefusal.NoContainer;
            if (!IsOpen) return StowRefusal.NotYours;
            return StowageRule.Check(tool.Tool.Stowage, Accepts, UsedSlots, Capacity);
        }

        /// <summary>
        /// Put <paramref name="tool"/> from <paramref name="hands"/> into the rack. On any refusal nothing
        /// has moved: the tool is still in the hand. A remembered rack writes it to the save.
        /// </summary>
        public StowRefusal TryStow(CarryHands hands, CarriableTool tool)
        {
            if (hands == null) return StowRefusal.NothingToStow;
            StowRefusal refusal = CanStow(tool);
            if (refusal != StowRefusal.None) return refusal;
            if (!hands.TryStowInto(tool, HolderTransform)) return StowRefusal.NothingToStow;

            tool.gameObject.SetActive(false);   // in the rack, out of the interact registry
            _stowed.Add(tool);
            Remember(tool);
            return StowRefusal.None;
        }

        /// <summary>
        /// Take the most recently stowed tool into <paramref name="hands"/>. Returns the hands' own refusal
        /// when they cannot take it (the tool stays in the rack), and
        /// <see cref="CarryRefusal.NotCarriable"/> when the rack is empty or not his to use.
        /// </summary>
        public CarryRefusal TryTake(CarryHands hands, out CarriableTool taken)
        {
            taken = null;
            if (hands == null || Count == 0 || !IsOpen) return CarryRefusal.NotCarriable;

            int last = _stowed.Count - 1;
            CarriableTool tool = _stowed[last];

            // Off the save row BEFORE the tool wakes, so nothing reads it as still stowed.
            Forget(tool);

            // Out of the rack to the scene root BEFORE the lift, because the hands remember the parent a
            // thing was lifted from and set it back down under that parent. Lifted straight off a boat's
            // rack, a rod later set down on the wharf would be parented to the boat and sail away with her.
            tool.transform.SetParent(null, worldPositionStays: true);
            tool.gameObject.SetActive(true);
            CarryRefusal refusal = hands.TryPickUp(tool);
            if (refusal != CarryRefusal.None)
            {
                // Refused: back in the rack exactly as it was, and on the save again.
                tool.transform.SetParent(HolderTransform, worldPositionStays: false);
                tool.transform.localPosition = Vector3.zero;
                tool.gameObject.SetActive(false);
                Remember(tool);
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

        // ---- memory (save v25) --------------------------------------------------------------------------

        /// <summary>
        /// Make the rack show what its save row says: tools it holds that the row does not list come out
        /// (set down where the rack stands), and listed tools found in any loaded scene go in. A tool in
        /// someone's hands or in another rack is left where it is. No-op for a rack that is not remembered
        /// or with no save. Public so a test can drive it without the player loop.
        /// </summary>
        public void BindToSave()
        {
            _bindPending = false;
            SaveData data = RememberedSave();
            if (data == null) return;
            _boundTo = data;

            _scratchIds.Clear();
            StoredContainers.InstancesIn(data, StorageId, _scratchIds);

            Prune();
            for (int i = _stowed.Count - 1; i >= 0; i--)
            {
                CarriableTool tool = _stowed[i];
                if (!tool.HasAuthoredId || _scratchIds.Contains(tool.Id)) continue;
                _stowed.RemoveAt(i);
                SetDownHere(tool);
            }

            _sceneTools = null;
            for (int i = 0; i < _scratchIds.Count; i++)
            {
                if (IndexOfStowed(_scratchIds[i]) >= 0) continue;
                CarriableTool tool = FindSceneTool(_scratchIds[i]);
                if (tool != null && !tool.IsCarried && !InAnyRack(tool)) Hold(tool);
            }
            _sceneTools = null;
            _scratchIds.Clear();
        }

        /// <summary>
        /// A remembered tool that wakes in a scene loaded after its rack bound (its <c>Start</c>) asks this:
        /// if the save lists it in a rack that is listed in <see cref="PlayerStorage"/>, it goes back in.
        /// True when it did.
        /// </summary>
        public static bool ReturnIfStored(CarriableTool tool)
        {
            if (tool == null || !tool.HasAuthoredId || tool.IsCarried) return false;
            SaveData data = GameServices.Save?.Current;
            string key = StoredContainers.ContainerOf(data, tool.Id);
            if (key == null || !(PlayerStorage.Find(key) is StowageRack rack)) return false;
            // A rack listed but switched off (its region root toggled away by a crossing) is not a place a
            // tool can go: hidden under an inactive root, it would vanish from the world it woke in.
            if (!rack.enabled || !rack.gameObject.activeInHierarchy) return false;
            if (rack.Holds(tool) || InAnyRack(tool)) return false;
            rack.Hold(tool);
            return true;
        }

        /// <summary>
        /// The tool is in someone's hands, so it is in no container any more: its save row goes, whichever
        /// container held it and however it was lifted (a rack's <see cref="TryTake"/>, a press on the tool,
        /// any other path through <see cref="CarryHands.TryPickUp"/>). Called from
        /// <see cref="CarriableTool.OnLifted"/>. True iff a row was removed.
        /// </summary>
        public static bool ForgetStored(CarriableTool tool)
        {
            if (tool == null || !tool.HasAuthoredId) return false;
            return StoredContainers.ForgetInstance(GameServices.Save?.Current, tool.Id);
        }

        private void Remember(CarriableTool tool)
        {
            SaveData data = RememberedSave();
            if (data == null || tool == null || !tool.HasAuthoredId) return;
            if (!StoredContainers.TryStoreInstance(data, StorageId, _def != null ? _def.Id : "", tool.DefId,
                                                   tool.Id, Capacity))
                Debug.LogWarning($"[StowageRack] '{StorageId}' could not remember '{tool.Id}' on the save.", this);
        }

        private void Forget(CarriableTool tool)
        {
            SaveData data = RememberedSave();
            if (data == null || tool == null || !tool.HasAuthoredId) return;
            StoredContainers.RemoveInstance(data, StorageId, tool.Id);
        }

        private void Hold(CarriableTool tool)
        {
            tool.transform.SetParent(HolderTransform, worldPositionStays: false);
            tool.transform.localPosition = Vector3.zero;
            tool.gameObject.SetActive(false);
            _stowed.Add(tool);
        }

        private void SetDownHere(CarriableTool tool)
        {
            tool.transform.SetParent(null, worldPositionStays: true);
            tool.transform.position = transform.position;
            tool.gameObject.SetActive(true);
        }

        private SaveData RememberedSave() => IsRemembered ? GameServices.Save?.Current : null;

        private int IndexOfStowed(string toolId)
        {
            for (int i = 0; i < _stowed.Count; i++)
                if (_stowed[i] != null && string.Equals(_stowed[i].Id, toolId, System.StringComparison.Ordinal))
                    return i;
            return -1;
        }

        private static CarriableTool FindSceneTool(string toolId)
        {
            _sceneTools ??= FindObjectsByType<CarriableTool>(FindObjectsInactive.Include);
            for (int i = 0; i < _sceneTools.Length; i++)
                if (_sceneTools[i] != null && string.Equals(_sceneTools[i].Id, toolId, System.StringComparison.Ordinal))
                    return _sceneTools[i];
            return null;
        }

        private static bool InAnyRack(CarriableTool tool)
        {
            for (int i = 0; i < PlayerStorage.Count; i++)
                if (PlayerStorage.GetContainer(i) is StowageRack rack && rack.Holds(tool)) return true;
            return false;
        }

        private void OnGameLoaded(GameLoaded _) => BindToSave();

        private void Start() => BindToSave();

        private void LateUpdate()
        {
            if (_bindPending) BindToSave();
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

        /// <summary>Available whenever it has a Def: a full, empty or not-yet-his rack earns a sentence, not
        /// silence.</summary>
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
        public bool CanReach(in InteractActor actor) => RoomFixtureReach.Allows(this, actor, WorldPosition);

        public void Interact(in InteractActor actor)
        {
            if (!CanReach(actor)) return;
            CarryHands hands = ResolveHands();
            if (hands == null)
            {
                EventBus.Publish(new DevNotice("No one here to use that."));
                return;
            }

            string rack = RackName();
            if (!IsOpen)
            {
                EventBus.Publish(new DevNotice($"The {rack} is not yours to use yet."));
                return;
            }

            CarriableTool held = HeldStowable(hands);
            if (held != null)
            {
                StowRefusal refusal = TryStow(hands, held);
                EventBus.Publish(new DevNotice(refusal switch
                {
                    StowRefusal.None => $"You stow the {ToolName(held)} in the {rack}.",
                    StowRefusal.Full => $"The {rack} is full.",
                    StowRefusal.WrongContainer => $"That does not go in the {rack}.",
                    StowRefusal.NotYours => $"The {rack} is not yours to use yet.",
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

        private void OnEnable()
        {
            RefreshRegistration();
            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
            ISaveService save = GameServices.Save;
            if (IsRemembered && save?.Current != null && !ReferenceEquals(save.Current, _boundTo))
                _bindPending = true;   // not now: another object may be mid-activation
        }

        private void OnDisable()
        {
            Interactables.Unregister(this);
            EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
        }

        private void OnDestroy() => Unlist();

        /// <summary>Register or relinquish according to whether a Def is wired, and list a remembered rack
        /// in <see cref="PlayerStorage"/> (listed on enable, taken off on destroy, never on disable). Call
        /// after a late <see cref="Configure"/> (and explicitly in EditMode, where <c>OnEnable</c> does not
        /// fire).</summary>
        public void RefreshRegistration()
        {
            if (_def != null) Interactables.Register(this);
            else Interactables.Unregister(this);

            if (IsRemembered && !_listed)
            {
                PlayerStorage.Register(this);
                _listed = true;
            }
            else if (!IsRemembered && _listed)
            {
                Unlist();
            }
        }

        /// <summary>Wire the rack in one call (tests / editor builder). A negative
        /// <paramref name="reachMeters"/> leaves the serialized reach untouched.</summary>
        public void Configure(StowageDef def, Transform holder, string id, float reachMeters = -1f)
        {
            Unlist();   // the id is the list key; a new one is listed again by RefreshRegistration
            _def = def;
            _holder = holder;
            _id = id;
            _resolvedId = null;
            _boundTo = null;
            if (reachMeters >= 0f) _reachMeters = reachMeters;
        }

        // ---- helpers --------------------------------------------------------------------------------------

        private void Unlist()
        {
            if (!_listed) return;
            PlayerStorage.Unregister(this);
            _listed = false;
        }

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
