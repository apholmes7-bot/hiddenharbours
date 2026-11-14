using System;
using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    [Flags]
    public enum ShopDeliveryDays
    {
        None = 0, Monday = 1, Tuesday = 2, Wednesday = 4, Thursday = 8,
        Friday = 16, Saturday = 32, Sunday = 64
    }

    /// <summary>Logical fixture slots only. Slice 3 supplies matching anchors and artwork.</summary>
    [Serializable]
    public sealed class ShopShelfSlots
    {
        public string FixtureId;
        public int SlotCount;
        public ShopShelfSlots Snapshot() => (ShopShelfSlots)MemberwiseClone();
    }

    [Serializable]
    public sealed class ShopStockLine
    {
        public string Id;
        public string ListingId;
        public string FixtureId;
        public int FirstSlot;
        public int MaxOnShelf;
        public int UnitsPerItem;
        public ShopDeliveryDays DeliveryWeekdays;
        public int ItemsPerDelivery;
        public int Floor;
        public ShopStockLine Snapshot() => (ShopStockLine)MemberwiseClone();
    }

    /// <summary>Reserved data only; there is no credit implementation.</summary>
    [Serializable]
    public sealed class ShopCredit
    {
        public bool Enabled;
        public int MinimumLifetimeSales;
        public int Ceiling;
        public ShopCredit Snapshot() => (ShopCredit)MemberwiseClone();
    }

    /// <summary>Engine-free authoring snapshot. Counts are shelf objects, not consumable units.</summary>
    [Serializable]
    public sealed class ShopStockSpec
    {
        public string Id;
        public string SellerId;
        public string PremisesId;
        public ShopShelfSlots[] Shelves = Array.Empty<ShopShelfSlots>();
        public ShopStockLine[] Stock = Array.Empty<ShopStockLine>();
        public ShopCredit Credit = new ShopCredit();

        public ShopStockSpec Snapshot()
        {
            Validate();
            return new ShopStockSpec
            {
                Id = Id, SellerId = SellerId, PremisesId = PremisesId,
                Shelves = Array.ConvertAll(Shelves, shelf => shelf.Snapshot()),
                Stock = Array.ConvertAll(Stock, line => line.Snapshot()), Credit = Credit.Snapshot()
            };
        }

        /// <param name="stocksListing">Content validation supplies a real catalog lookup for this seller.</param>
        public void Validate(Func<string, string, bool> stocksListing = null)
        {
            Require(StableId(Id, "shop."), "Shop needs a stable shop.snake_case id.");
            Require(StableId(SellerId, "seller."), "Shop needs a seller id.");
            Require(StableId(PremisesId, "premises."), "Shop needs a premises id.");
            Require(Shelves != null && Shelves.Length > 0 && Stock != null && Stock.Length > 0,
                "Shop needs logical fixtures and stock lines.");
            Require(Credit != null && !Credit.Enabled && Credit.MinimumLifetimeSales == 0 && Credit.Ceiling == 0,
                "Credit must remain an empty, disabled data hook.");
            var fixtures = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var shelf in Shelves)
            {
                Require(shelf != null && StableId(shelf.FixtureId, "fixture.") && shelf.SlotCount > 0,
                    "Fixture needs an id and positive slot capacity.");
                Require(!fixtures.ContainsKey(shelf.FixtureId), "Duplicate fixture id.");
                fixtures.Add(shelf.FixtureId, shelf.SlotCount);
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var listings = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < Stock.Length; i++)
            {
                var line = Stock[i];
                Require(line != null && StableId(line.Id, "line."), "Stock needs a stable line id.");
                Require(ids.Add(line.Id), "Duplicate stock line id.");
                Require(!string.IsNullOrWhiteSpace(line.ListingId) && listings.Add(line.ListingId),
                    "Each listing occurs once per shop.");
                Require(line.FixtureId != null && fixtures.ContainsKey(line.FixtureId), "Unknown fixture.");
                Require(line.FirstSlot >= 0 && line.MaxOnShelf > 0 &&
                    (long)line.FirstSlot + line.MaxOnShelf <= fixtures[line.FixtureId], "Stock exceeds fixture slots.");
                Require(line.UnitsPerItem > 0 && line.Floor >= 0 && line.Floor <= line.MaxOnShelf,
                    "Invalid pack size or stock floor.");
                int days = (int)line.DeliveryWeekdays;
                int validDays = (1 << ((int)Weekday.Sunday + 1)) - 1;
                Require(days > 0 && (days & ~validDays) == 0 && line.ItemsPerDelivery > 0,
                    "Restock needs delivery weekdays and a positive object count.");
                if (stocksListing != null)
                    Require(stocksListing(SellerId, line.ListingId), "Listing is missing or not sold by this seller: " + line.ListingId);
                for (int j = 0; j < i; j++)
                {
                    var previous = Stock[j];
                    Require(previous.FixtureId != line.FixtureId ||
                        (long)previous.FirstSlot + previous.MaxOnShelf <= line.FirstSlot ||
                        (long)line.FirstSlot + line.MaxOnShelf <= previous.FirstSlot, "A shelf slot is assigned twice.");
                }
            }
        }

        private static void Require(bool valid, string message)
        {
            if (!valid) throw new ArgumentException(message);
        }

        private static bool StableId(string value, string prefix)
        {
            if (value == null || !value.StartsWith(prefix, StringComparison.Ordinal) || value.Length == prefix.Length)
                return false;
            for (int i = prefix.Length; i < value.Length; i++)
                if ((value[i] < 'a' || value[i] > 'z') && (value[i] < '0' || value[i] > '9') && value[i] != '_')
                    return false;
            return true;
        }
    }
}
