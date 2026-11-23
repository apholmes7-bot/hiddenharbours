using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// What came of asking to buy one item off a store's shelf (buying at the shelf, slice 1). Every value
    /// but <see cref="Bought"/> means nothing changed: no money moved, nothing was given, the shelf is as it
    /// was. Append-only.
    /// </summary>
    public enum ShelfBuyResult
    {
        /// <summary>One item was paid for, taken off the shelf and put where that good goes.</summary>
        Bought = 0,

        /// <summary>This store does not sell that good at its shelves (or nobody is at the till).</summary>
        NotSold = 1,

        /// <summary>The shelf is empty.</summary>
        SoldOut = 2,

        /// <summary>The player's money does not cover the price.</summary>
        TooDear = 3,

        /// <summary>The good is carried off in a hand, and no hand is free for it.</summary>
        HandsFull = 4,
    }

    /// <summary>A shelf good as the player sees it on approach: its name, its price and how many are on
    /// the shelf. A value; reading one never charges or gives anything.</summary>
    public readonly struct ShelfQuote
    {
        /// <summary>The counter listing (<c>supply.bread</c>, <c>bait.capelin</c>).</summary>
        public readonly string ListingId;

        /// <summary>The good's name for people.</summary>
        public readonly string DisplayName;

        /// <summary>What one costs.</summary>
        public readonly int Price;

        /// <summary>Items on the shelf now.</summary>
        public readonly int OnShelf;

        public bool SoldOut => OnShelf <= 0;

        public ShelfQuote(string listingId, string displayName, int price, int onShelf)
        {
            ListingId = listingId; DisplayName = displayName ?? ""; Price = price; OnShelf = onShelf;
        }
    }

    /// <summary>
    /// <b>A store's till for the goods on its shelves</b> (buying at the shelf, slice 1). The Economy module
    /// implements it over the counter's own purchase seams; the World module's shelf goods ask it, by shop
    /// id through <see cref="ShelfTills"/>, without naming an Economy class (rule 4).
    /// FLAG lead-architect: additive Core contract (shelf till).
    /// </summary>
    public interface IShelfTill
    {
        /// <summary>The shop (<c>shop.snake_case</c>) whose shelves this till sells off.</summary>
        string ShopId { get; }

        /// <summary>The name, price and shelf count of <paramref name="listingId"/> at
        /// <paramref name="totalSeconds"/>; false when this store does not sell it at its shelves.</summary>
        bool TryQuote(string listingId, double totalSeconds, out ShelfQuote quote);

        /// <summary>Buys ONE of <paramref name="listingId"/> off the shelf at <paramref name="totalSeconds"/>:
        /// the price is taken, the shelf drops by one and the good goes where it goes for that kind of good.
        /// Anything but <see cref="ShelfBuyResult.Bought"/> changed nothing.</summary>
        ShelfBuyResult TryBuyOne(string listingId, double totalSeconds);
    }

    /// <summary>Where a store's till can be asked for, by shop. Registered by the Economy module, read by
    /// the World module's shelf goods. Cleared by <see cref="GameServices.Reset"/>.</summary>
    public static class ShelfTills
    {
        private static readonly Dictionary<string, IShelfTill> _byShop =
            new Dictionary<string, IShelfTill>(StringComparer.Ordinal);

        /// <summary>Registers (or replaces) the till of <see cref="IShelfTill.ShopId"/>.</summary>
        public static void Register(IShelfTill till)
        {
            if (till == null || string.IsNullOrEmpty(till.ShopId)) return;
            _byShop[till.ShopId] = till;
        }

        /// <summary>Removes this till, only if it is the one registered for its shop.</summary>
        public static void Unregister(IShelfTill till)
        {
            if (till == null || string.IsNullOrEmpty(till.ShopId)) return;
            if (_byShop.TryGetValue(till.ShopId, out IShelfTill held) && ReferenceEquals(held, till))
                _byShop.Remove(till.ShopId);
        }

        /// <summary>The till of a shop, when one is registered.</summary>
        public static bool TryGet(string shopId, out IShelfTill till)
        {
            till = null;
            return !string.IsNullOrEmpty(shopId) && _byShop.TryGetValue(shopId, out till) && till != null;
        }

        public static void Clear() => _byShop.Clear();
    }
}
