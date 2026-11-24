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

    /// <summary>What an authored local squall adds to the PICTURE that the shaded sample cannot carry:
    /// the dark front the player sees before he is in the weather, and rain of its own. Drawn only;
    /// like the shade it is never the weather, never read by gameplay and never saved.</summary>
    public readonly struct LocalSquall
    {
        /// <summary>How much of the dark front is across the view: 0 = none of it, 1 = the whole view
        /// is under it.</summary>
        public readonly float Loom01;
        /// <summary>Unit direction from the player toward the weather, world space.</summary>
        public readonly Vector2 Toward;
        /// <summary>How dark the sky is under the front (0 none .. 1 black).</summary>
        public readonly float Darkness01;
        /// <summary>How soft the front's leading edge is, as a share of the view (0 hard .. 1 the whole view).</summary>
        public readonly float Feather01;
        /// <summary>The colour of the dark.</summary>
        public readonly Color Tint;
        /// <summary>Rain the squall brings whatever the sim's sky is doing (0 dry .. 1 a downpour).</summary>
        public readonly float Rain01;
        /// <summary>How much wider (x) and longer (y) a streak of that rain is than ordinary rain, at
        /// a downpour. (1, 1) = ordinary.</summary>
        public readonly Vector2 RainStreakScale;

        public LocalSquall(float loom01, Vector2 toward, float darkness01, float feather01, Color tint,
                           float rain01, Vector2 rainStreakScale)
        {
            Loom01 = Mathf.Clamp01(loom01);
            Toward = toward.sqrMagnitude > 0f ? toward.normalized : Vector2.zero;
            Darkness01 = Mathf.Clamp01(darkness01);
            Feather01 = Mathf.Clamp01(feather01);
            Tint = tint;
            Rain01 = Mathf.Clamp01(rain01);
            RainStreakScale = new Vector2(Mathf.Max(1f, rainStreakScale.x), Mathf.Max(1f, rainStreakScale.y));
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
    /// FLAG lead-architect: new Core contract (presentation-only weather shade, and the squall's
    /// front and rain beside it).
    /// </summary>
    public static class LocalWeather
    {
        private static LocalWeatherShade _shade;
        private static LocalSquall _squall;
        private static bool _shadesSurface;
        private static int _surfaceEdition;
        private static readonly ShadedWaveWind Waves = new ShadedWaveWind();

        /// <summary>The shade in force. Strength 0 when nothing is set.</summary>
        public static LocalWeatherShade Current => _shade;

        /// <summary>True while a shade with any strength is set.</summary>
        public static bool Active => _shade.Strength01 > 0f;

        /// <summary>Set the shade for the readers that have always taken it (the sky's grade, the
        /// falling rain, the wave field, the sound). The water surface and its foam, mist and spray
        /// are NOT shaded by this: see <see cref="Set(in LocalWeatherShade, bool)"/>.</summary>
        public static void Set(in LocalWeatherShade shade) => Set(in shade, false);

        /// <summary>Set the shade, and say whether the water surface and its foam, mist and spray
        /// take it too (<see cref="ShadeSurface"/>). An effect that does not ask leaves those four
        /// reading the sim's own sample, exactly.</summary>
        public static void Set(in LocalWeatherShade shade, bool shadesSurface)
        {
            _shade = shade;
            TakeSurface(shadesSurface && shade.Strength01 > 0f);
        }

        /// <summary>Clear the shade and the squall together.</summary>
        public static void Clear()
        {
            _shade = default;
            _squall = default;
            TakeSurface(false);
        }

        private static void TakeSurface(bool on)
        {
            if (on == _shadesSurface) return;
            _shadesSurface = on;
            unchecked { _surfaceEdition++; }
        }

        /// <summary>True while a shade is set that the water surface and its foam, mist and spray
        /// take as well.</summary>
        public static bool ShadesSurface => _shadesSurface;

        /// <summary>Changes each time <see cref="ShadesSurface"/> turns on or off, and at no other
        /// time. A surface reader that keeps a sample between its own slow ticks compares this and
        /// reads again at once, so a shade that lifts is not drawn on from a kept sample.</summary>
        public static int SurfaceEdition => _surfaceEdition;

        /// <summary>The sim's sample as the WATER SURFACE and its foam, mist and spray should draw it:
        /// shaded only while the effect in force asked for that (<see cref="ShadesSurface"/>);
        /// otherwise <paramref name="sim"/> itself, untouched.</summary>
        public static EnvironmentSample ShadeSurface(in EnvironmentSample sim) =>
            _shadesSurface ? Shade(in sim) : sim;

        /// <summary>The squall in force: its dark front and its rain. All zero when nothing is set.</summary>
        public static LocalSquall Squall => _squall;

        /// <summary>True while a squall shows anything: any of its front, or any of its rain.</summary>
        public static bool SquallActive => _squall.Loom01 > 0f || _squall.Rain01 > 0f;

        public static void SetSquall(in LocalSquall squall) => _squall = squall;

        /// <summary>The rain to draw: what the sky's own mood gives (<paramref name="derived01"/>), or
        /// the squall's own, whichever is more. The falling streaks and the rings on the water both
        /// ask here, so they stay one rain.</summary>
        public static float Rain(float derived01) => Mathf.Max(derived01, _squall.Rain01);

        /// <summary>How much wider (x) and longer (y) to draw a rain streak: (1, 1) with no squall
        /// rain, the squall's own scale at a downpour.</summary>
        public static Vector2 RainStreakScale =>
            Vector2.Lerp(Vector2.one, _squall.RainStreakScale, _squall.Rain01);

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
