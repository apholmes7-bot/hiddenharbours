using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One set of "the numbers"</b>: a spot kept as the two time differences the set showed there,
    /// on a named chain and secondary pair, under a short slot number. This is what a skipper wrote
    /// in the back of the logbook for a ground, and what one skipper might tell another.
    ///
    /// <para>Kept as TDs, not as a position, on purpose: the chain's error is repeatable, so coming
    /// back to the same numbers brings the boat back to the same water even where the chart and the
    /// numbers disagree. Resolve to a position only to steer (<see cref="LoranReceiver.TryResolve"/>).</para>
    /// </summary>
    public readonly struct LoranWaypoint
    {
        /// <summary>The slot number shown on the set, 1-based.</summary>
        public readonly int Number;
        public readonly string ChainId;
        public readonly int SecondaryA, SecondaryB;
        public readonly double TdA, TdB;

        public LoranWaypoint(int number, string chainId, int secondaryA, int secondaryB, double tdA, double tdB)
        {
            Number = number; ChainId = chainId ?? "";
            SecondaryA = secondaryA; SecondaryB = secondaryB; TdA = tdA; TdB = tdB;
        }

        public bool IsValid => Number > 0 && !string.IsNullOrEmpty(ChainId)
                               && SecondaryA >= 0 && SecondaryB >= 0 && SecondaryA != SecondaryB
                               && !double.IsNaN(TdA) && !double.IsNaN(TdB)
                               && !double.IsInfinity(TdA) && !double.IsInfinity(TdB);
    }

    /// <summary>
    /// <b>The numbers the set remembers</b> for one hull's Loran: save the current reading, enter a
    /// pair by hand, forget one, look one up by its slot number.
    ///
    /// <para>⚠ <b>Runtime only.</b> Nothing here is saved yet: the waypoints are lost on reload. When
    /// they persist they need one append-only, per-hull list in the save (hull id, slot number, chain
    /// id, secondary indexes A/B, TD A, TD B, all plain fields), read and written through a locker
    /// the way <c>InstrumentLocker</c> keeps the per-hull instrument rows. That is a save-version
    /// step of its own and is not taken here.</para>
    ///
    /// <para>A full set REFUSES another waypoint rather than dropping an old one - the chartplotter's
    /// rule: a waypoint is a deliberate act and losing one quietly is worse. Slots are numbered from
    /// 1 and a save takes the lowest free number.</para>
    /// </summary>
    public sealed class LoranWaypointStore
    {
        private readonly List<LoranWaypoint> _rows;
        private readonly int _capacity;

        /// <param name="capacity">From <see cref="LoranReceiverDef.MaxStoredWaypoints"/>; at least 1.</param>
        public LoranWaypointStore(int capacity)
        {
            _capacity = capacity < 1 ? 1 : capacity;
            _rows = new List<LoranWaypoint>(_capacity);
        }

        public int Capacity => _capacity;
        public int Count => _rows.Count;
        public bool IsFull => _rows.Count >= _capacity;

        /// <summary>The waypoint at list position <paramref name="index"/> (sorted by number).</summary>
        public LoranWaypoint At(int index) => _rows[index];

        /// <summary>Keep the numbers the set is showing now. False (and number 0) when the set shows
        /// no numbers or the store is full.</summary>
        public bool TrySave(in LoranReading reading, out int number)
        {
            number = 0;
            if (!reading.HasNumbers) return false;
            return TryEnter(reading.ChainId, reading.SecondaryA, reading.SecondaryB, reading.TdA, reading.TdB,
                            out number);
        }

        /// <summary>Enter a pair of numbers by hand (another skipper's numbers, an old logbook). False
        /// when the store is full or the entry is not usable.</summary>
        public bool TryEnter(string chainId, int secondaryA, int secondaryB, double tdA, double tdB, out int number)
        {
            number = 0;
            if (IsFull) return false;
            int free = LowestFreeNumber();
            var row = new LoranWaypoint(free, chainId, secondaryA, secondaryB, tdA, tdB);
            if (!row.IsValid) return false;
            int at = 0;
            while (at < _rows.Count && _rows[at].Number < free) at++;
            _rows.Insert(at, row);
            number = free;
            return true;
        }

        /// <summary>The waypoint in slot <paramref name="number"/>, if any.</summary>
        public bool TryGet(int number, out LoranWaypoint waypoint)
        {
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i].Number == number) { waypoint = _rows[i]; return true; }
            waypoint = default;
            return false;
        }

        /// <summary>Forget slot <paramref name="number"/>. True iff something was removed.</summary>
        public bool Remove(int number)
        {
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i].Number == number) { _rows.RemoveAt(i); return true; }
            return false;
        }

        private int LowestFreeNumber()
        {
            int next = 1;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Number != next) break;
                next++;
            }
            return next;
        }
    }
}
