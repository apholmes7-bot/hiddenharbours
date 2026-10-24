using System;
using System.Collections.Generic;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// NMC H4: which baked road chunks hold the streamer's slots, decided without the engine (so it is tested
    /// headless). Each frame the streamer names the chunks the camera sees, a ring wider; the planner wants the
    /// baked ones among them nearest the view's centre first, up to its capacity (the rest are its
    /// <see cref="Overflow"/>: drawn as the ground until they come nearer), starts loads for the wanted chunks it
    /// does not hold, and frees slots for them from the chunks wanted longest ago.
    ///
    /// <para>A chunk is absent, loading (its slot reserved, its maps on their way), resident (its slot published to
    /// the index) or failed (a map was refused: it stays the ground, and is not tried again). A loading chunk is
    /// never evicted, so a slot's maps always belong to the chunk the index names. An evicted chunk is reported so
    /// the streamer clears its index before the slot is reused: the index never points at another chunk's maps.</para>
    ///
    /// <para>Allocation-free per frame once built: the buffers are sized to the grid and the capacity.</para>
    /// </summary>
    public sealed class RoadFloorSlots
    {
        public enum State : byte { Absent, Loading, Resident, Failed }

        /// <summary>A load the streamer starts: this grid chunk into this slot.</summary>
        public struct Load { public int Chunk, Slot; }

        readonly int _w, _h, _capacity;
        readonly bool[] _baked;
        readonly State[] _state;
        readonly int[] _slot, _lastWanted, _holder;
        readonly long[] _keys;
        readonly int[] _candidates;
        readonly Stack<int> _free;
        int _wantedCount;

        /// <param name="gridW">The grid's chunks east.</param>
        /// <param name="gridH">The grid's chunks south.</param>
        /// <param name="baked">Per grid chunk (row-major, row 0 north): whether the bake holds it.</param>
        /// <param name="capacity">The slots: the arrays' depth.</param>
        public RoadFloorSlots(int gridW, int gridH, bool[] baked, int capacity)
        {
            if (gridW <= 0 || gridH <= 0) throw new ArgumentOutOfRangeException(nameof(gridW), "an empty grid.");
            if (baked == null || baked.Length != gridW * gridH) throw new ArgumentException("one flag per grid chunk.", nameof(baked));
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "no slots.");
            _w = gridW;
            _h = gridH;
            _capacity = capacity;
            _baked = (bool[])baked.Clone();
            int n = gridW * gridH;
            _state = new State[n];
            _slot = new int[n];
            _lastWanted = new int[n];
            for (int i = 0; i < n; i++) { _slot[i] = -1; _lastWanted[i] = int.MinValue; }
            _holder = new int[capacity];
            _free = new Stack<int>(capacity);
            for (int s = capacity - 1; s >= 0; s--) { _holder[s] = -1; _free.Push(s); }
            _keys = new long[n];
            _candidates = new int[n];
        }

        public int Capacity => _capacity;

        /// <summary>The baked chunks the last <see cref="Want"/> saw but could not hold: the view wants more than
        /// the slots. They are drawn as the ground until nearer chunks free them.</summary>
        public int Overflow { get; private set; }

        /// <summary>True when the last <see cref="Want"/> left a wanted chunk absent (the loads were throttled or
        /// every slot was loading): the streamer plans again next frame even if the view has not moved.</summary>
        public bool Pending { get; private set; }

        public State StateOf(int chunk) => _state[chunk];

        /// <summary>The chunk's slot while it is loading or resident, else -1.</summary>
        public int SlotOf(int chunk) => _slot[chunk];

        /// <summary>The chunks loading or resident.</summary>
        public int Held => _capacity - _free.Count;

        /// <summary>
        /// Plans one frame. The view is the inclusive chunk rectangle [x0, x1] x [y0, y1] (y south), already
        /// widened by the streamer's ring and clipped here to the grid; its centre is (centreX, centreY) in chunks
        /// (a chunk's own centre is its index + 0.5). Starts at most <paramref name="maxStarts"/> loads into
        /// <paramref name="loads"/> and names every chunk it evicts in <paramref name="evicted"/> (both cleared first).
        /// </summary>
        public void Want(int frame, int x0, int y0, int x1, int y1, float centreX, float centreY, int maxStarts,
                         List<Load> loads, List<int> evicted)
        {
            loads.Clear();
            evicted.Clear();
            x0 = Math.Max(x0, 0); y0 = Math.Max(y0, 0);
            x1 = Math.Min(x1, _w - 1); y1 = Math.Min(y1, _h - 1);

            int count = 0;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int c = y * _w + x;
                if (!_baked[c] || _state[c] == State.Failed) continue;
                double dx = x + 0.5 - centreX, dy = y + 0.5 - centreY;
                // Nearest first, the grid's order breaking ties: the same view always plans the same way.
                _keys[count] = (long)Math.Round((dx * dx + dy * dy) * 1024.0) * _baked.Length + c;
                _candidates[count] = c;
                count++;
            }
            Array.Sort(_keys, _candidates, 0, count);

            int wanted = Math.Min(count, _capacity);
            Overflow = count - wanted;
            for (int i = 0; i < wanted; i++) _lastWanted[_candidates[i]] = frame;
            _wantedCount = wanted;

            bool pending = false;
            for (int i = 0; i < wanted; i++)
            {
                int c = _candidates[i];
                if (_state[c] != State.Absent) continue;
                if (loads.Count >= maxStarts) { pending = true; break; }
                int s = TakeSlot(frame, evicted);
                if (s < 0) { pending = true; break; }
                _state[c] = State.Loading;
                _slot[c] = s;
                _holder[s] = c;
                loads.Add(new Load { Chunk = c, Slot = s });
            }
            Pending = pending;
        }

        /// <summary>The chunk's maps are in its slot: the streamer publishes it to the index.</summary>
        public void Loaded(int chunk)
        {
            if (_state[chunk] != State.Loading) throw new InvalidOperationException($"chunk {chunk} is not loading.");
            _state[chunk] = State.Resident;
        }

        /// <summary>A map was refused: the chunk frees its slot and stays the ground.</summary>
        public void LoadFailed(int chunk)
        {
            if (_state[chunk] != State.Loading) throw new InvalidOperationException($"chunk {chunk} is not loading.");
            Release(chunk);
            _state[chunk] = State.Failed;
        }

        /// <summary>A free slot, else the slot of the resident chunk wanted longest ago (not this frame); -1 when every
        /// slot is loading or wanted now.</summary>
        int TakeSlot(int frame, List<int> evicted)
        {
            if (_free.Count > 0) return _free.Pop();
            int best = -1;
            for (int s = 0; s < _capacity; s++)
            {
                int c = _holder[s];
                if (c < 0 || _state[c] != State.Resident || _lastWanted[c] == frame) continue;
                if (best < 0 || _lastWanted[c] < _lastWanted[_holder[best]]
                    || (_lastWanted[c] == _lastWanted[_holder[best]] && c < _holder[best]))
                    best = s;
            }
            if (best < 0) return -1;
            int old = _holder[best];
            Release(old);
            _state[old] = State.Absent;
            evicted.Add(old);
            return _free.Pop();
        }

        void Release(int chunk)
        {
            int s = _slot[chunk];
            _slot[chunk] = -1;
            if (s >= 0) { _holder[s] = -1; _free.Push(s); }
        }

        /// <summary>The chunks the last <see cref="Want"/> wanted, nearest first.</summary>
        public int WantedCount => _wantedCount;
        public int WantedAt(int i) => _candidates[i];
    }
}
