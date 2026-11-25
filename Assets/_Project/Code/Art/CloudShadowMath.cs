using System;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The pure arithmetic of the cloud shade. Everything is a function of the world seed, game time and the
    /// deterministic wind law; there is no hidden randomness and nothing to save.
    /// </summary>
    public static class CloudShadowMath
    {
        /// <summary>The weather's own overcast, 0..1: the day/night light's weather dimming as a fraction of
        /// its cap (fog or a building storm). Zero on an ordinary day.</summary>
        public static float Overcast(float visibility, float seaState, DayNightProfile profile)
            => profile != null && profile.WeatherDimMax > 0
                ? Mathf.Clamp01(DayNightMath.WeatherDim(visibility, seaState, profile) / profile.WeatherDimMax) : 0;

        /// <summary>Fair-weather cumulus, 0..<see cref="CloudShadowDef.FairCoverMax"/>: a slow, smooth channel
        /// of (seed, time) that is clear for long spells and builds to broken cloud between them.</summary>
        public static float FairCover(CloudShadowDef def, int seed, double gameSeconds)
        {
            if (def == null || def.FairCoverMax <= 0 || !(def.FairCoverPeriodSeconds > 0)) return 0;
            double u = gameSeconds / def.FairCoverPeriodSeconds;
            double cell = Math.Floor(u);
            float f = (float)(u - cell);
            long i = (long)cell;
            float a = Hash01(seed, i), b = Hash01(seed, i + 1);
            float n = Mathf.Lerp(a, b, f * f * (3 - 2 * f));
            float shaped = Mathf.InverseLerp(def.FairCoverClearBelow, 1, n);
            return def.FairCoverMax * shaped * shaped * (3 - 2 * shaped);
        }

        /// <summary>The cloud cover the shade reads: the larger of the fair-weather cloud and the weather's
        /// overcast.</summary>
        public static float Cover(CloudShadowDef def, int seed, double gameSeconds, float weatherOvercast)
            => Mathf.Clamp01(Mathf.Max(FairCover(def, seed, gameSeconds), Mathf.Clamp01(weatherOvercast)));

        /// <summary>How far a fully shaded pixel moves toward the shade colour: the cover curve, faded out as
        /// the sun nears the horizon (none at night), and by <paramref name="outdoors"/> (0 indoors).</summary>
        public static float Strength(CloudShadowDef def, float cover, float elevation, float outdoors = 1)
            => def == null || !def.Enabled || !(elevation > 0) ? 0
                : Mathf.Clamp01(def.Strength.Evaluate(Mathf.Clamp01(cover)))
                    * Mathf.Clamp01(elevation / def.FullSunElevation) * Mathf.Clamp01(outdoors);

        /// <summary>Where the shade's pattern starts on a given day. A fresh sky each day; the change happens
        /// at midnight, when the shade is already nothing.</summary>
        public static Vector2 DayOrigin(int seed, long day, float period)
            => new Vector2(Hash01(seed ^ 0x5bd1e995, day) * period, Hash01(seed ^ 0x27d4eb2f, day) * period);

        public static long DayIndex(double gameSeconds, double secondsPerDay)
            => secondsPerDay > 0 ? (long)Math.Floor(gameSeconds / secondsPerDay) : 0;

        public static double Wrap(double value, double period) => value - Math.Floor(value / period) * period;

        /// <summary>A deterministic hash of (seed, index) to [0, 1).</summary>
        public static float Hash01(int seed, long index)
        {
            unchecked
            {
                ulong x = (ulong)index * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)seed * 0xC2B2AE3D27D4EB4FUL;
                x ^= x >> 33; x *= 0xFF51AFD7ED558CCDUL; x ^= x >> 33; x *= 0xC4CEB9FE1A85EC53UL; x ^= x >> 33;
                return (x >> 40) / (float)(1UL << 24);
            }
        }

        /// <summary>
        /// The shade's tileable shape for a seed: smooth value-noise octaves on a wrapping lattice, then
        /// rank-equalised so a threshold of <c>1 - coverage</c> leaves about <c>coverage</c> of the ground
        /// under a patch. Built once per seed; the caller owns the buffer.
        /// </summary>
        public static byte[] BuildNoise(int seed, int size, int cells, int octaves)
        {
            var field = new float[size * size];
            float amplitude = 1;
            int lattice = cells;
            for (int o = 0; o < octaves && lattice <= size; o++, amplitude *= .5f, lattice *= 2)
            {
                var corners = new float[lattice * lattice];
                for (int i = 0; i < corners.Length; i++) corners[i] = Hash01(seed + o * 7919, i);
                float texelsPerCell = size / (float)lattice;
                for (int y = 0; y < size; y++)
                {
                    float gy = y / texelsPerCell; int y0 = (int)gy; float fy = Smooth(gy - y0);
                    int ya = y0 % lattice, yb = (y0 + 1) % lattice;
                    for (int x = 0; x < size; x++)
                    {
                        float gx = x / texelsPerCell; int x0 = (int)gx; float fx = Smooth(gx - x0);
                        int xa = x0 % lattice, xb = (x0 + 1) % lattice;
                        float top = Mathf.Lerp(corners[ya * lattice + xa], corners[ya * lattice + xb], fx);
                        float bottom = Mathf.Lerp(corners[yb * lattice + xa], corners[yb * lattice + xb], fx);
                        field[y * size + x] += amplitude * Mathf.Lerp(top, bottom, fy);
                    }
                }
            }
            // Equalise by rank: value = share of texels below it. Ties broken by index, so it is exact.
            var order = new int[field.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (p, q) => field[p] != field[q] ? field[p].CompareTo(field[q]) : p.CompareTo(q));
            var bytes = new byte[field.Length];
            float last = field.Length - 1;
            for (int rank = 0; rank < order.Length; rank++) bytes[order[rank]] = (byte)Mathf.RoundToInt(rank / last * 255);
            return bytes;
        }

        static float Smooth(float t) => t * t * t * (t * (t * 6 - 15) + 10);
    }

    /// <summary>
    /// Where the wind has carried the shade since midnight: the wind law integrated over a fixed grid of
    /// game time from the start of the day (trapezoids, the last one partial). A pure function of
    /// (source, seed, time); the running sum is only a memo of the same additions in the same order, so a
    /// drift stepped frame by frame and one asked cold give the identical answer, and time going back
    /// (a load) simply recomputes. Nothing is saved.
    /// </summary>
    public sealed class CloudDrift
    {
        int _seed; long _day = long.MinValue, _cells; double _step = -1, _dayLength = -1, _sumX, _sumY;
        Vector2 _edge;

        /// <summary>Metres of downwind travel per (m/s) of wind since the start of <paramref name="day"/>:
        /// the integral of the wind vector over game seconds.</summary>
        public Vector2 WindIntegral(IWaveWindSource source, int seed, double gameSeconds, double secondsPerDay,
                                    double stepSeconds, out long day)
        {
            day = CloudShadowMath.DayIndex(gameSeconds, secondsPerDay);
            if (source == null || !(stepSeconds > 0)) return Vector2.zero;
            double start = day * secondsPerDay;
            double local = Math.Max(0, gameSeconds - start);
            long n = (long)Math.Floor(local / stepSeconds);
            if (seed != _seed || day != _day || n < _cells || stepSeconds != _step || secondsPerDay != _dayLength)
            {
                _seed = seed; _day = day; _step = stepSeconds; _dayLength = secondsPerDay;
                _cells = 0; _sumX = 0; _sumY = 0;
                _edge = source.WaveWindAt(start).Wind;
            }
            while (_cells < n)
            {
                Vector2 next = source.WaveWindAt(start + (_cells + 1) * stepSeconds).Wind;
                _sumX += ((double)_edge.x + next.x) * .5 * stepSeconds;
                _sumY += ((double)_edge.y + next.y) * .5 * stepSeconds;
                _edge = next; _cells++;
            }
            double f = local - n * stepSeconds;
            if (!(f > 0)) return new Vector2((float)_sumX, (float)_sumY);
            Vector2 end = source.WaveWindAt(start + (n + 1) * stepSeconds).Wind;
            double k = f * f / (2 * stepSeconds);
            return new Vector2((float)(_sumX + f * _edge.x + (end.x - (double)_edge.x) * k),
                               (float)(_sumY + f * _edge.y + (end.y - (double)_edge.y) * k));
        }

        /// <summary>The shader's two offsets (primary xy, secondary zw), wrapped to the pattern's repeat.</summary>
        public Vector4 Offsets(IWaveWindSource source, int seed, double gameSeconds, double secondsPerDay, CloudShadowDef def)
        {
            Vector2 travel = WindIntegral(source, seed, gameSeconds, secondsPerDay, def.DriftStepSeconds, out long day)
                             * def.DriftMetresPerGameSecondPerMps;
            Vector2 origin = CloudShadowMath.DayOrigin(seed, day, def.ScaleMetres);
            double period = def.ScaleMetres;
            return new Vector4(
                (float)CloudShadowMath.Wrap(origin.x + (double)travel.x, period),
                (float)CloudShadowMath.Wrap(origin.y + (double)travel.y, period),
                (float)CloudShadowMath.Wrap(origin.y + (double)travel.x * def.SecondaryRate, period),
                (float)CloudShadowMath.Wrap(origin.x + (double)travel.y * def.SecondaryRate, period));
        }
    }

    /// <summary>One frame's inputs. The wind is a law of time, never a single sample, so the shape that
    /// is under you is the same however you arrived at this moment.</summary>
    public readonly struct CloudShadowFrame
    {
        public readonly int Seed;
        public readonly double Seconds;
        public readonly IWaveWindSource Wind;
        public readonly float Cover, Elevation;
        public CloudShadowFrame(int seed, double seconds, IWaveWindSource wind, float cover, float elevation)
        { Seed = seed; Seconds = seconds; Wind = wind; Cover = cover; Elevation = elevation; }
    }
}
