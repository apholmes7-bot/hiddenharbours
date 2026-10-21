using System;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The heading dial of the figure pixel look</b> (<see cref="GameConfig.FigureHeadingSteps"/>,
    /// the owner's D2 of 10-15: built at 0, smooth). A mesh figure turns through every angle; a sprite
    /// turns through its sheet's few facings. N steps lets the owner try the sprite's rhythm on the
    /// mesh: the figure's ON-SCREEN facing snaps to N equally spaced compass headings.
    ///
    /// <list type="bullet">
    ///   <item><b>0 (and below) is the identity</b>, bit for bit — the shipped value, and the only value
    ///   while <see cref="GameConfig.FigurePixelTarget"/> is OFF (<see cref="LiveSteps"/>).</item>
    ///   <item><b>Ashore</b> the heading is quantized BEFORE the def's measured azimuth sign is applied;
    ///   the steps are symmetric about 0, so either sign gives the same N facings.</item>
    ///   <item><b>Aboard</b> the figure's yaw rides the hull's continuous turn (the hull turntable and the
    ///   figure's own z turn add, sign for sign), so it is the SUM that is quantized and the hull's
    ///   part taken back out: the hull keeps turning smoothly under her while she steps.</item>
    /// </list>
    ///
    /// <para>Presentation only (rule 5): no simulated heading is read back from here.</para>
    /// </summary>
    public static class FigureHeadingDial
    {
        /// <summary>The steps in force this frame: the config's, while the look is ON; 0 otherwise.</summary>
        public static int LiveSteps => GameServices.FigurePixelTarget ? GameServices.FigureHeadingSteps : 0;

        /// <summary><paramref name="degrees"/> on the nearest of <paramref name="steps"/> equal turns
        /// (halves away from zero, so the dial is odd: <c>Quantize(−a) = −Quantize(a)</c>).
        /// <paramref name="steps"/> ≤ 0 returns the input untouched.</summary>
        public static float Quantize(float degrees, int steps)
        {
            if (steps <= 0) return degrees;
            double step = 360d / steps;
            return (float)(Math.Round(degrees / step, MidpointRounding.AwayFromZero) * step);
        }

        /// <summary>An ashore figure's compass heading, before the def's azimuth sign.</summary>
        public static float Heading(float compassDegrees, int steps) => Quantize(compassDegrees, steps);

        /// <summary>An aboard figure's yaw on her hull: the yaw that puts her on-screen facing
        /// (<paramref name="hullTurnDegrees"/> + yaw) on a step. <paramref name="steps"/> ≤ 0 returns
        /// <paramref name="yawDegrees"/> untouched.</summary>
        public static float DeckYaw(float yawDegrees, float hullTurnDegrees, int steps)
            => steps <= 0 ? yawDegrees : Quantize(hullTurnDegrees + yawDegrees, steps) - hullTurnDegrees;
    }
}
