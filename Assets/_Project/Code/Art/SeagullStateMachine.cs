using System;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// One bird's whole simulated state. A plain mutable struct on purpose: <see cref="GullFlock"/>
    /// keeps an array of these and steps them in place, so a flock costs one allocation at Awake and
    /// none per frame (rule 7).
    ///
    /// <para>Positions are METRES on the world plane. <see cref="AltitudeMetres"/> is height above the
    /// surface under the pivot — the presenter turns it into a SCREEN OFFSET and never into a scale
    /// (owner, 2026-09-09: there are no giant gulls).</para>
    /// </summary>
    public struct SeagullSimBird
    {
        /// <summary>Index into <see cref="SeagullStates.Order"/>.</summary>
        public int State;
        /// <summary>Milliseconds elapsed inside <see cref="State"/>.</summary>
        public double StateMilliseconds;
        /// <summary>Frame within the state's strip.</summary>
        public int Frame;
        /// <summary>Completed loops, for a state that declares <c>cycles</c>.</summary>
        public int Cycle;
        /// <summary>Loops to run before handing back to <see cref="ReturnState"/>; 0 = loop forever.</summary>
        public int CycleTarget;
        /// <summary>Where a cycles-limited state hands back to (<c>float</c>, for preen and peck).</summary>
        public int ReturnState;

        /// <summary>World-plane position, metres.</summary>
        public double X, Y;
        /// <summary>Height above the surface under the pivot, metres.</summary>
        public double AltitudeMetres;
        /// <summary>Travel direction, radians, in the rig's <c>atan2(vx, vy)</c> convention.</summary>
        public double Heading;
        /// <summary>Sheet facing row, 0..7.</summary>
        public int Dir;

        /// <summary>True while the flock wheel owns this bird's position, altitude and anim.</summary>
        public bool FlockBound;
        /// <summary>Milliseconds into the rejoin ramp after a takeoff; see
        /// <see cref="SeagullStateMachine.RejoinBlend01"/>.</summary>
        public double RejoinMilliseconds;

        /// <summary>The arrival state (<c>land</c> or <c>splash</c>) this bird has been commanded to,
        /// or −1.</summary>
        public int IntentState;
        /// <summary>Where the descent began — the fixed end of the path lerp.</summary>
        public double AlightFromX, AlightFromY, AlightFromAltitude;
        /// <summary>The downwind point the bird turns at, so the run-in is flown into the wind.</summary>
        public double AlightApproachX, AlightApproachY;
        /// <summary>The spot on the ground or water the bird is coming down on.</summary>
        public double AlightTargetX, AlightTargetY;
        /// <summary>Fraction of the descent spent on the into-wind run-in, 0..1.</summary>
        public double AlightRunInFraction;
    }

    /// <summary>
    /// <b>THE BIRD'S STATE MACHINE — a pure function of (bird, dt, table).</b>
    ///
    /// <para>Nothing here reads a clock, allocates, or touches <c>UnityEngine</c>: the caller passes
    /// the elapsed milliseconds it got from <c>GameServices.Clock</c>, and the same inputs always
    /// produce the same bird (rule 5). Two runs on one seed agreeing is a TEST, not a hope.</para>
    ///
    /// <para><b>Altitude has exactly two owners.</b> While a bird is flock-bound, the wheel owns it —
    /// <see cref="SeagullFlockMath.Flock"/>'s <c>z</c> is adopted verbatim, INCLUDING where it dips
    /// below a state's advisory band (the rig happily flies at 3 m in a state the sidecar bands at
    /// 6-25; the approved motion wins over the advisory number). Every other state integrates
    /// <c>vz</c> and clamps to its own band. There is no third path and no blend except the one
    /// documented at <see cref="RejoinBlend01"/>.</para>
    ///
    /// <para><b>Coming down is a PLAN, not an edge.</b> A bird at 14 m cannot simply be told "land":
    /// <c>land</c> is entered at 0.6 m and only from <c>swoop</c> or <c>glide</c>. So
    /// <see cref="CommandAlight"/> records a descent — where the bird started, the downwind point it
    /// turns at, and the spot itself — and the bird flies it: it takes the legal edge into a
    /// descending state, loses height at that state's own <c>vz</c>, and closes on the spot in step
    /// with the height it has left. At the bottom the two arrive together, so the edge into
    /// <c>land</c> is taken with the pivot ALREADY on the spot and the altitude ALREADY at the entry
    /// height. No snap, and no shadow sliding out from under the bird.</para>
    ///
    /// <para><b>The arrival is vertical.</b> The sidecar gives <c>land</c> 0.5 m and <c>splash</c> 0.6 m
    /// of travel — the flare's forward carry. We spend that on the RUN-IN instead and pin the pivot for
    /// the length of the arrival state, because the thing the player is watching is the shadow, and a
    /// shadow that slides half a metre while the bird settles onto it reads as a miss. The bird
    /// descends onto a stationary shadow; that is the acceptance test.</para>
    /// </summary>
    public static class SeagullStateMachine
    {
        /// <summary>Below this many metres two altitudes are the same altitude. A tenth of a pixel at
        /// 32 px/m — finer than anything that can be drawn.</summary>
        public const double AltitudeEpsilonMetres = 0.003125;

        // ── stepping ────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Advances one bird by <paramref name="deltaMilliseconds"/>.
        /// </summary>
        /// <param name="bird">The bird, stepped in place.</param>
        /// <param name="deltaMilliseconds">Elapsed sim time. Non-positive is a no-op.</param>
        /// <param name="behaviour">The state table.</param>
        /// <param name="flockSample">Where the wheel would have this bird right now. Read when the
        /// bird is flock-bound or rejoining; ignored otherwise.</param>
        /// <param name="flockCentreX">World-plane X of the flock's centre, metres — the wheel's
        /// offsets are relative to it.</param>
        /// <param name="flockCentreY">World-plane Y of the flock's centre, metres.</param>
        public static void Step(ref SeagullSimBird bird, double deltaMilliseconds,
                                SeagullBehaviour behaviour,
                                in SeagullFlockMath.SeagullFlockBird flockSample,
                                double flockCentreX, double flockCentreY)
        {
            if (behaviour == null || deltaMilliseconds <= 0.0) return;

            double wheelX = flockCentreX + flockSample.X;
            double wheelY = flockCentreY + flockSample.Y;

            if (bird.FlockBound)
            {
                AdoptWheel(ref bird, behaviour, flockSample, wheelX, wheelY);
                return;
            }

            bird.StateMilliseconds += deltaMilliseconds;
            double dtSeconds = deltaMilliseconds / 1000.0;

            AdvanceAltitudeAndPosition(ref bird, behaviour, dtSeconds);
            AdvanceFrameAndChain(ref bird, behaviour);

            // A bird that has left the ground rejoins the wheel over the sidecar's regroup time — the
            // same number the rig gives a scattered flock to reform. takeoff leaves off at ≤1.2 m and
            // the wheel cruises at 3-14 m; without the ramp that gap is a pop.
            if (bird.RejoinMilliseconds > 0.0)
            {
                double ramp = RejoinMillisecondsTotal(behaviour);
                bird.RejoinMilliseconds += deltaMilliseconds;
                double u = ramp <= 0.0 ? 1.0 : bird.RejoinMilliseconds / ramp;
                if (u >= 1.0)
                {
                    bird.RejoinMilliseconds = 0.0;
                    bird.FlockBound = true;
                    AdoptWheel(ref bird, behaviour, flockSample, wheelX, wheelY);
                }
                else
                {
                    bird.X += (wheelX - bird.X) * u;
                    bird.Y += (wheelY - bird.Y) * u;
                    bird.AltitudeMetres += (flockSample.Z - bird.AltitudeMetres) * u;
                    bird.Dir = SeagullFlockMath.DirOf(bird.Heading);
                }
            }
        }

        /// <summary>How far through the post-takeoff rejoin ramp a bird is, 0..1. The presenter needs
        /// it for nothing; it is here so a test can see the ramp rather than infer it.</summary>
        public static double RejoinBlend01(in SeagullSimBird bird, SeagullBehaviour behaviour)
        {
            if (bird.FlockBound) return 1.0;
            if (bird.RejoinMilliseconds <= 0.0) return 0.0;
            double ramp = RejoinMillisecondsTotal(behaviour);
            if (ramp <= 0.0) return 1.0;
            double u = bird.RejoinMilliseconds / ramp;
            return u < 0.0 ? 0.0 : u > 1.0 ? 1.0 : u;
        }

        private static double RejoinMillisecondsTotal(SeagullBehaviour behaviour)
            => behaviour.Flock.RegroupSeconds * 1000.0;

        private static void AdoptWheel(ref SeagullSimBird bird, SeagullBehaviour behaviour,
                                       in SeagullFlockMath.SeagullFlockBird sample,
                                       double wheelX, double wheelY)
        {
            bird.State = behaviour.StateOf(sample.Anim);
            bird.X = wheelX;
            bird.Y = wheelY;
            bird.AltitudeMetres = sample.Z;
            bird.Heading = sample.Heading;
            bird.Dir = sample.Dir;
            bird.Frame = sample.Frame;
            bird.IntentState = -1;
            bird.CycleTarget = 0;
        }

        private static void AdvanceAltitudeAndPosition(ref SeagullSimBird bird,
                                                       SeagullBehaviour behaviour, double dtSeconds)
        {
            int s = bird.State;
            var row = behaviour.Row(s);

            if (behaviour.SurfaceOf(s) != SeagullSurface.None)
            {
                // On a surface the pivot IS the surface. Altitude is exactly zero, not "about zero" —
                // the shadow test reads this number.
                bird.AltitudeMetres = 0.0;
                if (row.SpeedMetresPerSecond > 0.0)
                {
                    bird.X += Math.Sin(bird.Heading) * row.SpeedMetresPerSecond * dtSeconds;
                    bird.Y += Math.Cos(bird.Heading) * row.SpeedMetresPerSecond * dtSeconds;
                }
                bird.Dir = SeagullFlockMath.DirOf(bird.Heading);
                return;
            }

            bool arriving = behaviour.ArrivesOnSurface(s);
            double floorMetres = behaviour.AltitudeMin(s);
            double ceilMetres = behaviour.AltitudeMax(s);

            // A commanded descent treats the state's band floor as a CRUISE floor, not a deck: the bird
            // is allowed all the way down to the altitude its arrival state is entered at, so the two
            // meet with no step. Without this a swoop stops at its 1 m floor and land's 0.6 m entry
            // becomes a 10-pixel jump.
            if (bird.IntentState >= 0)
            {
                double entry = behaviour.EntryAltitude(bird.IntentState);
                if (entry < floorMetres) floorMetres = entry;
            }

            if (row.ClimbMetresPerSecond != 0.0)
                bird.AltitudeMetres += row.ClimbMetresPerSecond * dtSeconds;
            if (bird.AltitudeMetres < floorMetres) bird.AltitudeMetres = floorMetres;
            if (bird.AltitudeMetres > ceilMetres) bird.AltitudeMetres = ceilMetres;

            if (arriving)
            {
                // The flare's travel was spent on the run-in — the pivot does not move while the bird
                // comes down onto it. See the class remarks.
                bird.X = bird.AlightTargetX;
                bird.Y = bird.AlightTargetY;
                bird.Dir = SeagullFlockMath.DirOf(bird.Heading);
                return;
            }

            if (bird.IntentState >= 0)
            {
                StepAlightPath(ref bird, behaviour);
                return;
            }

            if (row.SpeedMetresPerSecond > 0.0)
            {
                bird.X += Math.Sin(bird.Heading) * row.SpeedMetresPerSecond * dtSeconds;
                bird.Y += Math.Cos(bird.Heading) * row.SpeedMetresPerSecond * dtSeconds;
            }
            bird.Dir = SeagullFlockMath.DirOf(bird.Heading);
        }

        /// <summary>
        /// Closes the bird on its landing spot in step with the height it has left, so the descent and
        /// the approach finish together. The path has two legs — a long one to the downwind turn point
        /// and a short one straight into the wind — and the heading comes from the leg the bird is
        /// actually on, which is why the run-in is into the wind without anyone steering it there.
        /// </summary>
        private static void StepAlightPath(ref SeagullSimBird bird, SeagullBehaviour behaviour)
        {
            double entry = behaviour.EntryAltitude(bird.IntentState);
            double span = bird.AlightFromAltitude - entry;
            double p = span <= AltitudeEpsilonMetres
                ? 1.0
                : 1.0 - (bird.AltitudeMetres - entry) / span;
            if (p < 0.0) p = 0.0; else if (p > 1.0) p = 1.0;

            double q = bird.AlightRunInFraction;
            if (q < 0.0) q = 0.0; else if (q > 1.0) q = 1.0;
            double turn = 1.0 - q;

            double x, y, dx, dy;
            if (p <= turn && turn > 0.0)
            {
                double u = p / turn;
                x = bird.AlightFromX + (bird.AlightApproachX - bird.AlightFromX) * u;
                y = bird.AlightFromY + (bird.AlightApproachY - bird.AlightFromY) * u;
                dx = bird.AlightApproachX - bird.AlightFromX;
                dy = bird.AlightApproachY - bird.AlightFromY;
            }
            else
            {
                double u = q <= 0.0 ? 1.0 : (p - turn) / q;
                if (u < 0.0) u = 0.0; else if (u > 1.0) u = 1.0;
                x = bird.AlightApproachX + (bird.AlightTargetX - bird.AlightApproachX) * u;
                y = bird.AlightApproachY + (bird.AlightTargetY - bird.AlightApproachY) * u;
                dx = bird.AlightTargetX - bird.AlightApproachX;
                dy = bird.AlightTargetY - bird.AlightApproachY;
            }

            bird.X = x;
            bird.Y = y;
            if (dx * dx + dy * dy > 1e-12) bird.Heading = Math.Atan2(dx, dy);
            bird.Dir = SeagullFlockMath.DirOf(bird.Heading);
        }

        private static void AdvanceFrameAndChain(ref SeagullSimBird bird, SeagullBehaviour behaviour)
        {
            var row = behaviour.Row(bird.State);
            if (row.Frames <= 0 || row.FrameMilliseconds <= 0.0) { bird.Frame = 0; return; }

            int elapsedFrames = (int)Math.Floor(bird.StateMilliseconds / row.FrameMilliseconds);

            if (row.Loop)
            {
                bird.Frame = elapsedFrames % row.Frames;
                int cycles = elapsedFrames / row.Frames;
                bird.Cycle = cycles;
                if (bird.CycleTarget > 0 && cycles >= bird.CycleTarget &&
                    behaviour.CanGo(bird.State, bird.ReturnState))
                {
                    Enter(ref bird, bird.ReturnState, behaviour);
                }
                return;
            }

            if (elapsedFrames < row.Frames) { bird.Frame = elapsedFrames; return; }

            int next = behaviour.Next(bird.State);
            if (next >= 0 && behaviour.CanGo(bird.State, next))
            {
                // Carry the overrun so a long tick cannot lose time at a chain boundary.
                double overrun = bird.StateMilliseconds - row.DurationMilliseconds;
                Enter(ref bird, next, behaviour);
                if (overrun > 0.0)
                {
                    bird.StateMilliseconds = overrun;
                    var nrow = behaviour.Row(bird.State);
                    if (nrow.Frames > 0 && nrow.FrameMilliseconds > 0.0)
                    {
                        int f = (int)Math.Floor(overrun / nrow.FrameMilliseconds);
                        bird.Frame = nrow.Loop ? f % nrow.Frames : Math.Min(f, nrow.Frames - 1);
                    }
                }
                return;
            }

            bird.Frame = row.Frames - 1;   // no chain target: hold the last frame
        }

        // ── commands ────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Takes a declared edge immediately. Returns false — and changes nothing — when the table does
        /// not permit it, which is how a caller finds out it was wrong rather than by watching a gull
        /// teleport into a pose it cannot reach.
        /// </summary>
        public static bool TryTransition(ref SeagullSimBird bird, int to, SeagullBehaviour behaviour)
        {
            if (behaviour == null || !behaviour.CanGo(bird.State, to)) return false;
            int from = bird.State;
            Enter(ref bird, to, behaviour);
            if (behaviour.Row(to).CyclesMax > 0) bird.ReturnState = from;
            return true;
        }

        /// <summary>
        /// Commands the bird down onto a spot. <paramref name="arrivalState"/> is <c>land</c> for
        /// ground and <c>splash</c> for water — the caller resolves which, because only it knows what
        /// is under the pivot.
        ///
        /// <para>The wind is the SHARED wind: pass the heading the wind BLOWS TOWARD, in the rig's
        /// convention. The bird turns downwind of the spot and runs in against it, because the sidecar
        /// says <c>approach_into_wind</c> and because a gull that lands downwind overshoots.</para>
        /// </summary>
        /// <returns>False when the state table cannot get there from here at all — a bird already on
        /// the ground, for instance.</returns>
        public static bool CommandAlight(ref SeagullSimBird bird, int arrivalState,
                                         double targetX, double targetY, double windBlowsTowardHeading,
                                         SeagullBehaviour behaviour)
        {
            if (behaviour == null || !behaviour.ArrivesOnSurface(arrivalState)) return false;
            if (behaviour.SurfaceOf(bird.State) != SeagullSurface.None) return false;

            bird.FlockBound = false;
            bird.RejoinMilliseconds = 0.0;
            bird.IntentState = arrivalState;

            double runIn = ApproachRunMetres(behaviour, arrivalState);
            // Downwind of the spot by the run-in, so the last leg is flown straight into the wind.
            double wx = Math.Sin(windBlowsTowardHeading);
            double wy = Math.Cos(windBlowsTowardHeading);
            bird.AlightTargetX = targetX;
            bird.AlightTargetY = targetY;
            bird.AlightApproachX = targetX + wx * runIn;
            bird.AlightApproachY = targetY + wy * runIn;
            bird.AlightFromX = bird.X;
            bird.AlightFromY = bird.Y;
            bird.AlightFromAltitude = bird.AltitudeMetres;

            double legX = bird.AlightApproachX - bird.X;
            double legY = bird.AlightApproachY - bird.Y;
            double lead = Math.Sqrt(legX * legX + legY * legY);
            double total = lead + runIn;
            bird.AlightRunInFraction = total <= 1e-9 ? 1.0 : runIn / total;

            // Get into a state that actually loses height. swoop is the steep one and is reachable from
            // both cruise states; if we are already descending, stay put.
            int swoop = behaviour.IndexOf(SeagullStates.Swoop);
            if (!behaviour.CanGo(bird.State, arrivalState) && behaviour.CanGo(bird.State, swoop))
                Enter(ref bird, swoop, behaviour);

            return true;
        }

        /// <summary>
        /// Commands a bird off a surface. Legal from <c>stand</c>, <c>walk</c>, <c>perch</c> and
        /// <c>float</c>; the chain carries it to <c>fly</c> and the rejoin ramp puts it back on the
        /// wheel.
        /// </summary>
        public static bool CommandDepart(ref SeagullSimBird bird, SeagullBehaviour behaviour)
        {
            if (behaviour == null) return false;
            int takeoff = behaviour.IndexOf(SeagullStates.Takeoff);
            if (!behaviour.CanGo(bird.State, takeoff)) return false;
            bird.IntentState = -1;
            Enter(ref bird, takeoff, behaviour);
            return true;
        }

        /// <summary>
        /// Should the plan hand over yet? True once the bird is on its spot and down to the arrival
        /// state's entry height, with a legal edge to take.
        /// </summary>
        public static bool ReadyToAlight(in SeagullSimBird bird, SeagullBehaviour behaviour)
        {
            if (bird.IntentState < 0 || behaviour == null) return false;
            if (!behaviour.CanGo(bird.State, bird.IntentState)) return false;
            double entry = behaviour.EntryAltitude(bird.IntentState);
            return bird.AltitudeMetres <= entry + AltitudeEpsilonMetres;
        }

        /// <summary>Takes the arrival edge when <see cref="ReadyToAlight"/>. Call it after
        /// <see cref="Step"/>; it is separate so a caller can see the moment rather than have it happen
        /// inside the integrator.</summary>
        public static bool TryAlight(ref SeagullSimBird bird, SeagullBehaviour behaviour)
        {
            if (!ReadyToAlight(in bird, behaviour)) return false;
            int arrival = bird.IntentState;
            Enter(ref bird, arrival, behaviour);
            bird.X = bird.AlightTargetX;
            bird.Y = bird.AlightTargetY;
            bird.AltitudeMetres = behaviour.EntryAltitude(arrival);
            return true;
        }

        /// <summary>
        /// How much ground the into-wind run-in covers: the distance the descending state carries the
        /// bird while it loses the last of its height, plus the flare's own travel. Derived from the
        /// table rather than picked, so retuning <c>swoop</c> retunes the approach with it.
        /// </summary>
        public static double ApproachRunMetres(SeagullBehaviour behaviour, int arrivalState)
        {
            int swoop = behaviour.IndexOf(SeagullStates.Swoop);
            var s = behaviour.Row(swoop);
            double drop = behaviour.AltitudeMin(swoop) - behaviour.EntryAltitude(arrivalState);
            if (drop < 0.0) drop = 0.0;
            double climb = Math.Abs(s.ClimbMetresPerSecond);
            double glideRun = climb <= 0.0 ? 0.0 : s.SpeedMetresPerSecond * (drop / climb);
            return glideRun + behaviour.Row(arrivalState).TravelMetres;
        }

        // ── entering a state ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Puts the bird into a state and settles everything that depends on which state that is. Does
        /// NOT check the edge table — <see cref="TryTransition"/> is the checked door; this is what the
        /// machine's own chains use once the edge is already known to be legal.
        /// </summary>
        public static void Enter(ref SeagullSimBird bird, int state, SeagullBehaviour behaviour)
        {
            bool wasSurface = behaviour.SurfaceOf(bird.State) != SeagullSurface.None;

            bird.State = state;
            bird.StateMilliseconds = 0.0;
            bird.Frame = 0;
            bird.Cycle = 0;
            bird.CycleTarget = 0;

            var row = behaviour.Row(state);

            if (behaviour.SurfaceOf(state) != SeagullSurface.None)
            {
                bird.AltitudeMetres = 0.0;
                bird.IntentState = -1;
                bird.FlockBound = false;
                bird.RejoinMilliseconds = 0.0;
            }
            else
            {
                double lo = behaviour.AltitudeMin(state);
                double hi = behaviour.AltitudeMax(state);
                if (bird.IntentState >= 0)
                {
                    double entry = behaviour.EntryAltitude(bird.IntentState);
                    if (entry < lo) lo = entry;
                }
                if (bird.AltitudeMetres < lo) bird.AltitudeMetres = lo;
                if (bird.AltitudeMetres > hi) bird.AltitudeMetres = hi;
            }

            if (state == bird.IntentState) bird.IntentState = -1;

            // Leaving the ground starts the rejoin ramp; it runs through takeoff and on into fly.
            if (wasSurface && behaviour.LeavesSurface(state))
            {
                bird.FlockBound = false;
                bird.RejoinMilliseconds = 1.0;   // any positive value arms the ramp
            }
        }

        /// <summary>
        /// Rolls how many loops a cycles-limited state should run, from the bird's own stream.
        /// <c>preen</c> and <c>peck</c> are the only two that declare cycles; both hand back to
        /// <c>float</c>.
        /// </summary>
        public static void RollCycles(ref SeagullSimBird bird, SeagullBehaviour behaviour,
                                      ref SeagullFlockMath.Mulberry32 rng)
        {
            var row = behaviour.Row(bird.State);
            if (row.CyclesMax <= 0) { bird.CycleTarget = 0; return; }
            int lo = row.CyclesMin < 1 ? 1 : row.CyclesMin;
            int hi = row.CyclesMax < lo ? lo : row.CyclesMax;
            bird.CycleTarget = lo + (int)(rng.Next() * (hi - lo + 1));
            if (bird.CycleTarget > hi) bird.CycleTarget = hi;
        }
    }
}
