using System;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>TODAY'S PAINT, AS THE PLAN READS IT.</b> The zone the committed splat shows at each cell: the TerrainSplat
    /// shader's height bands under the painted weights (pass 9's <c>stp.ground_weights</c>), the largest slot where the
    /// paint covers at least half the cell, else <see cref="TerrainPlanZones.Unpainted"/>.
    ///
    /// <para>Pure, and in the prototype's precision: the bands and the paint in single precision, the sector weight in
    /// double, as numpy computed them, so the gatherer's reading and the prototype's agree cell for cell. The gatherer
    /// hands it today's 8-bit codes (the height map and splat A to E, as their PNGs hold them); the headless run checks
    /// it against the prototype's reading of the same files.</para>
    /// </summary>
    public static class TerrainPlanToday
    {
        /// <summary>The paint covers the cell when its slots sum to at least this (the prototype's rule).</summary>
        public const float Covers = 0.5f;

        /// <summary>
        /// Today's zone per cell, row-major, row 0 north.
        /// </summary>
        /// <param name="grid">the plan's grid</param>
        /// <param name="bands">the shader's band constants as the builder pushes them</param>
        /// <param name="heightCodes">today's height map, one 8-bit code per cell, row 0 north</param>
        /// <param name="heightMin">the height map's range minimum (m)</param>
        /// <param name="heightMax">the height map's range maximum (m)</param>
        /// <param name="splatCodes">splat A to E's 20 slots, [slot][cell], 8-bit codes, row 0 north</param>
        public static byte[] Zones(TerrainPlanGrid grid, PlanShaderBands bands, byte[] heightCodes, float heightMin, float heightMax,
                                   byte[][] splatCodes)
        {
            int n = grid.Count;
            if (heightCodes == null || heightCodes.Length != n) throw new ArgumentException("[TerrainPlan] today's height does not fill the grid.");
            if (splatCodes == null || splatCodes.Length != TerrainPlanZones.SplatSlots)
                throw new ArgumentException("[TerrainPlan] today's splat needs its " + TerrainPlanZones.SplatSlots + " slots.");
            foreach (var s in splatCodes)
                if (s == null || s.Length != n) throw new ArgumentException("[TerrainPlan] a splat slot does not fill the grid.");

            var zones = new byte[n];
            var P = new float[TerrainPlanZones.SplatSlots];
            var w = new float[6];
            float span = heightMax - heightMin;
            for (int r = 0; r < grid.H; r++)
            {
                double Y = grid.YG(r);
                for (int c = 0; c < grid.W; c++)
                {
                    int i = r * grid.W + c;
                    for (int k = 0; k < P.Length; k++) P[k] = splatCodes[k][i] / 255f;
                    float s = Sum20(P);
                    float tot = s < 0f ? 0f : s > 1f ? 1f : s;
                    if (!(tot >= Covers)) { zones[i] = TerrainPlanZones.Unpainted; continue; }
                    float e = heightMin + heightCodes[i] / 255f * span;
                    BandWeights(bands, e, grid.XG(c), Y, w);
                    float keep = 1f - tot, den = s > 1e-4f ? s : 1e-4f;
                    int best = 0; float bestW = float.NegativeInfinity;
                    for (int k = 0; k < P.Length; k++)
                    {
                        float g = (k < 6 ? w[k] : 0f) * keep + P[k] / den * tot;
                        if (g > bestW) { best = k; bestW = g; }                          // numpy's argmax: the first largest
                    }
                    zones[i] = (byte)best;
                }
            }
            return zones;
        }

        /// <summary>numpy's float32 sum of 20 values along a row: eight running sums, paired, then the last four.</summary>
        static float Sum20(float[] a)
        {
            float r0 = a[0] + a[8], r1 = a[1] + a[9], r2 = a[2] + a[10], r3 = a[3] + a[11];
            float r4 = a[4] + a[12], r5 = a[5] + a[13], r6 = a[6] + a[14], r7 = a[7] + a[15];
            float res = ((r0 + r1) + (r2 + r3)) + ((r4 + r5) + (r6 + r7));
            for (int i = 16; i < 20; i++) res += a[i];
            return res;
        }

        /// <summary>
        /// The shader's band weights for its six band slots (stp.band_weights): the bands in single precision, the
        /// weather sector and the bar in double, each product stored in single, as numpy stored them.
        /// </summary>
        public static void BandWeights(PlanShaderBands b, float E, double X, double Y, float[] w)
        {
            float e = Math.Max((float)b.PaintFloor, E);
            float bg = Band(e, b.GrassFloor, b.BandBlend), bm = Band(e, b.MarramFloor, b.BandBlend), bs = Band(e, b.SandFloor, b.BandBlend);
            float br = Band(e, b.RippleFloor, b.BandBlend), bh = Band(e, b.ShingleFloor, b.BandBlend);
            float sGrass = bg, sMarram = bm * (1f - bg);
            float sSand = bs * (1f - bm) * (1f - bg), sRipple = br * (1f - bs) * (1f - bm) * (1f - bg);
            float sShelf = (1f - br) * (1f - bs) * (1f - bm) * (1f - bg);
            float wShingle = bh * (1f - bg), wShelf = (1f - bh) * (1f - bg);
            double dx = X - b.IslandCentre.X, dy = (Y - b.IslandCentre.Y) * b.IslandAspect;
            double n = Math.Sqrt(dx * dx + dy * dy) + 1e-6;
            double bearing = (dx * b.Weather.X + dy * b.Weather.Y) / n;
            double barW = 1 - Smooth(b.BarHalfWidth - b.BarEdge, b.BarHalfWidth + b.BarEdge, SegDist(X, Y, b.BarFrom, b.BarTo));
            double ws = Smooth(-b.SectorBlend, b.SectorBlend, bearing) * (1 - barW);
            w[0] = sGrass;
            w[1] = (float)(sMarram * (1 - ws));
            w[2] = (float)(sSand * (1 - ws));
            w[3] = (float)(wShingle * ws);
            w[4] = (float)(sRipple * (1 - ws));
            w[5] = (float)(sShelf * (1 - ws) + wShelf * ws);
        }

        /// <summary>stp.band in single precision: smoothstep(floor - blend, floor + blend, e).</summary>
        static float Band(float e, double floor, double blend)
        {
            float lo = (float)(floor - blend), span = (float)(floor + blend - (floor - blend));
            float t = (e - lo) / span;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return t * t * (3f - 2f * t);
        }

        static double Smooth(double a, double b, double x)
        {
            double t = (x - a) / (b - a);
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            return t * t * (3 - 2 * t);
        }

        static double SegDist(double px, double py, PlanPoint a, PlanPoint b)
        {
            double abx = b.X - a.X, aby = b.Y - a.Y;
            double t = ((px - a.X) * abx + (py - a.Y) * aby) / (abx * abx + aby * aby);
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            double qx = px - (a.X + t * abx), qy = py - (a.Y + t * aby);
            return Math.Sqrt(qx * qx + qy * qy);
        }
    }
}
