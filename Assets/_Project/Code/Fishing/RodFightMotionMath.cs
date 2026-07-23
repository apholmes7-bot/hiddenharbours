using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The PURE maths of <b>how a hooked fish moves on screen</b> during the surface fight (Rod Fishing
    /// v2, design §3/§5) — the generator the mouse-steer reads against. Each authored
    /// <see cref="RodFightMovement"/> becomes a distinct on-screen character:
    ///
    /// <list type="bullet">
    ///   <item><b>Darter</b> — short, sharp direction flips: she snaps to a fresh point on the roam disc
    ///   every <see cref="DarterSegmentSeconds"/>, covering the hop in the first
    ///   <see cref="DarterMoveFraction"/> of the segment (a lunge, then a hover). Hard to counter-steer
    ///   because the answer keeps changing.</item>
    ///   <item><b>Bulldog</b> — digs in and won't travel: the same lunge machinery, but on a shrunken
    ///   disc (<see cref="BulldogRadiusFraction"/>) with long, dogged segments
    ///   (<see cref="BulldogSegmentSeconds"/>). The reluctant RISE half of a bulldogger lives in the
    ///   authored <c>surfaceThreshold01</c> (she stays in the Deep phase longer), not here.</item>
    ///   <item><b>Circler</b> — one long sweeping arc around the entry point
    ///   (<see cref="CirclerRevSeconds"/> per lap, hash-picked handedness). The steer answer rotates
    ///   steadily — readable, but it never stops moving.</item>
    ///   <item><b>Thrasher</b> — rapid side-to-side head-thrashing across the anchor
    ///   (<see cref="ThrasherPeriodSeconds"/> per full swing). The dart flips twice a swing.</item>
    /// </list>
    ///
    /// <para><b>Pure and deterministic by construction</b> (the <see cref="RodFightMath"/> /
    /// <see cref="DepthDropMath"/> lane discipline): every read is a closed-form function of
    /// <c>(seed, secondsInSurfaceFight)</c> — no RNG object, no integration, no state — hashing through
    /// the process-stable <see cref="StableHash"/>, so a seeded fight replays its choreography
    /// bit-for-bit in an EditMode test, and a paused/resumed caller can't drift. NaN-safe throughout.</para>
    ///
    /// <para><b>These constants are art direction, not balance</b> (the <c>RodLineMath.FirstTapPhase</c>
    /// precedent): they shape how each personality READS on screen. The balance axis — how much a good
    /// or bad steer matters — lives entirely in the Def's <c>counterSteerRelief</c> and the alignment
    /// value <see cref="SteerAlignment"/> hands the fight maths. The roam RADIUS (world metres) is the
    /// caller's tunable — a serialized dial on the controller, per rule 6.</para>
    ///
    /// <para><b>The ¾-iso squash.</b> Vertical (screen-Y) excursions are foreshortened by
    /// <see cref="IsoYSquash"/> so a circling fish sweeps an ellipse that sits ON the water plane
    /// instead of a screen-space circle standing upright in it.</para>
    /// </summary>
    public static class RodFightMotionMath
    {
        // ---- pattern shapes (art direction — see the class note) --------------------------------

        /// <summary>Seconds per darter segment: one snap-and-hover per segment.</summary>
        public const float DarterSegmentSeconds = 0.7f;

        /// <summary>Fraction of a darter segment spent actually crossing to the new point — small, so
        /// the hop reads as a SNAP (sharp direction change), then she hovers.</summary>
        public const float DarterMoveFraction = 0.35f;

        /// <summary>Seconds per bulldog segment — long and dogged; few direction changes.</summary>
        public const float BulldogSegmentSeconds = 2.4f;

        /// <summary>The bulldog's roam disc, as a fraction of the caller's radius — he digs in and
        /// won't travel; his short lunges stay close.</summary>
        public const float BulldogRadiusFraction = 0.45f;

        /// <summary>Fraction of a bulldog segment spent lunging — brief, then he holds his ground.</summary>
        public const float BulldogMoveFraction = 0.25f;

        /// <summary>Seconds per full circler lap around the entry point.</summary>
        public const float CirclerRevSeconds = 6f;

        /// <summary>Seconds per full thrasher swing (left→right→left).</summary>
        public const float ThrasherPeriodSeconds = 0.9f;

        /// <summary>¾-iso foreshortening of screen-Y excursions, so surface motion lies on the water
        /// plane (the ellipse of a circling fish), matching the world's own iso read.</summary>
        public const float IsoYSquash = 0.5f;

        /// <summary>Seconds over which the roam grows from the anchor to full radius after she breaks
        /// the surface — so every pattern OPENS at the line's entry point and swims out (no pattern
        /// pops 2 m sideways on the crossing frame).</summary>
        public const float SurfaceRampSeconds = 1.5f;

        // ---- the two reads the fight consumes ---------------------------------------------------

        /// <summary>
        /// Where the fish is RIGHT NOW, as a world-metre offset from the surface anchor (the line's
        /// entry point), for a fight <paramref name="seed"/> at <paramref name="t"/> seconds into the
        /// surface phase, roaming a disc of <paramref name="radiusM"/> metres. Closed-form and
        /// deterministic — same (seed, t) in, same point out. t ≤ 0 or a non-positive radius reads as
        /// the anchor itself. Pure, NaN-safe.
        /// </summary>
        public static Vector2 Offset(RodFightMovement pattern, int seed, float t, float radiusM)
        {
            float time = Safe(t);
            float radius = Mathf.Max(0f, Safe(radiusM));
            if (time <= 0f || radius <= 0f) return Vector2.zero;

            // Every pattern swims OUT from the entry point rather than popping onto its curve.
            radius *= Mathf.Clamp01(time / SurfaceRampSeconds);

            switch (pattern)
            {
                case RodFightMovement.Circler:
                {
                    float theta = CirclerAngle(seed, time);
                    return new Vector2(Mathf.Cos(theta), Mathf.Sin(theta) * IsoYSquash) * radius;
                }
                case RodFightMovement.Thrasher:
                {
                    float phase = 2f * Mathf.PI * (time / ThrasherPeriodSeconds + Hash01(seed, 0, 3));
                    return new Vector2(Mathf.Sin(phase) * radius, 0f);
                }
                case RodFightMovement.Bulldog:
                    return SegmentOffset(seed, time, radius * BulldogRadiusFraction,
                                         BulldogSegmentSeconds, BulldogMoveFraction);
                default: // Darter
                    return SegmentOffset(seed, time, radius, DarterSegmentSeconds, DarterMoveFraction);
            }
        }

        /// <summary>
        /// The direction of her CURRENT dart — the unit vector the counter-steer is measured against
        /// (steer OPPOSITE this to tire her, design §3). Always a unit vector (a degenerate/paused
        /// moment falls back to screen-right, so the read never vanishes mid-fight). Deterministic from
        /// (seed, t), like <see cref="Offset"/>. Pure, NaN-safe.
        /// </summary>
        public static Vector2 DartDir(RodFightMovement pattern, int seed, float t)
        {
            float time = Mathf.Max(0f, Safe(t));
            switch (pattern)
            {
                case RodFightMovement.Circler:
                {
                    float theta = CirclerAngle(seed, time);
                    // The tangent of the swept ellipse, respecting handedness (d/dt of Offset).
                    float dir = CirclerHandedness(seed);
                    var tangent = new Vector2(-Mathf.Sin(theta), Mathf.Cos(theta) * IsoYSquash) * dir;
                    return Normalize(tangent);
                }
                case RodFightMovement.Thrasher:
                {
                    float phase = 2f * Mathf.PI * (time / ThrasherPeriodSeconds + Hash01(seed, 0, 3));
                    return Mathf.Cos(phase) >= 0f ? Vector2.right : Vector2.left;
                }
                case RodFightMovement.Bulldog:
                    return SegmentDartDir(seed, time, BulldogSegmentSeconds);
                default: // Darter
                    return SegmentDartDir(seed, time, DarterSegmentSeconds);
            }
        }

        /// <summary>
        /// The <c>steerAlignment</c> the fight maths consumes (−1 = steering OPPOSITE her dart, the
        /// good counter; +1 = steering WITH her, the mistake): the cosine between the player's steer
        /// vector (pointer − angler, world space) and her current dart. A steer shorter than
        /// <paramref name="deadzoneM"/> (the pointer resting on the character) or a degenerate dart
        /// reads as 0 — neutral, never a hidden penalty. Pure, NaN-safe.
        /// </summary>
        public static float SteerAlignment(Vector2 steerWorld, Vector2 dartDir, float deadzoneM)
        {
            float sx = Safe(steerWorld.x), sy = Safe(steerWorld.y);
            float dx = Safe(dartDir.x), dy = Safe(dartDir.y);
            float steerLen = Mathf.Sqrt(sx * sx + sy * sy);
            float dartLen = Mathf.Sqrt(dx * dx + dy * dy);
            if (steerLen < Mathf.Max(0f, Safe(deadzoneM)) || steerLen <= 1e-6f || dartLen <= 1e-6f)
                return 0f;
            return Mathf.Clamp((sx * dx + sy * dy) / (steerLen * dartLen), -1f, 1f);
        }

        // ---- the segment machinery (darter/bulldog: hash-picked points, lunge-then-hover) --------

        /// <summary>The hash-picked roam point for segment <paramref name="i"/> — uniform-ish on the
        /// unit disc, iso-squashed. Segment 0 is pinned to the ANCHOR so the surface fight opens where
        /// the line entered the water and lunges outward from there.</summary>
        private static Vector2 SegmentPoint(int seed, int i)
        {
            if (i <= 0) return Vector2.zero;
            float angle = 2f * Mathf.PI * Hash01(seed, i, 1);
            float r = Mathf.Sqrt(Hash01(seed, i, 2));   // sqrt → area-uniform on the disc
            return new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r * IsoYSquash);
        }

        private static Vector2 SegmentOffset(int seed, float t, float radius,
                                             float segmentSeconds, float moveFraction)
        {
            int i = Mathf.FloorToInt(t / segmentSeconds);
            float u = (t - i * segmentSeconds) / segmentSeconds;                 // 0..1 within the segment
            Vector2 from = SegmentPoint(seed, i);
            Vector2 to = SegmentPoint(seed, i + 1);
            // Linear (not eased) crossing in the first moveFraction — sharp, then she holds.
            float move = Mathf.Clamp01(u / Mathf.Max(1e-4f, moveFraction));
            return Vector2.Lerp(from, to, move) * radius;
        }

        private static Vector2 SegmentDartDir(int seed, float t, float segmentSeconds)
        {
            int i = Mathf.FloorToInt(t / segmentSeconds);
            Vector2 leap = SegmentPoint(seed, i + 1) - SegmentPoint(seed, i);
            return Normalize(leap);
        }

        // ---- circler internals --------------------------------------------------------------------

        private static float CirclerHandedness(int seed) => Hash01(seed, 0, 4) < 0.5f ? -1f : 1f;

        private static float CirclerAngle(int seed, float t)
            => 2f * Mathf.PI * (Hash01(seed, 0, 5) + CirclerHandedness(seed) * t / CirclerRevSeconds);

        // ---- hashing (the StableHash lane — process-stable, no RNG object) ------------------------

        /// <summary>A 0..1 value from (seed, index, salt) via the repo's process-stable FNV-1a — the
        /// same choreography every run, every platform (StableHash's whole point).</summary>
        private static float Hash01(int seed, int index, int salt)
        {
            uint h = StableHash.Fold(2166136261u, seed);
            h = StableHash.Fold(h, index);
            h = StableHash.Fold(h, salt);
            return (StableHash.Finalize(h) & 0xFFFFFF) / (float)0x1000000;
        }

        private static Vector2 Normalize(Vector2 v)
        {
            float len = Mathf.Sqrt(v.x * v.x + v.y * v.y);
            return len > 1e-6f ? v / len : Vector2.right;   // never a vanishing dart mid-fight
        }

        /// <summary>NaN → 0 (the safe, neutral value) — the module's standard input guard.</summary>
        private static float Safe(float x) => float.IsNaN(x) ? 0f : x;
    }
}
