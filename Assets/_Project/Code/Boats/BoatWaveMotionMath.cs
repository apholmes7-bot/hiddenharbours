using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The wave motion a hull reads at one instant, decomposed against its own heading — what
    /// <see cref="BoatWaveMotion"/> turns into the visual rock (ADR 0018 B2). Raw, unscaled sim
    /// units: the component's tunables map these onto degrees/pixels.
    /// </summary>
    public readonly struct BoatWaveMotionSample
    {
        /// <summary>Wave slope along the hull's HEADING (bow axis) — the head-sea read. Positive =
        /// the surface rises ahead of the bow (bow riding UP the face), negative = bow dropping into
        /// the trough. Zero on a pure beam sea. Units: surface gradient (m height per m travelled).</summary>
        public readonly float Pitch;

        /// <summary>Wave slope along the hull's STARBOARD axis — the beam-sea read. Positive = the
        /// surface is higher to starboard (deck tilts starboard-up), negative = higher to port.
        /// Zero on a pure head sea. Units: surface gradient.</summary>
        public readonly float Roll;

        /// <summary>Surface height under the hull (metres about the tide level) — the whole-boat
        /// lift on a crest / drop into a trough.</summary>
        public readonly float Bob;

        public BoatWaveMotionSample(float pitch, float roll, float bob)
        {
            Pitch = pitch;
            Roll = roll;
            Bob = bob;
        }

        /// <summary>Dead still (glass, or motion switched off). Equivalent to <c>default</c>.</summary>
        public static readonly BoatWaveMotionSample None = default;
    }

    /// <summary>
    /// Pure decomposition of a <c>WaveMath</c> sample against a hull heading (ADR 0018 B2) — the
    /// owner's ask, verbatim: <i>"a wave to the beam needs to rock the vessel, sailing through the
    /// waves to the bow rocks the bow and stern."</i> Projecting the sampled surface gradient onto
    /// the hull's own axes delivers exactly that by construction: a beam sea (slope perpendicular to
    /// the heading) lands entirely in <see cref="BoatWaveMotionSample.Roll"/>, a head sea (slope
    /// along the heading) entirely in <see cref="BoatWaveMotionSample.Pitch"/>, a quartering sea in
    /// both — and the split retargets live as the PLAYER TURNS, which is the point of the mechanic.
    ///
    /// <para>Static, stateless, allocation-free, deterministic — EditMode-testable headless
    /// (<c>BoatWaveMotionMathTests</c>), same discipline as <c>WaveMath</c> itself.</para>
    /// </summary>
    public static class BoatWaveMotionMath
    {
        /// <summary>Below this squared magnitude a heading vector is treated as undefined and falls
        /// back to +Y (north) — the same defined-fallback convention as
        /// <see cref="DirectionalBoatSprite.HeadingDegreesFromBow"/> and <c>WaveTrain</c>. Never NaN.</summary>
        public const float MinHeadingSqrMagnitude = 1e-12f;

        /// <summary>
        /// The hull's starboard direction for a heading: the heading rotated 90° CLOCKWISE
        /// (<c>(x,y) → (y,−x)</c>). Heading north (+Y) → starboard east (+X). Same length as the
        /// input (unit in → unit out).
        /// </summary>
        public static Vector2 Starboard(Vector2 heading) => new Vector2(heading.y, -heading.x);

        /// <summary>
        /// Decompose a wave sample against the hull. <paramref name="slope"/> and
        /// <paramref name="height"/> come from <c>WaveMath.Sample</c> at the hull's position
        /// (<c>Slope</c>/<c>Height</c>); <paramref name="heading"/> is the physics root's bow axis
        /// (<c>transform.up</c>), normalized here (near-zero → +Y, never NaN);
        /// <paramref name="strength"/> is the master scale — <b>0 = identically zero motion</b>
        /// (the off switch), 1 = the raw read, clamped ≥ 0. Pure and deterministic: same inputs,
        /// same result, forever.
        /// </summary>
        public static BoatWaveMotionSample Decompose(Vector2 slope, float height, Vector2 heading, float strength = 1f)
        {
            float s = Mathf.Max(0f, strength);
            if (s <= 0f) return BoatWaveMotionSample.None;

            float sqrMagnitude = heading.x * heading.x + heading.y * heading.y;
            Vector2 bow;
            if (sqrMagnitude < MinHeadingSqrMagnitude)
            {
                bow = Vector2.up;
            }
            else
            {
                float invMagnitude = 1f / Mathf.Sqrt(sqrMagnitude);
                bow = new Vector2(heading.x * invMagnitude, heading.y * invMagnitude);
            }
            Vector2 starboard = Starboard(bow);

            float pitch = (slope.x * bow.x + slope.y * bow.y) * s;
            float roll = (slope.x * starboard.x + slope.y * starboard.y) * s;
            float bob = height * s;
            return new BoatWaveMotionSample(pitch, roll, bob);
        }
    }
}
