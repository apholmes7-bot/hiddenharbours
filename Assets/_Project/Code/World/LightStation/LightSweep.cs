using System;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>Where a turning lens points, from the clock alone.</b> Pure arithmetic: the same second always gives
    /// the same bearing, so a reload, a time skip or a second player cannot disagree about the light (rule 5).
    /// Bearings are degrees on the ground, 0 along +x, counter-clockwise; the viewer looks from -y.
    /// </summary>
    public static class LightSweep
    {
        /// <summary>The bearing a beam leaves toward the viewer on, degrees.</summary>
        public const float TowardViewerDegrees = 270f;

        const double FullTurn = 360d;

        /// <summary>The first beam's ground bearing at <paramref name="seconds"/>, degrees in [0, 360).</summary>
        public static float BearingDegrees(double seconds, float turnSeconds, float phaseSeconds)
        {
            if (!(turnSeconds > 0f)) return 0f;
            double turns = (seconds + phaseSeconds) / turnSeconds;
            double part = turns - Math.Floor(turns);
            float bearing = (float)(part * FullTurn);
            return bearing >= 360f ? 0f : bearing;
        }

        /// <summary>How far the nearest of <paramref name="beams"/> evenly spaced beams is from pointing at the
        /// viewer, degrees in [0, 180 / beams].</summary>
        public static float OffViewerDegrees(float bearingDegrees, int beams)
        {
            if (beams < 1) beams = 1;
            double spacing = FullTurn / beams;
            double off = (bearingDegrees - TowardViewerDegrees) % spacing;
            if (off < 0d) off += spacing;
            return (float)Math.Min(off, spacing - off);
        }

        /// <summary>Is a beam crossing the viewer, so the lantern shows its flash?</summary>
        public static bool IsFlashing(float bearingDegrees, int beams, float flashHalfAngleDegrees) =>
            OffViewerDegrees(bearingDegrees, beams) <= flashHalfAngleDegrees;

        /// <summary>Seconds between flashes: one for each beam in a turn.</summary>
        public static float FlashEverySeconds(float turnSeconds, int beams) => turnSeconds / Math.Max(1, beams);

        /// <summary>
        /// The angle a ground bearing is DRAWN at, degrees counter-clockwise from screen right, when ground depth is
        /// drawn at <paramref name="groundDepthScale"/> of its length. A beam along the ground's x keeps its
        /// angle; one running away from the viewer is squeezed toward the horizontal like everything else.
        /// </summary>
        public static float DrawnDegrees(float bearingDegrees, float groundDepthScale)
        {
            double r = bearingDegrees * Math.PI / 180d;
            double a = Math.Atan2(Math.Sin(r) * groundDepthScale, Math.Cos(r)) * 180d / Math.PI;
            return (float)(a < 0d ? a + FullTurn : a);
        }

        /// <summary>How long a beam of <paramref name="reach"/> ground metres is drawn at that bearing.</summary>
        public static float DrawnReach(float bearingDegrees, float reach, float groundDepthScale)
        {
            double r = bearingDegrees * Math.PI / 180d;
            double x = Math.Cos(r), y = Math.Sin(r) * groundDepthScale;
            return (float)(reach * Math.Sqrt(x * x + y * y));
        }
    }
}
