using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Vehicles
{
    /// <summary>
    /// <b>The truck stays on the land</b> (ADR 0035) — the road vehicle's answer to the same question
    /// <c>BoatCrossing</c> asks for a hull, off the same single number, and coming out the other way round.
    ///
    /// <para>A hull may pass where <c>depth ≥ draught</c>; a truck may pass where the ground is
    /// <b>EXPOSED</b> — <c>depth ≤ 0</c>, i.e. <see cref="TidalExposure.IsExposed(float,float)"/>. That is
    /// not a similar rule, it is the SAME rule read from the other side: both are
    /// <c>waterLevel − authoredGround</c>, the deterministic water level (recomputed from
    /// <c>(worldSeed, gameTime)</c>, never saved — rule 5) against the authored terrain. So the bar that
    /// bares for a walker as the tide falls is drivable at exactly the moment it becomes walkable, and the
    /// channel the dory crosses at high water is the one the truck is refused. One number, three vehicles'
    /// worth of rules, no chance of them disagreeing.</para>
    ///
    /// <para><b>It reads the world only through Core</b> (<see cref="GameServices"/>) — the Vehicles module
    /// references Core and nothing else (rule 4), so it cannot call <c>BoatCrossing</c> in the Boats lane
    /// and does not want to: what it needs is <see cref="TidalExposure"/>, which is already Core, and
    /// borrowing a boat's helper to ask a truck's question would be the coupling this module was split out
    /// to avoid.</para>
    ///
    /// <para><b>No height map means no rule</b>, exactly as it does for the hull: an unwired terrain or
    /// environment reports drivable, so the gate self-disables in EditMode, in an art scene, and in any
    /// region that never authored tidal ground. A missing map must never be the thing that strands a truck
    /// — the failure would look like a physics bug and be a wiring one.</para>
    ///
    /// <para><b>Non-punishing (P5).</b> This is a passability gate and not a damage model: too-wet ground
    /// simply cannot be entered. There is no bogging, no stalling and no recovery minigame, and the refusal
    /// is DIRECTIONAL — see <see cref="LeadOffsetMeters"/> — so a truck stopped at the water's edge can
    /// always reverse off it. A gate that could strand you would be a gate that needs a tow truck, and the
    /// village does not have one.</para>
    /// </summary>
    public static class VehicleGrounding
    {
        /// <summary>How many samples the road march may take. A rule-7 budget, not a gameplay number: it
        /// bounds the loop for a machine tuned to brake so badly that her stopping distance runs to
        /// hundreds of metres, by coarsening the step rather than by spending the frame.</summary>
        public const int MaxRoadSamples = 32;

        /// <summary>
        /// How far she needs to stop from her current speed, in metres — <c>v² / 2a</c>, the ordinary
        /// constant-deceleration result. This is the horizon the road march looks out to: exactly as far
        /// as matters and not one metre further, <b>with no look-ahead tunable to get wrong</b> (rule 6).
        /// Both terms are already published — the speed she is doing, and the braking rate off her own def.
        /// </summary>
        public static float StoppingDistanceMeters(float speedMetersPerSecond,
                                                   float brakingMetersPerSecondSquared)
        {
            float a = Mathf.Max(0.0001f, brakingMetersPerSecondSquared);
            float v = Mathf.Abs(speedMetersPerSecond);
            return v * v / (2f * a);
        }

        /// <summary>
        /// The inverse: the fastest she may be going with <paramref name="clearMeters"/> of dry road left
        /// in front of her, <c>√(2·a·d)</c> — the speed from which she can still stop on the gravel.
        ///
        /// <para><b>This is what makes the gate a smooth stop rather than a stutter</b>, and it is worth
        /// saying why the obvious implementation is wrong. Refusing the speed OUTRIGHT when a look-ahead
        /// probe lands on water sets up a limit cycle: zeroed, she has no stopping distance, so the probe
        /// pulls back onto dry ground, so the throttle is allowed again, so she accelerates until the probe
        /// trips — for ever, at the water's edge, several times a second. Capping instead of refusing has
        /// no such cycle, because the cap falls continuously to zero exactly as the clear road does.</para>
        /// </summary>
        public static float MaxApproachSpeedMetersPerSecond(float clearMeters,
                                                            float brakingMetersPerSecondSquared)
        {
            float a = Mathf.Max(0.0001f, brakingMetersPerSecondSquared);
            return Mathf.Sqrt(2f * a * Mathf.Max(0f, clearMeters));
        }

        /// <summary>
        /// <b>How much dry road is left</b> — march out from <paramref name="from"/> along
        /// <paramref name="direction"/> and return the distance to the first drowned sample, or
        /// <paramref name="horizonMeters"/> if the whole look-ahead is dry.
        ///
        /// <para>A march rather than a single probe because the answer needed is a DISTANCE, not a
        /// yes/no: it is what the speed cap is computed from, and one sample can only ever say "trouble
        /// somewhere within the horizon". The resolution is her own wheel radius — published geometry, so
        /// nothing is invented — coarsened if a pathological braking tune would otherwise make the march
        /// longer than <see cref="MaxRoadSamples"/>.</para>
        /// </summary>
        public static float ClearRoadMeters(ITidalTerrain terrain, IEnvironmentService environment,
                                            double totalSeconds, Vector2 from, Vector2 direction,
                                            float horizonMeters, float stepMeters)
        {
            if (terrain == null || environment == null) return horizonMeters;   // no map, no rule

            float horizon = Mathf.Max(0f, horizonMeters);
            float step = Mathf.Max(0.01f, stepMeters);
            if (horizon / step > MaxRoadSamples) step = horizon / MaxRoadSamples;

            Vector2 dir = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector2.up;

            // Sample 0 is the leading axle ITSELF, so a wheel already standing in water reads zero clear
            // road and caps her dead — which is the state a truck nosed up to the barachois is in, and the
            // state she must be able to REVERSE out of (the caller marches the other way for that).
            for (float d = 0f; d <= horizon; d += step)
                if (!IsDryLand(terrain, environment, totalSeconds, from + dir * d)) return d;

            return horizon;
        }

        /// <summary>
        /// Water depth (m) over a world point, from the authored terrain and the deterministic water level.
        /// Returns <see cref="float.NegativeInfinity"/> when nothing is wired — "no map, so infinitely dry",
        /// which is the value that makes <see cref="IsDryLand"/> self-disable. (The hull's twin returns
        /// POSITIVE infinity for the same wiring, because open water is the permissive answer there and dry
        /// land is the permissive answer here. Same convention, opposite pole.)
        /// </summary>
        public static float DepthAt(ITidalTerrain terrain, IEnvironmentService environment,
                                    double totalSeconds, Vector2 worldPos)
        {
            if (terrain == null || environment == null) return float.NegativeInfinity;
            float waterLevel = environment.WaterLevelAt(totalSeconds);
            float ground = terrain.ElevationAt(worldPos);
            return TidalExposure.WaterDepth(waterLevel, ground);
        }

        /// <summary>True when a road vehicle may stand at a world point right now — the ground is bared
        /// (<c>depth ≤ 0</c>). The same test <c>ControlSwitcher.IsStandableLandByDepth</c> applies to a
        /// fisher stepping off a deck, because it is the same question: is there anything here to stand
        /// on?</summary>
        public static bool IsDryLand(ITidalTerrain terrain, IEnvironmentService environment,
                                     double totalSeconds, Vector2 worldPos)
            => DepthAt(terrain, environment, totalSeconds, worldPos) <= 0f;

        /// <summary>Convenience over the live Core services at the current clock time — what
        /// <see cref="VehicleController"/> calls each physics tick; tests drive the explicit overload with
        /// fakes and a plain double.</summary>
        public static bool IsDryLandNow(Vector2 worldPos)
        {
            IGameClock clock = GameServices.Clock;
            double now = clock != null ? clock.TotalSeconds : 0.0;
            return IsDryLand(GameServices.TidalTerrain, GameServices.Environment, now, worldPos);
        }

        /// <summary>
        /// <b>The whole gate, as one number</b>: the fastest she may travel at <paramref name="speed"/>'s
        /// sign, given where she is and which way she is pointed. Positive; the caller applies it to
        /// whichever direction she is going.
        ///
        /// <para><b>The LEADING AXLE is what must stay dry</b> — not the bumper and not her centre. A
        /// vehicle is carried by her wheels, her front overhang may hang out over the water at a slipway
        /// with nothing wrong, and it is a wheel leaving the gravel that puts her in trouble. Both axle
        /// positions are measured art off the rig, so a machine with a different wheelbase probes her own
        /// geometry with nothing re-transcribed.</para>
        ///
        /// <para><b>Which end leads is decided by the direction she is ABOUT to travel</b>, and that is
        /// what makes the refusal escapable. Probing a fixed end would strand her: a truck stopped
        /// nose-to-the-water would go on being refused while the driver begged her to reverse. Fed the
        /// TENTATIVE next speed — the one the pedals have just asked for — backing away marches from the
        /// tail, finds gravel, and is uncapped.</para>
        /// </summary>
        public static float SpeedCapMetersPerSecond(ITidalTerrain terrain, IEnvironmentService environment,
                                                    double totalSeconds, Vector2 origin, Vector2 nose,
                                                    float speed, float frontAxleY, float rearAxleY,
                                                    float brakingMetersPerSecondSquared,
                                                    float wheelRadiusMeters)
        {
            if (terrain == null || environment == null) return float.PositiveInfinity;

            Vector2 dir = speed >= 0f ? nose : -nose;
            float leadDistance = speed >= 0f ? frontAxleY : -rearAxleY;
            Vector2 from = origin + (dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector2.up) * leadDistance;

            // Look exactly as far as she needs to stop — plus one step, so a stationary truck still sees
            // the ground her leading wheel is actually standing on.
            float step = Mathf.Max(0.01f, wheelRadiusMeters);
            float horizon = StoppingDistanceMeters(speed, brakingMetersPerSecondSquared) + step;

            float clear = ClearRoadMeters(terrain, environment, totalSeconds, from, dir, horizon, step);
            return MaxApproachSpeedMetersPerSecond(clear, brakingMetersPerSecondSquared);
        }

        /// <summary>The live-services overload — what <see cref="VehicleController"/> calls each physics
        /// tick.</summary>
        public static float SpeedCapNow(Vector2 origin, Vector2 nose, float speed, float frontAxleY,
                                        float rearAxleY, float brakingMetersPerSecondSquared,
                                        float wheelRadiusMeters)
        {
            IGameClock clock = GameServices.Clock;
            double now = clock != null ? clock.TotalSeconds : 0.0;
            return SpeedCapMetersPerSecond(GameServices.TidalTerrain, GameServices.Environment, now,
                                           origin, nose, speed, frontAxleY, rearAxleY,
                                           brakingMetersPerSecondSquared, wheelRadiusMeters);
        }
    }
}
