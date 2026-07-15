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
        }

        public int CapacityUnits => _hull != null ? _hull.HoldUnits : 0;
        public int UsedUnits => _items.Count;
        public IReadOnlyList<CatchItem> Items => _items;

        public bool TryAdd(CatchItem item)
        {
            if (_items.Count >= CapacityUnits) return false;
            _items.Add(item);
            return true;
        }

        public void Clear() => _items.Clear();

        /// <summary>
        /// Swap the hull so capacity tracks the active boat (VS-16, driven by OwnedFleet). Any catch
        /// already aboard is kept (buying up the ladder grows the hold, 6→14). A small public setter
        /// so the swapper doesn't reach into the private serialized field.
        /// </summary>
        public void SetHull(BoatHullDef hull) => _hull = hull;
    }
}
