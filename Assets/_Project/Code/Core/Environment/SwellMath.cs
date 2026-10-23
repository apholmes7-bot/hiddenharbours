using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Stage C: two fixed-frequency swell trains in slots 6 and 7, after wind memory.</summary>
    public static class SwellMath
    {
        public const int FirstSlot = 6;

        public static Vector2 Direction(in SwellClimateSettings climate, int seed, double time, float secondsPerDay)
        {
            double days = time / Math.Max(1.0, secondsPerDay);
            double cycle = WaveFetch.Positive(climate.DirectionCycleDays, 6f, 1f, 60f);
            double angle = (climate.FromBearingDegrees + Mathf.Clamp(climate.DirectionDriftDegrees, 0f, 45f)
                * Math.Sin(2.0 * Math.PI * (days / cycle + Hash01(seed, 0)))) * Math.PI / 180.0;
            return new Vector2(-(float)Math.Sin(angle), -(float)Math.Cos(angle));
        }

        public static float Height(in SwellClimateSettings climate, int seed, double time, float secondsPerDay)
        {
            double days = time / Math.Max(1.0, secondsPerDay);
            double cycle = WaveFetch.Positive(climate.HeightCycleDays, 4f, 1f, 60f);
            return Mathf.Clamp(climate.HeightMeters, 0f, 2f) * (float)(1.0
                + Mathf.Clamp01(climate.HeightDriftFraction) * Math.Sin(2.0 * Math.PI * (days / cycle + Hash01(seed, 1))));
        }

        /// <summary>Keep the six wind rungs byte for byte, without renormalizing their energy.</summary>
        public static WaveTrains Compose(in WaveTrains wind, in SwellClimateSettings climate,
                                         int seed, double time, float secondsPerDay, float gravity)
        {
            Span<WaveTrain> trains = stackalloc WaveTrain[WaveTrains.MaxTrains];
            for (int i = 0; i < Math.Min(FirstSlot, wind.Count); i++) trains[i] = wind[i];
            Vector2 direction = Direction(in climate, seed, time, secondsPerDay);
            float amplitude = Height(in climate, seed, time, secondsPerDay) * 0.5f;
            float wavelength = WaveFetch.Positive(climate.WavelengthMeters, 40f, 1f, 200f);
            float ratio = WaveFetch.Positive(climate.SecondaryWavelengthRatio, 0.81f, 0.5f, 1f);
            float primary = Mathf.Clamp01(climate.PrimaryShare);
            trains[6] = new WaveTrain(direction, wavelength, amplitude * primary,
                Hash01(seed, 2) * (2f * Mathf.PI), gravity);
            trains[7] = new WaveTrain(direction, wavelength * ratio, amplitude * (1f - primary),
                Hash01(seed, 3) * (2f * Mathf.PI), gravity);
            int dominant = 0;
            for (int i = 1; i < WaveTrains.MaxTrains; i++)
                if (trains[i].Amplitude > trains[dominant].Amplitude) dominant = i;
            return WaveTrains.From(trains, WaveTrains.MaxTrains, wind.CrestSharpening, dominant);
        }

        /// <summary>Transmission through a clear incoming path. Land anywhere in the reach stops
        /// the swell exactly; unlike wind sea, it cannot grow again behind that land.</summary>
        public static float ShelterAt(Vector2 position, Vector2 direction, float level, ITidalTerrain terrain,
                                      in SwellClimateSettings climate)
            => ShelterAt(position, direction, level, terrain, in climate, default);

        public static float ShelterAt(Vector2 position, Vector2 direction, float level, ITidalTerrain terrain,
                                      in SwellClimateSettings climate, in SwellShelterSettings basins)
        {
            float transmission = basins.TransmissionAt(position);
            if (transmission <= 0f || terrain == null) return transmission;
            Vector2 upstream = WaveFetch.Upwind(direction);
            float step = ShelterStep(in climate);
            float band = ShelterBand(in climate);
            for (int i = 1; i <= WaveFetch.MarchSteps; i++)
            {
                Vector2 p = WaveFetch.Pixelize(new Vector2(position.x + upstream.x * (step * i),
                                                          position.y + upstream.y * (step * i)));
                transmission *= WaveFetch.SmoothstepEdge(0f, band, level - terrain.ElevationAt(p));
            }
            return transmission;
        }

        public static float ShelterStep(in SwellClimateSettings climate)
            => WaveFetch.Positive(climate.ShelterStepMeters, 4f, 0.05f, 16f);
        public static float ShelterBand(in SwellClimateSettings climate)
            => WaveFetch.Positive(climate.ShelterDepthBandMeters, 0.25f, 0.001f, 2f);

        private static float Hash01(int seed, int stream)
        {
            unchecked
            {
                uint n = (uint)seed ^ ((uint)stream * 0x9e3779b9u) ^ 0x85ebca6bu;
                n ^= n >> 16; n *= 0x7feb352du; n ^= n >> 15; n *= 0x846ca68bu; n ^= n >> 16;
                return (n >> 8) / 16777216f;
            }
        }
    }
}
