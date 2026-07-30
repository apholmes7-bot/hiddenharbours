using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The boat's cargo hold. Capacity comes from the hull (HU). Implements <see cref="IHold"/> so
    /// Fishing fills it and the market empties it, all through the Core contract.
    /// </summary>
    public class ShipHold : MonoBehaviour, IHold
    {
        [SerializeField] private BoatHullDef _hull;

        private readonly List<CatchItem> _items = new();

        /// <summary>The hull this hold reads its capacity from. Same-module read for the deck container
        /// (the tray needs the hull's container Def + deck anchor); cross-module callers keep to
        /// <see cref="IHold"/>.</summary>
        public BoatHullDef Hull => _hull;

        private void Awake()
        {
            // The hold SHOWS its contents: the deck container (fish tray now, the blue totes up the
            // ladder in M2) is this hold's diegetic readout (owner canon — no HUD counter; the tray IS
            // the read). Runtime-spawned so every boat rig with a hold gets its tray without a builder
            // re-run; play mode only (EditMode tests add ShipHolds freely and must stay presentation-free).
            if (Application.isPlaying && GetComponent<DeckContainerPresenter>() == null)
                gameObject.AddComponent<DeckContainerPresenter>();
            // The hold's cold chain (M1 §7.3): ice + lid state and the melt maths ride the same
            // runtime-spawn pattern, so every boat with a hold can take ice with no builder re-run.
            if (Application.isPlaying && GetComponent<DeckIceBox>() == null)
                gameObject.AddComponent<DeckIceBox>();
            // The hold's freshness READ (M1 §7.3): the tray says how much and how far gone, but a tint
            // cannot say WHEN — the tag adds the countdown to a buyer's refusal, and stays hidden until
            // the catch actually starts to turn. Same runtime-spawn pattern, same no-builder-re-run rule.
            if (Application.isPlaying && GetComponent<DeckFreshnessTag>() == null)
                gameObject.AddComponent<DeckFreshnessTag>();
        }

        // The hold's catch persists (M1 §7.3, SaveData v5): every mutation pushes a snapshot into the
        // save blob (cheap — a handful of units, event-time only; the DISK write stays SaveService's
        // autosave), and the GameLoaded edge restores it — so freshness survives a reload exactly
        // (spoil is a pure function of the restored state + the restored clock).
        private void OnEnable() => EventBus.Subscribe<GameLoaded>(OnGameLoaded);
        private void OnDisable() => EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);

        private void OnGameLoaded(GameLoaded _)
        {
            ISaveService save = GameServices.Save;
            if (save == null || save.Current == null) return;
            var restored = new List<CatchItem>();
            int units = HoldCatchCodec.Restore(save.Current.BoatHoldCatch, GameServices.CatchFactory, restored);
            ReplaceAll(restored);   // capacity-exempt: the hold was legal when saved
            int saved = CountUnits(save.Current.BoatHoldCatch);
            if (units < saved)
                Debug.LogWarning($"[ShipHold] Restored {units}/{saved} held catch — some species ids " +
                                 "no longer resolve (skipped, never a crash).", this);
        }

        private void SyncToSave()
        {
            ISaveService save = GameServices.Save;
            if (save == null || save.Current == null) return;
            HoldCatchCodec.Capture(_items, save.Current.BoatHoldCatch ??= new List<HeldCatchDto>());
        }

        private static int CountUnits(List<HeldCatchDto> records)
        {
            int n = 0;
            if (records != null)
                for (int i = 0; i < records.Count; i++) n += records[i].Quantity;
            return n;
        }

        public int CapacityUnits => _hull != null ? _hull.HoldUnits : 0;
        public int UsedUnits => _items.Count;
        public IReadOnlyList<CatchItem> Items => _items;

        public bool TryAdd(CatchItem item)
        {
            if (_items.Count >= CapacityUnits) return false;
            _items.Add(item);
            SyncToSave();
            return true;
        }

        public void Clear()
        {
            _items.Clear();
            SyncToSave();
        }

        /// <summary>
        /// SURGICAL replace of the hold's contents — the freshness restamp / save-restore write
        /// (M1 §7.3). Deliberately capacity-EXEMPT: callers replace like-for-like (a mode restamp of
        /// the same units) or restore a hold that was legal when saved, so refusing here could only
        /// lose catch. Never a gameplay "add" path — new catch still lands through <see cref="TryAdd"/>.
        /// </summary>
        public void ReplaceAll(IReadOnlyList<CatchItem> items)
        {
            _items.Clear();
            if (items != null)
                for (int i = 0; i < items.Count; i++)
                    _items.Add(items[i]);
            SyncToSave();
        }

        /// <summary>
        /// Swap the hull so capacity tracks the active boat (VS-16, driven by OwnedFleet). Any catch
        /// already aboard is kept (buying up the ladder grows the hold, 6→14). A small public setter
        /// so the swapper doesn't reach into the private serialized field.
        /// </summary>
        public void SetHull(BoatHullDef hull) => _hull = hull;
    }
}
