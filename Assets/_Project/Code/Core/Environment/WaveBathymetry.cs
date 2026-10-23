using System;
using System.Collections.Generic;
using System.IO;

namespace HiddenHarbours.Core
{
    /// <summary>Offline first-arrival eikonal and energy transport. No engine, clock or terrain object.</summary>
    public static class WaveBathymetry
    {
        // Format constants, not balance controls. Integer division rounds toward zero; roots round down.
        public const int PathUnitsPerMeter = 1024;
        public const int DirectionUnits = 32767;
        public const int EnergyUnits = 65536;
        public const int HeightUnits = 4096;
        public const int BytesPerCell = 16;
        public const int MaxExactPhaseUnits = 1 << 24;
        const long Far = long.MaxValue / 8;

        public sealed class Settings
        {
            public int CellSizeMm, OffshoreDepthMm, MinimumDepthMm, DryDepthMm;
            public int HeightCapMilli, TransportFloorMilli, OpenEdges;
            public int[] DirectionX, DirectionY, WaterLevelsMm;

            public void Validate()
            {
                if (CellSizeMm < 1000 || CellSizeMm > 32000 || OffshoreDepthMm < 100
                    || OffshoreDepthMm > 100000 || MinimumDepthMm < 10
                    || MinimumDepthMm > OffshoreDepthMm || DryDepthMm < 0 || DryDepthMm > MinimumDepthMm
                    || HeightCapMilli < 1000 || HeightCapMilli > 8000
                    || TransportFloorMilli < 100 || TransportFloorMilli > 1000 || OpenEdges < 1 || OpenEdges > 15)
                    throw new ArgumentException("Invalid bathymetry limits.");
                if (DirectionX == null || DirectionY == null || DirectionX.Length != DirectionY.Length
                    || DirectionX.Length < 1 || DirectionX.Length > 64)
                    throw new ArgumentException("Expected 1..64 direction vectors.");
                for (int b = 0; b < DirectionX.Length; b++)
                {
                    long norm = Root((long)DirectionX[b] * DirectionX[b] + (long)DirectionY[b] * DirectionY[b]);
                    if (Math.Abs(norm - DirectionUnits) > 2)
                        throw new ArgumentException("Direction vectors must use signed Q15 unit components.");
                }
                if (WaterLevelsMm == null || WaterLevelsMm.Length < 1 || WaterLevelsMm.Length > 17)
                    throw new ArgumentException("Expected 1..17 water levels.");
                for (int i = 0; i < WaterLevelsMm.Length; i++)
                    if (Math.Abs((long)WaterLevelsMm[i]) > 100000 || (i > 0 && WaterLevelsMm[i] <= WaterLevelsMm[i - 1]))
                        throw new ArgumentException("Water levels must increase, within 100 m of datum.");
            }
        }

        public sealed class Grid
        {
            public readonly int Width, Height, MinXmm, MinYmm;
            public readonly int[] ElevationMm, StillLevelMm;
            public Grid(int width, int height, int minXmm, int minYmm, int[] elevationMm, int[] stillLevelMm = null)
            {
                if (width < 2 || height < 2 || width > 2048 || height > 2048
                    || elevationMm == null || elevationMm.Length != width * height
                    || (stillLevelMm != null && stillLevelMm.Length != width * height)
                    || Math.Abs((long)minXmm) > 10000000 || Math.Abs((long)minYmm) > 10000000)
                    throw new ArgumentException("Invalid bathymetry grid.");
                foreach (int e in elevationMm)
                    if (Math.Abs((long)e) > 100000) throw new ArgumentException("Elevation exceeds 100 m.");
                if (stillLevelMm != null)
                    foreach (int s in stillLevelMm)
                        if (s != int.MinValue && Math.Abs((long)s) > 100000)
                            throw new ArgumentException("Still level exceeds 100 m.");
                Width = width; Height = height; MinXmm = minXmm; MinYmm = minYmm;
                ElevationMm = elevationMm; StillLevelMm = stillLevelMm;
            }
        }

        public sealed class Slice
        {
            // Bottom-up, x fastest. Phase is optical distance in metres; S = kEntry * OpticalMeters.
            // At a wet cell, Direction is the normalized upwind phase gradient, quantized to Q15.
            // Zero height carries no incoming energy. Excluded or disconnected cells also have zero
            // phase/direction; an attenuated wet characteristic keeps its phase when energy rounds to zero.
            public readonly int[] X, Y, HeightQ, PhaseQ;
            public readonly long[] Arrival;
            public Slice(int count)
            {
                X = new int[count]; Y = new int[count]; HeightQ = new int[count];
                PhaseQ = new int[count]; Arrival = new long[count];
            }
            public byte[] Pixels()
            {
                using var stream = new MemoryStream(X.Length * BytesPerCell);
                using var writer = new BinaryWriter(stream);
                for (int i = 0; i < X.Length; i++)
                {
                    if (Math.Abs((long)PhaseQ[i]) > MaxExactPhaseUnits)
                        throw new InvalidDataException("Optical distance exceeds the exact float path range.");
                    writer.Write(X[i] / (float)DirectionUnits);
                    writer.Write(Y[i] / (float)DirectionUnits);
                    writer.Write(HeightQ[i] / (float)HeightUnits);
                    writer.Write(PhaseQ[i] / (float)PathUnitsPerMeter);
                }
                return stream.ToArray();
            }
        }

        /// <summary>The deepest incoming sea in this slice, bounded by the recipe's offshore cap.</summary>
        public static int EntryDepthMm(Grid grid, Settings settings, int levelMm)
        {
            int deepest = settings.MinimumDepthMm;
            for (int i = 0; i < grid.ElevationMm.Length; i++)
                if (grid.StillLevelMm == null || grid.StillLevelMm[i] <= levelMm)
                    deepest = Math.Max(deepest, levelMm - grid.ElevationMm[i]);
            return Math.Min(settings.OffshoreDepthMm, deepest);
        }

        /// <summary>Optional slowness is Q10 optical metres per metre, for dispersion diagnostics.</summary>
        public static Slice Bake(Grid grid, Settings settings, int levelMm, int bin, int[] slownessQ = null)
        {
            settings.Validate();
            if (bin < 0 || bin >= settings.DirectionX.Length || Math.Abs((long)levelMm) > 100000)
                throw new ArgumentException("Invalid slice.");
            int w = grid.Width, h = grid.Height, count = w * h;
            if (slownessQ != null && slownessQ.Length != count) throw new ArgumentException("Invalid slowness grid.");
            var result = new Slice(count);
            long[] time = result.Arrival;
            var step = new long[count];
            var state = new byte[count]; // 0 trial, 1 accepted, 2 fixed incident boundary, 3 excluded
            var sources = new bool[count];
            var heap = new Heap();
            var order = new List<int>(count);
            int dx = settings.DirectionX[bin], dy = settings.DirectionY[bin];
            int entryDepth = EntryDepthMm(grid, settings, levelMm);
            bool identity = slownessQ == null && (((settings.OpenEdges & 1) != 0 && dx > 0)
                || ((settings.OpenEdges & 2) != 0 && dx < 0) || ((settings.OpenEdges & 4) != 0 && dy > 0)
                || ((settings.OpenEdges & 8) != 0 && dy < 0));
            for (int i = 0; i < count; i++)
            {
                int depth = levelMm - grid.ElevationMm[i];
                bool still = grid.StillLevelMm != null && grid.StillLevelMm[i] > levelMm;
                time[i] = Far;
                if (depth <= settings.DryDepthMm || still) { state[i] = 3; identity = false; continue; }
                int d = Math.Min(entryDepth, Math.Max(settings.MinimumDepthMm, depth));
                long n = slownessQ == null
                    ? Root((long)entryDepth * PathUnitsPerMeter * PathUnitsPerMeter / d)
                    : slownessQ[i];
                if (n < 1 || n > 100 * PathUnitsPerMeter) throw new ArgumentException("Slowness exceeds format limits.");
                step[i] = Math.Max(1, n * settings.CellSizeMm / 1000);
                identity &= depth >= entryDepth;
            }
            if (identity)
            {
                for (int i = 0; i < count; i++)
                {
                    result.X[i] = dx; result.Y[i] = dy; result.HeightQ[i] = HeightUnits;
                    result.PhaseQ[i] = checked((int)Plane(grid, settings, i, dx, dy));
                    time[i] = result.PhaseQ[i];
                }
                return result;
            }
            for (int i = 0; i < count; i++)
            {
                if (state[i] == 3) continue;
                int x = i % w, y = i / w, mask = settings.OpenEdges;
                bool entry = (x == 0 && dx > 0 && (mask & 1) != 0)
                    || (x == w - 1 && dx < 0 && (mask & 2) != 0)
                    || (y == 0 && dy > 0 && (mask & 4) != 0)
                    || (y == h - 1 && dy < 0 && (mask & 8) != 0);
                if (!entry) continue;
                state[i] = 2; sources[i] = true; time[i] = Plane(grid, settings, i, dx, dy);
                heap.Push(time[i], i);
            }
            while (heap.Count > 0)
            {
                heap.Pop(out long t, out int i);
                if (time[i] != t || state[i] == 1) continue;
                bool entry = state[i] == 2;
                state[i] = 1; order.Add(i);
                int x = i % w, y = i / w;
                if (x > 0) Relax(i - 1);
                if (x + 1 < w) Relax(i + 1);
                if (y > 0) Relax(i - w);
                if (y + 1 < h) Relax(i + w);
                if (entry) { result.X[i] = dx; result.Y[i] = dy; }
            }
            // Upwind derivatives avoid averaging two crossing fronts into a false third direction.
            foreach (int i in order)
            {
                if (result.X[i] != 0 || result.Y[i] != 0) continue;
                Axis(i, out int ax, out int ay);
                long gx = ax < 0 ? 0 : (time[i] - time[ax]) * (ax < i ? 1 : -1);
                long gy = ay < 0 ? 0 : (time[i] - time[ay]) * (ay < i ? 1 : -1);
                long norm = Root(gx * gx + gy * gy);
                result.X[i] = norm == 0 ? dx : (int)(gx * DirectionUnits / norm);
                result.Y[i] = norm == 0 ? dy : (int)(gy * DirectionUnits / norm);
            }
            // Transport H^2*c along the accepted characteristic DAG. Finite-volume spreading reduces
            // flux in the lee; convergence can focus it, bounded by the recipe's height cap.
            var energy = new long[count];
            long capQ = (long)settings.HeightCapMilli * HeightUnits / 1000;
            foreach (int i in order)
            {
                Axis(i, out int ax, out int ay);
                long wx = ax < 0 ? 0 : Math.Max(0, time[i] - time[ax]);
                long wy = ay < 0 ? 0 : Math.Max(0, time[i] - time[ay]);
                if (sources[i] || wx + wy == 0) energy[i] = EnergyUnits;
                else
                {
                    long upstream = ((ax < 0 ? 0 : energy[ax] * wx) + (ay < 0 ? 0 : energy[ay] * wy)) / (wx + wy);
                    int x = i % w, y = i / w;
                    long div = Component(i, x < w - 1 ? i + 1 : i, true) - Component(i, x > 0 ? i - 1 : i, true)
                        + Component(i, y < h - 1 ? i + w : i, false) - Component(i, y > 0 ? i - w : i, false);
                    long speed = Math.Max(1, Math.Abs(result.X[i]) + Math.Abs(result.Y[i]));
                    long denominator = Math.Max(settings.TransportFloorMilli, 1000 + div * 500 / speed);
                    energy[i] = Math.Min(capQ * capQ * EnergyUnits / (HeightUnits * HeightUnits), upstream * 1000 / denominator);
                }
                long nQ = step[i] * 1000 / settings.CellSizeMm;
                long ampQ = Root(energy[i] * nQ * HeightUnits * HeightUnits / (EnergyUnits * PathUnitsPerMeter));
                result.HeightQ[i] = (int)Math.Min(capQ, ampQ);
                result.PhaseQ[i] = checked((int)time[i]);
            }
            return result;

            long Component(int center, int other, bool horizontal) => state[other] == 1
                ? (horizontal ? result.X[other] : result.Y[other])
                : (horizontal ? result.X[center] : result.Y[center]);

            void Axis(int i, out int ax, out int ay)
            {
                int x = i % w, y = i / w;
                ax = Pick(x > 0 ? i - 1 : -1, x + 1 < w ? i + 1 : -1, i);
                ay = Pick(y > 0 ? i - w : -1, y + 1 < h ? i + w : -1, i);
            }
            int Pick(int a, int b, int center)
            {
                if (a < 0 || state[a] != 1 || time[a] >= time[center]) a = -1;
                if (b < 0 || state[b] != 1 || time[b] >= time[center]) b = -1;
                return a < 0 ? b : b < 0 || time[a] <= time[b] ? a : b;
            }
            void Relax(int i)
            {
                if (state[i] != 0) return;
                int x = i % w, y = i / w;
                long a = Math.Min(Accepted(x > 0 ? i - 1 : -1), Accepted(x + 1 < w ? i + 1 : -1));
                long b = Math.Min(Accepted(y > 0 ? i - w : -1), Accepted(y + 1 < h ? i + w : -1));
                long lo = Math.Min(a, b), hi = Math.Max(a, b), s = step[i];
                if (lo == Far) return;
                long candidate = hi == Far || hi - lo >= s ? lo + s
                    : (lo + hi + Root(2 * s * s - (hi - lo) * (hi - lo))) / 2;
                if (candidate >= time[i]) return;
                time[i] = candidate; heap.Push(candidate, i);
            }
            long Accepted(int i) => i >= 0 && state[i] == 1 ? time[i] : Far;
        }

        static long Plane(Grid g, Settings s, int i, int dx, int dy)
        {
            long x = g.MinXmm + (long)(i % g.Width) * s.CellSizeMm + s.CellSizeMm / 2;
            long y = g.MinYmm + (long)(i / g.Width) * s.CellSizeMm + s.CellSizeMm / 2;
            return (x * dx + y * dy) * PathUnitsPerMeter / (1000L * DirectionUnits);
        }

        /// <summary>Exact floor(sqrt(n)); independent of floating point and CPU instructions.</summary>
        public static long Root(long n)
        {
            if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
            ulong value = (ulong)n, answer = 0, bit = 1UL << 62;
            while (bit > value) bit >>= 2;
            while (bit != 0)
            {
                if (value >= answer + bit) { value -= answer + bit; answer = (answer >> 1) + bit; }
                else answer >>= 1;
                bit >>= 2;
            }
            return (long)answer;
        }

        sealed class Heap
        {
            readonly List<(long Time, int Cell)> _items = new List<(long, int)>();
            public int Count => _items.Count;
            static bool Before((long Time, int Cell) a, (long Time, int Cell) b) =>
                a.Time < b.Time || (a.Time == b.Time && a.Cell < b.Cell);
            public void Push(long time, int cell)
            {
                var item = (time, cell); int at = _items.Count; _items.Add(item);
                while (at > 0)
                {
                    int parent = (at - 1) / 2;
                    if (!Before(item, _items[parent])) break;
                    _items[at] = _items[parent]; at = parent;
                }
                _items[at] = item;
            }
            public void Pop(out long time, out int cell)
            {
                var top = _items[0]; time = top.Time; cell = top.Cell;
                int end = _items.Count - 1; var item = _items[end]; _items.RemoveAt(end);
                if (end == 0) return;
                int at = 0;
                while (2 * at + 1 < end)
                {
                    int child = 2 * at + 1;
                    if (child + 1 < end && Before(_items[child + 1], _items[child])) child++;
                    if (!Before(_items[child], item)) break;
                    _items[at] = _items[child]; at = child;
                }
                _items[at] = item;
            }
        }
    }
}
