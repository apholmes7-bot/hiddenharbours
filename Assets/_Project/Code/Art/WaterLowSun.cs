using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Pure, headless-testable twins of the water shader's LOW SUN layer: what the sea does while the sun is
    /// near the horizon and the day/night tint is strongly coloured.
    ///
    /// The day/night overlay multiplies the finished frame by the tint. A multiply can only darken, so under a
    /// red-gold tint the sea was the noon sea made dark and brown, with no light on it, while white foam took
    /// the tint whole and stood far above it. Three things answer that, and all three are exact passthroughs at
    /// high sun, at night, and while no day/night cycle runs:
    ///   * a SHEEN: the glowing sky the sea mirrors, added under the foam, gold toward the sun and dusk-rose
    ///     away from it. It fades with the tint's own colour, so a grey (overcast) evening has none;
    ///   * a HUE HOLD: how far the sea keeps its own hue under the coloured light;
    ///   * SPARKLES: the sun path drawn as many short level glints in a lane, in place of long noise lanes.
    /// The dials live on the water material (<c>_LowSun*</c>, <c>_SunGlitterForm</c>, <c>_SunGlitterCell</c>,
    /// <c>_SunGlitterDash</c>, <c>_SunGlitterSpark</c>); every shader default is the passthrough.
    /// Everything here is a pure function of its arguments: no state, no randomness.
    /// </summary>
    public static class WaterLowSun
    {
        /// <summary>The floor every tint-compensated add divides by (the shader's DN_COMP_MIN_CHANNEL).</summary>
        public const float MinTintChannel = 0.02f;

        /// <summary>A sparkle is born short, flares to full length and dies short: the pulse above which it
        /// is full length, the pulse above which it is half length, and the pulse below which it is unlit.</summary>
        public const float SparkleFullAbove = 0.66f;
        public const float SparkleHalfAbove = 0.25f;
        public const float SparkleLitAbove = 0.08f;

        /// <summary>
        /// Twin of <c>LowSunWeight</c>. 0 at and below <paramref name="belowHorizonEnd"/> (a negative
        /// elevation: the night is untouched), 1 from the horizon up to <paramref name="highFadeStart"/>, and 0
        /// at and above <paramref name="highFadeEnd"/> (the high-sun day is untouched, exactly). 0 while no
        /// day/night cycle runs (<paramref name="tint"/> all zero).
        /// </summary>
        public static float Weight(float sunElevation, Color tint, float belowHorizonEnd, float highFadeStart,
            float highFadeEnd)
        {
            if (tint.r + tint.g + tint.b <= 1e-3f) return 0f;
            float below = WaterSurface.Smoothstep(Mathf.Min(belowHorizonEnd, -1e-4f), 0f, sunElevation);
            float high = 1f - WaterSurface.Smoothstep(highFadeStart, Mathf.Max(highFadeEnd, highFadeStart + 1e-4f),
                sunElevation);
            return Mathf.Clamp01(below * high);
        }

        /// <summary>
        /// Twin of <c>LowSunColourful</c>: the tint's saturation as a share of
        /// <paramref name="fullSaturation"/>, 0..1. A grey tint returns 0.
        /// </summary>
        public static float Colourful(Color tint, float fullSaturation)
        {
            float mx = Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b));
            float mn = Mathf.Min(tint.r, Mathf.Min(tint.g, tint.b));
            float sat = mx <= 1e-5f ? 0f : (mx - mn) / mx;
            return Mathf.Clamp01(sat / Mathf.Max(fullSaturation, 1e-3f));
        }

        /// <summary>
        /// Twin of the sheen's place between the far colour (0) and the near colour (1): half way at the
        /// viewer, full near at <paramref name="reach"/> metres toward the sun, full far the same distance away.
        /// </summary>
        public static float SheenToward(float metresTowardSun, float reach)
        {
            return Mathf.Clamp01(0.5f + 0.5f * metresTowardSun / Mathf.Max(reach, 1e-3f));
        }

        /// <summary>
        /// Twin of <c>LowSunSheen</c>: the colour ADDED to the sea before the foam and the grade. It is divided
        /// by the tint, so once the overlay has multiplied the frame it is the authored colour on screen.
        /// </summary>
        /// <param name="faceSigned">The sea's facing, -1..1 (+1 = tipped to the sun).</param>
        /// <param name="lane">0..1, how far inside the sun's lane this point is.</param>
        public static Vector3 Sheen(Color near, Color far, float strength, float relief, float laneLift,
            float toward, float lane, float faceSigned, float weight, float colourful, Color tint)
        {
            float amount = Mathf.Max(strength, 0f) * weight * colourful;
            if (amount <= 0.0005f) return Vector3.zero;
            toward = Mathf.Clamp01(toward);
            Vector3 sky = Vector3.Lerp(new Vector3(far.r, far.g, far.b), new Vector3(near.r, near.g, near.b), toward);
            float shape = Mathf.Max(1f + relief * faceSigned, 0f)
                        * (1f + Mathf.Max(laneLift, 0f) * Mathf.Clamp01(lane) * toward);
            sky *= amount * shape;
            return new Vector3(sky.x / Mathf.Max(tint.r, MinTintChannel), sky.y / Mathf.Max(tint.g, MinTintChannel),
                sky.z / Mathf.Max(tint.b, MinTintChannel));
        }

        /// <summary>
        /// Twin of <c>lowSunDilute</c>: the hull waves' low-light shade was sized on the dark sea, so it is
        /// scaled by how far the sheen lifted the sea (wakeHold 0), or kept as a share of the lit sea (1). Exactly 1 with no sheen.
        /// </summary>
        public static float HullWaveDilute(float seaLuma, float sheenLuma, float wakeHold)
        {
            if (sheenLuma <= 0f) return 1f;
            return Mathf.Lerp(Mathf.Clamp01(seaLuma / Mathf.Max(seaLuma + sheenLuma, 1e-4f)), 1f, Mathf.Clamp01(wakeHold));
        }

        /// <summary>
        /// Twin of <c>LowSunHueHoldFactor</c>: what the finished sea is multiplied by so the overlay's tint
        /// lands on it as <c>lerp(tint, grey(tint), share)</c>. Exactly (1, 1, 1) at share 0.
        /// </summary>
        public static Vector3 HueHoldFactor(Color tint, float share)
        {
            float r = Mathf.Max(tint.r, MinTintChannel), g = Mathf.Max(tint.g, MinTintChannel),
                  b = Mathf.Max(tint.b, MinTintChannel);
            float grey = r * 0.299f + g * 0.587f + b * 0.114f;
            share = Mathf.Clamp01(share);
            return new Vector3(Mathf.Lerp(r, grey, share) / r, Mathf.Lerp(g, grey, share) / g,
                Mathf.Lerp(b, grey, share) / b);
        }

        /// <summary>
        /// Twin of the sparkle form's lit share: the share of grid cells that hold a glint here. The lane and
        /// the sunward side raise the COUNT of glints, never their brightness.
        /// </summary>
        /// <param name="surf">The surface noise, 0..1, that gathers glints into patches.</param>
        public static float SparkleLitShare(float cellShare, float outsideShare, float lane, float toward, float surf)
        {
            float inLane = Mathf.Clamp01(lane) * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(toward));
            return Mathf.Clamp01(Mathf.Clamp01(cellShare) * Mathf.Lerp(Mathf.Clamp01(outsideShare), 1f, inLane)
                 * Mathf.Lerp(0.4f, 1.6f, Mathf.Clamp01(surf)));
        }

        /// <summary>
        /// Twin of one sparkle's length over its life (0..1) as a share of its full length: 0 (a single
        /// pixel), 0.5 or 1. Three steps, not a fade.
        /// </summary>
        public static float SparkleLength(float life01)
        {
            float pulse = 1f - Mathf.Abs(Mathf.Repeat(life01, 1f) * 2f - 1f);
            return pulse > SparkleFullAbove ? 1f : (pulse > SparkleHalfAbove ? 0.5f : 0f);
        }

        /// <summary>Twin of whether a sparkle is drawn at all at this point of its life (0..1).</summary>
        public static bool SparkleLit(float life01)
        {
            return 1f - Mathf.Abs(Mathf.Repeat(life01, 1f) * 2f - 1f) >= SparkleLitAbove;
        }
    }
}
