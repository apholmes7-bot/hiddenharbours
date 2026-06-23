using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// The on-foot <b>clam bucket</b> — the player's hand-carried hold before they have a boat (St Peters
    /// opening). Implements the Core <see cref="IHold"/> contract exactly like the boat's <c>ShipHold</c>,
    /// so the clam-dig interaction fills it and the Greywick stall empties it through the same seam — no
    /// special-casing an "on-foot hold". It holds up to <see cref="Capacity"/> clams (the design's 20-clam
    /// pail); when full, you head to Greywick to sell.
    ///
    /// <para><b>Owned-gear gated.</b> The bucket is a capability granted by owning <c>gear.bucket</c>
    /// (<see cref="PlayerGear.HasBucket()"/>, starting gear on St Peters). Until owned its capacity reads as
    /// 0, so nothing can be stowed — you can't dig into a bucket you don't have. The dig interaction also
    /// checks ownership, so this is belt-and-braces.</para>
    ///
    /// <para><b>Tunable, not a magic number.</b> The 20-unit cap is a serialized field (the bucket's hold
    /// size, the gameplay-systems mechanic the GearOffer flavour defers to us), editable in the inspector,
    /// not a literal buried in logic.</para>
    /// </summary>
    public class ClamBucket : MonoBehaviour, IHold
    {
        [Tooltip("How many clams the pail holds (the design's 20-clam bucket). The gameplay-systems hold " +
                 "mechanic the gear.bucket GearOffer defers to — a tunable, not a hard-coded number.")]
        [SerializeField] private int _capacity = 20;

        [Tooltip("If true, the bucket only has capacity once the player owns gear.bucket (PlayerGear). " +
                 "Leave on for the real opening; tests/tools can turn it off to use the bucket unconditionally.")]
        [SerializeField] private bool _requireOwnedBucket = true;

        private readonly List<CatchItem> _items = new();

        /// <summary>The pail's clam capacity (0 when the player doesn't own a bucket and ownership is required).</summary>
        public int Capacity => _capacity;

        public int CapacityUnits => (_requireOwnedBucket && !PlayerGear.HasBucket()) ? 0 : _capacity;
        public int UsedUnits => _items.Count;
        public IReadOnlyList<CatchItem> Items => _items;

        public bool TryAdd(CatchItem item)
        {
            if (_items.Count >= CapacityUnits) return false;
            _items.Add(item);
            return true;
        }

        public void Clear() => _items.Clear();

        /// <summary>Wire capacity/ownership in one call (tests / editor).</summary>
        public void Configure(int capacity, bool requireOwnedBucket)
        {
            _capacity = capacity;
            _requireOwnedBucket = requireOwnedBucket;
        }
    }
}
