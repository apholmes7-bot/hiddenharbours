using System;
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The camper's chest freezer</b> (owner 2026-11-17: "bigger freezer"; the seat: landed catch goes in it
    /// the same way it goes in Ginny's). A catch hold: what goes in is held Frozen, which stops rot, and it
    /// comes out back on the clock. The capacity and the deed are its <see cref="FreezerDef"/>'s (40 units,
    /// <c>home.ginny_lot_camper</c>): until the camper is his it takes nothing, gives nothing and shows nothing.
    ///
    /// <para><b>One of the player's storage containers</b> under the fixed id
    /// <see cref="StorageIds.CamperFreezer"/>, at the home freezer's priority
    /// (<see cref="PlayerStorage.PriorityHomeFreezer"/>), so the Give rows list its catch and a gift is taken
    /// from it surgically (<see cref="IHoldRemove"/>).</para>
    ///
    /// <para><b>The save is the truth.</b> Its catch lives in the v25 storage rows under its id
    /// (<see cref="StoredContainers.WriteCatch"/>, the same <see cref="HoldCatchCodec"/> records Ginny's freezer
    /// keeps), written on every change and read back on enable (when the save has changed) and on
    /// <see cref="GameLoaded"/>; a restore lands every fish exactly as saved, never re-moded.</para>
    ///
    /// <para>The press is the <see cref="GinnyFreezer"/> exchange: carrying a full pail, what fits goes in;
    /// with an empty pail, what fits comes out.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CamperFreezer : MonoBehaviour, IHold, IHoldRemove, IStorageContainer, IInteractable
    {
        [Header("What it is (content is data — rule 2)")]
        [Tooltip("The freezer's Def: capacity and the deed that opens it. Null makes it inert.")]
        [SerializeField] private FreezerDef _def;

        [Header("Reach (rule 6 — a tunable, not a literal)")]
        [Tooltip("How close (m) he must stand to use it.")]
        [SerializeField, Min(0f)] private float _reachMeters = 1.2f;

        [Tooltip("Where it can be used from. On foot by default.")]
        [SerializeField] private InteractContext _contexts = InteractContext.OnFoot;

        private static readonly CatchItem[] NoItems = Array.Empty<CatchItem>();

        private readonly List<CatchItem> _items = new List<CatchItem>();
        private SaveData _boundTo;   // the save blob it last restored from
        private ClamBucket _bucket;

        // ---- storage ------------------------------------------------------------------------------------

        /// <summary>The fixed id (<see cref="StorageIds.CamperFreezer"/>): its save key.</summary>
        public string StorageId => StorageIds.CamperFreezer;

        /// <summary>The home freezer's priority; a tie with Ginny's sorts by id.</summary>
        public int StoragePriority => PlayerStorage.PriorityHomeFreezer;

        /// <summary>Open when it has a Def and that Def's deed (if any) is his.</summary>
        public bool IsOpen => _def != null && StorageAccess.IsOpen(_def.DeedHomeId);

        /// <summary>The Def's capacity (0 with no Def).</summary>
        public int CapacityUnits => _def != null ? _def.CapacityUnits : 0;

        /// <summary>The freezer's Def (its capacity, its deed and its look).</summary>
        public FreezerDef Def => _def;

        /// <summary>What is in it (frozen) as a reader sees it: the same count as <see cref="Items"/>, so 0
        /// while it is not his. The catch itself is kept (in memory and in the save) and counts again the
        /// moment the deed is his.</summary>
        public int UsedUnits => IsOpen ? _items.Count : 0;

        /// <summary>What a reader sees: the catch when open, nothing while it is not his.</summary>
        public IReadOnlyList<CatchItem> Items => IsOpen ? _items : (IReadOnlyList<CatchItem>)NoItems;

        /// <summary>Put one catch in, frozen. False when closed, full, or a protected species.</summary>
        public bool TryAdd(CatchItem item)
        {
            if (!IsOpen || !IsBound() || !CatchKeep.MayKeep(in item) || _items.Count >= CapacityUnits) return false;
            SpoilContext spoil = SpoilContext.Capture();
            _items.Add(item.WithFreshness(Freshness.WithMode(item.Freshness, spoil.NowGameSeconds,
                                                             item.SpoilPerDay, spoil.SecondsPerDay,
                                                             spoil.Policy, StorageMode.Frozen)));
            SyncToSave();
            return true;
        }

        /// <summary>Empty it. Refused while it is not his, so nothing is ever lost through a closed door.</summary>
        public void Clear()
        {
            if (!IsOpen || !IsBound()) return;
            _items.Clear();
            SyncToSave();
        }

        /// <summary>Take out the one item at <paramref name="index"/> and nothing else (IHoldRemove).</summary>
        public bool TryRemoveAt(int index)
        {
            if (!IsOpen || !IsBound() || index < 0 || index >= _items.Count) return false;
            _items.RemoveAt(index);
            SyncToSave();
            return true;
        }

        /// <summary>Wire the Def after the component (a builder, a test).</summary>
        public void Configure(FreezerDef def)
        {
            _def = def;
            RefreshRegistration();
        }

        /// <summary>Read its catch back from the save it has not read yet (or <paramref name="force"/>).
        /// Public for EditMode, where <c>OnEnable</c> does not fire.
        ///
        /// <para>⚠️ With no <see cref="GameServices.CatchFactory"/> yet, a restore can rebuild nothing, so it does
        /// NOT count as read: the in-memory view is emptied, the save row is left exactly as it is, and the
        /// freezer stays unbound (every change refused, see <see cref="IsBound"/>) until a later bind finds a
        /// factory. Binding there would let the next change write the empty view over the saved catch.</para></summary>
        public void BindToSave(bool force = false)
        {
            SaveData data = GameServices.Save?.Current;
            if (data == null || (!force && ReferenceEquals(data, _boundTo))) return;
            ICatchItemFactory factory = GameServices.CatchFactory;
            if (factory == null)
            {
                _items.Clear();   // a view of an older save must not show; the row itself is untouched
                _boundTo = null;
                return;
            }
            _boundTo = data;
            HoldCatchCodec.Restore(StoredContainers.CatchIn(data, StorageId), factory, _items);
        }

        /// <summary>True when a change may be written: there is no save to guard, or the current save has
        /// been read (binding now if it can). False means the saved row has not been read yet, so a change
        /// would overwrite catch it cannot see.</summary>
        public bool IsBound()
        {
            SaveData data = GameServices.Save?.Current;
            if (data == null) return true;
            if (!ReferenceEquals(data, _boundTo)) BindToSave();
            return ReferenceEquals(data, _boundTo);
        }

        private void SyncToSave()
            => StoredContainers.WriteCatch(GameServices.Save?.Current, StorageId, _def != null ? _def.Id : "", _items);

        // ---- the press ----------------------------------------------------------------------------------

        /// <summary>A fixture.</summary>
        public string Id => StorageId;
        public Vector2 WorldPosition => transform.position;
        public float ReachMeters => _reachMeters;
        public int Priority => InteractPriority.Fixture;
        public InteractContext Contexts => _contexts;
        public bool RequiresFacing => false;
        public bool IsAvailable => _def != null;
        public string VerbLabel => "Use the freezer";

        public void Interact(in InteractActor actor) => Exchange();

        /// <summary>The <see cref="GinnyFreezer"/> exchange: a full pail goes in, an empty pail comes away full.</summary>
        public void Exchange()
        {
            if (!IsOpen)
            {
                EventBus.Publish(new DevNotice($"The {Name()} is not yours to use yet."));
                return;
            }
            _bucket ??= FindAnyObjectByType<ClamBucket>();
            if (_bucket == null) { EventBus.Publish(new DevNotice("You've no bucket to carry with.")); return; }

            if (_bucket.UsedUnits > 0) Deposit();
            else Withdraw();
        }

        private void Deposit()
        {
            var carried = new List<CatchItem>(_bucket.Items);
            var keptBack = new List<CatchItem>();
            int stowed = 0;
            for (int i = 0; i < carried.Count; i++)
            {
                if (TryAdd(carried[i])) stowed++;
                else keptBack.Add(carried[i]);
            }
            _bucket.Clear();
            for (int i = 0; i < keptBack.Count; i++) _bucket.TryAdd(keptBack[i]);

            EventBus.Publish(new DevNotice(stowed > 0
                ? $"The {Name()} takes {stowed}. They'll keep. ({UsedUnits}/{CapacityUnits} frozen.)"
                : $"The {Name()} is full."));
        }

        private void Withdraw()
        {
            if (_items.Count == 0) { EventBus.Publish(new DevNotice($"The {Name()} is empty.")); return; }

            SpoilContext spoil = SpoilContext.Capture();
            int taken = 0;
            while (_items.Count > 0)
            {
                CatchItem it = _items[0];
                CatchItem thawed = it.WithFreshness(Freshness.WithMode(it.Freshness, spoil.NowGameSeconds,
                                                                       it.SpoilPerDay, spoil.SecondsPerDay,
                                                                       spoil.Policy, StorageMode.Ambient));
                if (!_bucket.TryAdd(thawed)) break;   // bucket full: the rest stays frozen
                _items.RemoveAt(0);
                taken++;
            }
            if (taken > 0) SyncToSave();
            EventBus.Publish(new DevNotice(taken > 0
                ? $"You take {taken} from the {Name()}. Back on the clock now."
                : "Your bucket has no room."));
        }

        private string Name() => _def != null && !string.IsNullOrEmpty(_def.DisplayName) ? _def.DisplayName : "freezer";

        // ---- lifetime -----------------------------------------------------------------------------------

        private void OnEnable()
        {
            RefreshRegistration();
            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
            BindToSave();   // a scene enabled after the load edge binds to the save it has not read
        }

        private void OnDisable()
        {
            Interactables.Unregister(this);
            EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
        }

        // Listed until destroyed, never dropped on disable: a region hop toggles roots (PlayerStorage).
        private void OnDestroy() => PlayerStorage.Unregister(this);

        private void OnGameLoaded(GameLoaded _) => BindToSave(force: true);

        /// <summary>List it (always: the id is fixed) and offer the press when it has a Def. Called on
        /// enable; call it explicitly in EditMode.</summary>
        public void RefreshRegistration()
        {
            PlayerStorage.Register(this);
            if (_def != null) Interactables.Register(this);
            else Interactables.Unregister(this);
        }
    }
}
