using System;
using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Dormant until explicitly composed. No Unity lifecycle, frame update, catalog hook or save writer.
    /// Construct with restored sparse records; reads and SlowTick settle the same delivery watermark.
    /// Unpaid hands must be reservations layered above this committed-stock API in later slices.
    /// </summary>
    public sealed class ShopStockService : IShopStock
    {
        private readonly IGameClock _clock;
        private readonly IShopStockStateStore _state;
        private readonly uint _worldSeed;
        private readonly ShopStockSpec[] _shops;
        private readonly Dictionary<(string shop, string line), ShopStockLine> _lines =
            new Dictionary<(string, string), ShopStockLine>();
        private readonly Action<ShopStockChanged> _notify;
        private int _observedDay = -1;
        private int _slowTickDay = -1;

        public ShopStockService(ShopStockSpec[] shops, uint worldSeed, IGameClock clock,
            IShopStockStateStore state, Action<ShopStockChanged> notify = null)
        {
            if (shops == null) throw new ArgumentNullException(nameof(shops));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _worldSeed = worldSeed;
            _notify = notify ?? EventBus.Publish<ShopStockChanged>;
            _shops = new ShopStockSpec[shops.Length];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int today = Today();
            for (int i = 0; i < shops.Length; i++)
            {
                if (shops[i] == null) throw new ArgumentException("Missing shop specification.");
                var shop = shops[i].Snapshot();
                if (!ids.Add(shop.Id)) throw new ArgumentException("Duplicate shop id.");
                _shops[i] = shop;
                foreach (var line in shop.Stock)
                {
                    _lines.Add((shop.Id, line.Id), line);
                    if (_state.TryRead(shop.Id, line.Id, out var record) &&
                        (record.Count < 0 || record.SettledThroughDay < 0 || record.SettledThroughDay > today))
                        throw new ArgumentException("Stock record is invalid or ahead of the supplied clock.");
                }
            }
            SlowTick();
        }

        public int CountOf(string shopId, string lineId) => Settle(shopId, Line(shopId, lineId), Today());
        public bool IsSoldOut(string shopId, string lineId) => CountOf(shopId, lineId) == 0;

        public bool TryTakeOne(string shopId, string lineId) => ChangeOne(shopId, lineId, true);
        public bool TryPutBackOne(string shopId, string lineId) => ChangeOne(shopId, lineId, false);

        private bool ChangeOne(string shopId, string lineId, bool take)
        {
            var line = Line(shopId, lineId);
            int today = Today();
            int count = Settle(shopId, line, today);
            int remaining;
            bool success = take ? ShopRestock.TryTakeOne(line, count, out remaining) :
                ShopRestock.TryPutBackOne(line, count, out remaining);
            if (!success) return false;
            Write(shopId, line, remaining, today);
            if (remaining != count)
                _notify(new ShopStockChanged(shopId, line.Id, remaining,
                    take ? ShopStockChange.Taken : ShopStockChange.Returned));
            return true;
        }

        public void SlowTick()
        {
            int today = Today();
            if (today == _slowTickDay) return;
            _slowTickDay = today;
            for (int i = 0; i < _shops.Length; i++)
            {
                var shop = _shops[i];
                for (int j = 0; j < shop.Stock.Length; j++) Settle(shop.Id, shop.Stock[j], today);
            }
        }

        private int Settle(string shopId, ShopStockLine line, int today)
        {
            if (!_state.TryRead(shopId, line.Id, out var record)) return line.MaxOnShelf;
            int count = ShopRestock.Settle(line, record.Count, record.SettledThroughDay, today, _worldSeed);
            // Even a non-delivery day advances the watermark; a sale cannot replay past deliveries.
            if (record.SettledThroughDay != today || record.Count != count || count == line.MaxOnShelf)
                Write(shopId, line, count, today);
            if (record.Count != count) _notify(new ShopStockChanged(shopId, line.Id, count, ShopStockChange.Restocked));
            return count;
        }

        private void Write(string shopId, ShopStockLine line, int count, int today)
        {
            if (count == line.MaxOnShelf) _state.Remove(shopId, line.Id);
            else _state.Write(new ShopStockRecord(shopId, line.Id, count, today));
        }

        private ShopStockLine Line(string shopId, string lineId)
        {
            if (!_lines.TryGetValue((shopId, lineId), out var line))
                throw new KeyNotFoundException("Unknown shop stock line.");
            return line;
        }

        private int Today()
        {
            int day = _clock.DayIndex;
            if (day < 0 || day < _observedDay)
                throw new InvalidOperationException("Stock time cannot rewind; restore into a new service.");
            _observedDay = day;
            return day;
        }
    }
}
