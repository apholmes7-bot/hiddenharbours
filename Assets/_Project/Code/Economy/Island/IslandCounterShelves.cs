using System;
using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// <b>Which counter listings come off which island shelf</b> (store sold-out slice). Built from each
    /// <see cref="IslandStoreShelvesDef"/>'s <see cref="IslandShelfLine.CounterListingIds"/> by the
    /// <see cref="IslandShelvesHost"/>, read by <see cref="BuyCatalog"/> so the seller's book refuses a good
    /// whose shelf is empty and takes one item off the shelf for each one sold. A listing bound to no shelf is
    /// not counted. Static because the book is; a test binds its own and calls <see cref="Clear"/>.
    /// </summary>
    public static class IslandCounterShelves
    {
        private readonly struct Binding
        {
            public readonly IIslandShelves Shelves;
            public readonly string LineId;
            public readonly bool AtShelf;

            public Binding(IIslandShelves shelves, string lineId, bool atShelf)
            {
                Shelves = shelves; LineId = lineId; AtShelf = atShelf;
            }
        }

        private static readonly Dictionary<(string seller, string listing), Binding> _bindings =
            new Dictionary<(string, string), Binding>();

        /// <summary>Binds every counter listing of <paramref name="def"/>'s lines, sold as
        /// <see cref="IslandStoreShelvesDef.SellerId"/>, to <paramref name="shelves"/>.</summary>
        public static void Bind(IslandStoreShelvesDef def, IIslandShelves shelves)
        {
            if (def == null || shelves == null || string.IsNullOrEmpty(def.SellerId) || def.Lines == null) return;
            foreach (IslandShelfLine line in def.Lines)
            {
                if (line?.CounterListingIds == null || !shelves.HasLine(line.Id)) continue;
                foreach (string listing in line.CounterListingIds)
                    Bind(def.SellerId, listing, shelves, line.Id, line.SoldAtShelf);
            }
        }

        /// <summary>Binds one listing sold by one seller to one shelf line. <paramref name="atShelf"/>: the
        /// player takes it off the shelf himself and the counter's book does not list it (buying at the
        /// shelf); otherwise the book sells it, counted against the shelf.</summary>
        public static void Bind(string sellerId, string listingId, IIslandShelves shelves, string lineId,
                                bool atShelf = false)
        {
            if (string.IsNullOrEmpty(sellerId) || string.IsNullOrEmpty(listingId) || shelves == null) return;
            if (!shelves.HasLine(lineId)) throw new ArgumentException("No such shelf line: " + lineId);
            _bindings[(sellerId, listingId)] = new Binding(shelves, lineId, atShelf);
        }

        /// <summary>True when the player buys this seller's listing at the shelf, not from the book.</summary>
        public static bool IsSoldAtShelf(string sellerId, string listingId)
            => !string.IsNullOrEmpty(sellerId) && !string.IsNullOrEmpty(listingId)
               && _bindings.TryGetValue((sellerId, listingId), out Binding b) && b.AtShelf;

        /// <summary>Drops every binding to <paramref name="shelves"/>.</summary>
        public static void Unbind(IIslandShelves shelves)
        {
            if (shelves == null) return;
            var gone = new List<(string, string)>();
            foreach (KeyValuePair<(string seller, string listing), Binding> kv in _bindings)
                if (ReferenceEquals(kv.Value.Shelves, shelves)) gone.Add(kv.Key);
            foreach ((string, string) key in gone) _bindings.Remove(key);
        }

        /// <summary>The shelf a seller's listing comes off, when it is counted.</summary>
        public static bool TryFind(string sellerId, string listingId, out IIslandShelves shelves, out string lineId)
        {
            shelves = null;
            lineId = null;
            if (string.IsNullOrEmpty(sellerId) || string.IsNullOrEmpty(listingId)) return false;
            if (!_bindings.TryGetValue((sellerId, listingId), out Binding b)) return false;
            shelves = b.Shelves;
            lineId = b.LineId;
            return true;
        }

        public static void Clear() => _bindings.Clear();
    }
}
