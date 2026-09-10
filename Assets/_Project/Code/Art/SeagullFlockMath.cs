using System;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>THE ART DIRECTOR'S <c>flock()</c>, PORTED — bit-faithful, allocation-free, and clock-free.</b>
    ///
    /// <para><c>docs/art/rigs/seagullIsoRig.js</c> ships the wheel the birds fly: nine random draws per
    /// bird off one <c>mulberry32</c> stream, a squashed ellipse, a glide duty cycle, and a swoop that
    /// only the LEAD bird takes. That is the motion the owner approved when he approved the rig, so it
    /// is ported rather than reinvented — this class is the only place the shape of a gull's wheel is
    /// written down on our side, and <c>SeagullFlockPortParityTests</c> runs the rig itself in the
    /// repo's own ClearScript V8 and compares, draw for draw, on a fixed seed.</para>
    ///
    /// <para><b>Individuals are a flock of one.</b> There is no second code path for a lone gull: a
    /// single bird is <c>Flock(1, …)</c> with its own seed. (Note the rig's own asymmetry, preserved
    /// here: <c>i === 0</c> is the only bird that ever swoops, so a flock of one swoops and a flock of
    /// nine has exactly one swooper.)</para>
    ///
    /// <para><b>JS semantics that are NOT C# semantics</b> — each one has cost this project a bug
    /// before (see the porting-a-rig-to-csharp note):</para>
    /// <list type="bullet">
    ///   <item>The rig's <c>seed || 7</c> — a seed of ZERO is not a seed, it is the rig's default 7.</item>
    ///   <item><c>(seed * 2654435761) &gt;&gt;&gt; 0</c> — the multiply happens in FLOAT64 and only
    ///         then becomes a uint32. Doing it in <c>int</c> or <c>uint</c> wraps a step too early and
    ///         gives a different stream for every seed above 1.</item>
    ///   <item><c>Math.imul</c> is a 32-bit WRAPPING multiply, which is plain <c>uint</c> multiply
    ///         here (C# is unchecked by default) — not <c>long</c> arithmetic.</item>
    ///   <item><c>Math.round</c> rounds a half UP (toward +∞), unlike <see cref="Math.Round(double)"/>
    ///         which rounds a half to EVEN. See <see cref="JsRound"/>.</item>
    ///   <item><c>%</c> in JS is a truncated remainder (like C#'s), so <c>mod</c> needs the
    ///         add-and-remainder dance to stay non-negative. See <see cref="Mod"/>.</item>
    /// </list>
    ///
    /// <para><b>Determinism (rule 5).</b> Nothing here reads a clock, allocates, or touches
    /// <c>UnityEngine</c>: <see cref="Flock"/> is a pure function of
    /// <c>(count, timeMilliseconds, seed, radius, tuning)</c> and fills a caller-owned buffer. The sim
    /// time comes from <c>GameServices.Clock</c> at the presenter's edge, never from
    /// <c>Time.time</c>.</para>
    ///
    /// <para><b>⚠️ <see cref="SeagullFlockBird.Scale"/> IS PRODUCED AND MUST NOT BE APPLIED.</b> The
    /// rig's ninth draw per bird is a 0.92..1.08 size jitter. It is kept because dropping the draw
    /// would shift every later bird's stream and break parity — but the owner's 2026-09-09 ruling is
    /// strict world scale at every altitude (a 1.40 m span is 45 px on the wharf and 45 px at 25 m),
    /// so <see cref="GullFlock"/> draws every bird at <c>localScale</c> 1. Altitude is a SCREEN
    /// OFFSET, never a size.</para>
    /// </summary>
    public static class SeagullFlockMath
    {
        /// <summary>2^32, as a double — the divisor mulberry32 normalises by, and the modulus of
        /// ECMAScript's ToUint32.</summary>
        public const double TwoPow32 = 4294967296.0;

        /// <summary>Knuth's multiplicative constant, the rig's seed whitener (<c>seed * 2654435761</c>).
        /// Applied in FLOAT64 exactly as the rig does it, before ToUint32.</summary>
        public const double SeedWhitener = 2654435761.0;

        /// <summary>The rig's <c>opts.seed || 7</c> fallback — a seed of 0 IS 7, not 0.</summary>
        public const int DefaultSeed = 7;

        // =============================================================================================
        //  THE RIG'S OWN LITERALS
        // =============================================================================================
        //
        // ⚠️ These are TRANSCRIBED CONSTANTS OF A PORT, not tunables. Rule 6 is about numbers the owner
        // steers; these are numbers the art director already steered, in a file we do not own. Every one
        // is quoted from `flock()` in docs/art/rigs/seagullIsoRig.js. Changing one does not retune the
        // birds — it breaks parity with the rig, and SeagullFlockPortParityTests says so. Anything the
        // OWNER should be able to turn lives on SeagullFlockTuning, which comes from the sidecar.

        /// <summary>The rig's <c>6.283</c> — its stand-in for a full turn of the wheel. Deliberately NOT
        /// <c>2π</c>: the rig types the truncated literal, and 2π would drift the phase.</summary>
        public const double RigTurn = 6.283;

        /// <summary>The rig's <c>6.28</c> — its stand-in for a full turn when scattering PHASES. One
        /// digit shorter than <see cref="RigTurn"/>, in the same function. Faithful, not a typo.</summary>
        public const double RigPhaseTurn = 6.28;

        /// <summary>Loop radius is <c>R * (0.6 + rng()*0.7)</c> — birds wheel on 0.6..1.3 of the flock
        /// radius, so a flock is a spread of wheels, not one ring.</summary>
        public const double RadiusJitterBase = 0.6, RadiusJitterSpan = 0.7;

        /// <summary>The ellipse squash <c>0.7 + rng()*0.3</c>: the wheel's depth axis is 0.7..1.0 of its
        /// across axis, which is what makes a circle read as a circle in a ¾ view.</summary>
        public const double SquashBase = 0.7, SquashSpan = 0.3;

        /// <summary>The per-bird size jitter <c>0.92 + rng()*0.16</c>. PRODUCED, NEVER APPLIED — see the
        /// class remarks.</summary>
        public const double ScaleJitterBase = 0.92, ScaleJitterSpan = 0.16;

        /// <summary>A swoop occupies the first <c>0.28</c> of each swoop interval.</summary>
        public const double SwoopDuty = 0.28;

        /// <summary>Seconds per cycle of the glide oscillator (<c>Math.sin(tt/3.1 + gph)</c>).</summary>
        public const double GlideOscillatorSeconds = 3.1;

        /// <summary>The altitude breathe, <c>Math.sin(tt*0.7 + gph) * 0.6</c> — rad/s, then metres.</summary>
        public const double BreatheRadPerSecond = 0.7, BreatheMetres = 0.6;

        /// <summary>A swoop dips to <c>alt * (0.25 + 0.75 * |sw/0.28 - 0.5| * 2)</c> — a quarter of the
        /// bird's cruise altitude at the bottom of the dive, back to full at either end.</summary>
        public const double SwoopFloorFraction = 0.25, SwoopRiseFraction = 0.75;

        /// <summary>The rig's <c>ph * 3</c> frame-phase scatter, so a flock does not flap in unison.</summary>
        public const double FramePhaseScale = 3.0;

        // =============================================================================================
        //  ECMASCRIPT PRIMITIVES
        // =============================================================================================

        /// <summary>
        /// ECMAScript <c>ToUint32</c>: truncate toward zero, then take the value modulo 2^32 into
        /// [0, 2^32). Done in <c>double</c> on purpose — <c>seed * 2654435761</c> can exceed
        /// <see cref="int"/> and even crowd <see cref="long"/>, and the rig performs the multiply in
        /// float64 before <c>&gt;&gt;&gt;0</c> ever sees it.
        /// </summary>
        public static uint ToUint32(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return 0u;
            double r = Math.Truncate(x) % TwoPow32;
            if (r < 0.0) r += TwoPow32;
            return (uint)r;
        }

        /// <summary>
        /// ECMAScript <c>Math.round</c>: the half goes UP, toward +∞ (so −3.5 rounds to −3, not −4).
        /// <see cref="Math.Round(double)"/> is banker's rounding and gives a different facing at every
        /// exact 22.5° boundary. Written as "floor, then step if the fraction reaches a half" rather
        /// than <c>floor(x + 0.5)</c>, because adding 0.5 first double-rounds the largest double below
        /// a half up to the wrong integer.
        /// </summary>
        public static double JsRound(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return x;
            double f = Math.Floor(x);
            return (x - f) >= 0.5 ? f + 1.0 : f;
        }

        /// <summary>The rig's <c>mod=(a,n)=&gt;((a%n)+n)%n</c> — a remainder that is never negative.
        /// C#'s <c>%</c> truncates exactly as JS's does, so the same dance is needed.</summary>
        public static double Mod(double a, double n) => ((a % n) + n) % n;

        /// <summary>
        /// The rig's <c>dirOf(h) = mod(Math.round(h/(π/4)), 8)</c> — a heading in radians to a facing
        /// ROW of the sheet. ⚠️ The seagull rig is CLOCKWISE (measured at intake, and the minority
        /// convention in this repo): row <c>r</c> depicts heading +45°·r, and the sheet's rows are NOT
        /// remapped on the way out of the baker, so row = dir with no correction.
        /// </summary>
        public static int DirOf(double headingRadians)
            => (int)Mod(JsRound(headingRadians / (Math.PI / 4.0)), 8.0);

        /// <summary>
        /// <b>mulberry32</b>, the rig's generator, transcribed. One <c>uint</c> of state; each call
        /// returns a double in [0, 1). The whole flock rides ONE stream, so the draw ORDER in
        /// <see cref="Flock"/> is part of the contract — inserting a draw renumbers every bird after it.
        /// </summary>
        public struct Mulberry32
        {
            private uint _a;

            /// <summary>Seeds exactly as <c>flock()</c> does: <c>mulberry((seed || 7) * 2654435761)</c>,
            /// with the multiply in float64 and ToUint32 last.</summary>
            public static Mulberry32 ForFlockSeed(int seed)
            {
                int s = seed == 0 ? DefaultSeed : seed;
                return new Mulberry32 { _a = ToUint32((double)s * SeedWhitener) };
            }

            /// <summary>Seeds from an already-whitened 32-bit value (what <c>mulberry(x)</c> itself
            /// takes).</summary>
            public static Mulberry32 FromState(uint state) => new Mulberry32 { _a = state };

            /// <summary>The current internal state — exposed so a test can pin the stream itself, not
            /// just its outputs.</summary>
            public uint State => _a;

            /// <summary>One draw, in [0, 1). Every operation is 32-bit and wrapping, as
            /// <c>Math.imul</c> and <c>|0</c> make them in JS.</summary>
            public double Next()
            {
                unchecked
                {
                    _a = _a + 0x6D2B79F5u;
                    uint t = (_a ^ (_a >> 15)) * (1u | _a);
                    t = ((t + ((t ^ (t >> 7)) * (61u | t))) ^ t);
                    return (double)(t ^ (t >> 14)) / TwoPow32;
                }
            }
        }

        // =============================================================================================
        //  THE FLOCK
        // =============================================================================================

        /// <summary>The three states <c>flock()</c> can put a bird in. Their ids on the sheet are
        /// <c>fly</c>, <c>glide</c> and <c>swoop</c>.</summary>
        public enum SeagullFlockAnim { Fly = 0, Glide = 1, Swoop = 2 }

        /// <summary>
        /// One bird as <c>flock()</c> emits it. Positions are METRES relative to the flock's centre;
        /// <see cref="Z"/> is ALTITUDE in metres above the surface — the presenter turns it into a
        /// screen offset, never a scale.
        /// </summary>
        public readonly struct SeagullFlockBird
        {
            /// <summary>Across-screen offset from the flock centre, metres.</summary>
            public readonly double X;
            /// <summary>Depth offset from the flock centre, metres (the wheel's squashed axis).</summary>
            public readonly double Y;
            /// <summary>Altitude above the surface, metres. NOT a world Y and NOT a scale.</summary>
            public readonly double Z;
            /// <summary>Travel direction, radians, in the rig's own <c>atan2(vx, vy)</c> convention
            /// (0 = +Y = the bill toward N, growing CLOCKWISE).</summary>
            public readonly double Heading;
            /// <summary>Sheet facing row, 0..7 — <see cref="DirOf"/> of <see cref="Heading"/>.</summary>
            public readonly int Dir;
            /// <summary>Which of the three airborne states the wheel is in.</summary>
            public readonly SeagullFlockAnim Anim;
            /// <summary>Frame index within <see cref="Anim"/>, already wrapped.</summary>
            public readonly int Frame;
            /// <summary>The rig's per-bird size jitter, 0.92..1.08. ⚠️ PRODUCED, NEVER APPLIED —
            /// strict world scale at every altitude (owner, 2026-09-09).</summary>
            public readonly double Scale;

            public SeagullFlockBird(double x, double y, double z, double heading, int dir,
                                    SeagullFlockAnim anim, int frame, double scale)
            {
                X = x; Y = y; Z = z; Heading = heading; Dir = dir;
                Anim = anim; Frame = frame; Scale = scale;
            }
        }

        /// <summary>The sheet id of a flock anim — the name the sidecar, the contract and the slicer
        /// all use.</summary>
        public static string AnimId(SeagullFlockAnim anim)
        {
            switch (anim)
            {
                case SeagullFlockAnim.Glide: return SeagullStates.Glide;
                case SeagullFlockAnim.Swoop: return SeagullStates.Swoop;
                default: return SeagullStates.Fly;
            }
        }

        /// <summary>
        /// Everything <see cref="Flock"/> needs that is NOT a transcription constant: the sidecar's
        /// FLOCK block and the three airborne states' frame counts and frame durations. It comes from
        /// <c>seagullIsoRig.gameplay.json</c> by way of <see cref="SeagullVisualDef"/>, so the owner
        /// retunes birds by re-exporting the rig, not by editing C# (rule 6).
        /// </summary>
        public readonly struct SeagullFlockTuning
        {
            public readonly double RadiusMetres;
            public readonly double PeriodMinSeconds, PeriodMaxSeconds;
            public readonly double AltitudeMinMetres, AltitudeMaxMetres;
            public readonly double SwoopEveryMinSeconds, SwoopEveryMaxSeconds;
            public readonly double GlideDuty;
            public readonly int FlyFrames, GlideFrames, SwoopFrames;
            public readonly double FlyMilliseconds, GlideMilliseconds, SwoopMilliseconds;

            public SeagullFlockTuning(double radiusMetres,
                                      double periodMinSeconds, double periodMaxSeconds,
                                      double altitudeMinMetres, double altitudeMaxMetres,
                                      double swoopEveryMinSeconds, double swoopEveryMaxSeconds,
                                      double glideDuty,
                                      int flyFrames, double flyMilliseconds,
                                      int glideFrames, double glideMilliseconds,
                                      int swoopFrames, double swoopMilliseconds)
            {
                RadiusMetres = radiusMetres;
                PeriodMinSeconds = periodMinSeconds; PeriodMaxSeconds = periodMaxSeconds;
                AltitudeMinMetres = altitudeMinMetres; AltitudeMaxMetres = altitudeMaxMetres;
                SwoopEveryMinSeconds = swoopEveryMinSeconds; SwoopEveryMaxSeconds = swoopEveryMaxSeconds;
                GlideDuty = glideDuty;
                FlyFrames = flyFrames; FlyMilliseconds = flyMilliseconds;
                GlideFrames = glideFrames; GlideMilliseconds = glideMilliseconds;
                SwoopFrames = swoopFrames; SwoopMilliseconds = swoopMilliseconds;
            }

            /// <summary>Frames in one loop of an airborne anim.</summary>
            public int FramesOf(SeagullFlockAnim a) =>
                a == SeagullFlockAnim.Glide ? GlideFrames
                : a == SeagullFlockAnim.Swoop ? SwoopFrames : FlyFrames;

            /// <summary>Milliseconds a frame of an airborne anim is held.</summary>
            public double MillisecondsOf(SeagullFlockAnim a) =>
                a == SeagullFlockAnim.Glide ? GlideMilliseconds
                : a == SeagullFlockAnim.Swoop ? SwoopMilliseconds : FlyMilliseconds;

            /// <summary>True when every number the port needs is present and sane. A tuning that is not
            /// <see cref="IsUsable"/> means the sidecar did not reach us — the presenter stands the
            /// birds down rather than flying a flock of zeroes.</summary>
            public bool IsUsable =>
                RadiusMetres > 0.0 && PeriodMinSeconds > 0.0 && PeriodMaxSeconds >= PeriodMinSeconds &&
                SwoopEveryMinSeconds > 0.0 && SwoopEveryMaxSeconds >= SwoopEveryMinSeconds &&
                FlyFrames > 0 && GlideFrames > 0 && SwoopFrames > 0 &&
                FlyMilliseconds > 0.0 && GlideMilliseconds > 0.0 && SwoopMilliseconds > 0.0;
        }

        /// <summary>
        /// The rig's <c>flock(n, t, {seed, radius})</c>, draw for draw.
        ///
        /// <para><b>The draw order is the contract</b> — nine per bird, in this sequence: loop radius,
        /// period, phase, turn direction, cruise altitude, ellipse squash, glide phase, swoop interval,
        /// and finally the size jitter. The jitter is last because JS evaluates object literal
        /// properties in source order and <c>scale</c> is the last property; a port that draws it
        /// earlier is a DIFFERENT flock from bird 1 onward, and looks perfectly plausible.</para>
        /// </summary>
        /// <param name="count">Birds to emit. Clamped to what <paramref name="into"/> can hold.</param>
        /// <param name="timeMilliseconds">Sim time in MILLISECONDS (the rig's <c>t</c>). The frame index
        /// reads it raw; everything else reads <c>t/1000</c>.</param>
        /// <param name="seed">The flock's seed. Zero means the rig's default of 7.</param>
        /// <param name="radiusMetres">Wheel radius, metres. Pass a non-positive value to take the
        /// sidecar's <see cref="SeagullFlockTuning.RadiusMetres"/> (the rig's <c>opts.radius ?? R</c>).</param>
        /// <param name="tuning">The sidecar's FLOCK block and airborne frame table.</param>
        /// <param name="into">Caller-owned buffer, filled from index 0. Never allocated here.</param>
        /// <returns>How many entries of <paramref name="into"/> were written.</returns>
        public static int Flock(int count, double timeMilliseconds, int seed, double radiusMetres,
                                in SeagullFlockTuning tuning, SeagullFlockBird[] into)
        {
            if (into == null || count <= 0) return 0;
            int n = count < into.Length ? count : into.Length;

            var rng = Mulberry32.ForFlockSeed(seed);
            double r = radiusMetres > 0.0 ? radiusMetres : tuning.RadiusMetres;
            double tt = timeMilliseconds / 1000.0;

            for (int i = 0; i < n; i++)
            {
                double rr = r * (RadiusJitterBase + rng.Next() * RadiusJitterSpan);
                double per = tuning.PeriodMinSeconds
                           + rng.Next() * (tuning.PeriodMaxSeconds - tuning.PeriodMinSeconds);
                double ph = rng.Next() * RigPhaseTurn;
                double ccw = rng.Next() < 0.5 ? 1.0 : -1.0;
                double alt = tuning.AltitudeMinMetres
                           + rng.Next() * (tuning.AltitudeMaxMetres - tuning.AltitudeMinMetres);
                double sq = SquashBase + rng.Next() * SquashSpan;
                double gph = rng.Next() * RigPhaseTurn;
                double swEvery = tuning.SwoopEveryMinSeconds
                               + rng.Next() * (tuning.SwoopEveryMaxSeconds - tuning.SwoopEveryMinSeconds);

                double a = ccw * (tt / per * RigTurn) + ph;
                double x = rr * Math.Cos(a);
                double y = rr * sq * Math.Sin(a);

                double vx = -rr * Math.Sin(a) * ccw;
                double vy = rr * sq * Math.Cos(a) * ccw;
                double heading = Math.Atan2(vx, vy);

                double sw = ((tt + ph) % swEvery) / swEvery;
                bool swooping = i == 0 && sw < SwoopDuty;
                bool gliding = Math.Sin(tt / GlideOscillatorSeconds + gph) > 1.0 - 2.0 * tuning.GlideDuty;

                SeagullFlockAnim anim = swooping ? SeagullFlockAnim.Swoop
                                      : gliding ? SeagullFlockAnim.Glide
                                                : SeagullFlockAnim.Fly;

                double zed = swooping
                    ? alt * (SwoopFloorFraction
                             + SwoopRiseFraction * Math.Abs(sw / SwoopDuty - 0.5) * 2.0)
                    : alt + Math.Sin(tt * BreatheRadPerSecond + gph) * BreatheMetres;

                int frames = tuning.FramesOf(anim);
                int frame = (int)Mod(
                    Math.Floor(timeMilliseconds / tuning.MillisecondsOf(anim) + ph * FramePhaseScale),
                    frames);

                double scale = ScaleJitterBase + rng.Next() * ScaleJitterSpan;

                into[i] = new SeagullFlockBird(x, y, zed, heading, DirOf(heading), anim, frame, scale);
            }

            return n;
        }

        // -- the float ROCK -----------------------------------------------------------------------
        //
        // A bird sitting on the water rides the same sea the fleet does, and the sidecar says by how
        // much: roll_deg 2.4 / heave_px 1 MAX, with the note "a gull rides higher than a hull". These
        // two are the whole of it - pure, capped, and driven by a caller-supplied angle so nothing in
        // here reads a clock.

        /// <summary>
        /// The phase angle of a floating bird's rock, radians. One full cycle per <paramref
        /// name="cycleMilliseconds"/> - hand it the <c>float</c> state's own strip duration and the
        /// bob is the bob the art director drew, so the rock and the wing-settle beat together.
        /// </summary>
        public static double RockAngle(double timeMilliseconds, double cycleMilliseconds, double phase)
        {
            if (cycleMilliseconds <= 0.0) return phase;
            return timeMilliseconds / cycleMilliseconds * (Math.PI * 2.0) + phase;
        }

        /// <summary>
        /// The rock pose of a bird on the water: a roll in DEGREES and a heave in PIXELS, quarter-cycle
        /// apart so the bird leans as it lifts.
        ///
        /// <para><b>Capped by construction.</b> Both are the sidecar's maximum times
        /// <paramref name="seaState01"/> (clamped 0..1) times a sine, so neither can exceed the cap in
        /// any sea - which is the claim the sidecar's WATER note makes and the one a test can check
        /// without simulating a storm.</para>
        /// </summary>
        public static void RockPose(double angleRadians, double seaState01,
                                    double rollDegreesMax, double heavePixelsMax,
                                    out double rollDegrees, out double heavePixels)
        {
            double s = seaState01 < 0.0 ? 0.0 : seaState01 > 1.0 ? 1.0 : seaState01;
            rollDegrees = Math.Sin(angleRadians) * rollDegreesMax * s;
            heavePixels = Math.Cos(angleRadians) * heavePixelsMax * s;
        }
    }
}
