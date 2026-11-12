using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>What an authored local effect lays over the weather the player sees and hears: how
    /// much of it is on (0..1), and what it is at full.</summary>
    public readonly struct LocalWeatherShade
    {
        /// <summary>0 = none, 1 = the whole effect.</summary>
        public readonly float Strength01;
        /// <summary>Wind added at full strength, m/s.</summary>
        public readonly Vector2 Wind;
        /// <summary>Visibility at full strength (0 thick .. 1 clear). Never clears the sim's own murk.</summary>
        public readonly float Visibility;
        /// <summary>Sea state at full strength (0 glassy .. 1 storm). Never calms the sim's own sea.</summary>
        public readonly float SeaState01;

        public LocalWeatherShade(float strength01, Vector2 wind, float visibility, float seaState01)
        {
            Strength01 = Mathf.Clamp01(strength01);
            Wind = wind;
            Visibility = Mathf.Clamp01(visibility);
            SeaState01 = Mathf.Clamp01(seaState01);
        }
    }

    /// <summary>
    /// The seam between an authored local effect (a campaign storm wall) and the things that DRAW and
    /// SOUND the weather. It is not the weather: <see cref="IEnvironmentService.Sample"/> is never
    /// wrapped, changed or saved by it (rule 5), and gameplay reads the sim as before. A presentation
    /// reader that wants the local effect passes its sim sample through <see cref="Shade(in EnvironmentSample)"/>;
    /// with nothing set that returns the sample untouched.
    ///
    /// <para>One writer at a time, the last one wins; <see cref="GameServices.Reset"/> clears it.
    /// Nothing here is saved or remembered between frames.</para>
    /// FLAG lead-architect: new Core contract (presentation-only weather shade).
    /// </summary>
    public static class LocalWeather
    {
        private static LocalWeatherShade _shade;
        private static readonly ShadedWaveWind Waves = new ShadedWaveWind();

        /// <summary>The shade in force. Strength 0 when nothing is set.</summary>
        public static LocalWeatherShade Current => _shade;

        /// <summary>True while a shade with any strength is set.</summary>
        public static bool Active => _shade.Strength01 > 0f;

        public static void Set(in LocalWeatherShade shade) => _shade = shade;

        public static void Clear() => _shade = default;

        /// <summary>The sim's sample as the player should see and hear it here. The stepped
        /// <see cref="SeaState"/>, the tide and the current are the sim's own, always.</summary>
        public static EnvironmentSample Shade(in EnvironmentSample sim)
        {
            float k = _shade.Strength01;
            if (k <= 0f) return sim;
            return new EnvironmentSample(
                sim.WindVector + _shade.Wind * k,
                sim.CurrentVector,
                sim.TideHeight,
                sim.SeaState,
                Mathf.Lerp(sim.Visibility, Mathf.Min(sim.Visibility, _shade.Visibility), k),
                Mathf.Lerp(sim.SeaState01, Mathf.Max(sim.SeaState01, _shade.SeaState01), k));
        }

        /// <summary>The wind the wave field is built from, with the local effect laid over every tap.</summary>
        public static WaveWindSample Shade(in WaveWindSample sim)
        {
            float k = _shade.Strength01;
            if (k <= 0f) return sim;
            return new WaveWindSample(sim.Wind + _shade.Wind * k,
                Mathf.Lerp(sim.SeaState01, Mathf.Max(sim.SeaState01, _shade.SeaState01), k));
        }

        /// <summary>A wind-history source that answers as <paramref name="sim"/> does, shaded. Returns
        /// <paramref name="sim"/> itself when no shade is on. No allocation: one shared wrapper.</summary>
        public static IWaveWindSource Shade(IWaveWindSource sim)
        {
            if (sim == null || _shade.Strength01 <= 0f) return sim;
            Waves.Source = sim;
            return Waves;
        }

        private sealed class ShadedWaveWind : IWaveWindSource
        {
            public IWaveWindSource Source;
            public WaveWindSample WaveWindAt(double gameTimeSeconds) => Shade(Source.WaveWindAt(gameTimeSeconds));
        }
    }
}
