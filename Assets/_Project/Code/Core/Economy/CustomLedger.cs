using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Session-only custom, indexed by the caller's absolute game day (IGameClock.DayIndex).
    /// No clock, bus subscriptions, save writes, randomness, or relationship side effects.
    /// Decimal units keep addition exact and independent of transaction arrival order.
    /// </summary>
    public sealed class CustomLedger
    {
        private readonly SortedDictionary<int, SortedDictionary<string, SortedDictionary<string, decimal>>>
            _days = new SortedDictionary<int, SortedDictionary<string, SortedDictionary<string, decimal>>>();

        public void Record(int gameDay, ICustomSignal signal)
        {
            if (signal == null || signal.CustomUnits <= 0m) return;
            TradeCounterparty party = signal.Counterparty;
            if (!_days.TryGetValue(gameDay, out var trades))
            {
                trades = new SortedDictionary<string, SortedDictionary<string, decimal>>(StringComparer.Ordinal);
                _days.Add(gameDay, trades);
            }
            if (!trades.TryGetValue(party.TradeId, out var merchants))
            {
                merchants = new SortedDictionary<string, decimal>(StringComparer.Ordinal);
                trades.Add(party.TradeId, merchants);
            }
            merchants.TryGetValue(party.MerchantId, out decimal previous);
            merchants[party.MerchantId] = previous + signal.CustomUnits;
        }

        /// <summary>Total units for this merchant across all its trades on one day.</summary>
        public decimal UnitsFor(int gameDay, string merchantId)
        {
            if (!_days.TryGetValue(gameDay, out var trades)) return 0m;
            string id = TradeCounterparty.KnownOrUnknown(merchantId);
            decimal total = 0m;
            foreach (var merchants in trades.Values)
                if (merchants.TryGetValue(id, out decimal units)) total += units;
            return total;
        }

        /// <summary>
        /// A detached, ordinally sorted snapshot. Shares divide custom in the same authored trade,
        /// including unknown merchants in that trade's denominator. Unvisited merchants have share 0.
        /// </summary>
        public IReadOnlyList<DailyCustom> Day(int gameDay)
        {
            var rows = new List<DailyCustom>();
            if (_days.TryGetValue(gameDay, out var trades))
                foreach (var trade in trades)
                {
                    decimal total = 0m;
                    foreach (decimal units in trade.Value.Values) total += units;
                    foreach (var merchant in trade.Value)
                        rows.Add(new DailyCustom(gameDay, trade.Key, merchant.Key, merchant.Value,
                                                 merchant.Value / total));
                }
            // SEAM(relationships): hand this completed day's snapshot to the relationships consumer.
            // The caller chooses when the day is complete; reading a snapshot triggers nothing.
            return rows.AsReadOnly();
        }

        public decimal ShareFor(int gameDay, string tradeId, string merchantId)
        {
            string trade = TradeCounterparty.KnownOrUnknown(tradeId);
            string merchant = TradeCounterparty.KnownOrUnknown(merchantId);
            foreach (DailyCustom row in Day(gameDay))
                if (row.TradeId == trade && row.MerchantId == merchant) return row.Share;
            return 0m;
        }

        /// <summary>Retire a handed-over day explicitly; this ledger is never persisted.</summary>
        public void ForgetDay(int gameDay) => _days.Remove(gameDay);

        /// <summary>Fuel signals are continuous. Invalid external values contribute no custom.</summary>
        public static decimal FuelUnits(float litres)
            => litres > 0f && !float.IsInfinity(litres) && (double)litres < (double)decimal.MaxValue
                ? (decimal)litres : 0m;
    }

    public readonly struct DailyCustom
    {
        public readonly int GameDay;
        public readonly string TradeId;
        public readonly string MerchantId;
        public readonly decimal Units;
        public readonly decimal Share;

        public DailyCustom(int gameDay, string tradeId, string merchantId, decimal units, decimal share)
        {
            GameDay = gameDay;
            TradeId = tradeId;
            MerchantId = merchantId;
            Units = units;
            Share = share;
        }
    }
}
