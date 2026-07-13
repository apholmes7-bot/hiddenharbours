using System;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// PURE local steering for the ambient fisher fleet (canon M2-33): seek-the-spot plus soft
    /// separation from other boats / the player / the player's buoys, plus a tide-aware shoal
    /// look-ahead. Greybox seamanship, not pathfinding — the world's truth is the painted seabed
    /// height field, so there is no NavMesh: the plan-time gate (<see cref="AmbientFleetPlan"/>)
    /// guarantees the ROUTE is safe at every tide, and these probes keep the moment-to-moment track
    /// looking like competent small-boat handling when something (usually the player) pushes a boat
    /// off it. Engine-light statics over <see cref="Vector2"/>, no allocation, no RNG, no state —
    /// EditMode-testable headless.
    ///
    /// <para><b>The seamanship model (owner feedback on #189: "spinning in circles").</b> Three
    /// habits keep the track honest: she <b>turns with way</b> (the bow swings with the speed she
    /// carries, never below bare steerage, and she eases down through a hard turn — a punt cannot
    /// pirouette); she <b>arrives and lies-to</b> (<see cref="HoldStation"/> — a settled boat keeps
    /// her heading and ignores a faint push, with hysteresis so a drifting-past player can't wake
    /// her into circles); and the head-on starboard bias in <see cref="ComposeHeading"/> engages
    /// <b>only near a true bow-to-bow meet</b> — curling every glancing repulsion sideways is
    /// exactly what turned "keep clear" into a stable orbit. <see cref="Step"/> is the whole
    /// per-boat integration, shared verbatim by the presenter and the EditMode convergence tests.</para>
    /// </summary>
    public static class AmbientFleetSteering
    {
        /// <summary>
        /// The summed repulsion away from every obstacle in <paramref name="obstacles"/> (first
        /// <paramref name="count"/> entries) closer than <paramref name="radius"/>: each contributes
        /// away-from-it, weighted linearly from 1 at contact to 0 at the radius edge. A co-located
        /// obstacle (distance ~0 — e.g. the boat itself in a shared list) is skipped: it has no
        /// direction to push. Zero when nothing is near.
        /// </summary>
        public static Vector2 Repulsion(Vector2 pos, Vector2[] obstacles, int count, float radius)
        {
            Vector2 sum = Vector2.zero;
            if (obstacles == null || radius <= 0f) return sum;
            int n = Mathf.Min(count, obstacles.Length);
            for (int i = 0; i < n; i++)
            {
                Vector2 away = pos - obstacles[i];
                float d = away.magnitude;
                if (d < 1e-4f || d >= radius) continue;
                sum += (away / d) * (1f - d / radius);
            }
            return sum;
        }

        /// <summary>
        /// Compose the seek direction with an avoidance sum into the desired heading (unit vector).
        /// A starboard bias (a clockwise-perpendicular component of the avoidance) is folded in so two
        /// boats meeting head-on — where seek and avoid cancel exactly — curl the same way around each
        /// other instead of deadlocking bow-to-bow; if the sum still degenerates, the boat falls off
        /// 45° to starboard of her seek. Deterministic, and reads as boats that agree how to pass.
        /// <para>Three habits keep this from ever reading as a spin (owner feedback on #189). The bias
        /// is GATED to the near-head-on case it exists for — fading in as seek and avoid oppose beyond
        /// <paramref name="headOnBeginDegrees"/>, full at <paramref name="headOnFullDegrees"/>; a
        /// glancing repulsion (a neighbour abeam, the player drifting past) composes straight, because
        /// curling those sideways is what turns "keep clear" into a stable orbit. The seek YIELDS to a
        /// saturating push — you don't press toward a mark something big is sitting on; she stands off
        /// at the balance ring instead of grinding in. And <paramref name="resolve01"/> reports how
        /// decisive the demand is (the pre-normalise magnitude, clamped 0..1): near a standoff it
        /// tends to zero so the caller can check her way — she waits, rather than sailing circles at
        /// full manoeuvring speed.</para>
        /// </summary>
        public static Vector2 ComposeHeading(Vector2 seek, Vector2 avoid, out float resolve01,
                                             float starboardBias = 0.35f,
                                             float headOnBeginDegrees = 120f, float headOnFullDegrees = 155f)
        {
            float push = avoid.magnitude;

            // A saturated push beats the seek outright — stand off the blocked mark and wait.
            Vector2 pressedSeek = seek * (1f - Mathf.Clamp01(push));

            // Engage the pass-to-starboard convention only when the push is nearly dead against the
            // course (180° = bow-to-bow). InverseLerp clamps, so a glancing push gets zero bias.
            float gate = 0f;
            if (seek.sqrMagnitude > 1e-8f && push > 1e-4f)
            {
                float opposition = Vector2.Angle(seek, avoid);
                gate = Mathf.InverseLerp(headOnBeginDegrees,
                                         Mathf.Max(headOnFullDegrees, headOnBeginDegrees + 1f), opposition);
            }

            // Clockwise perpendicular of the avoidance — "put the danger to port".
            Vector2 bias = new Vector2(avoid.y, -avoid.x) * (starboardBias * gate);
            Vector2 sum = pressedSeek + avoid + bias;
            float magnitude = sum.magnitude;
            resolve01 = Mathf.Clamp01(magnitude);
            if (magnitude > 1e-4f) return sum / magnitude;
            if (seek.sqrMagnitude > 1e-8f)
            {
                // Dead standoff: bear away 45° to starboard of the seek (deterministic tiebreak).
                Vector2 s = seek.normalized;
                const float Cos45 = 0.70710678f;
                return new Vector2(s.x * Cos45 + s.y * Cos45, -s.x * Cos45 + s.y * Cos45);
            }
            return Vector2.up;
        }

        /// <summary>Convenience overload when the caller has no use for the resolve.</summary>
        public static Vector2 ComposeHeading(Vector2 seek, Vector2 avoid, float starboardBias = 0.35f,
                                             float headOnBeginDegrees = 120f, float headOnFullDegrees = 155f)
        {
            return ComposeHeading(seek, avoid, out _, starboardBias, headOnBeginDegrees, headOnFullDegrees);
        }

        /// <summary>
        /// The tide-aware shoal look-ahead — the live twin of the plan-time depth gate. Probes the
        /// water depth (at the CURRENT water level — the caller samples it) ahead of the bow and off
        /// both bows at <paramref name="sideDegrees"/>. All deep → no correction, full speed. Shoal
        /// ahead → a correction toward the deeper bow (or hard astern-ward if both bows shoal too) and
        /// <paramref name="speedScale"/> eases her down in proportion — she feels her way off a bar,
        /// never plows onto it. Pure: depth comes through the sampler.
        /// </summary>
        /// <param name="depthAt">Water depth (m) at a world point — <c>waterLevel − elevation</c>, tide-aware.</param>
        public static Vector2 DepthAvoid(Vector2 pos, Vector2 heading, float lookAheadMeters,
                                         float sideDegrees, Func<Vector2, float> depthAt,
                                         float minDepthMeters, out float speedScale)
        {
            speedScale = 1f;
            if (depthAt == null || heading.sqrMagnitude < 1e-8f) return Vector2.zero;

            Vector2 fwd = heading.normalized;
            Vector2 left = Rotate(fwd, sideDegrees);
            Vector2 right = Rotate(fwd, -sideDegrees);

            float dAhead = depthAt(pos + fwd * lookAheadMeters);
            if (dAhead >= minDepthMeters) return Vector2.zero;   // clear water ahead — steer nothing

            float dLeft = depthAt(pos + left * lookAheadMeters);
            float dRight = depthAt(pos + right * lookAheadMeters);

            // Ease down in proportion to how bad it is ahead (never below a crawl — she keeps steerage).
            speedScale = Mathf.Clamp(dAhead / Mathf.Max(0.01f, minDepthMeters), 0.15f, 1f);

            float shortfall = 1f - Mathf.Clamp01(dAhead / Mathf.Max(0.01f, minDepthMeters));
            if (dLeft < minDepthMeters && dRight < minDepthMeters)
                return -fwd * shortfall;                          // shoal all round the bow — back off
            return (dLeft >= dRight ? left : right) * shortfall;  // swing toward the deeper bow
        }

        /// <summary>
        /// The arrive-and-lie-to gate, with hysteresis. A boat SETTLES (returns true) when she is
        /// inside <paramref name="holdRadius"/> of her spot and the summed social push (other boats /
        /// the player / player buoys — NOT the shoal correction; a planned spot is depth-safe at
        /// spring low by construction) is at most <paramref name="enterRepulsion"/>. Once settled she
        /// STAYS settled — bow steady, way off — until the push climbs past the (higher)
        /// <paramref name="wakeRepulsion"/> or she is somehow displaced beyond
        /// <paramref name="holdRadius"/> × <paramref name="releaseRadiusFactor"/>. The gap between the
        /// two thresholds is the hysteresis: a neighbour's faint residual push or the player drifting
        /// past must never wake a working boat into circles. A target change is the caller's release
        /// (clear the flag before calling).
        /// </summary>
        public static bool HoldStation(bool holding, float distToTarget, float holdRadius,
                                       float releaseRadiusFactor, float socialRepulsion,
                                       float enterRepulsion, float wakeRepulsion)
        {
            if (holding)
                return socialRepulsion < Mathf.Max(wakeRepulsion, enterRepulsion) &&
                       distToTarget <= holdRadius * Mathf.Max(1f, releaseRadiusFactor);
            return distToTarget <= holdRadius && socialRepulsion <= enterRepulsion;
        }

        /// <summary>
        /// Turn-with-way: the fraction of the full turn rate available at the boat's current way.
        /// A real boat only steers with water moving over the rudder — full rate at cruise, easing
        /// down to <paramref name="steerageFraction"/> (never zero: she always keeps bare steerage,
        /// and the floor is what breaks the classic pursuit orbit — near the spot the arrive ease
        /// keeps shedding speed while the turn rate stops falling, so her turning circle always
        /// shrinks inside the distance left).
        /// </summary>
        public static float TurnRateScale(float speedFraction, float steerageFraction)
        {
            return Mathf.Max(Mathf.Clamp01(steerageFraction), Mathf.Clamp01(speedFraction));
        }

        /// <summary>
        /// The commanded speed (as a fraction of cruise): ease down over the last
        /// <paramref name="arriveSlowRadius"/> metres so she comes alongside with the way already off,
        /// and ease down through a hard turn (<paramref name="headingErrorDegrees"/> toward
        /// <paramref name="slowForTurnDegrees"/> ramps to <paramref name="slowForTurnSpeedFraction"/>)
        /// — slow through the turn, drive out of it. A tight come-about then reads as deliberate
        /// seamanship, not a spin.
        /// </summary>
        public static float ArriveSpeedFraction(float distToTarget, float arriveSlowRadius,
                                                float headingErrorDegrees, float slowForTurnDegrees,
                                                float slowForTurnSpeedFraction)
        {
            float arrive = Mathf.Clamp01(distToTarget / Mathf.Max(0.1f, arriveSlowRadius));
            float turnEase = Mathf.Lerp(1f, Mathf.Clamp01(slowForTurnSpeedFraction),
                                        Mathf.Clamp01(Mathf.Abs(headingErrorDegrees) /
                                                      Mathf.Max(1f, slowForTurnDegrees)));
            return arrive * turnEase;
        }

        /// <summary>
        /// Re-express a stored steering vector when the frame it was captured in has swung: rotates
        /// <paramref name="v"/> by the signed angle from <paramref name="fromDir"/> to
        /// <paramref name="toDir"/>. Used to keep the slow-tick shoal correction BOW-RELATIVE — the
        /// bow can swing a long way between probes, and a correction left in stale world coordinates
        /// ends up pushing the wrong way. Degenerate inputs return <paramref name="v"/> unchanged.
        /// </summary>
        public static Vector2 RotateFromTo(Vector2 v, Vector2 fromDir, Vector2 toDir)
        {
            if (v.sqrMagnitude < 1e-12f || fromDir.sqrMagnitude < 1e-8f || toDir.sqrMagnitude < 1e-8f)
                return v;
            return Rotate(v, Vector2.SignedAngle(fromDir, toDir));
        }

        /// <summary>
        /// One frame of the ambient boat's whole drive: hold gate → compose → turn-with-way → shaped
        /// speed → move. Pure (the Def is read-only data; all state rides the ref parameters), shared
        /// verbatim between <c>AmbientFleetPresenter.UpdateFleet</c> and the EditMode convergence
        /// tests — what the tests prove is exactly what ships.
        /// <para>Order of the speed shaping: arrive/turn ease first; then the manoeuvring nudge floor,
        /// applied only against a REAL push (above the hold-enter threshold — a faint neighbour must
        /// not keep her endlessly under way, which was the old circling failure); then the demand's
        /// resolve (a near-standoff checks her way — she waits, never rings the blockage at full
        /// manoeuvring speed); then the shoal ease multiplies LAST so the bar is never argued with.
        /// Way builds at the Def's acceleration and comes off instantly — drag stops a punt far
        /// faster than oars drive one.</para>
        /// </summary>
        /// <param name="socialAvoid">Summed repulsion from boats/player/buoys — gates the hold.</param>
        /// <param name="depthAvoid">The (already weighted, bow-consistent) shoal correction — steers
        /// but never wakes a held boat; a planned spot is depth-safe at every tide.</param>
        public static void Step(ref Vector2 position, ref Vector2 heading, ref float speedFraction,
                                ref bool holding, Vector2 target, Vector2 socialAvoid,
                                Vector2 depthAvoid, float depthSpeedScale, float cruiseSpeed,
                                AmbientFleetDef def, float dt)
        {
            if (dt <= 0f) return;   // a paused clock freezes the fleet — no drift, no hold flips

            Vector2 toTarget = target - position;
            float dist = toTarget.magnitude;
            float socialMag = socialAvoid.magnitude;

            holding = HoldStation(holding, dist, def.HoldRadius, def.HoldReleaseRadiusFactor,
                                  socialMag, def.HoldEnterRepulsion, def.HoldWakeRepulsion);
            if (holding)
            {
                speedFraction = 0f;   // lying-to alongside the buoy: way off, bow steady where it fell
                return;
            }

            Vector2 seek = dist > 1e-3f ? toTarget / dist : Vector2.zero;
            Vector2 desired = ComposeHeading(seek, socialAvoid + depthAvoid, out float resolve01,
                                             def.HeadOnStarboardBias,
                                             def.HeadOnBiasBeginDegrees, def.HeadOnBiasFullDegrees);

            // The bow swings with the way she carries (turn-with-way), never below bare steerage.
            float turnScale = TurnRateScale(speedFraction, def.SteerageTurnFraction);
            heading = RotateToward(heading, desired, def.TurnRateDegreesPerSecond * turnScale * dt);

            float error = Vector2.Angle(heading, desired);
            float commanded = ArriveSpeedFraction(dist, def.ArriveSlowRadius, error,
                                                  def.SlowForTurnDegrees, def.SlowForTurnSpeedFraction);
            if (socialMag > def.HoldEnterRepulsion)
                commanded = Mathf.Max(commanded, def.AvoidNudgeSpeedFraction);
            commanded *= resolve01 * Mathf.Clamp01(depthSpeedScale);

            speedFraction = commanded > speedFraction
                ? Mathf.Min(commanded, speedFraction + def.AccelFractionPerSecond * dt)
                : commanded;

            position += heading * (cruiseSpeed * speedFraction * dt);
        }

        /// <summary>Rotate <paramref name="current"/> toward <paramref name="desired"/> by at most
        /// <paramref name="maxDegrees"/> — the bow swings at a small boat's rate, never snaps. Both
        /// treated as directions; returns a unit vector (falls back to current/up when degenerate).</summary>
        public static Vector2 RotateToward(Vector2 current, Vector2 desired, float maxDegrees)
        {
            if (current.sqrMagnitude < 1e-8f) return desired.sqrMagnitude > 1e-8f ? desired.normalized : Vector2.up;
            if (desired.sqrMagnitude < 1e-8f) return current.normalized;
            float angle = Vector2.SignedAngle(current, desired);
            float step = Mathf.Clamp(angle, -maxDegrees, maxDegrees);
            return (Rotate(current, step)).normalized;
        }

        /// <summary>Rotate a vector counter-clockwise by <paramref name="degrees"/> (negative = clockwise).</summary>
        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }
    }
}
