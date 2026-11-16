using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>One drawn mark: a single boot, or a single tyre's length. A tyre PAIR signal becomes two.</summary>
    public struct GroundPrint
    {
        public Vector2 Position;
        public Vector2 Heading;
        public float LengthMetres;
        public float WidthMetres;
        public GroundPrintKind Kind;
        public GroundSurface Surface;
        public double MadeAt;
        public bool Alive;
    }

    /// <summary>
    /// <b>A fixed-capacity ring of prints.</b> Every slot is allocated once in the constructor; adding
    /// never allocates, and when the ring is full the OLDEST print is overwritten first. A print erased
    /// early (the tide) just goes dead in its slot and is reused when the ring comes round. Engine-light
    /// so EditMode can pin the capacity law.
    /// </summary>
    public sealed class GroundPrintRing
    {
        private readonly GroundPrint[] _slots;
        private int _next;      // the slot the next Add writes (the oldest once wrapped)
        private int _written;   // slots ever written, capped at capacity

        public GroundPrintRing(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _slots = new GroundPrint[capacity];
        }

        public int Capacity => _slots.Length;

        /// <summary>Slots that have ever been written (live or dead), up to <see cref="Capacity"/>.</summary>
        public int Written => _written;

        /// <summary>The slot <paramref name="index"/> (0..Capacity-1), by reference: no copy.</summary>
        public ref GroundPrint this[int index] => ref _slots[index];

        /// <summary>Write a print into the next slot (dropping the oldest when full); returns its slot.</summary>
        public int Add(in GroundPrint print)
        {
            int at = _next;
            _slots[at] = print;
            _slots[at].Alive = true;
            _next = (_next + 1) % _slots.Length;
            if (_written < _slots.Length) _written++;
            return at;
        }

        /// <summary>Kill the print in slot <paramref name="index"/> (erased early). Its slot stays put.</summary>
        public void Kill(int index) => _slots[index].Alive = false;

        /// <summary>How many slots hold a live print.</summary>
        public int CountAlive()
        {
            int n = 0;
            for (int i = 0; i < _written; i++) if (_slots[i].Alive) n++;
            return n;
        }

        /// <summary>Drop everything (a region change). Keeps the storage.</summary>
        public void Clear()
        {
            Array.Clear(_slots, 0, _slots.Length);
            _next = 0;
            _written = 0;
        }
    }
}
