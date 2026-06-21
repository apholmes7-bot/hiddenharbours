using System.Globalization;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// Pure conversions for the VS-19 heading <b>compass</b> + <b>set-&amp;-drift</b> read, built on the
    /// Core boat-heading seam (<see cref="BoatKinematics"/>, ADR 0007). Engine-light &amp; stateless so
    /// they are EditMode-testable; the no-per-frame-allocation discipline lives in
    /// <see cref="HudController"/>'s change-detection, not hidden here (mirrors <see cref="WindReadout"/>
    /// / <see cref="HudFormat"/>).
    ///
    /// <para>Bearings are compass degrees — 0 = N, 90 = E, clockwise — the SAME convention as
    /// <see cref="WindReadout"/>, so heading, wind, and course all read on one dial. A cross-check test
    /// pins this against <see cref="WindReadout.Cardinal"/> so they can never silently disagree on
    /// North.</para>
    /// </summary>
    public static class CompassReadout
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Speed-over-ground (m/s) below which course-over-ground is meaningless — you are
        /// drifting / dead in the water, so the set read shows "no course" rather than a jittery
        /// bearing. A named tunable, not a magic number.</summary>
        public const float UnderwaySpeedMps = 0.25f;

        /// <summary>Set angle (deg) within which the bow and the track count as aligned ("on track"),
        /// so a hair of leeway doesn't nag. Named, not magic.</summary>
        public const float OnTrackDeadbandDegrees = 3f;

        // 16-point compass, clockwise from North — the SAME table/convention as WindReadout.Cardinal.
        private static readonly string[] Compass16 =
        {
            "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
            "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"
        };

        // 8-point heading arrows (the SHAPE channel), clockwise from North. Mirrors WindReadout.ArrowGlyph.
        private static readonly string[] Arrows8 = { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" };

        // Set-direction markers (the bow is being set off its track) — shape, paired with the side word.
        private const string SetStarboard = "→";
        private const string SetPort      = "←";
        private const string OnTrack      = "on track";
        private const string NoCourse     = "COG —";   // at rest / drifting: no steady course

        /// <summary>16-point cardinal for a compass bearing in degrees (0 = N, clockwise). Wraps any input.</summary>
        public static string Cardinal(float bearingDegrees)
        {
            float b = Mathf.Repeat(bearingDegrees, 360f);
            int index = Mathf.RoundToInt(b / 22.5f) % 16;
            return Compass16[index];
        }

        /// <summary>A heading as a zero-padded 3-digit bearing like "045°". Wraps to [0, 360).</summary>
        public static string Degrees(float bearingDegrees)
        {
            int d = Mathf.RoundToInt(Mathf.Repeat(bearingDegrees, 360f)) % 360;
            return d.ToString("000", Inv) + "°";
        }

        /// <summary>8-point arrow glyph for where the bow points (shape redundant coding). Wraps input.</summary>
        public static string HeadingArrow(float bearingDegrees)
        {
            float b = Mathf.Repeat(bearingDegrees, 360f);
            int oct = Mathf.RoundToInt(b / 45f) % 8;
            return Arrows8[oct];
        }

        /// <summary>
        /// A flat compass "ribbon": a fixed-width tape centred on the heading, with single-char marks for
        /// the 8 rose points (N/E/S/W as letters, the intercardinals as '+') placed by bearing and '·'
        /// between. The boat's heading is the CENTRE column — pair it with a fixed centre needle and, as
        /// the bow turns, the rose scrolls past the needle (an aircraft-style heading tape). This is the
        /// SHAPE channel of the redundant read (cardinal word + degrees number + ribbon). <paramref
        /// name="width"/> should be odd (a true centre); <paramref name="halfWindowDegrees"/> is the span
        /// shown each side of the heading.
        /// </summary>
        public static string Ribbon(float bearingDegrees, int width = 21, float halfWindowDegrees = 90f)
        {
            if (width < 3) width = 3;
            if (halfWindowDegrees <= 0f) halfWindowDegrees = 90f;

            var tape = new char[width];
            for (int i = 0; i < width; i++) tape[i] = '·';

            float heading = Mathf.Repeat(bearingDegrees, 360f);
            for (int p = 0; p < 8; p++)
            {
                float pointBearing = p * 45f;
                char mark = (p % 2 == 0) ? "NESW"[p / 2] : '+'; // 0=N 2=E 4=S 6=W cardinals; odd = intercardinal
                float rel = BoatKinematics.RelativeBearingDegrees(heading, pointBearing); // [-180,180)
                if (rel < -halfWindowDegrees || rel > halfWindowDegrees) continue;        // outside the window
                int col = Mathf.RoundToInt((rel + halfWindowDegrees) / (2f * halfWindowDegrees) * (width - 1));
                if (col >= 0 && col < width) tape[col] = mark;
            }
            return new string(tape);
        }

        /// <summary>
        /// The set-&amp;-drift read: the boat's actual track (course-over-ground) vs where the bow points,
        /// so the player SEES the hull crabbing under wind/current. Redundant-coded — a side WORD
        /// (stbd/port) + a degrees NUMBER + a direction ARROW. Returns e.g. "COG 075°  → 30° stbd"
        /// (being set to starboard) / "COG 075°  ← 12° port" / "COG 075°  on track" when bow and track
        /// align / "COG —" when too slow to hold a course. Pure: heading + course + speed in, string out.
        /// </summary>
        public static string SetAndDrift(float headingDegrees, float courseOverGroundDegrees,
                                         float speedOverGround,
                                         float deadbandDegrees = OnTrackDeadbandDegrees)
        {
            if (speedOverGround < UnderwaySpeedMps) return NoCourse; // no steady course at rest

            string course = "COG " + Degrees(courseOverGroundDegrees);

            // Positive set = the track is clockwise of the heading → the boat is being set to STARBOARD
            // (the bow is crabbed to port of where it actually goes). Mirror of RelativeBearingDegrees.
            float set = BoatKinematics.RelativeBearingDegrees(headingDegrees, courseOverGroundDegrees);
            if (Mathf.Abs(set) <= deadbandDegrees) return course + "  " + OnTrack;

            bool starboard = set > 0f;
            string arrow = starboard ? SetStarboard : SetPort;
            string side  = starboard ? "stbd" : "port";
            int mag = Mathf.RoundToInt(Mathf.Abs(set));
            return course + "  " + arrow + " " + mag.ToString(Inv) + "° " + side;
        }
    }
}
