using System;
using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// <b>The run calendar for an open-ended game</b>: whether the runner can make a daylight round trip over
    /// the bar on a day, for any day, computed in fixed blocks of days through
    /// <see cref="IslandRunCalendar.DaylightRunDays"/> and kept for the few blocks the ledger's replay reads.
    /// Each day's answer is a pure function of the tide on that day (the bar windows are found with a day of
    /// margin either side), so the block boundaries never change an answer: a day reads the same whether the
    /// game was watched or slept through (rule 5). Allocates only when a new block is computed.
    /// </summary>
    public sealed class IslandRunDays
    {
        private readonly Func<double, float> _waterAt;
        private readonly SupplyRoleDef _runner;
        private readonly IslandSupplyConfig _route;
        private readonly GameConfig _cfg;
        private readonly int _blockDays;
        private readonly int _keepBlocks;
        private readonly Dictionary<int, Func<int, bool>> _blocks = new Dictionary<int, Func<int, bool>>();
        private readonly Queue<int> _order = new Queue<int>();

        /// <param name="blockDays">Days computed together; at least the replay's span keeps the cache warm.</param>
        /// <param name="keepBlocks">Blocks held before the oldest is dropped.</param>
        public IslandRunDays(Func<double, float> waterAt, SupplyRoleDef runner, IslandSupplyConfig route,
                             GameConfig cfg, int blockDays, int keepBlocks)
        {
            _waterAt = waterAt ?? throw new ArgumentNullException(nameof(waterAt));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _route = route ?? throw new ArgumentNullException(nameof(route));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            if (blockDays < 1 || keepBlocks < 1) throw new ArgumentException("Blocks need a size and a count.");
            _blockDays = blockDays;
            _keepBlocks = keepBlocks;
        }

        /// <summary>Blocks computed so far (a test reads this to see the cache is bounded).</summary>
        public int HeldBlocks => _blocks.Count;

        /// <summary>True when the runner can go over and back in daylight on <paramref name="day"/>.</summary>
        public bool Possible(int day)
        {
            if (day < 0) return false;
            int block = day / _blockDays;
            if (!_blocks.TryGetValue(block, out Func<int, bool> days))
            {
                int first = block * _blockDays;
                days = IslandRunCalendar.DaylightRunDays(_waterAt, _runner, _route, _cfg, first, first + _blockDays - 1);
                _blocks.Add(block, days);
                _order.Enqueue(block);
                while (_order.Count > _keepBlocks) _blocks.Remove(_order.Dequeue());
            }
            return days(day);
        }
    }
}
