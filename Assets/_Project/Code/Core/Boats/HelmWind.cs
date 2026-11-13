using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// What a helm's wind instrument reports, in the sails' own conventions (<see cref="SailWind"/>):
    /// angles are signed, ±180, off the bow, positive = wind over the STARBOARD bow; speeds are knots.
    ///
    /// <para><b>Apparent is always present; true is graded.</b> The analog dial
    /// (<see cref="WindInstrumentGrade.Dial"/>) measures only the wind over the deck, so
    /// <see cref="HasTrue"/> is false and the true fields are zero. They are zeroed rather than left
    /// holding the real numbers so that a consumer which forgets to ask <see cref="HasTrue"/> draws an
    /// obviously empty value instead of quietly showing a reading the instrument does not have.</para>
    ///
    /// <para>A pure value, recomputed on every read from the deterministic weather and the hull's own
    /// motion; never saved (rule 5).</para>
    /// </summary>
    public readonly struct HelmWindReading
    {
        /// <summary>Apparent wind angle off the bow, signed, ±180: true wind with the boat's own
        /// velocity taken out. What a masthead vane points at.</summary>
        public readonly float ApparentAngleDeg;

        /// <summary>Apparent wind speed, knots.</summary>
        public readonly float ApparentKn;

        /// <summary>True when the fitted instrument also reports true wind
        /// (<see cref="WindInstrumentGrade.Display"/> and above).</summary>
        public readonly bool HasTrue;

        /// <summary>True wind angle off the bow, signed, ±180. Zero unless <see cref="HasTrue"/>.</summary>
        public readonly float TrueAngleDeg;

        /// <summary>True wind speed, knots. Zero unless <see cref="HasTrue"/>.</summary>
        public readonly float TrueKn;

        public HelmWindReading(float apparentAngleDeg, float apparentKn, bool hasTrue,
                               float trueAngleDeg, float trueKn)
        {
            ApparentAngleDeg = apparentAngleDeg;
            ApparentKn = apparentKn;
            HasTrue = hasTrue;
            TrueAngleDeg = trueAngleDeg;
            TrueKn = trueKn;
        }
    }

    /// <summary>
    /// <b>The helm wind instrument's one read</b> (helm instruments plan, slice 3): the sails' wind
    /// resolve behind the fitted grade. Pure and static, no clock, no RNG, no state and no allocation,
    /// so the Boats-lane relay and an EditMode test cannot compute different winds.
    ///
    /// <para><b>There is still one wind in this game.</b> The numbers are
    /// <see cref="SailDrive.ResolveWind"/>'s, unchanged: this file adds the question "is this
    /// instrument allowed to say it?", never a second way of working the wind out. That is what closes
    /// bug K1 (a HUD line that called true wind off the bow "apparent"): a needle fed from here and the
    /// sails cannot disagree, because they are the same call.</para>
    /// </summary>
    public static class HelmWind
    {
        /// <summary>Does this grade report true wind as well as apparent?</summary>
        public static bool ReportsTrueWind(WindInstrumentGrade grade) => grade >= WindInstrumentGrade.Display;

        /// <summary>
        /// Read the wind through an instrument of the given grade.
        /// </summary>
        /// <param name="grade">The fitted wind instrument (<see cref="HelmFit.Wind"/>).</param>
        /// <param name="trueWindMs">The sim's wind vector, m/s, pointing DOWNWIND
        /// (<c>EnvironmentSample.WindVector</c>).</param>
        /// <param name="bowForward">The boat's heading (need not be normalised).</param>
        /// <param name="boatVelocityMs">The hull's velocity over the ground, m/s. Zero at rest, where
        /// apparent wind equals true wind.</param>
        /// <param name="reading">The reading; zeroed when this returns false.</param>
        /// <returns>False, meaning "show nothing", when no wind instrument is fitted
        /// (<see cref="WindInstrumentGrade.None"/>) or an input is not a finite number. A flat calm is
        /// a valid reading: zero knots, with the angle at its defined 0 fallback, so gate a needle on
        /// the speed.</returns>
        public static bool TryRead(WindInstrumentGrade grade, Vector2 trueWindMs, Vector2 bowForward,
                                   Vector2 boatVelocityMs, out HelmWindReading reading)
        {
            reading = default;
            if (grade <= WindInstrumentGrade.None) return false;
            if (!Finite(trueWindMs) || !Finite(bowForward) || !Finite(boatVelocityMs)) return false;

            SailWind wind = SailDrive.ResolveWind(trueWindMs, bowForward, boatVelocityMs);
            reading = ReportsTrueWind(grade)
                ? new HelmWindReading(wind.ApparentAngleDeg, wind.ApparentKn, true, wind.TrueAngleDeg, wind.TrueKn)
                : new HelmWindReading(wind.ApparentAngleDeg, wind.ApparentKn, false, 0f, 0f);
            return true;
        }

        private static bool Finite(Vector2 v)
            => !float.IsNaN(v.x) && !float.IsInfinity(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.y);
    }
}
