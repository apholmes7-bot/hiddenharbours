using UnityEngine;

namespace HiddenHarbours.App
{
    /// <summary>
    /// <b>THE HAND ON THE HELM DURING THE OPENING</b> — a pure function from (where she is, where she is
    /// pointing, where she is going) to the two numbers a helm has: throttle and steer.
    ///
    /// <para><b>⭐ WHY THIS IS A STATIC AND NOT A COMPONENT.</b> The arrival must run the same way every
    /// new game, and "the same way" has to mean something a test can hold. It cannot mean "the boat ends
    /// in the same pixel": she is a rigidbody on a moving sea, and the sea is recomputed from
    /// <c>(worldSeed, gameTime)</c> — a new game that starts at a different instant floats her
    /// differently, correctly. What CAN be deterministic, and what actually matters, is the COMMAND: the
    /// same pose against the same waypoint must always produce the same helm. That is this function, and
    /// it takes no clock, no random, no state of its own, so an EditMode test can pin it exactly.</para>
    ///
    /// <para><b>And why it drives <see cref="HiddenHarbours.Boats.BoatController.SetControl"/> rather
    /// than the transform.</b> A scripted approach that moved the hull directly would be a second boat
    /// physics — one that does not heel, does not lose rudder authority as it slows, does not feel the
    /// wave field, and would drift out of agreement with the one the player takes over. The opening is
    /// the first thing anyone sees of how this game's boats move; it had better be how they move. So the
    /// sequencer holds the same helm the player is about to be handed.</para>
    ///
    /// <para><b>The approach in one sentence:</b> point at the next mark, hold cruise while there is
    /// room, then hold the speed the remaining distance can absorb — going ASTERN if she is carrying
    /// more way than that. It closes a loop on SPEED rather than ramping a throttle, and that is not a
    /// refinement, it is the only thing that works: this hull's time constant is twenty seconds, so
    /// coasting to rest from her cruise takes the better part of two hundred metres — longer than the
    /// whole approach. A throttle ramp arrives at the wharf still making way. A skipper goes astern.</para>
    /// </summary>
    public static class ArrivalPilot
    {
        /// <summary>What a helm is: two numbers in −1..1. The same pair
        /// <see cref="HiddenHarbours.Boats.BoatController.SetControl"/> takes from the player.</summary>
        public readonly struct Helm
        {
            public readonly float Throttle;
            public readonly float Steer;
            public Helm(float throttle, float steer)
            {
                Throttle = Mathf.Clamp(throttle, -1f, 1f);
                Steer = Mathf.Clamp(steer, -1f, 1f);
            }
            public override string ToString() => $"throttle {Throttle:F3}, steer {Steer:F3}";
        }

        /// <summary>
        /// The skipper's tuning — every number the approach has, in one place and none of them in the
        /// code (rule 6). Authored on the sequencer so the owner can retune the pacing without a
        /// recompile, which is exactly the verdict the handoff asks him for ("too slow / too fast").
        /// </summary>
        [System.Serializable]
        public struct Settings
        {
            [Tooltip("The speed she runs the long legs at, in m/s. ⭐ A SPEED and not a throttle: a " +
                     "throttle is a statement about an engine and the owner's verdict is about how fast " +
                     "the arrival FEELS, which is metres per second. 3 m/s is about six knots — harbour " +
                     "speed for a working boat coming in.")]
            [Min(0.1f)] public float CruiseSpeedMetresPerSecond;

            [Tooltip("How hard she takes the way off, in m/s². The target speed on the approach is " +
                     "the speed she could still stop from at this rate — √(2·a·d) — capped at the " +
                     "cruise. ⚠ A DECELERATION and not a speed-per-metre: a target proportional to " +
                     "distance decays exponentially and reaches the berth only in the limit, so the " +
                     "boat crawls the last few metres forever. Constant deceleration arrives, and it " +
                     "is also what a docking actually looks like. Must stay under what her engine " +
                     "can give astern (0.42 m/s² for the cape islander) or she cannot hold the curve.")]
            [Min(0.01f)] public float ApproachDecelMetresPerSecondSquared;

            [Tooltip("Throttle per m/s of speed error. This is what puts her ASTERN when she is running " +
                     "too fast for the distance left, which she must be able to do: with a six-tonne " +
                     "hull against this drag the coast-to-a-stop from cruise is the better part of two " +
                     "hundred metres, so an approach that only ever eases the throttle cannot stop at a " +
                     "wharf at all. A skipper goes astern; so does this.")]
            [Min(0.01f)] public float ThrottlePerSpeedError;

            [Tooltip("Inside this distance (m) from the berth she is simply stopping — target speed " +
                     "zero, astern if she still carries way.")]
            [Min(0f)] public float StopMetres;

            [Tooltip("Helm per degree of bearing error. Small: a big gain makes her fishtail down a " +
                     "channel she is already lined up with.")]
            [Min(0f)] public float SteerPerDegree;

            [Tooltip("A waypoint is REACHED inside this radius (m). Wider than it looks it should be — " +
                     "a hull with way on does not stop on a point, and a mark she can never quite touch " +
                     "is a mark she circles.")]
            [Min(0.1f)] public float ArriveRadiusMetres;

            /// <summary>
            /// The pacing the arrival ships at, and every number of it is measured against THIS HULL
            /// rather than chosen.
            ///
            /// <para>⚠ <b>Why a speed controller and not a throttle ramp, in numbers.</b> The cape
            /// islander models at 60 kg against a forward drag of 3 N per m/s, so her time constant is
            /// <c>m/k = 20 s</c> — she is a very slippery object. Coasting to rest from her cruise takes
            /// <c>v·τ ≈ 190 m</c>, which is longer than the entire approach. A throttle ramp therefore
            /// CANNOT bring her alongside; it arrives at the wharf still making way. What stops a boat
            /// is her propeller turning the other way.</para>
            ///
            /// <para><b>And the numbers that fall out of 3 m/s at 0.3 m/s²:</b> she holds cruise until
            /// <c>v²/2a + StopMetres = 18 m</c> off the berth, then takes ten seconds and fifteen
            /// metres to walk it off — which fits inside the 49 m dredged channel with the turn onto
            /// it to spare. Her engine can give 0.42 m/s² astern, so 0.3 is a curve she can actually
            /// hold rather than one she saturates against.</para>
            /// </summary>
            public static Settings Default => new Settings
            {
                CruiseSpeedMetresPerSecond = 3f,
                ApproachDecelMetresPerSecondSquared = 0.3f,
                ThrottlePerSpeedError = 0.8f,
                StopMetres = 2f,
                SteerPerDegree = 0.05f,
                ArriveRadiusMetres = 4f,
            };
        }

        /// <summary>
        /// The helm for this instant. <b>Pure</b> — same arguments, same answer, forever.
        /// </summary>
        /// <param name="position">Where she is (world XY).</param>
        /// <param name="headingDegrees">Where her bow points, COMPASS (0 = north, clockwise) — the same
        /// frame <c>MooredBoat</c> and <c>CoastPlan.BearingAt</c> use, so nothing has to convert.</param>
        /// <param name="wayMetresPerSecond">Her speed THROUGH the water along the bow — the number the
        /// throttle is closing the loop on. Signed, so a boat gathering sternway is told to go ahead.</param>
        /// <param name="target">The mark she is steering for (world XY).</param>
        /// <param name="metresToBerth">How far she still has to run ALONG THE ROUTE to the berth — not
        /// the straight-line distance to it, which on a channel with a turn in it would have her taking
        /// the way off while she is still outside the entrance.</param>
        public static Helm Command(Vector2 position, float headingDegrees, float wayMetresPerSecond,
                                   Vector2 target, float metresToBerth, Settings settings)
        {
            float steer = Mathf.Clamp(
                SignedBearingError(position, headingDegrees, target) * settings.SteerPerDegree, -1f, 1f);

            float wanted = TargetSpeed(metresToBerth, settings);
            float throttle = (wanted - wayMetresPerSecond) * settings.ThrottlePerSpeedError;

            return new Helm(throttle, steer);
        }

        /// <summary>
        /// How fast she should be going with <paramref name="metresToBerth"/> still to run: cruise out
        /// on the legs, then the speed she could still stop from at her chosen deceleration, then
        /// nothing. Exposed because it is the whole PACING of the arrival in one line, and the
        /// owner's verdict lands on this curve.
        /// </summary>
        public static float TargetSpeed(float metresToBerth, Settings settings)
        {
            float run = metresToBerth - settings.StopMetres;
            if (run <= 0f) return 0f;
            return Mathf.Min(settings.CruiseSpeedMetresPerSecond,
                             Mathf.Sqrt(2f * settings.ApproachDecelMetresPerSecondSquared * run));
        }

        /// <summary>
        /// Degrees she must turn to point at <paramref name="target"/>: positive = to starboard, which is
        /// positive helm (<c>BoatController.RudderTorque</c>: "positive helm → bow right"). Wrapped to
        /// ±180 so a mark two degrees off the bow is never chased the long way round.
        /// </summary>
        public static float SignedBearingError(Vector2 position, float headingDegrees, Vector2 target)
        {
            Vector2 to = target - position;
            if (to.sqrMagnitude < 1e-8f) return 0f;
            return Wrap180(CompassOf(to) - headingDegrees);
        }

        /// <summary>Compass bearing (0 = north, clockwise) of a direction vector — the ONE conversion,
        /// so the pilot, the sequencer and the moored heading cannot come to mean different things by
        /// "heading".</summary>
        public static float CompassOf(Vector2 direction) =>
            Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;

        /// <summary>Compass heading of a transform's forward (<c>transform.up</c>, which is a 2D hull's
        /// bow).</summary>
        public static float HeadingOf(Transform t) =>
            t == null ? 0f : CompassOf(new Vector2(t.up.x, t.up.y));

        /// <summary>Fold an angle into −180..180.</summary>
        public static float Wrap180(float degrees)
        {
            degrees = Mathf.Repeat(degrees + 180f, 360f) - 180f;
            return degrees;
        }

        /// <summary>
        /// How far she still has to run along <paramref name="route"/> from <paramref name="leg"/>'s
        /// start, given she is at <paramref name="position"/> — the distance the throttle ease is
        /// measured against. Straight-line-to-berth would be wrong on a route with a turn in it: she
        /// would start slowing while still outside the entrance, and arrive with no way on to steer with.
        /// </summary>
        public static float MetresToBerth(Vector2 position, Vector2[] route, int leg)
        {
            if (route == null || route.Length == 0) return 0f;
            leg = Mathf.Clamp(leg, 0, route.Length - 1);
            float d = Vector2.Distance(position, route[leg]);
            for (int i = leg; i < route.Length - 1; i++) d += Vector2.Distance(route[i], route[i + 1]);
            return d;
        }
    }
}
