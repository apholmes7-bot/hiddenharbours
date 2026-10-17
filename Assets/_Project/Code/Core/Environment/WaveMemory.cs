using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Wind and its continuous sea-state axis at a specified world time.</summary>
    public readonly struct WaveWindSample
    {
        public readonly Vector2 Wind;
        public readonly float SeaState01;

        public WaveWindSample(Vector2 wind, float seaState01)
        {
            Wind = wind;
            SeaState01 = seaState01;
        }
    }

    /// <summary>Core seam for a deterministic wind law, including times before world time zero.
    /// Implementations evaluate the active region's law and dev overrides without storing history.</summary>
    public interface IWaveWindSource
    {
        WaveWindSample WaveWindAt(double gameTimeSeconds);
    }

    /// <summary>Finite, causal wind memory on three fixed world-time grids. No saved or frame state.</summary>
    public static class WaveMemory
    {
        /// <summary>Average the N completed grid cells ending at t. The wind is held over each cell;
        /// fractional end cells enter and leave continuously. Only taps at or before t are queried.
        /// Vector averaging weights direction by speed: zero wind contributes no bearing.
        /// Subtracting the first tap before summing preserves a steady input bit for bit.</summary>
        public static WaveWindSample Average(IWaveWindSource source, double time, float window, int taps)
        {
            int n = Mathf.Clamp(taps, 4, 64);
            double step = (double)window / n;
            double cell = Math.Floor(time / step);
            double fraction = time / step - cell;
            WaveWindSample first = source.WaveWindAt(cell * step);
            double x = 0, y = 0, sea = 0;
            for (int i = 1; i <= n; i++)
            {
                WaveWindSample tap = source.WaveWindAt((cell - i) * step);
                double weight = i == n ? 1.0 - fraction : 1.0;
                x += ((double)tap.Wind.x - first.Wind.x) * weight;
                y += ((double)tap.Wind.y - first.Wind.y) * weight;
                sea += ((double)tap.SeaState01 - first.SeaState01) * weight;
            }
            if (x == 0.0 && y == 0.0 && sea == 0.0) return first;
            return new WaveWindSample(
                new Vector2((float)(first.Wind.x + x / n), (float)(first.Wind.y + y / n)),
                (float)(first.SeaState01 + sea / n));
        }

        public static WaveWindSample ShortWind(IWaveWindSource source, double time,
                                               in WaveFieldSettings settings)
            => Average(source, time, ShortSeconds(settings), TapCount(settings));

        /// <summary>Raw trains, sampled at game time. Each slot uses its wavelength's wind window.
        /// TrainsFrom remains the steady-state derivation, including the spectrum and glass gate.</summary>
        public static WaveTrains TrainsAt(IWaveWindSource source, double time, in WaveFieldSettings settings)
        {
            float shortSeconds = ShortSeconds(settings);
            float middleSeconds = Mathf.Max(shortSeconds, Positive(settings.MemoryMiddleSeconds,
                WaveFieldSettings.Default.MemoryMiddleSeconds, 3600f));
            float longSeconds = Mathf.Max(middleSeconds, Positive(settings.MemoryLongSeconds,
                WaveFieldSettings.Default.MemoryLongSeconds, 7200f));
            int taps = TapCount(settings);
            var shortWind = Average(source, time, shortSeconds, taps);
            var middleWind = Average(source, time, middleSeconds, taps);
            var longWind = Average(source, time, longSeconds, taps);
            var shortTrains = WaveMath.TrainsFrom(shortWind.Wind, shortWind.SeaState01, in settings);
            // Preserve even the silent spectrum's peak slot when all windows agree.
            if (Same(shortWind, middleWind) && Same(shortWind, longWind)) return shortTrains;
            var middleTrains = WaveMath.TrainsFrom(middleWind.Wind, middleWind.SeaState01, in settings);
            var longTrains = WaveMath.TrainsFrom(longWind.Wind, longWind.SeaState01, in settings);
            float shortLimit = Positive(settings.MemoryShortMaxWavelengthMeters,
                WaveFieldSettings.Default.MemoryShortMaxWavelengthMeters, 100f);
            float middleLimit = Mathf.Max(shortLimit, Positive(settings.MemoryMiddleMaxWavelengthMeters,
                WaveFieldSettings.Default.MemoryMiddleMaxWavelengthMeters, 100f));
            Span<WaveTrain> trains = stackalloc WaveTrain[WaveTrains.MaxTrains];
            int dominant = 0;
            for (int i = 0; i < shortTrains.Count; i++)
            {
                float wavelength = shortTrains[i].Wavelength;
                trains[i] = wavelength <= shortLimit ? shortTrains[i]
                    : wavelength <= middleLimit ? middleTrains[i] : longTrains[i];
                if (trains[i].Amplitude > trains[dominant].Amplitude) dominant = i;
            }
            return WaveTrains.From(trains, shortTrains.Count, settings.CrestSharpening, dominant);
        }

        private static bool Same(WaveWindSample a, WaveWindSample b)
            => a.Wind.x == b.Wind.x && a.Wind.y == b.Wind.y && a.SeaState01 == b.SeaState01;

        private static float Positive(float value, float fallback, float maximum)
            => Mathf.Clamp(value > 0f ? value : fallback, 1f, maximum);

        private static float ShortSeconds(in WaveFieldSettings settings)
            => Positive(settings.MemoryShortSeconds, WaveFieldSettings.Default.MemoryShortSeconds, 600f);

        private static int TapCount(in WaveFieldSettings settings)
            => Mathf.Clamp(settings.MemoryTapCount > 0 ? settings.MemoryTapCount
                : WaveFieldSettings.Default.MemoryTapCount, 4, 64);
    }
}
