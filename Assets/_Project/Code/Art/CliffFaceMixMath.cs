using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>WHICH SKIN A STRETCH OF CLIFF WEARS.</b> Pure, deterministic arithmetic over a station's toe position
    /// in the plan and a seed: no Unity random, no time, nothing saved. The same coast every load.
    ///
    /// <para><b>Runs, not a checkerboard.</b> A smooth value noise over the plan, one lattice cell per
    /// <c>runMetres</c>, is cut at the level that leaves <c>share</c> of the coast above it. Neighbouring stations
    /// a metre apart sit at almost the same noise value, so a stretch keeps its skin for tens of metres and changes
    /// where the noise crosses the cut, the way one bed of rock gives way to the next along a shore.</para>
    ///
    /// <para><b>The change is a band, not a line.</b> Within <c>blendMetres</c> of the crossing the weight runs
    /// from 0 to 1; the shader turns that weight into the rock's own blocks giving way one by one
    /// (<see cref="PicksVariant"/>), never a fade. The cut level and the noise's mean slope are both MEASURED from
    /// the noise on a fixed grid (<see cref="SampleGrid"/>), so neither is a hand-picked constant.</para>
    /// </summary>
    public static class CliffFaceMixMath
    {
        /// <summary>The side of the fixed grid the cut level and mean slope are measured on, in lattice cells
        /// scaled by <see cref="SamplesPerCell"/>. Fixed, so a seed's measurement never changes.</summary>
        public const int SampleGrid = 256;

        /// <summary>Samples per lattice cell on the measuring grid (8 a cell: fine enough to see the smoothstep).</summary>
        public const int SamplesPerCell = 8;

        /// <summary>The shader's salt for the block pick (CliffHash salt 8). Twin of the shader's
        /// <c>CLIFF_VARIANT_SALT</c>.</summary>
        public const float BlockSalt = 8f;

        // ---- the noise -----------------------------------------------------------------------------------

        /// <summary>A stable 0..1 hash of an integer lattice corner and a seed.</summary>
        public static float CornerHash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ (uint)(seed + 1) * 0xCB1AB31Fu;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>Smooth value noise, 0..1, at a point measured in lattice cells.</summary>
        public static float Noise(double x, double y, int seed)
        {
            double fx = Math.Floor(x), fy = Math.Floor(y);
            int ix = (int)fx, iy = (int)fy;
            float tx = Smooth((float)(x - fx)), ty = Smooth((float)(y - fy));
            float a = CornerHash(ix, iy, seed), b = CornerHash(ix + 1, iy, seed);
            float c = CornerHash(ix, iy + 1, seed), d = CornerHash(ix + 1, iy + 1, seed);
            float ab = a + (b - a) * tx, cd = c + (d - c) * tx;
            return ab + (cd - ab) * ty;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>The noise at a toe position in metres, with the lattice <paramref name="runMetres"/> a cell.</summary>
        public static float NoiseAt(Vector2 toePlan, float runMetres, int seed) =>
            Noise((double)toePlan.x / runMetres, (double)toePlan.y / runMetres, seed);

        // ---- the measurement -------------------------------------------------------------------------------

        /// <summary>The noise's sorted values and mean slope (noise units per lattice cell) for a seed,
        /// measured on the fixed grid.</summary>
        public readonly struct Measure
        {
            public readonly float[] Sorted;
            public readonly float MeanSlopePerCell;
            public Measure(float[] sorted, float slope) { Sorted = sorted; MeanSlopePerCell = slope; }
        }

        private static readonly Dictionary<int, Measure> Measures = new Dictionary<int, Measure>();

        /// <summary>The measurement for a seed, computed once and cached.</summary>
        public static Measure MeasureFor(int seed)
        {
            lock (Measures)
            {
                if (Measures.TryGetValue(seed, out Measure m)) return m;
                int n = SampleGrid;
                double step = 1.0 / SamplesPerCell;
                var grid = new float[n * n];
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                        grid[j * n + i] = Noise(i * step, j * step, seed);
                double slope = 0; int count = 0;
                for (int j = 0; j < n - 1; j++)
                    for (int i = 0; i < n - 1; i++)
                    {
                        float v = grid[j * n + i];
                        float gx = (grid[j * n + i + 1] - v) * SamplesPerCell;
                        float gy = (grid[(j + 1) * n + i] - v) * SamplesPerCell;
                        slope += Math.Sqrt(gx * gx + gy * gy); count++;
                    }
                var sorted = (float[])grid.Clone();
                Array.Sort(sorted);
                m = new Measure(sorted, (float)(slope / count));
                Measures[seed] = m;
                return m;
            }
        }

        /// <summary>The noise level above which <paramref name="share"/> of the plan lies, for a seed.</summary>
        public static float CutLevel(float share, int seed)
        {
            float[] s = MeasureFor(seed).Sorted;
            int k = Mathf.Clamp(Mathf.RoundToInt((1f - Mathf.Clamp01(share)) * (s.Length - 1)), 0, s.Length - 1);
            return s[k];
        }

        // ---- the weight ------------------------------------------------------------------------------------

        /// <summary>
        /// The variant skin's weight at a toe position: 0 the base skin, 1 the variant, between them only within
        /// <paramref name="blendMetres"/> of a change. Share 0 is exactly 0 everywhere, share 1 exactly 1.
        /// </summary>
        public static float VariantWeight(Vector2 toePlan, float share, float runMetres, float blendMetres, int seed)
        {
            if (share <= 0f || runMetres <= 0f) return 0f;
            if (share >= 1f) return 1f;
            float n = NoiseAt(toePlan, runMetres, seed);
            float cut = CutLevel(share, seed);
            // The band's width in noise units: metres -> lattice cells -> the measured mean slope.
            float width = Mathf.Max(0f, blendMetres) / runMetres * MeasureFor(seed).MeanSlopePerCell;
            if (width <= 1e-6f) return n > cut ? 1f : 0f;
            return Mathf.Clamp01((n - cut) / width + 0.5f);
        }

        /// <summary>
        /// Whether one blend block draws in the variant palette at a weight: the block's own hash against the
        /// weight, so the covered share of blocks is the weight and the edge breaks along the rock's blocks.
        /// Twin of the shader's pick (<c>w &gt; CliffHash(floor(cell / block), CLIFF_VARIANT_SALT)</c>).
        /// </summary>
        public static bool PicksVariant(float weight, Vector2 texelCell, Vector2 blockTexels)
        {
            if (weight >= 0.999f) return true;
            if (weight <= 0f) return false;
            Vector2 b = new Vector2(Mathf.Max(1f, blockTexels.x), Mathf.Max(1f, blockTexels.y));
            Vector2 block = new Vector2(Mathf.Floor(texelCell.x / b.x), Mathf.Floor(texelCell.y / b.y));
            return weight > CliffWaterlineMath.Hash01(block, BlockSalt);
        }
    }
}
