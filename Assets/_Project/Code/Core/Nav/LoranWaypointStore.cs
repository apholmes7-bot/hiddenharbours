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
    /// <para><b>Saved with the game (v24), per hull.</b> A store opened on the live <see cref="SaveData"/>
    /// and a hull id (<see cref="LoranWaypointStore(int, SaveData, string)"/>, or <see cref="BindTo"/>)
    /// loads that hull's rows from <see cref="SaveData.HullLoranWaypoints"/> with their slot numbers, and
    /// every save, entry or removal writes that hull's rows straight back (<see cref="LoranWaypointSave"/>),
    /// so the next save write carries them and a reload shows the same numbers. Other hulls' rows are
    /// never touched. A store with no save or no hull id keeps its numbers in memory only.</para>
    ///
    /// <para>A full set REFUSES another waypoint rather than dropping an old one - the chartplotter's
    /// rule: a waypoint is a deliberate act and losing one quietly is worse. Slots are numbered from
    /// 1 and a save takes the lowest free number. For the same reason a load never drops a saved row
    /// to fit: if the receiver's memory has shrunk since, every saved row is kept and the set simply
    /// stays full until the player forgets enough of them.</para>
    /// </summary>
    public sealed class LoranWaypointStore
    {
        private readonly List<LoranWaypoint> _rows;
        private readonly int _capacity;
        private SaveData _save;
        private string _hullId = "";

        /// <param name="capacity">From <see cref="LoranReceiverDef.MaxStoredWaypoints"/>; at least 1.</param>
        public LoranWaypointStore(int capacity)
        {
            _capacity = capacity < 1 ? 1 : capacity;
            _rows = new List<LoranWaypoint>(_capacity);
        }

        /// <summary>A store bound to <paramref name="hullId"/>'s rows in <paramref name="save"/>: loaded now,
        /// written back on every change.</summary>
        public LoranWaypointStore(int capacity, SaveData save, string hullId) : this(capacity)
        {
            BindTo(save, hullId);
        }

        /// <summary>The hull whose rows this store keeps; empty when unbound.</summary>
        public string HullId => _hullId;

        /// <summary>True when changes are written back to a save.</summary>
        public bool IsBound => _save != null && _hullId.Length > 0;

        /// <summary>Forget what is held and take <paramref name="hullId"/>'s rows from <paramref name="save"/>
        /// instead. A null save or an empty hull id unbinds and leaves the store empty.</summary>
        public void BindTo(SaveData save, string hullId)
        {
            _rows.Clear();
            _save = save;
            _hullId = hullId ?? "";
            if (IsBound) LoranWaypointSave.Load(_save, _hullId, _rows);
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
            WriteBack();
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
                if (_rows[i].Number == number) { _rows.RemoveAt(i); WriteBack(); return true; }
            return false;
        }

        private void WriteBack()
        {
            if (IsBound) LoranWaypointSave.Write(_save, _hullId, _rows);
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

    /// <summary>
    /// <b>The Loran numbers' rows in the save</b> (v24, <see cref="SaveData.HullLoranWaypoints"/>), keyed by
    /// hull id like every other fitment: load one hull's rows into a <see cref="LoranWaypointStore"/> on
    /// bind, rewrite that hull's rows each time its numbers change. Null-safe: a null save or an empty hull
    /// id reads as no rows and a write to it does nothing.
    /// </summary>
    public static class LoranWaypointSave
    {
        /// <summary>Add <paramref name="hullId"/>'s saved rows to <paramref name="into"/>, keeping their slot
        /// numbers, sorted by number. A row that is not a usable waypoint (the migration's heal drops them
        /// anyway) or repeats a slot number already taken is skipped.</summary>
        public static void Load(SaveData save, string hullId, List<LoranWaypoint> into)
        {
            if (save?.HullLoranWaypoints == null || string.IsNullOrEmpty(hullId) || into == null) return;
            for (int i = 0; i < save.HullLoranWaypoints.Count; i++)
            {
                LoranWaypointDto r = save.HullLoranWaypoints[i];
                if (r == null || !string.Equals(r.HullId, hullId, System.StringComparison.Ordinal)) continue;
                LoranWaypoint w = r.ToWaypoint();
                if (!w.IsValid) continue;

                int at = 0;
                while (at < into.Count && into[at].Number < w.Number) at++;
                if (at < into.Count && into[at].Number == w.Number) continue;
                into.Insert(at, w);
            }
        }

        /// <summary>Replace <paramref name="hullId"/>'s rows with <paramref name="rows"/>, in their order.
        /// Every other hull's rows stay where they are.</summary>
        public static void Write(SaveData save, string hullId, IReadOnlyList<LoranWaypoint> rows)
        {
            if (save == null || string.IsNullOrEmpty(hullId)) return;
            save.HullLoranWaypoints ??= new List<LoranWaypointDto>();
            List<LoranWaypointDto> list = save.HullLoranWaypoints;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                LoranWaypointDto r = list[i];
                if (r == null || string.Equals(r.HullId, hullId, System.StringComparison.Ordinal)) list.RemoveAt(i);
            }
            if (rows == null) return;
            for (int i = 0; i < rows.Count; i++)
            {
                LoranWaypoint w = rows[i];
                list.Add(LoranWaypointDto.From(hullId, in w));
            }
        }
    }
}
