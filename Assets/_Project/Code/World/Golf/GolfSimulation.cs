using System;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World.Golf
{
    public readonly struct GolfSwing
    {
        public readonly Vector2 Aim;
        public readonly float Power;
        public GolfSwing(Vector2 aim, float power)
        {
            GolfCheck.Point(aim); GolfCheck.Unit(power, nameof(power));
            if (aim.sqrMagnitude < 1e-12f || power == 0) throw new ArgumentException("Swing needs aim and positive power.");
            double length = Math.Sqrt((double)aim.x * aim.x + (double)aim.y * aim.y);
            Aim = new Vector2((float)(aim.x / length), (float)(aim.y / length)); Power = power;
        }
    }

    /// <summary>Caller captures Core's wind at GameSeconds, once. No clock, service locator or private weather generator.</summary>
    public readonly struct GolfShotContext
    {
        public readonly int WorldSeed, ShotIndex;
        public readonly double GameSeconds;
        public readonly Vector2 Wind;
        public GolfShotContext(int worldSeed, double gameSeconds, int shotIndex, in EnvironmentSample environment)
            : this(worldSeed, gameSeconds, shotIndex, environment.WindVector) { }
        public GolfShotContext(int worldSeed, double gameSeconds, int shotIndex, Vector2 wind)
        {
            GolfCheck.Finite(gameSeconds, nameof(gameSeconds)); GolfCheck.Point(wind);
            if (gameSeconds < 0 || shotIndex < 0) throw new ArgumentOutOfRangeException(nameof(gameSeconds));
            WorldSeed = worldSeed; GameSeconds = gameSeconds; ShotIndex = shotIndex; Wind = wind;
        }
    }

    public enum GolfShotStatus { Rest, Holed, Water, OutOfBounds, Unresolved }

    public readonly struct GolfShotResult
    {
        public readonly GolfShotStatus Status;
        public readonly Vector2 Position, FirstLanding;
        public readonly GolfLie Lie;
        public readonly float Seconds, FlightSeconds, PeakHeightAboveStart;
        public readonly int Steps;
        internal GolfShotResult(GolfShotStatus status, Vector2 p, Vector2 landing, GolfLie lie,
            float seconds, float flight, float peak, int steps)
        {
            Status = status; Position = p; FirstLanding = landing; Lie = lie;
            Seconds = seconds; FlightSeconds = flight; PeakHeightAboveStart = peak; Steps = steps;
        }
    }

    /// <summary>Bounded fixed-step flight and Coulomb roll on a logic-only plane, in ground metres.
    /// Call on a shot intent, never Update. Bit-repeatable on the same runtime; no cross-CPU lockstep claim.</summary>
    public static class GolfSimulation
    {
        public static GolfShotResult Shoot(GolfHole hole, in GolfClub club, in GolfRules rules,
            Vector2 start, in GolfSwing swing, in GolfShotContext context)
        {
            if (hole == null) throw new ArgumentNullException(nameof(hole));
            GolfCheck.Point(start);
            // Reject default value structs too; authored snapshots can only be built through validated Defs.
            GolfCheck.Positive(club.Speed, nameof(club)); GolfCheck.Positive(rules.StepSeconds, nameof(rules));
            GolfCheck.Positive(swing.Power, nameof(swing)); GolfCheck.Point(swing.Aim);
            if (swing.Aim.sqrMagnitude < 0.5f) throw new ArgumentException("Uninitialised swing.");
            GolfLie initialLie = hole.LieAt(start);
            if (initialLie == GolfLie.Water || initialLie == GolfLie.OutOfBounds)
                throw new ArgumentException("Cannot hit a ball from a penalty lie.");
            GolfSurface surface = hole.SurfaceAt(start);
            double angle = Spread(context, club.Id) * club.AimSpreadDegrees * Math.PI / 180;
            var aim = new Vector2((float)(swing.Aim.x * Math.Cos(angle) - swing.Aim.y * Math.Sin(angle)),
                (float)(swing.Aim.x * Math.Sin(angle) + swing.Aim.y * Math.Cos(angle)));
            float speed = club.Speed * (float)Math.Pow(swing.Power, club.PowerExponent) * surface.LaunchSpeedMultiplier;
            double loft = club.LoftDegrees * Math.PI / 180;
            Vector2 velocity = aim * (speed * (float)Math.Cos(loft));
            float vertical = speed * (float)Math.Sin(loft);
            float startHeight = hole.HeightAt(start), height = startHeight;
            Vector2 p = start, landing = start;
            float elapsed = 0, flight = 0, peak = 0;
            int steps = 0;
            bool airborne = vertical > 0, landed = !airborne;
            GolfShotResult Result(GolfShotStatus status, GolfLie lie) =>
                new GolfShotResult(status, p, landing, lie, elapsed, flight, peak, steps);
            while (steps < rules.MaxSteps && elapsed < rules.MaxSeconds)
            {
                steps++;
                GolfCheck.Point(velocity); GolfCheck.Finite(height, "ball height");
                float dt = Math.Min(rules.StepSeconds, rules.MaxSeconds - elapsed);
                float length = velocity.magnitude;
                if (length > 0) dt = Math.Min(dt, rules.MaxTravel / length);
                if (dt <= 0) break;
                if (airborne)
                {
                    Vector2 acceleration = context.Wind * rules.WindAcceleration - velocity * rules.AirDrag;
                    Vector2 next = p + velocity * dt + acceleration * (0.5f * dt * dt);
                    float nextHeight = height + vertical * dt - 0.5f * rules.Gravity * dt * dt;
                    float nextVertical = vertical - rules.Gravity * dt;
                    float gap = height - hole.HeightAt(p), nextGap = nextHeight - hole.HeightAt(next);
                    if (nextGap <= 0 && nextVertical - Vector2.Dot(hole.Slope, velocity) < 0)
                    {
                        // First contact within this step. Reuse the remaining time in the next iteration.
                        float fraction = gap > 0 ? gap / (gap - nextGap) : 0;
                        dt *= fraction;
                        p += velocity * dt + acceleration * (0.5f * dt * dt);
                        velocity += acceleration * dt; vertical -= rules.Gravity * dt;
                        height = hole.HeightAt(p);
                        if (!landed) { landing = p; landed = true; }
                        elapsed += dt; flight += dt;
                        GolfLie lie = hole.LieAt(p);
                        if (lie == GolfLie.OutOfBounds) return Result(GolfShotStatus.OutOfBounds, lie);
                        if (lie == GolfLie.Water) return Result(GolfShotStatus.Water, lie);
                        surface = hole.SurfaceAt(p);
                        float impact = Math.Max(0, Vector2.Dot(hole.Slope, velocity) - vertical);
                        velocity *= surface.LandingSpeedRetention;
                        float rebound = impact * surface.Restitution;
                        airborne = rebound >= rules.MinimumBounceSpeed;
                        vertical = Vector2.Dot(hole.Slope, velocity) + (airborne ? rebound : 0);
                    }
                    else
                    {
                        p = next; height = nextHeight; vertical = nextVertical; velocity += acceleration * dt;
                        elapsed += dt; flight += dt; peak = Math.Max(peak, height - startHeight);
                    }
                    continue;
                }

                // Probe just beyond a shared edge to select its outgoing surface, without stepping over it.
                Vector2 probe = length > 0 ? p + velocity / length * 0.0001f : p;
                GolfLie groundLie = hole.LieAt(probe);
                if (groundLie == GolfLie.OutOfBounds) return Result(GolfShotStatus.OutOfBounds, groundLie);
                if (groundLie == GolfLie.Water) return Result(GolfShotStatus.Water, groundLie);
                surface = hole.SurfaceAt(probe);
                float slopeLength = hole.Slope.magnitude;
                float normalGravity = rules.Gravity / (float)Math.Sqrt(1 + slopeLength * slopeLength);
                Vector2 downhill = -hole.Slope * (rules.Gravity / (1 + slopeLength * slopeLength));
                if (length <= rules.StopSpeed && downhill.magnitude <= surface.StaticFriction * normalGravity)
                {
                    if (groundLie == GolfLie.Green && (p - hole.CupPosition).sqrMagnitude <= rules.CupRadius * rules.CupRadius)
                    { p = hole.CupPosition; return Result(GolfShotStatus.Holed, GolfLie.Holed); }
                    return Result(GolfShotStatus.Rest, hole.LieAt(p));
                }
                Vector2 direction = length > rules.StopSpeed ? velocity / length : downhill.normalized;
                float friction = surface.RollingFriction * normalGravity;
                Vector2 rollAcceleration = downhill - direction * friction;
                // On a level/subcritical slope, stop exactly; friction must never reverse the ball.
                float braking = -Vector2.Dot(rollAcceleration, direction);
                if (braking > 0 && length > 0) dt = Math.Min(dt, length / braking);
                Vector2 end = p + velocity * dt + rollAcceleration * (0.5f * dt * dt);
                float edge = hole.NextEdge(p, end);
                if (edge < 1)
                {
                    // Solve time to edge by bisection; acceleration means distance fraction is not time fraction.
                    Vector2 segment = end - p;
                    float target = segment.magnitude * edge, low = 0, high = dt;
                    for (int i = 0; i < 24; i++)
                    {
                        float middle = (low + high) * 0.5f;
                        float travelled = Vector2.Dot(velocity * middle + rollAcceleration * (0.5f * middle * middle), segment.normalized);
                        if (travelled < target) low = middle; else high = middle;
                    }
                    dt = high; end = p + segment * edge;
                }
                Vector2 nextVelocity = velocity + rollAcceleration * dt;
                // Continuous cup intersection: a small cup can be crossed between samples.
                Vector2 delta = end - p, toCup = hole.CupPosition - p;
                float along = delta.sqrMagnitude > 0 ? Math.Max(0, Math.Min(1, Vector2.Dot(toCup, delta) / delta.sqrMagnitude)) : 0;
                Vector2 nearest = p + delta * along;
                if (hole.LieAt(nearest) == GolfLie.Green &&
                    (nearest - hole.CupPosition).sqrMagnitude <= rules.CupRadius * rules.CupRadius &&
                    (velocity + rollAcceleration * (dt * along)).magnitude <= rules.CupSpeed)
                { p = hole.CupPosition; elapsed += dt * along; return Result(GolfShotStatus.Holed, GolfLie.Holed); }
                p = end; velocity = nextVelocity; height = hole.HeightAt(p); elapsed += dt;
                // Inclusive water edges terminate now, even when the polygon is thinner than the outgoing probe.
                if (hole.LieAt(p) == GolfLie.Water) return Result(GolfShotStatus.Water, GolfLie.Water);
            }
            return Result(GolfShotStatus.Unresolved, hole.LieAt(p));
        }

        static float Spread(in GolfShotContext c, string clubId)
        {
            unchecked
            {
                // Explicit byte-stable mix: never string.GetHashCode, Random, wall time or global state.
                ulong time = (ulong)BitConverter.DoubleToInt64Bits(c.GameSeconds);
                uint h = 2166136261u;
                h = (h ^ (uint)c.WorldSeed) * 16777619u; h = (h ^ (uint)c.ShotIndex) * 16777619u;
                h = (h ^ (uint)time) * 16777619u; h = (h ^ (uint)(time >> 32)) * 16777619u;
                foreach (char ch in clubId) h = (h ^ ch) * 16777619u;
                h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16;
                return (h >> 8) / 16777215f * 2 - 1;
            }
        }
    }
}
