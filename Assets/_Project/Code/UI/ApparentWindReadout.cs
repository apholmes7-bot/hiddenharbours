using System.Globalization;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// Pure conversions for the VS-19 <b>apparent wind</b> read — the true wind expressed RELATIVE to
    /// the boat's heading, so the player reads the wind hitting them (off which bow / beam / quarter)
    /// rather than only its absolute compass direction. Built on the Core heading seam
    /// (<see cref="BoatKinematics"/>, ADR 0007) + the environment's wind vector, composed via
    /// <see cref="BoatKinematics.RelativeBearingDegrees"/> so the UI never references the Boats module.
    /// Engine-light &amp; stateless → EditMode-testable; the no-per-frame-allocation discipline lives in
    /// <see cref="HudController"/>'s change-detection (mirrors <see cref="WindReadout"/> /
    /// <see cref="CompassReadout"/>).
    ///
    /// <para><b>Convention.</b> The result is where the wind COMES FROM, measured off the bow, in
    /// [-180, 180): 0° = dead ahead (a headwind), +90° = from the starboard side, ±180° = from dead
    /// astern (a following wind), -90° = from port. This is derived from the same bearing math as the
    /// compass and the wind widget (the wind vector points where the air BLOWS TOWARD, so the source is
    /// its reverse), so heading, true wind, and apparent wind all share one dial. A cross-check test
    /// pins this against <see cref="WindReadout"/> / <see cref="BoatKinematics"/> so they can't drift.</para>
    /// </summary>
    public static class ApparentWindReadout
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Wind speed (m/s) below which direction is meaningless — shown as calm, not a jittery
        /// bearing. Named tunable, not a magic number.</summary>
        public const float CalmSpeedMps = 0.05f;

        /// <summary>Within this of the bow (or of dead astern) the wind reads as a clean head/following
        /// wind rather than nagging a side. Named, not magic.</summary>
        public const float AlignedDeadbandDegrees = 12f;

        /// <summary>Off-the-bow magnitude (deg) up to which the wind is on the "bow"; past
        /// <see cref="QuarterSectorMinDegrees"/> it is on the "quarter"; between, the "beam".</summary>
        public const float BowSectorMaxDegrees = 60f;
        public const float QuarterSectorMinDegrees = 120f;

        // 8-point arrows clockwise from "ahead" (bow = up), the SHAPE channel. Same glyph set/convention
        // as WindReadout/CompassReadout, but read in the BOAT frame: ↑ = from ahead, → = from starboard,
        // ↓ = from astern, ← = from port. The arrow points to where the wind comes FROM (a wind vane).
        private static readonly string[] Arrows8 = { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" };

        private const string Label    = "Apparent ";
        private const string Ahead    = "ahead";    // headwind
        private const string Astern   = "astern";   // following wind
        private const string Calm     = "calm";

        /// <summary>True when the wind is too light to give a meaningful direction.</summary>
        public static bool IsCalm(Vector2 windVector, float calmSpeed = CalmSpeedMps)
            => windVector.sqrMagnitude < calmSpeed * calmSpeed;

        /// <summary>
        /// The apparent wind angle: where the wind comes FROM, measured off the bow, in [-180, 180).
        /// 0 = dead ahead, + = from starboard, - = from port, ±180 = dead astern. Composed from the
        /// shared Core primitives (the wind's source bearing is the reverse of its blow-toward vector).
        /// Meaningless for a calm vector — gate on <see cref="IsCalm"/> first.
        /// </summary>
        public static float RelativeBearing(float headingDegrees, Vector2 windVector)
        {
            float sourceBearing = BoatKinematics.BearingDegrees(-windVector); // where it blows FROM
            return BoatKinematics.RelativeBearingDegrees(headingDegrees, sourceBearing);
        }

        /// <summary>8-point arrow pointing to where the wind comes FROM, in the boat frame (bow up).</summary>
        public static string Arrow(float relativeBearingDegrees)
        {
            float b = Mathf.Repeat(relativeBearingDegrees, 360f);
            int oct = Mathf.RoundToInt(b / 45f) % 8;
            return Arrows8[oct];
        }

        /// <summary>
        /// The point-of-sail word for an apparent-wind angle: "ahead" / "astern" near the bow/stern,
        /// else a side + sector ("stbd bow", "port beam", "stbd quarter"). Redundant with the arrow
        /// SHAPE and the degrees NUMBER (§8 — never colour alone).
        /// </summary>
        public static string PointOfSail(float relativeBearingDegrees,
                                         float alignedDeadband = AlignedDeadbandDegrees)
        {
            float mag = Mathf.Abs(relativeBearingDegrees);
            if (mag <= alignedDeadband) return Ahead;
            if (mag >= 180f - alignedDeadband) return Astern;

            string side = relativeBearingDegrees > 0f ? "stbd" : "port";
            string sector = mag < BowSectorMaxDegrees ? "bow"
                          : mag > QuarterSectorMinDegrees ? "quarter"
                          : "beam";
            return side + " " + sector;
        }

        /// <summary>
        /// The full apparent-wind read: "Apparent ↗ 45° stbd bow" (arrow SHAPE + degrees NUMBER +
        /// point-of-sail WORD), or "Apparent ○ calm" when there's no usable wind. Pure: heading +
        /// true-wind vector in, string out (HudController change-detects it, so this never allocates
        /// on an unchanged read).
        /// </summary>
        public static string Format(float headingDegrees, Vector2 windVector)
        {
            if (IsCalm(windVector)) return Label + HudStrings.WindCalm + " " + Calm;

            float rel = RelativeBearing(headingDegrees, windVector);
            int mag = Mathf.RoundToInt(Mathf.Abs(rel));
            return Label + Arrow(rel) + " " + mag.ToString(Inv) + "° " + PointOfSail(rel);
        }
    }
}
