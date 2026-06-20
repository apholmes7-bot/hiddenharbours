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
    }
}
