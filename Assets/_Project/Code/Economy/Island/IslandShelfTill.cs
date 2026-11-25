using System;
using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// <b>The till for the goods a store sells at its shelves</b> (buying at the shelf, slice 1). The player
    /// walks up to a good, sees its name and price, and buys one there instead of asking the clerk. This sells
    /// through exactly the rows the counter's book would have sold (<see cref="BuyCatalog.BuildCounted"/>) and
    /// exactly its Confirm (<see cref="BuyCatalog.Confirm(in BuyRow, double)"/>): the same price, the same
    /// purse, the same vendor seam putting the good where that kind of good goes (a supply in the supply
    /// locker, bait in the tackle box), and the same one-item take off the shelf with its
    /// <see cref="IslandShelvesChanged"/>. Nothing new is bought or kept here.
    ///
    /// <para>Only listings whose shelf line is <see cref="IslandShelfLine.SoldAtShelf"/> are sold here; the
    /// rest stay in the book. Registered in the Core <see cref="ShelfTills"/> by
    /// <see cref="IslandShelvesHost"/>; the World module's shelf goods reach it only through
    /// <see cref="IShelfTill"/>.</para>
    ///
    /// <para><b>Cheap.</b> A good's name and price are data and do not change in a session, so they are
    /// learned once from the first build and kept; a quote then reads only the shelf's live count. A purchase
    /// rebuilds the rows once (the book's own scene sweep), on the press, never per frame.</para>
    /// </summary>
    public sealed class IslandShelfTill : IShelfTill
    {
        /// <summary>Who sells the shelf goods: the arms, the purse, the save and the licences a build reads.
        /// The game's is the live services; a test hands in its own.</summary>
        public interface IRowSource
        {
            void Build(string sellerId, double now, List<BuyRow> into);
        }

        private sealed class LiveRows : IRowSource
        {
            public static readonly LiveRows Instance = new LiveRows();

            public void Build(string sellerId, double now, List<BuyRow> into)
                => BuyCatalog.BuildCounted(sellerId, BuyCatalog.ArmsFor(sellerId), GameServices.Wallet?.Money ?? 0,
                                           GameServices.Save?.Current, GameServices.Licenses, now, into);
        }

        private readonly struct Known
        {
            public readonly string Name;
            public readonly int Price;
            public readonly IIslandShelves Shelves;
            public readonly string Line;

            public Known(string name, int price, IIslandShelves shelves, string line)
            {
                Name = name; Price = price; Shelves = shelves; Line = line;
            }
        }

        private readonly string _sellerId;
        private readonly IRowSource _rows;
        private readonly List<BuyRow> _scratch = new List<BuyRow>();
        private readonly Dictionary<string, Known> _known = new Dictionary<string, Known>(StringComparer.Ordinal);

        public IslandShelfTill(string shopId, string sellerId, IRowSource rows = null)
        {
            ShopId = shopId;
            _sellerId = sellerId;
            _rows = rows ?? LiveRows.Instance;
        }

        public string ShopId { get; }

        /// <summary>The seller whose counter rows this till sells through.</summary>
        public string SellerId => _sellerId;

        public bool TryQuote(string listingId, double totalSeconds, out ShelfQuote quote)
        {
            quote = default;
            if (!IslandCounterShelves.IsSoldAtShelf(_sellerId, listingId)) return false;
            if (!_known.TryGetValue(listingId, out Known known))
            {
                Learn(totalSeconds);
                if (!_known.TryGetValue(listingId, out known)) return false;
            }
            quote = new ShelfQuote(listingId, known.Name, known.Price, known.Shelves.CountAt(known.Line, totalSeconds));
            return true;
        }

        public ShelfBuyResult TryBuyOne(string listingId, double totalSeconds)
        {
            if (!IslandCounterShelves.IsSoldAtShelf(_sellerId, listingId)) return ShelfBuyResult.NotSold;
            _rows.Build(_sellerId, totalSeconds, _scratch);
            Remember();
            for (int i = 0; i < _scratch.Count; i++)
            {
                BuyRow row = _scratch[i];
                if (!string.Equals(row.Id, listingId, StringComparison.Ordinal)) continue;
                ShelfBuyResult judged = Judge(row);
                if (judged != ShelfBuyResult.Bought) return judged;
                // The Confirm re-asks the shelf and the vendor; a refusal there moved nothing.
                return BuyCatalog.Confirm(row, totalSeconds) ? ShelfBuyResult.Bought : ShelfBuyResult.TooDear;
            }
            return ShelfBuyResult.NotSold;
        }

        /// <summary>
        /// <b>Whether a counted row may be bought at the shelf</b>, and if not why: not a shelf good (or no
        /// vendor at the counter), an empty shelf, or a purse that does not cover it. Pure: it reads the
        /// row's quote as the book built it.
        /// </summary>
        public static ShelfBuyResult Judge(in BuyRow row)
        {
            if (row.Shelves == null || row.Vendor == null || row.Quote.Owned) return ShelfBuyResult.NotSold;
            if (row.Quote.SoldOut) return ShelfBuyResult.SoldOut;
            if (!row.Quote.Affordable) return ShelfBuyResult.TooDear;
            return ShelfBuyResult.Bought;
        }

        private void Learn(double now)
        {
            _rows.Build(_sellerId, now, _scratch);
            Remember();
        }

        // Keeps the name and price of every shelf good the last build held (data; fixed for the session).
        private void Remember()
        {
            for (int i = 0; i < _scratch.Count; i++)
            {
                BuyRow row = _scratch[i];
                if (row.Shelves == null || !IslandCounterShelves.IsSoldAtShelf(_sellerId, row.Id)) continue;
                _known[row.Id] = new Known(row.DisplayName, row.Quote.Price, row.Shelves, row.ShelfLine);
            }
        }
    }
}
