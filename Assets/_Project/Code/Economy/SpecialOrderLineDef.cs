using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HiddenHarbours.Economy
{
    [CreateAssetMenu(menuName = "Economy/Special Order Line", fileName = "SpecialOrderLine")]
    public sealed class SpecialOrderLineDef : ScriptableObject
    {
        public string Id;
        public BaitDef Bait;
        public string SellerId;
        [Min(1)] public int DeliveryDays;
        [Min(1)] public int MaxOnOrder;

        public string ItemId => Bait == null ? null : Bait.Id;
        public int Price => Bait == null ? 0 : Bait.Price;

        public SpecialOrderLine BuildLine() => new SpecialOrderLine(
            Id, ItemId, SellerId, Price, DeliveryDays, MaxOnOrder);
    }

    /// <summary>One immutable offer. Price and limit count individual delivered units.</summary>
    public sealed class SpecialOrderLine
    {
        public string Id { get; }
        public string ItemId { get; }
        public string SellerId { get; }
        public int Price { get; }
        public int DeliveryDays { get; }
        public int MaxOnOrder { get; }

        public SpecialOrderLine(string id, string itemId, string sellerId, int price,
            int deliveryDays, int maxOnOrder)
        {
            if (!StableId(id, "specialorder") || !StableId(itemId, "bait") || !StableId(sellerId, "seller")
                || price <= 0 || deliveryDays <= 0 || maxOnOrder <= 0)
                throw new ArgumentException("Order lines require stable ids and positive price, wait and limit.");
            Id = id; ItemId = itemId; SellerId = sellerId;
            Price = price; DeliveryDays = deliveryDays; MaxOnOrder = maxOnOrder;
        }

        private static bool StableId(string id, string prefix = null) => id != null
            && Regex.IsMatch(id, @"\A[a-z][a-z0-9_]*\.[a-z][a-z0-9_]*\z")
            && (prefix == null || id.StartsWith(prefix + ".", StringComparison.Ordinal));
    }
}
