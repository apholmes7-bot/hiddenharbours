using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// A snapshot of the active boat's motion — where the bow points and where the hull is actually
    /// going — produced by the Boats lane and read through Core by anything that must reason about
    /// the boat WITHOUT referencing the Boats module (the HUD's VS-19 compass, set-&amp;-drift
    /// predictor, and apparent-wind read). Mirrors <see cref="EnvironmentSample"/>: a small,
    /// immutable value pulled on demand (see <see cref="IActiveBoatService"/>).
    ///
    /// <para><b>Heading vs course-over-ground.</b> <see cref="HeadingDegrees"/> is where the BOW
    /// points; <see cref="Velocity"/> is where the hull actually MOVES (course-over-ground), which
    /// includes the set &amp; drift of wind and tidal current. Their difference is the crabbing read
    /// at the heart of P1 navigation (ADR 0004/0006) — the boat can point one way and travel
    /// another. This is true for the hand-rowed dory exactly as for an engine boat: the bow direction
    /// is the hull's facing regardless of whether oars or a rudder turned it (ADR 0007).</para>
    ///
    /// <para><b>Bearing convention.</b> Degrees are a compass bearing: 0 = North (+Y), 90 = East
    /// (+X), clockwise, in [0, 360). This matches the wind widget's convention
    /// (HiddenHarbours.UI.WindReadout) so heading, wind, and course all read on one dial.</para>
    /// </summary>
    public readonly struct BoatKinematics
    {
        /// <summary>True when these values describe a real, controllable active boat. When false
        /// (on foot / no boat aboard) the other fields are zeroed and must not be read as a heading.</summary>
        public readonly bool HasBoat;

        /// <summary>Bow bearing in degrees, 0 = N, 90 = E, clockwise, [0, 360). Where the boat POINTS.</summary>
        public readonly float HeadingDegrees;

        /// <summary>Course-over-ground velocity in world space (m/s) — where the boat actually GOES,
        /// wind- and current-set included. Its bearing is <see cref="CourseOverGroundDegrees"/>.</summary>
        public readonly Vector2 Velocity;

        /// <summary>Speed over ground (m/s) — the magnitude of <see cref="Velocity"/>. Carried so the
        /// HUD never recomputes a square root each sample.</summary>
        public readonly float SpeedOverGround;

        public BoatKinematics(bool hasBoat, float headingDegrees, Vector2 velocity, float speedOverGround)
        {
            HasBoat = hasBoat;
            HeadingDegrees = headingDegrees;
            Velocity = velocity;
            SpeedOverGround = speedOverGround;
        }

        /// <summary>The "no active boat" snapshot (on foot / before boarding). Equivalent to default.</summary>
        public static readonly BoatKinematics None = default;

        /// <summary>
        /// Build a snapshot from the bow's forward vector and the hull's world velocity. The single
        /// place the producer turns engine vectors into the Core bearing/SOG contract, so the
        /// convention can't drift between producer and consumer.
        /// </summary>
        public static BoatKinematics FromBow(Vector2 bowForward, Vector2 velocity)
            => new BoatKinematics(true, BearingDegrees(bowForward), velocity, velocity.magnitude);

        /// <summary>
        /// Course-over-ground as a compass bearing (where the hull is actually travelling). Only
        /// meaningful when <see cref="SpeedOverGround"/> is above a small threshold — direction is
        /// undefined at a standstill (returns 0/North there). Gate on speed before drawing it.
        /// </summary>
        public float CourseOverGroundDegrees => BearingDegrees(Velocity);

        // ---- pure bearing math (engine-light, deterministic, shared by producer + consumer) ------

        /// <summary>
        /// Compass bearing of a direction vector: 0 = N (+Y), 90 = E (+X), clockwise, in [0, 360).
        /// A (near-)zero vector has no direction → returns 0 (North) as a defined fallback; callers
        /// that care should gate on magnitude first. Matches WindReadout's atan2(x, y) convention.
        /// </summary>
        public static float BearingDegrees(Vector2 direction)
        {
            if (direction.sqrMagnitude < 1e-12f) return 0f;
            float bearing = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg; // 0 at N, +90 at E
            if (bearing < 0f) bearing += 360f;
            return bearing;
        }

        /// <summary>
        /// Signed smallest angle FROM a reference bearing TO a target bearing, in [-180, 180)
        /// (dead astern resolves to -180). Positive = the target lies clockwise of the reference (to
        /// starboard when the reference is the boat's heading). The primitive the UI composes for
        /// apparent wind (wind vs heading) and for the set angle (course-over-ground vs heading).
        /// Both inputs are compass bearings.
        /// </summary>
        public static float RelativeBearingDegrees(float referenceDegrees, float targetDegrees)
            => Mathf.Repeat(targetDegrees - referenceDegrees + 180f, 360f) - 180f;
    }
}
