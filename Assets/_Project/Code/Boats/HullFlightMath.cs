using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>What a flight step reports. At most one event per step.</summary>
    public enum HullFlightEvent
    {
        None = 0,
        /// <summary>Her keel has been clear of the water for the settings' minimum time.</summary>
        Launched = 1,
        /// <summary>She came back down after a counted flight.</summary>
        Landed = 2,
    }

    /// <summary>The flight law's state: one hull's height and vertical speed, and whether she is in the
    /// air. Owned by the caller (the boat's physics tick keeps one), reset to <c>default</c> to put her
    /// back on the water.</summary>
    public struct HullFlightState
    {
        /// <summary>Her height (m) in the frame the surface is measured in.</summary>
        public float RideMeters;
        public float VelocityMetersPerSec;
        public float PrevSurfaceMeters;
        public bool Primed;
        /// <summary>Her keel is clear of the water this step (counted or not).</summary>
        public bool KeelClear;
        /// <summary>She is in a counted flight.</summary>
        public bool Airborne;
        /// <summary>Seconds her keel has been clear.</summary>
        public float AirSeconds;
        /// <summary>The most air (m) under her keel this flight.</summary>
        public float PeakClearanceMeters;
    }

    /// <summary>One step's answer.</summary>
    public readonly struct HullFlightStep
    {
        public readonly HullFlightEvent Event;
        /// <summary>Metres of air under her keel now (0 while she is in the water).</summary>
        public readonly float ClearanceMeters;
        /// <summary>On <see cref="HullFlightEvent.Landed"/>: how hard, 0..1.</summary>
        public readonly float Impact01;
        /// <summary>On landing: the speed (m/s) she and the water closed at.</summary>
        public readonly float ClosingSpeed;
        /// <summary>On landing: seconds the flight lasted.</summary>
        public readonly float AirSeconds;
        /// <summary>On landing: the most air (m) she had under her keel.</summary>
        public readonly float PeakClearanceMeters;

        public HullFlightStep(HullFlightEvent e, float clearance, float impact01, float closing,
                              float airSeconds, float peak)
        {
            Event = e; ClearanceMeters = clearance; Impact01 = impact01; ClosingSpeed = closing;
            AirSeconds = airSeconds; PeakClearanceMeters = peak;
        }
    }

    /// <summary>
    /// <b>WHEN A HULL LEAVES THE WATER, AND HOW HARD SHE COMES BACK</b> (owner, 2026-11-12). Pure,
    /// static, allocation-free; deterministic for a given sequence of (surface, dt), which on the
    /// physics tick is a function of the world seed, the game time and where the hull is.
    ///
    /// <para><b>The law.</b> She is a mass on the spring of her own waterplane
    /// (<see cref="HullHeaveResponseMath"/> gives its frequency and damping from her length, beam and
    /// draught). Going faster for her length the water pushes her up harder, so the spring stiffens with
    /// her Froude number (<see cref="PlaningStiffness"/>). The water can push her up without limit; it
    /// can pull her down no faster than gravity; and once her keel is clear nothing acts on her but
    /// gravity. So a slow hull follows the surface, and a fast one that meets a steep face is thrown up
    /// faster than the back of the wave falls away. No threshold says "fly": she flies when her keel is
    /// out of the water.</para>
    ///
    /// <para>The same idea as <see cref="StormRockMath.StepHeaveWeight"/>, which draws the hull's ride.
    /// That one is presentation and runs on frames; this one runs on the physics tick because the slam
    /// takes speed off the boat, and a frame-timed chase may not feed the simulation.</para>
    /// </summary>
    public static class HullFlightMath
    {
        /// <summary>Integrator sub-step ceiling (s). A guard, as in <see cref="StormRockMath"/>.</summary>
        public const float MaxSubStepSeconds = 0.02f;
        /// <summary>Ceiling on sub-steps per call. A guard.</summary>
        public const int MaxSubSteps = 16;
        /// <summary>Points along her length the caller means the surface over (see
        /// <see cref="FootprintOffset"/>). A wave shorter than the hull cancels under her.</summary>
        public const int FootprintPoints = 5;
        /// <summary>Share of her length those points span.</summary>
        public const float FootprintSpan = 0.8f;

        /// <summary>Metres along her keel from her middle to footprint point <paramref name="i"/>.</summary>
        public static float FootprintOffset(int i, float lengthMeters)
            => ((float)i / (FootprintPoints - 1) - 0.5f) * FootprintSpan * lengthMeters;
        /// <summary>Ceiling on the surface's finite-difference rate (m/s): beyond it is a teleport, not
        /// a wave. A guard.</summary>
        public const float MaxSurfaceRateMetersPerSec = 20f;

        /// <summary>Her speed for her length: <c>U / sqrt(g L)</c>. 0 for a hull with no length.</summary>
        public static float FroudeNumber(float speedMetersPerSec, float lengthMeters, float gravity)
        {
            if (!(lengthMeters > 0f) || !(gravity > 0f)) return 0f;
            return Mathf.Max(0f, speedMetersPerSec) / Mathf.Sqrt(gravity * lengthMeters);
        }

        /// <summary>How many times stiffer than at rest the water holds her up at this speed: 1 at
        /// rest, rising with her Froude number, capped.</summary>
        public static float PlaningStiffness(float froude, in HullFlightSettings cfg)
        {
            float k = 1f + Mathf.Max(0f, cfg.PlaningStiffnessPerFroude) * Mathf.Max(0f, froude);
            return Mathf.Min(k, Mathf.Max(1f, cfg.MaxPlaningStiffness));
        }

        /// <summary>How hard a landing is, 0..1, from the speed she and the water closed at.</summary>
        public static float Impact01(float closingSpeed, in HullFlightSettings cfg)
        {
            float knee = Mathf.Max(0f, cfg.SlamSpeedKnee);
            float full = Mathf.Max(knee + 0.01f, cfg.SlamFullSpeed);
            return Mathf.Clamp01((closingSpeed - knee) / (full - knee));
        }

        /// <summary>The share of her speed she keeps through a landing this hard.</summary>
        public static float SpeedKept01(float impact01, in HullFlightSettings cfg)
            => 1f - Mathf.Clamp(cfg.SpeedShedAtFullSlam, 0f, 0.9f) * Mathf.Clamp01(impact01);

        /// <summary>Degrees her bow lifts with this much air under her keel.</summary>
        public static float NoseUpDegrees(float clearanceMeters, in HullFlightSettings cfg)
            => Mathf.Min(Mathf.Max(0f, clearanceMeters) * Mathf.Max(0f, cfg.NoseUpDegreesPerMetre),
                         Mathf.Max(0f, cfg.MaxNoseUpDegrees));

        /// <summary>
        /// One step. <paramref name="surfaceMeters"/> is the water's height under her now (the mean
        /// over <see cref="FootprintPoints"/> points of her length); <paramref name="restOmega"/> and <paramref name="dampingRatio"/> are her
        /// own heave spring at rest; <paramref name="speedMetersPerSec"/> is her speed through the water.
        /// </summary>
        public static HullFlightStep Step(ref HullFlightState s, float surfaceMeters, float dt, float gravity,
                                          float restOmega, float dampingRatio, float draughtMeters,
                                          float lengthMeters, float speedMetersPerSec,
                                          in HullFlightSettings cfg)
        {
            if (!cfg.Enabled || !(draughtMeters > 0f) || !(restOmega > 0f) || !(gravity > 0f))
            {
                s = default;
                return default;
            }
            if (!s.Primed)
            {
                s = default;
                s.RideMeters = surfaceMeters;   // she wakes ON the water
                s.PrevSurfaceMeters = surfaceMeters;
                s.Primed = true;
            }

            float surfaceRate = 0f;
            if (dt > 0f)
                surfaceRate = Mathf.Clamp((surfaceMeters - s.PrevSurfaceMeters) / dt,
                                          -MaxSurfaceRateMetersPerSec, MaxSurfaceRateMetersPerSec);
            s.PrevSurfaceMeters = surfaceMeters;

            float clear = Mathf.Max(0.01f, cfg.KeelClearFraction) * draughtMeters;
            float zeta = Mathf.Max(Mathf.Max(0f, dampingRatio), cfg.MinDampingRatio);
            float bury = Mathf.Max(1f, cfg.MaxBuryDraughts) * draughtMeters;
            // The fastest the water can lift her: a face steeper than she can follow she punches through.
            float maxRise = Mathf.Max(0f, cfg.FollowRiseBase)
                          + Mathf.Max(0f, cfg.MaxFollowSlope) * Mathf.Max(0f, speedMetersPerSec);
            float stiff = PlaningStiffness(FroudeNumber(speedMetersPerSec, lengthMeters, gravity), in cfg);
            float x = s.RideMeters, v = s.VelocityMetersPerSec;

            float remaining = Mathf.Min(Mathf.Max(0f, dt), MaxSubSteps * MaxSubStepSeconds);
            while (remaining > 0f)
            {
                float h = Mathf.Min(remaining, MaxSubStepSeconds);
                remaining -= h;
                float omega = restOmega * stiff;
                if (x - surfaceMeters >= clear)
                {
                    v -= gravity * h;                       // keel out: nothing but gravity
                }
                else
                {
                    // The spring and its damper, solved implicitly: stable at any stiffness.
                    float next = (v + h * (omega * omega * (surfaceMeters - x) + 2f * zeta * omega * surfaceRate))
                               / (1f + 2f * zeta * omega * h + omega * omega * h * h);
                    if (next < v - gravity * h) next = v - gravity * h;   // the water never pulls her down
                    if (next > maxRise) next = Mathf.Max(v, maxRise);     // ...and lifts her only so fast
                    v = next;
                }
                x += v * h;
            }

            // Pushed under as far as she goes, the water carries her bodily.
            if (x < surfaceMeters - bury)
            {
                x = surfaceMeters - bury;
                if (v < surfaceRate) v = Mathf.Min(surfaceRate, maxRise);
            }

            float clearance = x - surfaceMeters - clear;
            HullFlightEvent e = HullFlightEvent.None;
            float impact = 0f, closing = 0f, airSeconds = 0f, peak = 0f;
            if (clearance > 0f)
            {
                if (!s.KeelClear) { s.KeelClear = true; s.AirSeconds = 0f; s.PeakClearanceMeters = 0f; }
                s.AirSeconds += Mathf.Max(0f, dt);
                if (clearance > s.PeakClearanceMeters) s.PeakClearanceMeters = clearance;
                if (!s.Airborne && s.AirSeconds >= Mathf.Max(0f, cfg.MinAirSeconds))
                {
                    s.Airborne = true;
                    e = HullFlightEvent.Launched;
                }
            }
            else
            {
                if (s.Airborne)
                {
                    closing = Mathf.Max(0f, surfaceRate - v);
                    impact = Impact01(closing, in cfg);
                    airSeconds = s.AirSeconds;
                    peak = s.PeakClearanceMeters;
                    e = HullFlightEvent.Landed;
                }
                s.KeelClear = false;
                s.Airborne = false;
                clearance = 0f;
            }

            s.RideMeters = x;
            s.VelocityMetersPerSec = v;
            return new HullFlightStep(e, s.Airborne ? clearance : 0f, impact, closing, airSeconds, peak);
        }
    }
}
