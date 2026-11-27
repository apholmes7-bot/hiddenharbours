using System;
using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// <b>An island store's shelves, recomputed for any moment</b> (living island economy slice 2,
    /// <c>docs/design/st-peters-living-economy.md</c> §5). Each line shows its back-room good in whole items:
    /// in the morning the shelf holds what the room holds after the supply run; through the store's hours
    /// the islanders buy the day's use evenly; the player's purchases come off on top. At closing the shelf
    /// equals the ledger's level at dusk. A run the bar forbids leaves the shelves thinning day by day until
    /// they stand empty.
    ///
    /// <para><b>The shelves are never saved; the player's purchases are.</b> A read settles the
    /// <see cref="IslandStoreLedger"/> for the day (a bounded replay, cached until the day or the purchases
    /// change) and does plain arithmetic for the hour, so the shelves look the same whether the player
    /// watched the days or slept through them. What the player took is the one thing that cannot be
    /// recomputed: it is handed out as save rows (<see cref="ExportPurchases"/>) and taken back in on a load
    /// (<see cref="ImportPurchases"/>), so a loaded game's shelves are exactly the shelves that were left.
    /// A run that tops the room up clears a purchase's effect, and purchases older than the ledger's
    /// lookback are dropped (<see cref="DropOldPurchases"/>): no replay can reach them. Booked reserves
    /// instead retain their purchase history: only explicit sales and actual deliveries change them,
    /// including when a blocked crossing lasts longer than that lookback.</para>
    ///
    /// <para>Reads allocate nothing. Plain C#, no Unity lifecycle: warm it from the day tick with
    /// <see cref="SlowTick"/> or read it lazily.</para>
    /// </summary>
    public sealed class IslandStoreShelves : IIslandShelves
    {
        private struct Draw
        {
            public int Good;
            public int Line;
            public int Day;
            public double Seconds;
            public double Units;
        }

        private readonly IslandStoreLedger _ledger;
        private readonly double _secondsPerDay;
        private readonly double _opensHour;
        private readonly double _closesHour;
        private readonly Dictionary<string, int> _lineById = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly string[] _lineIds;
        private readonly int[] _goodOfLine;
        private readonly double[] _unitsPerItem;
        private readonly int[] _maxOnShelf;
        private readonly double[] _shareOfGood;
        private readonly bool[] _reservedForSales;
        private readonly double[] _reservedMorning;
        private readonly Action<int> _replayReservedDay;
        private int _reserveReadDay;
        private int _reserveStartDay;
        private readonly List<Draw> _draws = new List<Draw>();

        // The settled day, per ledger good: the level after the morning run, and the islanders' use that day.
        private readonly double[] _morning;
        private readonly double[] _use;
        private int _settledDay = -1;
        private int _settledVersion = -1;
        private int _version;

        /// <param name="shopId">The shop these shelves are in.</param>
        /// <param name="ledger">The back room; its <see cref="IslandStoreLedger.ExtraDraw"/> is taken over by
        /// these shelves (the player's purchases).</param>
        /// <param name="lines">One line per good; every good must be one the ledger stocks.</param>
        /// <param name="secondsPerDay">Clock seconds per game day (<see cref="GameConfig.SecondsPerDay"/>).</param>
        /// <param name="opensHour">Hour the islanders' buying starts.</param>
        /// <param name="closesHour">Hour the islanders have bought the day's use.</param>
        public IslandStoreShelves(string shopId, IslandStoreLedger ledger, IEnumerable<IslandShelfLine> lines,
                                  double secondsPerDay, double opensHour, double closesHour)
        {
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            if (!(secondsPerDay > 0.0)) throw new ArgumentException("Seconds per day must be positive.");
            if (!(opensHour >= 0.0 && opensHour < closesHour && closesHour <= 24.0))
                throw new ArgumentException("The store must open before it closes, within the day.");
            ShopId = shopId;
            _secondsPerDay = secondsPerDay;
            _opensHour = opensHour;
            _closesHour = closesHour;

            var goods = new List<int>();
            var units = new List<double>();
            var max = new List<int>();
            var shares = new List<double>();
            var reserved = new List<bool>();
            foreach (IslandShelfLine line in lines)
            {
                if (line == null || string.IsNullOrEmpty(line.Id)) throw new ArgumentException("A shelf line needs an id.");
                if (_lineById.ContainsKey(line.Id)) throw new ArgumentException("Duplicate shelf line id: " + line.Id);
                if (line.Good == null) throw new ArgumentException("Shelf line has no good: " + line.Id);
                if (!(line.UnitsPerItem > 0f) || line.MaxOnShelf < 1)
                    throw new ArgumentException("Shelf line needs positive item units and room: " + line.Id);
                int good = GoodIndex(ledger, line.Good);
                if (good < 0) throw new ArgumentException("The back room does not stock the good of " + line.Id);
                double share = ShareOf(line);
                if (!(share > 0.0) || share > 1.0)
                    throw new ArgumentException("A shelf line's share of its good must be above 0 and at most 1: " + line.Id);
                double shown = share;
                for (int i = 0; i < goods.Count; i++)
                    if (goods[i] == good) shown += shares[i];
                if (shown > 1.0 + ShareTolerance)
                    throw new ArgumentException("The shelf lines on one good show more than all of it: " + line.Id);
                _lineById.Add(line.Id, goods.Count);
                goods.Add(good);
                units.Add(line.UnitsPerItem);
                max.Add(line.MaxOnShelf);
                shares.Add(share);
                reserved.Add(line.ReservedForSales);
            }
            _lineIds = new string[goods.Count];
            foreach (KeyValuePair<string, int> line in _lineById) _lineIds[line.Value] = line.Key;
            _goodOfLine = goods.ToArray();
            _unitsPerItem = units.ToArray();
            _maxOnShelf = max.ToArray();
            _shareOfGood = shares.ToArray();
            _reservedForSales = reserved.ToArray();
            _reservedMorning = new double[goods.Count];
            _replayReservedDay = ReplayReservedDay;
            _morning = new double[ledger.GoodCount];
            _use = new double[ledger.GoodCount];
            _ledger.ExtraDraw = DrawnOn;
        }

        /// <summary>Builds the shelves an <see cref="IslandStoreShelvesDef"/> describes, over the ledger its
        /// <see cref="IslandStoreShelvesDef.Ledger"/> describes (see <see cref="IslandStoreLedger.FromDef"/>).</summary>
        public static IslandStoreShelves FromDef(IslandStoreShelvesDef def, IslandSupplyConfig route,
                                                 IReadOnlyDictionary<string, CarrierDef> carriers,
                                                 Func<int, bool> runPossible, double secondsPerDay,
                                                 Func<int, bool> winter = null)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            IslandStoreLedger ledger = IslandStoreLedger.FromDef(def.Ledger, route, carriers, runPossible, winter);
            return new IslandStoreShelves(def.ShopId, ledger, def.Lines, secondsPerDay, def.OpensHour, def.ClosesHour);
        }

        public string ShopId { get; }

        /// <summary>The back room these shelves follow.</summary>
        public IslandStoreLedger Ledger => _ledger;

        public bool HasLine(string lineId) => lineId != null && _lineById.ContainsKey(lineId);

        public int MaxOnShelf(string lineId) => _maxOnShelf[Line(lineId)];

        public int CountAt(string lineId, double totalSeconds)
        {
            int line = Line(lineId);
            if (!(totalSeconds > 0.0)) totalSeconds = 0.0;
            int day = DayOf(totalSeconds);
            Settle(day);
            return Items(line, StockAt(line, day, totalSeconds));
        }

        public bool TryTakeOne(string lineId, double totalSeconds)
        {
            int line = Line(lineId);
            if (!(totalSeconds > 0.0)) totalSeconds = 0.0;
            int day = DayOf(totalSeconds);
            Settle(day);
            if (Items(line, StockAt(line, day, totalSeconds)) < 1) return false;

            DropBefore(day - _ledger.LookbackDays);
            _draws.Add(new Draw { Good = _goodOfLine[line], Line = line, Day = day, Seconds = totalSeconds, Units = _unitsPerItem[line] });
            _version++;
            PurchasesChanged?.Invoke();
            return true;
        }

        // ---- the player's purchases, out to the save and back ------------------------------------------

        /// <summary>Raised after the held purchases change by a purchase (not by an import or a drop), so
        /// whoever keeps the save can write them down.</summary>
        public Action PurchasesChanged;

        /// <summary>
        /// Drops the purchases no replay can reach from <paramref name="totalSeconds"/> on: those made
        /// before the ledger's lookback, except booked reserves whose sales must survive a longer gap.
        /// True when any went (the save's rows are then out of date).
        /// </summary>
        public bool DropOldPurchases(double totalSeconds)
            => DropBefore(DayOf(Math.Max(0.0, totalSeconds)) - _ledger.LookbackDays);

        // Older draws can no longer reach any replay that starts at or after the day's lookback.
        private bool DropBefore(int oldestDay)
        {
            bool any = false;
            for (int i = _draws.Count - 1; i >= 0; i--)
            {
                if (_draws[i].Day >= oldestDay || _reservedForSales[_draws[i].Line]) continue;
                _draws.RemoveAt(i);
                any = true;
            }
            if (any) _version++;
            return any;
        }

        /// <summary>
        /// Appends the held purchases to <paramref name="into"/> as save rows, one per purchase, with the
        /// units as they were taken (a retuned item size does not change an old purchase).
        /// </summary>
        public void ExportPurchases(List<StorePurchaseRowDto> into)
        {
            if (into == null) return;
            for (int i = 0; i < _draws.Count; i++)
            {
                Draw d = _draws[i];
                into.Add(new StorePurchaseRowDto
                {
                    ShopId = ShopId, ShelfLineId = _lineIds[d.Line], Units = d.Units, Day = d.Day, Seconds = d.Seconds,
                });
            }
        }

        /// <summary>
        /// Replaces the held purchases with the rows of this shop in <paramref name="rows"/> (a loaded
        /// game). Rows of other shops, of a line this store no longer has, or with no amount are skipped.
        /// The day a purchase counts on is the day its time falls in, so the shelf and the back room always
        /// agree. Returns how many were taken in.
        /// </summary>
        public int ImportPurchases(IReadOnlyList<StorePurchaseRowDto> rows)
        {
            _draws.Clear();
            _version++;
            if (rows == null) return 0;
            for (int i = 0; i < rows.Count; i++)
            {
                StorePurchaseRowDto r = rows[i];
                if (r == null || !string.Equals(r.ShopId, ShopId, StringComparison.Ordinal)) continue;
                if (r.ShelfLineId == null || !_lineById.TryGetValue(r.ShelfLineId, out int line)) continue;
                if (!(r.Units > 0.0) || double.IsInfinity(r.Units) || !(r.Seconds >= 0.0) || double.IsInfinity(r.Seconds)) continue;
                _draws.Add(new Draw { Good = _goodOfLine[line], Line = line, Day = DayOf(r.Seconds), Seconds = r.Seconds, Units = r.Units });
            }
            return _draws.Count;
        }

        public bool RunCameOn(int day) => _ledger.RunsOn(day);

        public int DaysSinceRun(int day)
        {
            if (day < 0) return -1;
            for (int k = 0; k <= _ledger.LookbackDays && day - k >= 0; k++)
                if (_ledger.RunsOn(day - k)) return k;
            return -1;
        }

        /// <summary>The day tick: settles the day <paramref name="totalSeconds"/> falls in, so the first read
        /// of the day costs nothing. Repeated calls on one day are inert.</summary>
        public void SlowTick(double totalSeconds) => Settle(DayOf(Math.Max(0.0, totalSeconds)));

        /// <summary>The purchases held (booked reserves retain their history beyond the lookback).</summary>
        public int HeldDrawCount => _draws.Count;

        private int DayOf(double totalSeconds) => (int)Math.Floor(totalSeconds / _secondsPerDay);

        private void Settle(int day)
        {
            if (day == _settledDay && _version == _settledVersion) return;
            _reserveReadDay = day;
            _reserveStartDay = day;
            bool hasReservedDraw = false;
            for (int i = 0; i < _reservedMorning.Length; i++)
                _reservedMorning[i] = ReservedTarget(i, 0);
            for (int i = 0; i < _draws.Count; i++)
            {
                Draw draw = _draws[i];
                if (!_reservedForSales[draw.Line] || draw.Day > day) continue;
                hasReservedDraw = true;
                _reserveStartDay = Math.Min(_reserveStartDay, draw.Day);
            }
            if (hasReservedDraw)
                _ledger.Replay(day, Math.Max(0, _reserveStartDay - _ledger.LookbackDays), _replayReservedDay);
            _ledger.Settle(day);
            for (int i = 0; i < _morning.Length; i++)
            {
                _morning[i] = _ledger.LevelAtDawn(i) + _ledger.Delivered(i);
                _use[i] = _ledger.DailyUse(i, day);
            }
            _settledDay = day;
            _settledVersion = _version;
        }

        private double ReservedTarget(int line, int day)
            => Math.Min(_maxOnShelf[line] * _unitsPerItem[line],
                        _ledger.Target(_goodOfLine[line], day) * _shareOfGood[line]);

        private void ReplayReservedDay(int day)
        {
            if (day < _reserveStartDay) return;
            for (int line = 0; line < _reservedMorning.Length; line++)
            {
                if (!_reservedForSales[line]) continue;
                int good = _goodOfLine[line];
                double delivered = _ledger.Delivered(good);
                if (delivered <= 0.0) continue;
                double wanted = _ledger.Target(good, day) - _ledger.LevelAtDawn(good);
                double fraction = wanted > 0.0 ? Math.Min(1.0, delivered / wanted) : 0.0;
                double target = ReservedTarget(line, day);
                _reservedMorning[line] = Math.Min(target,
                    _reservedMorning[line] + Math.Max(0.0, target - _reservedMorning[line]) * fraction);
            }
            // Today's timed purchases are applied by StockAt, so a read before a sale stays historical.
            if (day == _reserveReadDay) return;
            for (int i = 0; i < _draws.Count; i++)
            {
                Draw draw = _draws[i];
                if (_reservedForSales[draw.Line] && draw.Day == day)
                    _reservedMorning[draw.Line] = Math.Max(0.0, _reservedMorning[draw.Line] - draw.Units);
            }
        }

        /// <summary>The line's stock at a moment of the settled day: its share of the good's morning level less
        /// the islanders' buying so far, less the player's purchases off this line so far. A line holding the
        /// whole good equals the ledger's dusk level after closing.</summary>
        private double StockAt(int line, int day, double totalSeconds)
        {
            int good = _goodOfLine[line];
            double hour = (totalSeconds - day * _secondsPerDay) / _secondsPerDay * 24.0;
            double share = hour <= _opensHour ? 0.0
                         : hour >= _closesHour ? 1.0
                         : (hour - _opensHour) / (_closesHour - _opensHour);
            double stock = _morning[good] - _use[good] * share;
            if (stock < 0.0) stock = 0.0;
            stock *= _shareOfGood[line];
            if (_reservedForSales[line]) stock = _reservedMorning[line];
            for (int i = 0; i < _draws.Count; i++)
            {
                Draw d = _draws[i];
                if (d.Line == line && d.Day == day && d.Seconds <= totalSeconds) stock -= d.Units;
            }
            return stock < 0.0 ? 0.0 : stock;
        }

        private int Items(int line, double stock)
        {
            // A whisker of tolerance so a level that is exactly N items in exact arithmetic reads N.
            double items = Math.Floor(stock / _unitsPerItem[line] + 1e-9);
            if (items <= 0.0) return 0;
            return items >= _maxOnShelf[line] ? _maxOnShelf[line] : (int)items;
        }

        /// <summary>The ledger's extra draw: everything the player took of a good on a day.</summary>
        private double DrawnOn(int good, int day)
        {
            double sum = 0.0;
            for (int i = 0; i < _draws.Count; i++)
                if (_draws[i].Good == good && _draws[i].Day == day) sum += _draws[i].Units;
            return sum;
        }

        private int Line(string lineId)
        {
            if (lineId == null || !_lineById.TryGetValue(lineId, out int line))
                throw new KeyNotFoundException("Unknown island shelf line: " + lineId);
            return line;
        }

        // Slack for shares authored as decimals (0.2 + 0.3 + 0.5) that add up to a whisker over one.
        private const double ShareTolerance = 1e-6;

        /// <summary>The share of its good a line shows; unset (0) means the whole good.</summary>
        public static double ShareOf(IslandShelfLine line) => line.ShareOfGood > 0f ? line.ShareOfGood : 1.0;

        private static int GoodIndex(IslandStoreLedger ledger, IslandGoodDef good)
        {
            for (int i = 0; i < ledger.GoodCount; i++)
                if (ledger.Good(i) == good || (good.Id != null && ledger.Good(i).Id == good.Id)) return i;
            return -1;
        }
    }
}
