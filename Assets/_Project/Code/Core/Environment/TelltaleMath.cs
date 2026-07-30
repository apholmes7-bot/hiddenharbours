using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>What a pennant is doing right now: which way it streams, and how hard.</summary>
    public readonly struct TelltaleState
    {
        /// <summary>Bearing the pennant STREAMS toward, degrees clockwise from North — i.e. where the
        /// apparent wind is going, not where it came from. A flag points downwind.</summary>
        public readonly float StreamBearingDegrees;

        /// <summary>Apparent wind speed at the masthead, m/s — true wind less the boat's own motion.</summary>
        public readonly float ApparentSpeed;

        /// <summary>How hard it is streaming, 0 (hanging dead) .. 1 (board-stiff and snapping).</summary>
        public readonly float Stiffness01;

        public TelltaleState(float streamBearingDegrees, float apparentSpeed, float stiffness01)
        {
            StreamBearingDegrees = streamBearingDegrees;
            ApparentSpeed = apparentSpeed;
            Stiffness01 = stiffness01;
        }

        /// <summary>Below the dead zone there is no honest direction to point — the pennant hangs.</summary>
        public bool IsLimp => Stiffness01 <= 0f;
    }

    /// <summary>
    /// The maths behind a masthead pennant — the oldest wind instrument there is, and the one that
    /// answers "what is the wind doing" without a single pixel of HUD (VS-19).
    ///
    /// <para><b>Apparent, not true.</b> A pennant on a moving boat streams with the wind the BOAT feels:
    /// true wind minus the boat's own velocity. That is why motoring hard into a flat calm still makes
    /// the burgee stream astern, and why bearing away in a blow makes it go soft. Reading apparent wind
    /// is the skill — it is what tells you how the boat is actually being pushed — so the instrument
    /// shows apparent and the numbers elsewhere stay true.</para>
    ///
    /// <para><b>Deterministic and engine-light.</b> A pure function of <c>(trueWind, boatVelocity)</c>
    /// plus named tunables — no clock read, no RNG, no state — so it is EditMode-testable and cannot
    /// drift between the presenter and a test. Vector2 only; nothing here touches a Transform.</para>
    /// </summary>
    public static class TelltaleMath
    {
        /// <summary>Apparent wind at the masthead: what the boat feels, in m/s.</summary>
        public static Vector2 ApparentWind(Vector2 trueWind, Vector2 boatVelocity)
            => trueWind - boatVelocity;

        /// <summary>
        /// Resolve the pennant's state.
        /// </summary>
        /// <param name="trueWind">The sim's wind vector (direction × strength, m/s).</param>
        /// <param name="boatVelocity">The boat's course-over-ground velocity, m/s.</param>
        /// <param name="settings">Owner tunables — the dead zone and the speed that reads as stiff.</param>
        public static TelltaleState Resolve(Vector2 trueWind, Vector2 boatVelocity,
                                            in TelltaleSettings settings)
        {
            Vector2 apparent = ApparentWind(trueWind, boatVelocity);
            float speed = apparent.magnitude;

            // Under the dead zone the direction is numerical noise. Hang the pennant and say nothing
            // rather than have it spin — a limp burgee IS the correct read for a calm.
            if (speed <= settings.LimpBelowMetresPerSecond || speed <= 0f)
                return new TelltaleState(0f, speed, 0f);

            float stiff = settings.StiffAtMetresPerSecond > settings.LimpBelowMetresPerSecond
                ? Mathf.InverseLerp(settings.LimpBelowMetresPerSecond,
                                    settings.StiffAtMetresPerSecond, speed)
                : 1f;

            return new TelltaleState(BoatKinematics.BearingDegrees(apparent), speed, Mathf.Clamp01(stiff));
        }
    }

    /// <summary>
    /// Owner tunables for the masthead pennant (rule 6): where it gives up and hangs, and where it is
    /// flying board-stiff. Between the two it reads proportionally, so the player learns the boat's
    /// wind by the burgee's shape long before they read a number.
    /// </summary>
    [System.Serializable]
    public struct TelltaleSettings
    {
        [Tooltip("Apparent wind (m/s) at or below which the pennant simply hangs. A calm has no " +
                 "direction worth pointing at, and a burgee that spins in still air is noise, not a read.")]
        [Min(0f)] public float LimpBelowMetresPerSecond;

        [Tooltip("Apparent wind (m/s) at which the pennant is fully extended and snapping. Above this " +
                 "it cannot look any stiffer — the sea state and the sound carry the rest.")]
        [Min(0.01f)] public float StiffAtMetresPerSecond;

        [Tooltip("How quickly the pennant chases a change in the wind (higher = twitchier). Purely " +
                 "presentation smoothing — the underlying read is instantaneous and deterministic.")]
        [Min(0.01f)] public float ResponsePerSecond;

        /// <summary>
        /// The shipped feel. Limp under ~0.6 m/s (a dead calm); board-stiff by ~10 m/s, which is a
        /// fresh breeze — around Beaufort 5, the point at which the cove stops being comfortable. The
        /// response is quick enough to see a gust arrive but slow enough not to jitter.
        /// </summary>
        public static TelltaleSettings Default => new TelltaleSettings
        {
            LimpBelowMetresPerSecond = 0.6f,
            StiffAtMetresPerSecond = 10f,
            ResponsePerSecond = 6f,
        };
    }
}
