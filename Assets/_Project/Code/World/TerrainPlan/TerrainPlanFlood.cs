using System;
using System.Collections.Generic;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>STILL WATER BY FLOOD</b> (terrain PR 5 B; part 2 §7.4). Where water stands on a height raster:
    /// <list type="bullet">
    /// <item>a priority flood gives each cell its spill level, the lowest level at which water there reaches the raster's
    /// border (the lowest, over every path out, of the highest ground along it);</item>
    /// <item>a cell under its level is wet, and 4-connected wet cells are one basin, standing at its spill;</item>
    /// <item>a pond's water is the plain flood: from its centre over the 4-connected cells under its surface.</item>
    /// </list>
    /// Pure and deterministic: a level is a function of the ground alone, and the heap pops ties by cell index.
    /// </summary>
    public static class TerrainPlanFlood
    {
        /// <summary>Water deeper than this stands (m): the rule's own tolerance on a level against its ground (part 2 §7.4).</summary>
        public const double WetEps = 1e-9;

        /// <summary>One closed basin: its cells (row-major, sorted), its spill, and its deepest water and where.</summary>
        public sealed class Basin
        {
            public int[] Cells = new int[0];
            public double Spill;
            public double Deepest;
            public int DeepestAt = -1;
        }

        /// <summary>
        /// Each cell's spill level on a <paramref name="w"/> × <paramref name="h"/> raster (row-major): the border stands at
        /// max(its ground, <paramref name="floor"/>), and water inside reaches it over 4-connected cells.
        /// </summary>
        public static double[] SpillLevels(double[] z, int w, int h, double floor)
        {
            if (z == null || w < 1 || h < 1 || z.Length != w * h) throw new ArgumentException("[TerrainPlanFlood] the raster does not fill its size.");
            int n = w * h;
            var lvl = new double[n];
            var seen = new bool[n];
            var heap = new MinHeap(Math.Max(16, 2 * (w + h)));
            for (int c = 0; c < w; c++) { Seed(c); Seed((h - 1) * w + c); }
            for (int r = 1; r < h - 1; r++) { Seed(r * w); Seed(r * w + w - 1); }
            while (heap.Count > 0)
            {
                heap.Pop(out double l, out int i);
                int r = i / w, c = i - r * w;
                if (c > 0) Step(i - 1, l);
                if (c < w - 1) Step(i + 1, l);
                if (r > 0) Step(i - w, l);
                if (r < h - 1) Step(i + w, l);
            }
            return lvl;

            void Seed(int i)
            {
                if (seen[i]) return;
                seen[i] = true;
                lvl[i] = Math.Max(z[i], floor);
                heap.Push(lvl[i], i);
            }

            void Step(int j, double l)
            {
                if (seen[j]) return;
                seen[j] = true;
                lvl[j] = Math.Max(l, z[j]);
                heap.Push(lvl[j], j);
            }
        }

        /// <summary>
        /// The basins on a raster and its spill levels: 4-connected cells whose level stands more than <paramref name="eps"/>
        /// over their ground and over <paramref name="floor"/>, in the order of their first cell. A basin's spill is the
        /// highest level among its cells (a basin inside a basin stands at the outer one's spill).
        /// </summary>
        public static List<Basin> Basins(double[] z, double[] lvl, int w, int h, double floor, double eps)
        {
            int n = w * h;
            var wet = new bool[n];
            for (int i = 0; i < n; i++) wet[i] = lvl[i] - z[i] > eps && lvl[i] > floor + eps;
            var label = new bool[n];
            var o = new List<Basin>();
            var stack = new Stack<int>();
            var cells = new List<int>();
            for (int s = 0; s < n; s++)
            {
                if (!wet[s] || label[s]) continue;
                cells.Clear();
                label[s] = true;
                stack.Push(s);
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    cells.Add(i);
                    int r = i / w, c = i - r * w;
                    if (c > 0 && wet[i - 1] && !label[i - 1]) { label[i - 1] = true; stack.Push(i - 1); }
                    if (c < w - 1 && wet[i + 1] && !label[i + 1]) { label[i + 1] = true; stack.Push(i + 1); }
                    if (r > 0 && wet[i - w] && !label[i - w]) { label[i - w] = true; stack.Push(i - w); }
                    if (r < h - 1 && wet[i + w] && !label[i + w]) { label[i + w] = true; stack.Push(i + w); }
                }
                cells.Sort();
                var b = new Basin { Cells = cells.ToArray(), Spill = double.NegativeInfinity };
                foreach (int i in b.Cells)
                {
                    b.Spill = Math.Max(b.Spill, lvl[i]);
                    double d = lvl[i] - z[i];
                    if (d > b.Deepest) { b.Deepest = d; b.DeepestAt = i; }
                }
                o.Add(b);
            }
            return o;
        }

        /// <summary>The basin holding <paramref name="cell"/>, or null when the cell is dry.</summary>
        public static Basin BasinAt(List<Basin> basins, int cell)
        {
            foreach (var b in basins) if (Array.BinarySearch(b.Cells, cell) >= 0) return b;
            return null;
        }

        /// <summary>
        /// A pond's water by flood on a grid raster: from <paramref name="seed"/> over the 4-connected cells whose ground is
        /// under <paramref name="surface"/>, inside rows [r0, r1) and columns [c0, c1). The cells (sorted, plan order), and
        /// whether the flood reached the window's edge, which is a leak. A dry seed floods nothing.
        /// </summary>
        public static int[] Under(double[] z, TerrainPlanGrid g, int seed, double surface, int r0, int r1, int c0, int c1, out bool leaks)
        {
            leaks = false;
            var o = new List<int>();
            if (seed < 0 || seed >= g.Count || !(z[seed] < surface)) return o.ToArray();
            var seen = new HashSet<int> { seed };
            var stack = new Stack<int>();
            stack.Push(seed);
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                o.Add(i);
                int r = i / g.W, c = i - r * g.W;
                if (r <= r0 || r >= r1 - 1 || c <= c0 || c >= c1 - 1) leaks = true;
                Try(c > c0 ? i - 1 : -1);
                Try(c < c1 - 1 ? i + 1 : -1);
                Try(r > r0 ? i - g.W : -1);
                Try(r < r1 - 1 ? i + g.W : -1);
            }
            o.Sort();
            return o.ToArray();

            void Try(int j)
            {
                if (j < 0 || !(z[j] < surface) || !seen.Add(j)) return;
                stack.Push(j);
            }
        }

        /// <summary>A binary min-heap of (level, cell), ties broken by the cell, so the flood is the same on every run.</summary>
        sealed class MinHeap
        {
            double[] _k;
            int[] _v;
            public int Count { get; private set; }

            public MinHeap(int capacity)
            {
                _k = new double[capacity];
                _v = new int[capacity];
            }

            static bool Less(double ka, int va, double kb, int vb) => ka < kb || (ka == kb && va < vb);

            public void Push(double k, int v)
            {
                if (Count == _k.Length) { Array.Resize(ref _k, Count * 2); Array.Resize(ref _v, Count * 2); }
                int i = Count++;
                while (i > 0)
                {
                    int p = (i - 1) >> 1;
                    if (!Less(k, v, _k[p], _v[p])) break;
                    _k[i] = _k[p]; _v[i] = _v[p];
                    i = p;
                }
                _k[i] = k; _v[i] = v;
            }

            public void Pop(out double k, out int v)
            {
                k = _k[0]; v = _v[0];
                int n = --Count;
                if (n == 0) return;
                double lk = _k[n];
                int lv = _v[n], i = 0;
                while (true)
                {
                    int a = 2 * i + 1;
                    if (a >= n) break;
                    int b = a + 1;
                    int m = b < n && Less(_k[b], _v[b], _k[a], _v[a]) ? b : a;
                    if (!Less(_k[m], _v[m], lk, lv)) break;
                    _k[i] = _k[m]; _v[i] = _v[m];
                    i = m;
                }
                _k[i] = lk; _v[i] = lv;
            }
        }
    }
}
