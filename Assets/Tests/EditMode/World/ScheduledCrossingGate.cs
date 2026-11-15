using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// A test double for <see cref="ICrossingGate"/>: every crossing is open except inside the closed
    /// windows it is given, each <c>[from, to)</c> in absolute game hours. A window may be open-ended
    /// (<see cref="double.PositiveInfinity"/>) for a gate that never opens again. Pure: asked twice, it
    /// answers the same, as the contract requires.
    /// </summary>
    public sealed class ScheduledCrossingGate : ICrossingGate
    {
        private readonly string _crossingId;
        private readonly List<(double from, double to)> _closed = new List<(double, double)>();

        /// <param name="crossingId">The crossing these windows apply to; every other crossing is open.</param>
        public ScheduledCrossingGate(string crossingId) { _crossingId = crossingId; }

        /// <summary>Shut the crossing over <c>[from, to)</c>, absolute game hours.</summary>
        public ScheduledCrossingGate Closed(double from, double to)
        {
            _closed.Add((from, to));
            return this;
        }

        public bool IsOpen(string crossingId, double gameHour)
        {
            if (crossingId != _crossingId) return true;
            foreach (var (from, to) in _closed)
                if (gameHour >= from && gameHour < to) return false;
            return true;
        }

        public double NextOpenHour(string crossingId, double fromGameHour)
        {
            double t = fromGameHour;
            // Windows may chain; walk forward until t is outside all of them.
            for (int guard = 0; guard <= _closed.Count; guard++)
            {
                bool moved = false;
                foreach (var (from, to) in _closed)
                {
                    if (crossingId == _crossingId && t >= from && t < to) { t = to; moved = true; }
                }
                if (!moved) return t;
            }
            return t;
        }
    }
}
