using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// AUNT GINNY'S FREEZER (M1 §7.3) — the mode-setting interactable that buys TIME ASHORE: free,
    /// but only at home. Walk up with your pail and press the key: whatever you're carrying goes in
    /// and is FROZEN (arrested — and because <see cref="Freshness.WithMode"/> settles first, the hour
    /// it spent in the sun on the way up is banked forever, never undone). Press again with an empty
    /// pail and the lot comes back out, ambient and on the clock again.
    ///
    /// <para>Deliberately shallow in M1 (the §7.3 note): it exists so Ginny can hold your catch until
    /// you've a whole bucket to carry across the bar. Holding stock to time the MARKET is M2-21's —
    /// don't grow this into it. Implements <see cref="IHold"/> so persistence and the fill visuals
    /// read it through the same Core contract as every other hold.</para>
    ///
    /// <para>Proximity + on-foot gating is the stall pattern (<see cref="StallReach"/> — no builder
    /// wiring for the player ref); the exchange partner is the player's hand-carried
    /// <see cref="ClamBucket"/>. New Input System only; feedback rides <see cref="DevNotice"/>.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GinnyFreezer : MonoBehaviour, IHold, IHoldRemove, IStorageContainer
    {
        /// <summary>The freezer's fixed id (<see cref="StorageIds.GinnyFreezer"/>).</summary>
        public string StorageId => StorageIds.GinnyFreezer;

        /// <summary>After the boat's hold (<see cref="PlayerStorage.PriorityHomeFreezer"/>).</summary>
        public int StoragePriority => PlayerStorage.PriorityHomeFreezer;

        // The save blob this freezer last restored from: a scene enabled after GameLoaded binds on enable.
        private SaveData _boundTo;

        [Tooltip("How many units the chest freezer holds. A tunable, not a magic number (rule 6).")]
        [SerializeField] private int _capacity = 20;

        [Tooltip("Interact key (shared with other proximity interactables — ranges don't overlap, the " +
                 "WorldInteractor precedent). ui-ux owns the real Interact mapping later (§7.6).")]
        [SerializeField] private Key _interactKey = Key.F;

        [Tooltip("On-foot + in-range gate (the shared StallReach pattern).")]
        [SerializeField] private StallReach _reach = new StallReach();

        private readonly List<CatchItem> _items = new();
        private ClamBucket _bucket;

        public int CapacityUnits => _capacity;
        public int UsedUnits => _items.Count;
        public IReadOnlyList<CatchItem> Items => _items;

        /// <summary>Adds one unit, FROZEN (settled first — anything entering this box is arrested).</summary>
        public bool TryAdd(CatchItem item)
        {
            if (!CatchKeep.MayKeep(in item)) return false;   // protected: release-only, never held
            if (_items.Count >= _capacity) return false;
            if (!IsBound()) return false;   // the saved catch is not read yet: a write would bury it
            SpoilContext spoil = SpoilContext.Capture();
            _items.Add(item.WithFreshness(Freshness.WithMode(item.Freshness, spoil.NowGameSeconds,
                                                             item.SpoilPerDay, spoil.SecondsPerDay,
                                                             spoil.Policy, StorageMode.Frozen)));
            SyncToSave();
            return true;
        }

        public void Clear()
        {
            if (!IsBound()) return;
            _items.Clear();
            SyncToSave();
        }

        /// <summary>Take out the one item at <paramref name="index"/> and nothing else (IHoldRemove).</summary>
        public bool TryRemoveAt(int index)
        {
            if (index < 0 || index >= _items.Count || !IsBound()) return false;
            _items.RemoveAt(index);
            SyncToSave();
            return true;
        }

        /// <summary>Surgical restore write (save/load) — items land EXACTLY as given, no re-mode.</summary>
        public void ReplaceAll(IReadOnlyList<CatchItem> items)
        {
            if (!IsBound()) return;
            _items.Clear();
            if (items != null)
                for (int i = 0; i < items.Count; i++)
                    _items.Add(items[i]);
            SyncToSave();
        }

        // The picture of what is in it (fish art pass 2, 4a). The freezer already standing in a saved scene
        // predates it, so it is added here when the scene did not carry one; a rebuilt scene carries it.
        private void Awake()
        {
            if (!TryGetComponent(out FreezerContentsPresenter _)) gameObject.AddComponent<FreezerContentsPresenter>();
        }

        private void OnEnable()
        {
            _reach.Enable();
            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
            // One of the player's storage containers (what the gift rows list): listed on enable, taken
            // off on destroy, never on disable, so a region hop does not drop it (PlayerStorage).
            PlayerStorage.Register(this);
            // The save is the truth and this freezer a view of it: a scene enabled AFTER the GameLoaded edge
            // (loaded late, or toggled on by a crossing) binds to the save it has not yet read, instead of
            // starting empty and then writing that empty over the saved catch on its first change.
            BindToSave();
        }

        private void OnDestroy() => PlayerStorage.Unregister(this);

        private void OnDisable()
        {
            _reach.Disable();
            EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
        }

        // Banked catch persists (M1 §7.3, SaveData v5) — the ShipHold pattern; restore lands items
        // EXACTLY as saved (already Frozen), never re-moded.
        private void OnGameLoaded(GameLoaded _) => BindToSave(force: true);

        /// <summary>Read the catch back from the save it has not read yet (or <paramref name="force"/>).
        /// Public for EditMode, where <c>OnEnable</c> does not fire.
        ///
        /// <para>⚠️ With no <see cref="GameServices.CatchFactory"/> yet, a restore rebuilds nothing, so it does
        /// NOT count as read: the in-memory view is emptied (as a factory-less restore always did), the saved
        /// <c>FreezerCatch</c> is left exactly as it is, and the freezer stays unbound (changes refused, see
        /// <see cref="IsBound"/>) until a later bind finds a factory.</para></summary>
        public void BindToSave(bool force = false)
        {
            SaveData data = GameServices.Save?.Current;
            if (data == null || (!force && ReferenceEquals(data, _boundTo))) return;
            ICatchItemFactory factory = GameServices.CatchFactory;
            if (factory == null)
            {
                _items.Clear();
                _boundTo = null;
                return;
            }
            _boundTo = data;
            HoldCatchCodec.Restore(data.FreezerCatch, factory, _items);
        }

        /// <summary>True when a change may be written: no save to guard, or the current save has been read
        /// (binding now if it can).</summary>
        public bool IsBound()
        {
            SaveData data = GameServices.Save?.Current;
            if (data == null) return true;
            if (!ReferenceEquals(data, _boundTo)) BindToSave();
            return ReferenceEquals(data, _boundTo);
        }

        private void SyncToSave()
        {
            ISaveService save = GameServices.Save;
            if (save == null || save.Current == null) return;
            HoldCatchCodec.Capture(_items, save.Current.FreezerCatch ??= new List<HeldCatchDto>());
        }

        private void Update()
        {
            var kb = Keyboard.current;   // New Input System only
            if (kb == null || !kb[_interactKey].wasPressedThisFrame) return;
            if (!_reach.CanInteract(transform.position)) return;
            Exchange();
        }

        /// <summary>Carrying catch → it all goes in (what fits). Empty-handed → the lot comes out
        /// (what fits), ambient again. Public so tests drive it without input.</summary>
        public void Exchange()
        {
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
                if (TryAdd(carried[i])) stowed++;   // TryAdd freezes (settle-then-arrest)
                else keptBack.Add(carried[i]);
            }
            _bucket.Clear();
            for (int i = 0; i < keptBack.Count; i++) _bucket.TryAdd(keptBack[i]);

            EventBus.Publish(new DevNotice(stowed > 0
                ? $"Ginny's freezer takes {stowed} — they'll keep. ({UsedUnits}/{_capacity} frozen.)"
                : "The freezer is full."));
        }

        private void Withdraw()
        {
            if (_items.Count == 0) { EventBus.Publish(new DevNotice("The freezer is empty.")); return; }

            SpoilContext spoil = SpoilContext.Capture();
            int taken = 0;
            for (int i = 0; i < _items.Count;)
            {
                CatchItem it = _items[i];
                CatchItem thawed = it.WithFreshness(Freshness.WithMode(it.Freshness, spoil.NowGameSeconds,
                                                                       it.SpoilPerDay, spoil.SecondsPerDay,
                                                                       spoil.Policy, StorageMode.Ambient));
                if (_bucket.TryAdd(thawed)) { _items.RemoveAt(i); taken++; }
                else break;   // bucket full — the rest stays frozen
            }
            if (taken > 0) SyncToSave();   // the withdraw mutates _items directly — snapshot it too
            EventBus.Publish(new DevNotice(taken > 0
                ? $"You take {taken} from the freezer — back on the clock now."
                : "Your bucket has no room."));
        }
    }
}
