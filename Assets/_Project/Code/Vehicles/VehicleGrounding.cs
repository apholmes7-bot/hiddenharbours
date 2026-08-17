using System;
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
    /// is DIRECTIONAL — the speed cap marches from whichever end is about to LEAD — so a truck stopped at
    /// the water's edge can always reverse off it. A gate that could strand you would be a gate that needs
    /// a tow truck, and the village does not have one.</para>
    ///
    /// <para>⭐ <b>AND THE SAME NUMBER MEANS THE OPPOSITE THING FOR AN AMPHIBIAN.</b> Everything above
    /// reads one depth: <c>waterLevel − authoredGround</c>, recomputed from <c>(worldSeed, gameTime)</c>
    /// and never saved (rule 5). For a <see cref="VehicleKind.RoadVehicle"/> a deep sample is a WALL and
    /// the speed falls to nothing in front of it; for a <see cref="VehicleKind.AmphibiousVehicle"/> the
    /// very same sample is the SWIM state and there is no wall at all. One reading of the world, two rules,
    /// chosen by what the machine IS — and the kind arrives here through
    /// <see cref="VehicleKinds"/> off her def rather than as a literal or a guess, so a machine that
    /// declares a word nobody maps is refused at install and never reaches a launch ramp. Every switch on
    /// the kind below NAMES both arms and THROWS on a third, because <see cref="VehicleKind"/> is an
    /// append-only Core enum and a <c>default:</c> arm meaning "road" is exactly how the next kind gets
    /// silently drowned (the trap #560 closed in <c>InteractActor.ContextFor</c>).</para>
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

        // =================================================================================================
        //  THE DISCRIMINATOR — one depth read, two rules
        // =================================================================================================

        /// <summary>
        /// ⭐ <b>The land gate, dispatched on what she IS.</b>
        ///
        /// <list type="bullet">
        ///   <item><see cref="VehicleKind.RoadVehicle"/> — the march above, unchanged to the last float.
        ///   <b>The Dually does not learn to swim from this file</b>, and that is the one arm worth
        ///   sabotaging a test at: an amphibian arriving in the fleet must not relax a rule that was
        ///   already holding a truck out of the harbour.</item>
        ///   <item><see cref="VehicleKind.AmphibiousVehicle"/> — <b>no cap at all.</b> Not a bigger cap, not
        ///   a cap she can push through: the gate is simply not her rule. She drives at the water at
        ///   whatever speed she likes, and past her float draft she is afloat (see
        ///   <see cref="IsAfloat"/>). Marching a look-ahead for her would be arithmetic in service of a
        ///   refusal nobody wants.</item>
        /// </list>
        ///
        /// <para>Both arms NAMED and a third THROWS — see the class doc.</para>
        /// </summary>
        public static float SpeedCapMetersPerSecond(VehicleKind kind, ITidalTerrain terrain,
                                                    IEnvironmentService environment, double totalSeconds,
                                                    Vector2 origin, Vector2 nose, float speed,
                                                    float frontAxleY, float rearAxleY,
                                                    float brakingMetersPerSecondSquared,
                                                    float wheelRadiusMeters) => kind switch
        {
            VehicleKind.RoadVehicle => SpeedCapMetersPerSecond(
                terrain, environment, totalSeconds, origin, nose, speed, frontAxleY, rearAxleY,
                brakingMetersPerSecondSquared, wheelRadiusMeters),

            VehicleKind.AmphibiousVehicle => float.PositiveInfinity,

            _ => throw new ArgumentOutOfRangeException(
                     nameof(kind), kind,
                     "VehicleKind has grown a member with no grounding rule. Name the arm — a " +
                     "default meaning 'road' holds an amphibian out of the water she was built for, " +
                     "and a default meaning 'amphibious' drowns a truck."),
        };

        /// <summary>The live-services overload of the kind-dispatched gate — what
        /// <see cref="VehicleController"/> calls each physics tick.</summary>
        public static float SpeedCapNow(VehicleKind kind, Vector2 origin, Vector2 nose, float speed,
                                        float frontAxleY, float rearAxleY,
                                        float brakingMetersPerSecondSquared, float wheelRadiusMeters)
        {
            IGameClock clock = GameServices.Clock;
            double now = clock != null ? clock.TotalSeconds : 0.0;
            return SpeedCapMetersPerSecond(kind, GameServices.TidalTerrain, GameServices.Environment, now,
                                           origin, nose, speed, frontAxleY, rearAxleY,
                                           brakingMetersPerSecondSquared, wheelRadiusMeters);
        }

        // =================================================================================================
        //  AFLOAT — the state that lives on the MACHINE
        // =================================================================================================

        /// <summary>
        /// ⭐ <b>Is she swimming?</b> The medium she is in, from the same deterministic depth every other
        /// rule here reads, with a dead band so the water's edge is a line she crosses rather than one she
        /// flickers on.
        ///
        /// <para><b>Hysteresis, and why it is not optional.</b> The threshold is her float draft — the depth
        /// at which her hull genuinely takes her weight. A naked comparison against it swaps her medium on
        /// any wobble of a number that really does wobble: the tide moves continuously, she is moving, and
        /// the ground under her is authored per texel. Two thresholds a few centimetres apart turn that into
        /// one transition each way. She floats past <c>draft + band</c> and takes the beach again below
        /// <c>draft − band</c>, so the two can never be satisfied at once and the state has no chatter
        /// mode.</para>
        ///
        /// <para><b>Deterministic (rule 5).</b> Nothing here is saved and nothing is random: the depth is
        /// <c>WaterLevelAt(t) − ElevationAt(pos)</c>, recomputed from <c>(worldSeed, gameTime)</c>, and the
        /// decision is a pure function of it, her published draft, her tuned band, and the state she was in.
        /// Evaluated twice at the same <c>(seed, time, position)</c> from the same prior state it returns
        /// the same bit, which is the arm the fixture pins.</para>
        ///
        /// <para><b>No map, no water.</b> An unwired terrain reads <see cref="float.NegativeInfinity"/>
        /// (see <see cref="DepthAt"/>), which is never past any draft — so an EditMode fixture, an art
        /// scene and a region with no authored tidal ground all leave her on her wheels, which is the safe
        /// answer and the same self-disabling the road gate has.</para>
        /// </summary>
        /// <param name="wasAfloat">The state she is in NOW — the hysteresis needs it.</param>
        /// <param name="floatDraftMeters">Her published draft afloat
        /// (<see cref="VehicleMeshDef.FloatDraftMeters"/>); 0 = a machine that does not swim.</param>
        /// <param name="hysteresisMeters">Half the dead band
        /// (<see cref="VehicleDef.SwimHysteresisMeters"/>).</param>
        public static bool IsAfloat(VehicleKind kind, bool wasAfloat, ITidalTerrain terrain,
                                    IEnvironmentService environment, double totalSeconds,
                                    Vector2 worldPos, float floatDraftMeters,
                                    float hysteresisMeters) => kind switch
        {
            // Water is a wall for her, and a wall is not a state. The road gate already stopped her
            // short of it — this is the second lock on the same door, and it is what a sabotage test
            // aims at: nothing in this PR may let the truck float.
            VehicleKind.RoadVehicle => false,

            VehicleKind.AmphibiousVehicle => IsAfloatAtDepth(
                wasAfloat, DepthAt(terrain, environment, totalSeconds, worldPos),
                floatDraftMeters, hysteresisMeters),

            _ => throw new ArgumentOutOfRangeException(
                     nameof(kind), kind,
                     "VehicleKind has grown a member with no flotation rule. Name the arm."),
        };

        /// <summary>
        /// The hysteresis itself, on a depth already read — pure, so the band's behaviour is pinned as
        /// arithmetic rather than felt for at a shoreline.
        ///
        /// <para>Strict <c>&gt;=</c> going in and strict <c>&gt;</c> coming out, so a machine sitting
        /// exactly at <c>draft − band</c> takes the beach rather than hovering: an exact tie must resolve
        /// toward the state that cannot strand anybody.</para>
        /// </summary>
        public static bool IsAfloatAtDepth(bool wasAfloat, float depthMeters, float floatDraftMeters,
                                           float hysteresisMeters)
        {
            if (floatDraftMeters <= 0f) return false;   // she has no flotation data: she does not swim
            float band = Mathf.Max(0f, hysteresisMeters);
            return wasAfloat
                ? depthMeters > floatDraftMeters - band
                : depthMeters >= floatDraftMeters + band;
        }

        /// <summary>Convenience over the live Core services at the current clock time — what
        /// <see cref="VehicleController"/> calls each physics tick.</summary>
        public static bool IsAfloatNow(VehicleKind kind, bool wasAfloat, Vector2 worldPos,
                                       float floatDraftMeters, float hysteresisMeters)
        {
            IGameClock clock = GameServices.Clock;
            double now = clock != null ? clock.TotalSeconds : 0.0;
            return IsAfloat(kind, wasAfloat, GameServices.TidalTerrain, GameServices.Environment, now,
                            worldPos, floatDraftMeters, hysteresisMeters);
        }

        /// <summary>
        /// <b>The SHARED heave, for something that is not a boat</b> — the displaced sea's lift under a
        /// world point, in metres, by the one rule every displaced-water consumer draws with.
        ///
        /// <para>Character-for-character the read <c>BoatWaveMotion</c> makes for a hull, reached entirely
        /// through Core (rule 4 — Vehicles may not call into Boats, and does not want to: what it needs is
        /// <see cref="SharedWaveField"/>, <see cref="DisplacedSea"/> and <see cref="ShoreFadeMath"/>, all of
        /// which are already the shared seam the two feature modules meet at). Three details are
        /// load-bearing and all three are borrowed rather than re-invented:</para>
        /// <list type="bullet">
        ///   <item><b>The PUBLISHED trains, not a local animator.</b> The animator accumulates travel phase
        ///   from zero at its own first tick, so a second one seeded at a different moment holds the same
        ///   wave at the wrong moment — a machine bobbing up as the drawn water goes down.</item>
        ///   <item><b>Sampled at the published FREQUENCY SCALE</b> (position × <c>FreqScale</c>), because
        ///   the surface's vertex stage draws the field at 2.8 at the owner's tuned materials. Sampling at 1
        ///   rides a different wave from the one drawn around her — the "boats are nearly submerged"
        ///   defect.</item>
        ///   <item><b>At <c>timeSeconds = 0</c></b>: the accumulated travel already rides in each train's
        ///   phase offset. A real game time would add it twice.</item>
        /// </list>
        ///
        /// <para><b>Nothing published ⇒ exactly 0</b> — no bridge, no displaced sea, an EditMode fixture, an
        /// edit-time builder: she sits at her flat waterline and the render is byte-identical to a machine
        /// with no flotation at all. That is the same A/B contract the boat fleet ships under.</para>
        /// </summary>
        public static float SharedSeaRideMeters(ITidalTerrain terrain, IEnvironmentService environment,
                                                double totalSeconds, Vector2 worldPos)
        {
            if (!DisplacedSea.TryGet(out DisplacedSeaState sea)) return 0f;
            if (!SharedWaveField.TryGet(out WaveTrains trains) || trains.Count == 0) return 0f;

            // The wind-fetch envelope is resolved at the TRUE position — fetch is a real distance over
            // real water, and marching the frequency-scaled one would shelter her behind a headland that
            // is not there.
            float fetch = GameServices.FetchEnvelopeAt(worldPos);
            float height = WaveMath.Sample(worldPos * sea.FreqScale, 0.0, in trains, fetch).Height;
            float depth = DepthAt(terrain, environment, totalSeconds, worldPos);
            return ShoreFadeMath.DisplacedHeight(height, depth, sea.ShoreFadeBandMeters, sea.Exaggeration);
        }

        /// <summary>The live-services overload — what <see cref="VehicleMeshDriver"/> calls each
        /// LateUpdate while she is afloat.</summary>
        public static float SharedSeaRideMetersNow(Vector2 worldPos)
        {
            IGameClock clock = GameServices.Clock;
            double now = clock != null ? clock.TotalSeconds : 0.0;
            return SharedSeaRideMeters(GameServices.TidalTerrain, GameServices.Environment, now, worldPos);
        }
    }
}
