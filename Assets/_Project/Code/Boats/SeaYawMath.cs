using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The pure law of the <b>sea yaw</b>: how hard the wind and the waves TURN a hull's head (P1, the sea
    /// has moods). Engine-light and side-effect free so it is unit-testable and deterministic: every input
    /// is the sim's own wind vector and wave samples, which are functions of (worldSeed, gameTime), and
    /// nothing here draws a random number.
    ///
    /// <para><b>The unit is a share of her own helm.</b> Each term returns a fraction of the hull's FULL
    /// helm torque (<see cref="FullHelmTorque"/>), positive = counter-clockwise (bow to port, the Box2D
    /// sign). <see cref="Combine"/> caps the sum at <see cref="SeaYawSettings.MaxHelmShare"/>, so with way
    /// on the rudder always has more authority than the sea asks of it.</para>
    ///
    /// <para><b>Why a new term and not the seakeeping slew.</b> The seakeeping yaw
    /// (<see cref="SeakeepingForcesMath.Resolve"/>) reads the sea at the hull's middle only, so on a
    /// passing wave it swings both ways and averages out; and the wind force is applied at the centre of
    /// mass, so it has no lever at all. A hull turns because the sea is different under her bow and under
    /// her stern, and because her windage is not centred on her pivot. Those are the two terms here.</para>
    /// </summary>
    public static class SeaYawMath
    {
        /// <summary>The smallest wind span the wind ramp divides by, so a mis-set calm/reference pair
        /// (reference at or under calm) cannot divide by zero.</summary>
        public const float MinWindSpanMetresPerSecond = 0.01f;

        /// <summary>
        /// The torque her helm gives at its best, in the design units the drive applies (before the feel
        /// scale): the rudder at full authority for a sailing or engine hull; for a rowed hull, one oar at
        /// full pull with the other idle (the steering stroke a rower keeps while still making way, so the
        /// rudder-wins promise holds without asking him to back an oar). Zero for no hull.
        /// </summary>
        public static float FullHelmTorque(BoatHullDef hull)
        {
            if (hull == null) return 0f;
            if (hull.HasSailPlan || BoatController.UsesEngineHelm(hull.Propulsion))
                return Mathf.Max(0f, hull.RudderAuthority);
            return Mathf.Abs(BoatController.OarYawTorque(1f, 0f, hull.OarPower, hull.OarLateralOffset));
        }

        /// <summary>
        /// How readily the wind turns this hull compared with the reference hull: more windage turns her
        /// more, more mass turns her less (by the settings' exponent), capped so nothing is thrown about.
        /// </summary>
        public static float WindSusceptibility(float windExposure, float massKg, in SeaYawSettings s)
        {
            if (windExposure <= 0f || massKg <= 0f) return 0f;
            float exposure = windExposure / Mathf.Max(0.01f, s.ReferenceWindExposure);
            float mass = Mathf.Pow(Mathf.Max(1f, s.ReferenceMassKg) / massKg, Mathf.Max(0f, s.MassExponent));
            return Mathf.Clamp(exposure * mass, 0f, Mathf.Max(0f, s.MaxSusceptibility));
        }

        /// <summary>
        /// How readily the waves turn this hull: her seakeeping response (liveliness over mass factor, the
        /// same number the swell force uses, so a hull that corks about is also turned about), capped.
        /// </summary>
        public static float WaveSusceptibility(in SeakeepingResponse response, in SeaYawSettings s)
            => Mathf.Clamp(response.Response, 0f, Mathf.Max(0f, s.MaxSusceptibility));

        /// <summary>
        /// Which way, and how hard, the wind turns her this tick. +1 = bow falls off downwind, negative =
        /// she comes up into it:
        /// <list type="bullet">
        /// <item>anchor down → <see cref="SeaYawSettings.AnchoredWindYawBias"/> (the rode holds her bow
        /// to the wind);</item>
        /// <item>an unmanned hull with a sail plan → 0, because the sail drive's own unmanned round-up
        /// already owns what a sailing hull left to herself does;</item>
        /// <item>otherwise the hull's fall-off, less her weather helm while her sails draw.</item>
        /// </list>
        /// </summary>
        public static float WindYawBias(bool anchored, bool sailPlan, bool unmanned, bool sailsDrawing,
                                        float sailWeatherHelm, in SeaYawSettings s)
        {
            if (anchored) return s.AnchoredWindYawBias;
            if (sailPlan && unmanned) return 0f;
            float bias = s.HullWindYawBias;
            if (sailPlan && sailsDrawing) bias -= Mathf.Max(0f, sailWeatherHelm);
            return bias;
        }

        /// <summary>
        /// The wind's share of her helm. Zero at and under the calm threshold, growing linearly to the
        /// reference wind (and on past it, so a gale is felt more than a breeze; <see cref="Combine"/>
        /// caps the total). Largest with the wind on her beam, zero with it dead ahead or astern, and it
        /// turns her towards the bias's end: with a positive bias the bow swings downwind.
        /// </summary>
        public static float WindShare(Vector2 windVector, Vector2 heading, float bias, float susceptibility,
                                      in SeaYawSettings s)
        {
            float speed = windVector.magnitude;
            float over = speed - Mathf.Max(0f, s.CalmWindMetresPerSecond);
            if (over <= 0f || susceptibility <= 0f || bias == 0f) return 0f;
            if (heading.sqrMagnitude < SeakeepingForcesMath.MinHeadingSqrMagnitude) return 0f;

            Vector2 bow = heading.normalized;
            Vector2 downwind = windVector / speed;
            // z of bow × downwind: positive when downwind lies to port of the bow, so a positive share
            // (counter-clockwise) swings the bow towards downwind.
            float cross = bow.x * downwind.y - bow.y * downwind.x;
            float span = Mathf.Max(MinWindSpanMetresPerSecond,
                                   s.ReferenceWindMetresPerSecond - Mathf.Max(0f, s.CalmWindMetresPerSecond));
            return s.WindHelmShare * (over / span) * bias * susceptibility * cross;
        }

        /// <summary>
        /// How far either side of her middle the bow and stern are read: half her length, or a fraction of
        /// the dominant wavelength if that is shorter (a hull longer than the waves bridges them). Zero
        /// with no waves.
        /// </summary>
        public static float WaveHalfSpan(float lengthMeters, float dominantWavelength, in SeaYawSettings s)
        {
            if (lengthMeters <= 0f || dominantWavelength <= 0f) return 0f;
            return Mathf.Min(lengthMeters * 0.5f, Mathf.Max(0f, s.WaveSpanWavelengthFraction) * dominantWavelength);
        }

        /// <summary>
        /// The waves' share of her helm, from the beam slope of the sea under her bow and under her stern.
        /// The slope points uphill; the water pushes her downhill, so a bow on a slope rising to starboard
        /// is pushed to port (counter-clockwise) and a stern on the same slope swings her the other way.
        /// On a beam sea both ends feel the same slope and the turn cancels; on a quartering or following
        /// sea they differ, and a hull meeting the waves slowly (running before them) feels the difference
        /// for long enough to wander.
        /// </summary>
        public static float WaveShare(Vector2 bowSlope, Vector2 sternSlope, Vector2 heading, float susceptibility,
                                      in SeaYawSettings s)
        {
            if (susceptibility <= 0f) return 0f;
            if (heading.sqrMagnitude < SeakeepingForcesMath.MinHeadingSqrMagnitude) return 0f;
            Vector2 bow = heading.normalized;
            Vector2 starboard = new Vector2(bow.y, -bow.x);
            float difference = Vector2.Dot(bowSlope, starboard) - Vector2.Dot(sternSlope, starboard);
            return s.WaveHelmShare * (difference / Mathf.Max(0.0001f, s.ReferenceWaveSlope)) * susceptibility;
        }

        /// <summary>
        /// The two shares together, faded by her exposure (sheltered water turns nothing) and capped at
        /// <see cref="SeaYawSettings.MaxHelmShare"/> so the rudder always has the last word. Zero when the
        /// master switch is off.
        /// </summary>
        public static float Combine(float windShare, float waveShare, float exposure01, in SeaYawSettings s)
        {
            if (!s.Enabled) return 0f;
            float cap = Mathf.Clamp01(s.MaxHelmShare);
            return Mathf.Clamp((windShare + waveShare) * Mathf.Clamp01(exposure01), -cap, cap);
        }

        /// <summary>The torque a share asks for, in Rigidbody2D units (her full helm times the feel scale
        /// the drive applies to the rudder).</summary>
        public static float Torque(float share, float fullHelmTorque, float feelScale)
            => share * fullHelmTorque * feelScale;
    }
}
