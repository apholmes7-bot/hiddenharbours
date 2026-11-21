using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The shore-wave field: for every cell of a region's seabed grid, how far it lies OFFSHORE of the
    /// shoreline (metres, negative inland), and the gradient of that distance. Built ONCE when a sea's
    /// seabed is fed to the water shader (never per frame, never saved), from the same elevations the
    /// shader draws, so it is a pure function of the region's seabed.
    ///
    /// <para><b>What it is for.</b> The drawn wave trains travel in one direction across the whole sea.
    /// Near a coast real waves turn to meet the beach face-on, because they slow in shallow water. The
    /// water shader cross-fades each train, inside a band of this distance, into a copy of itself whose
    /// phase runs along <c>-distance</c>: its crests lie along the contours of distance-from-shore, so they
    /// arrive parallel to every beach whichever way it faces, and wrap round headlands. VISUAL only: the
    /// sim's wave height, tide and the boat's ride never read it (rule 5).</para>
    ///
    /// <para><b>Signed, on purpose.</b> Inland of the reference shoreline the distance goes negative, so
    /// when the tide floods the flats the phase keeps running up the beach instead of the whole flooded
    /// area rising and falling as one pulse.</para>
    /// </summary>
    public static class ShoreWaveField
    {
        // A finite stand-in for "no feature on this line yet": large enough that every real squared
        // distance is far below it, small enough that sums and differences never overflow a float.
        private const float Far = 1e20f;

        /// <summary>
        /// Build the field. <paramref name="elevation"/> is row-major (y outer), <paramref name="width"/> by
        /// <paramref name="height"/> cells of <paramref name="cellX"/> by <paramref name="cellY"/> metres.
        /// A cell is land when its elevation is at or above <paramref name="referenceLevel"/>. Returns four
        /// floats per cell: distance offshore (m, clamped to +/- <paramref name="maxDistance"/>), its x and y
        /// gradient (m per m), and 1 (written so the texture's alpha reads "inside the field").
        /// A grid with no land reads <paramref name="maxDistance"/> with a zero gradient everywhere; a grid
        /// with no water reads <c>-maxDistance</c>, also with a zero gradient. Never NaN.
        /// </summary>
        public static float[] Build(float[] elevation, int width, int height, float cellX, float cellY,
                                    float referenceLevel, float maxDistance, int smoothingPasses)
        {
            if (elevation == null) throw new ArgumentNullException(nameof(elevation));
            if (width < 1 || height < 1 || elevation.Length != width * height)
                throw new ArgumentException("elevation must hold width x height cells");
            cellX = Mathf.Max(cellX, 1e-4f);
            cellY = Mathf.Max(cellY, 1e-4f);
            maxDistance = Mathf.Max(maxDistance, 1e-3f);

            int n = width * height;
            var land = new bool[n];
            bool anyLand = false, anyWater = false;
            for (int i = 0; i < n; i++)
            {
                float e = elevation[i];
                land[i] = !(e < referenceLevel);              // NaN elevation reads as land: no wave there
                if (land[i]) anyLand = true; else anyWater = true;
            }

            var signedDistance = new float[n];
            if (!anyLand || !anyWater)
            {
                float fill = anyLand ? -maxDistance : maxDistance;
                for (int i = 0; i < n; i++) signedDistance[i] = fill;
            }
            else
            {
                float[] toLand = DistanceTo(land, true, width, height, cellX, cellY);
                float[] toWater = DistanceTo(land, false, width, height, cellX, cellY);
                for (int i = 0; i < n; i++)
                {
                    // The shoreline sits half a cell between a land and a water cell: offset both sides by it
                    // so the field crosses zero there and its gradient has no step across the line.
                    float half = 0.5f * Mathf.Min(cellX, cellY);
                    float d = land[i] ? -(toWater[i] - half) : toLand[i] - half;
                    signedDistance[i] = Mathf.Clamp(d, -maxDistance, maxDistance);
                }
                for (int pass = 0; pass < smoothingPasses; pass++)
                    signedDistance = Smooth(signedDistance, width, height);
            }

            var field = new float[n * 4];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                int x0 = Math.Max(x - 1, 0), x1 = Math.Min(x + 1, width - 1);
                int y0 = Math.Max(y - 1, 0), y1 = Math.Min(y + 1, height - 1);
                float gx = x1 > x0 ? (signedDistance[y * width + x1] - signedDistance[y * width + x0]) / ((x1 - x0) * cellX) : 0f;
                float gy = y1 > y0 ? (signedDistance[y1 * width + x] - signedDistance[y0 * width + x]) / ((y1 - y0) * cellY) : 0f;
                // Where the clamp flattens the field the gradient is honestly zero; never let it exceed unit
                // length (a ridge between two shores can only make it shorter, never longer).
                float g = Mathf.Sqrt(gx * gx + gy * gy);
                if (g > 1f) { gx /= g; gy /= g; }
                field[i * 4 + 0] = signedDistance[i];
                field[i * 4 + 1] = gx;
                field[i * 4 + 2] = gy;
                field[i * 4 + 3] = 1f;
            }
            return field;
        }

        /// <summary>
        /// The direction a shore-turned wave travels at a cell: down the distance gradient, toward the
        /// shore. Zero where the field is flat (open sea past the clamp, or no shore at all).
        /// </summary>
        public static Vector2 ShorewardAt(float[] field, int width, int x, int y)
        {
            int i = (y * width + x) * 4;
            var g = new Vector2(-field[i + 1], -field[i + 2]);
            float m = g.magnitude;
            return m > 1e-5f ? g / m : Vector2.zero;
        }

        // Exact Euclidean distance (metres) from every cell to the nearest cell whose land flag equals
        // `target`, by two separable lower-envelope passes (Felzenszwalb and Huttenlocher, 2012), in world
        // units so a non-square cell measures true distance.
        private static float[] DistanceTo(bool[] land, bool target, int width, int height, float cellX, float cellY)
        {
            int n = width * height;
            var squared = new float[n];
            for (int i = 0; i < n; i++) squared[i] = land[i] == target ? 0f : Far;

            int longest = Math.Max(width, height);
            var line = new float[longest];
            var outLine = new float[longest];
            var v = new int[longest];
            var z = new float[longest + 1];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++) line[x] = squared[y * width + x];
                Envelope(line, width, cellX, outLine, v, z);
                for (int x = 0; x < width; x++) squared[y * width + x] = outLine[x];
            }
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++) line[y] = squared[y * width + x];
                Envelope(line, height, cellY, outLine, v, z);
                for (int y = 0; y < height; y++) squared[y * width + x] = outLine[y];
            }

            var distance = new float[n];
            for (int i = 0; i < n; i++) distance[i] = Mathf.Sqrt(Mathf.Min(squared[i], Far));
            return distance;
        }

        // One dimension of the squared distance transform over samples spaced `step` metres apart.
        private static void Envelope(float[] f, int count, float step, float[] d, int[] v, float[] z)
        {
            int k = 0;
            v[0] = 0;
            z[0] = float.NegativeInfinity;
            z[1] = float.PositiveInfinity;
            for (int q = 1; q < count; q++)
            {
                float s = Intersect(f, q, v[k], step);
                // z[0] is -infinity, so this always stops at k = 0 at the latest.
                while (s <= z[k])
                {
                    k--;
                    s = Intersect(f, q, v[k], step);
                }
                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = float.PositiveInfinity;
            }
            k = 0;
            for (int q = 0; q < count; q++)
            {
                float pq = q * step;
                while (z[k + 1] < pq) k++;
                float dv = pq - v[k] * step;
                d[q] = Mathf.Min(dv * dv + f[v[k]], Far);
            }
        }

        // Where the parabolas rooted at samples q and p (q > p) cross, in metres along the line.
        private static float Intersect(float[] f, int q, int p, float step)
        {
            float pq = q * step, pp = p * step;
            return ((f[q] + pq * pq) - (f[p] + pp * pp)) / (2f * (pq - pp));
        }

        // One 3x3 box pass with clamped edges: rounds the creases where two shores' distances meet, so a
        // crest drawn along a contour bends round a bay instead of folding into a sharp chevron.
        private static float[] Smooth(float[] src, int width, int height)
        {
            var dst = new float[src.Length];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float sum = 0f;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int sx = Math.Min(Math.Max(x + dx, 0), width - 1);
                    int sy = Math.Min(Math.Max(y + dy, 0), height - 1);
                    sum += src[sy * width + sx];
                }
                dst[y * width + x] = sum / 9f;
            }
            return dst;
        }
    }
}
