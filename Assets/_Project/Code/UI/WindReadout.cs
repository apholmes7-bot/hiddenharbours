using System;
using UnityEngine;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// Pure conversions for the wind widget: m/s to knots and Beaufort, and a wind vector to a
    /// 16-point compass cardinal. Engine-light (only <see cref="Vector2"/>) so the conversions are
    /// EditMode-testable. World/absolute terms only — wind *relative to heading* is a VS-19
    /// follow-up that needs a Core boat-heading contract that does not exist yet.
    /// </summary>
    public static class WindReadout
    {
        public const float MetresPerSecondToKnots = 1.943_844f; // 1 m/s = 1.94384 kn (exact-ish)

        /// <summary>Wind speed in knots from a m/s magnitude.</summary>
        public static float Knots(float metresPerSecond) => metresPerSecond * MetresPerSecondToKnots;

        /// <summary>Magnitude of a wind vector (m/s).</summary>
        public static float Strength(Vector2 windVector) => windVector.magnitude;

        // Beaufort upper bounds in m/s (force 0..12). A speed is force N if it is below
        // BeaufortUpperBoundsMs[N] (and >= the previous bound). Anything past the last bound is 12.
        // Standard scale (WMO). No magic numbers buried in logic — the table is the definition.
        private static readonly float[] BeaufortUpperBoundsMs =
        {
            0.5f,   // 0 Calm
            1.5f,   // 1 Light air
            3.3f,   // 2 Light breeze
            5.5f,   // 3 Gentle breeze
            7.9f,   // 4 Moderate breeze
            10.7f,  // 5 Fresh breeze
            13.8f,  // 6 Strong breeze
            17.1f,  // 7 Near gale
            20.7f,  // 8 Gale
            24.4f,  // 9 Strong gale
            28.4f,  // 10 Storm
            32.6f   // 11 Violent storm; >= this is 12 Hurricane force
        };

        /// <summary>Beaufort force (0..12) for a wind speed in m/s.</summary>
        public static int Beaufort(float metresPerSecond)
        {
            float s = metresPerSecond < 0f ? 0f : metresPerSecond;
            for (int force = 0; force < BeaufortUpperBoundsMs.Length; force++)
                if (s < BeaufortUpperBoundsMs[force]) return force;
            return 12;
        }

        // 16-point compass, clockwise from North. This is the direction the wind blows TOWARD
        // (the vector points where the air is going); the HUD labels it as the bearing of the arrow.
        private static readonly string[] Compass16 =
        {
            "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
            "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"
        };

        /// <summary>
        /// 16-point cardinal for the direction a wind vector points toward, in screen/world terms.
        /// Convention: +Y is North, +X is East (matches the game's top-down world axes). Returns
        /// "--" for a calm (near-zero) vector since direction is undefined.
        /// </summary>
        public static string Cardinal(Vector2 windVector, float deadZone = 0.01f)
        {
            if (windVector.sqrMagnitude < deadZone * deadZone) return "--";

            // Bearing clockwise from North (+Y). atan2(east, north) gives 0 at N, +90 at E.
            float bearing = Mathf.Atan2(windVector.x, windVector.y) * Mathf.Rad2Deg;
            if (bearing < 0f) bearing += 360f;

            // 16 sectors of 22.5 deg; offset by half a sector so a sector is centred on its label.
            int index = Mathf.RoundToInt(bearing / 22.5f) % 16;
            return Compass16[index];
        }

        // 8-point arrows clockwise from North — a coarse SHAPE channel for direction that complements
        // the 16-point Cardinal text (redundant coding; Unicode has clean glyphs only at 8-way).
        private static readonly string[] Arrows8 =
        {
            "↑", "↗", "→", "↘", "↓", "↙", "←", "↖"
        };

        /// <summary>
        /// 8-point arrow glyph for the direction a wind vector points toward (same +Y=N, +X=E
        /// convention as <see cref="Cardinal"/>). Returns "·" for a calm (near-zero) vector.
        /// </summary>
        public static string ArrowGlyph(Vector2 windVector, float deadZone = 0.01f)
        {
            if (windVector.sqrMagnitude < deadZone * deadZone) return "·";

            float bearing = Mathf.Atan2(windVector.x, windVector.y) * Mathf.Rad2Deg;
            if (bearing < 0f) bearing += 360f;

            int octant = Mathf.RoundToInt(bearing / 45f) % 8;
            return Arrows8[octant];
        }
    }
}
