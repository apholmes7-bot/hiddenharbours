using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// WHERE EACH FISH IN A SCHOOL IS, RIGHT NOW — the C# port of the catch pass 2 rig's
    /// <c>FishIso2.shoal(species, n, t, opts)</c> (<c>docs/art/rigs/catch-pass-2-kit/Art/fishIsoRig2.js</c>,
    /// contract published in <c>sidecars/fishIsoRig2.rig.json → shoal</c>).
    ///
    /// <para><b>Why a port and not the JS.</b> The sail-rig ruling stands for every kit: the rig is the
    /// ORACLE, the shipped game runs C#. No JS in the player. The sidecar states the contract —
    /// <i>"n singles: {x, y, z} metres from the shoal anchor, heading (rad, 0 = N, CW),
    /// dir = dirOf(heading), frame (swim), scale … one lazy figure-eight, n singles trailing one leader;
    /// deterministic per seed so two clients agree; nothing is baked as a group"</i> — and this file is
    /// that contract, arithmetic for arithmetic.</para>
    ///
    /// <para><b>Pose = f(t), never integrated</b> (rule 5). There is no per-fish state anywhere: every
    /// swimmer's position, heading and frame is a closed-form function of the game clock and the school's
    /// seed. A save/load, a region stream-in or a second client lands the same fish in the same place
    /// facing the same way — the same law the routine engine and the tide already keep. An Update-driven
    /// integration would be a second, drifting copy of the sea.</para>
    ///
    /// <para><b>Bit-exact with the rig's PRNG.</b> <see cref="Mulberry32"/> is the rig's
    /// <c>mulberry()</c> transcribed: the same 32-bit adds, xor-shifts and wrapping multiplies in the same
    /// order, so the i-th fish takes the i-th draw the review page gave it. The seed is deliberately kept
    /// under <see cref="MaxJsSafeSeed"/> — see <see cref="SeedFor"/>, which is the one place the JS's
    /// float64 seed multiply could have diverged from honest integer maths.</para>
    ///
    /// <para><b>Engine-light and allocation-free</b> (rule 7): <see cref="Vector2"/>/<see cref="Mathf"/>
    /// only, and <see cref="Fill"/> writes into a caller-owned array it never grows. Doubles throughout —
    /// the clock reaches millions of seconds in a long save and a float phase would visibly quantise the
    /// swim long before it wrapped.</para>
    /// </summary>
    public static class ShoalMath
    {
        /// <summary>Frames in the rig's <c>swim</c> anim (<c>ANIMS.swim.n</c>).</summary>
        public const int SwimFrames = 6;

        /// <summary>Milliseconds per <c>swim</c> frame (<c>ANIMS.swim.ms</c>).</summary>
        public const double SwimFrameMs = 120.0;

        /// <summary>The rig's default shoal seed (<c>shoal</c> defaults: <c>seed 11</c>).</summary>
        public const int DefaultSeed = 11;

        /// <summary>The rig's default loop radius in metres (<c>shoal</c> defaults: <c>radius 1.2</c>).</summary>
        public const float DefaultRadiusMetres = 1.2f;

        /// <summary>The rig's default loop speed in m/s (<c>shoal</c> defaults: <c>speed 0.35</c>) — the
        /// same number as <c>MOTION.swim.v</c>, which is not a coincidence: the leader travels at the
        /// speed the swim anim is drawn for.</summary>
        public const float DefaultSpeedMetresPerSecond = 0.35f;

        /// <summary>The rig's default swimmer height relative to the anchor, in metres
        /// (<c>shoal</c> defaults: <c>z -0.15</c>) — just under the surface.</summary>
        public const float DefaultZMetres = -0.15f;

        /// <summary>
        /// The largest seed for which the rig's JS <c>mulberry((seed || 11) * 2654435761)</c> is still
        /// exact. <b>The JS multiply is float64, not 32-bit</b>, so above
        /// <c>2^53 / 2654435761 ≈ 3.4e6</c> the review page silently loses low bits that honest integer
        /// maths keeps — the two would then disagree about fish nobody could call wrong.
        /// <see cref="SeedFor"/> folds every seed under this bar, so the port and the oracle stay
        /// comparable for every input the game can produce.
        /// </summary>
        public const int MaxJsSafeSeed = 1000000;

        /// <summary>One fish, at one instant — the rig's single, in world metres from the school anchor.</summary>
        public readonly struct Swimmer
        {
            /// <summary>Offset east of the school anchor, metres.</summary>
            public readonly float X;

            /// <summary>Offset north of the school anchor, metres.</summary>
            public readonly float Y;

            /// <summary>Height relative to the anchor's surface point, metres (negative = under it).</summary>
            public readonly float Z;

            /// <summary>Heading in radians, <b>0 = north, clockwise</b> — the rig's turntable convention,
            /// not <see cref="Mathf.Atan2"/>'s.</summary>
            public readonly float HeadingRad;

            /// <summary>The baked 8-direction row this heading snaps to (<see cref="DirOf"/>).</summary>
            public readonly int Dir;

            /// <summary>Column in the <c>swim</c> sheet, <c>0 .. <see cref="SwimFrames"/> - 1</c>.</summary>
            public readonly int Frame;

            /// <summary>Specimen scale — the rig's <c>scale</c>, 1 = the species' real length.</summary>
            public readonly float Scale;

            public Swimmer(float x, float y, float z, float headingRad, int dir, int frame, float scale)
            {
                X = x;
                Y = y;
                Z = z;
                HeadingRad = headingRad;
                Dir = dir;
                Frame = frame;
                Scale = scale;
            }

            /// <summary>The swimmer's offset from the anchor on the ground plane.</summary>
            public Vector2 Offset => new Vector2(X, Y);
        }

        /// <summary>
        /// MULBERRY32, transcribed from the rig. A mutable struct passed around by value inside one fill,
        /// so a whole school costs no allocation (rule 7).
        ///
        /// <para>The rig works in JS int32/uint32 (<c>|0</c>, <c>&gt;&gt;&gt;</c>, <c>Math.imul</c>).
        /// <c>Math.imul(a, b)</c> is the low 32 bits of the product read as signed — bit-identical to an
        /// unchecked <see cref="uint"/> multiply, which is what this uses, so the emitted bits match with
        /// no signed round-trip.</para>
        /// </summary>
        public struct Mulberry32
        {
            private uint _a;

            /// <summary>Seed the stream exactly as <c>mulberry(seed)</c> does
            /// (<c>a = seed &gt;&gt;&gt; 0</c>).</summary>
            public Mulberry32(uint seed) { _a = seed; }

            /// <summary>The next draw in <c>[0, 1)</c> — the rig's
            /// <c>((t ^ (t &gt;&gt;&gt; 14)) &gt;&gt;&gt; 0) / 4294967296</c>.</summary>
            public double Next()
            {
                unchecked
                {
                    _a += 0x6D2B79F5u;
                    uint t = _a;
                    t = (t ^ (t >> 15)) * (1u | t);
                    t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                    return (t ^ (t >> 14)) / 4294967296.0;
                }
            }
        }

        /// <summary>
        /// The rig's <c>mulberry((opts.seed || 11) * 2654435761)</c> starting state, for a seed already
        /// folded under <see cref="MaxJsSafeSeed"/> (<see cref="SeedFor"/>). Done in 64-bit integers and
        /// truncated to 32 — which is what the JS's <c>&gt;&gt;&gt; 0</c> does to its float64 product, and
        /// exactly so while that product stays inside <c>2^53</c>.
        /// </summary>
        public static uint InitialState(int seed)
        {
            long s = seed == 0 ? DefaultSeed : seed;
            if (s < 0) s = -s;
            return unchecked((uint)((s * 2654435761L) & 0xFFFFFFFFL));
        }

        /// <summary>
        /// Fold any world-derived key into a shoal seed the rig could also have been handed — stable,
        /// non-zero, and under <see cref="MaxJsSafeSeed"/> so the port and the review page stay
        /// comparable (see that constant for why the bar exists).
        /// </summary>
        public static int SeedFor(uint key)
        {
            int s = (int)(key % (uint)MaxJsSafeSeed);
            return s == 0 ? DefaultSeed : s;
        }

        /// <summary>
        /// The rig's <c>dirOf</c>: snap a heading to the 8-direction turntable,
        /// <c>mod(round(h / 45°), 8)</c>.
        ///
        /// <para>⚠ <b><c>Math.Floor(q + 0.5)</c>, not <see cref="System.Math.Round(double)"/>.</b> JS's
        /// <c>Math.round</c> breaks ties toward <b>+∞</b> (<c>Math.round(-1.5) === -1</c>), while every
        /// .NET rounding mode breaks them symmetrically — <c>AwayFromZero</c> would give <c>-2</c> and
        /// <c>ToEven</c> <c>-2</c> as well. Half the headings out of <c>Atan2</c> are negative, so the
        /// wrong transcription silently hands back the neighbouring sheet row for a fish crossing an exact
        /// 22.5° boundary. This is the one line in the file where the obvious .NET call is the wrong
        /// one.</para>
        /// </summary>
        public static int DirOf(double headingRad)
        {
            double q = headingRad / (System.Math.PI / 4.0);
            long r = (long)System.Math.Floor(q + 0.5);
            int d = (int)(r % 8);
            return d < 0 ? d + 8 : d;
        }

        /// <summary>
        /// Fill <paramref name="into"/> with the school's swimmers at <paramref name="gameSeconds"/> and
        /// return how many were written (<c>min(n, into.Length)</c>, never more).
        ///
        /// <para>Every term is the rig's, in the rig's order — critically the SIX draws each fish takes
        /// (back, side, lift, phase, rate, scale), because a stream consumed in a different order is a
        /// different shoal even with an identical PRNG.</para>
        /// </summary>
        /// <param name="lengthMetres">The species' drawn length at scale 1 (the rig's <c>SPECIES.len</c>);
        /// the school's own specimen scale is applied on top through <paramref name="scale"/>.</param>
        /// <param name="n">How many fish the school is showing (<c>FishSchool.MarkCount</c>, capped).</param>
        /// <param name="gameSeconds">The clock's <c>TotalSeconds</c> — the rig's <c>t</c> is this × 1000.</param>
        /// <param name="seed">The school's shoal seed (<see cref="SeedFor"/>).</param>
        /// <param name="radiusMetres">Radius of the leader's lazy figure-eight, metres.</param>
        /// <param name="speedMetresPerSecond">Leader speed along that loop, m/s.</param>
        /// <param name="scale">Specimen scale multiplier applied to every fish.</param>
        /// <param name="zMetres">Height of the shoal relative to the anchor, metres.</param>
        /// <param name="into">Caller-owned destination, never grown.</param>
        public static int Fill(float lengthMetres, int n, double gameSeconds, int seed,
                               float radiusMetres, float speedMetresPerSecond, float scale, float zMetres,
                               Swimmer[] into)
        {
            if (into == null || into.Length == 0 || n <= 0) return 0;
            int count = n < into.Length ? n : into.Length;

            double scl = scale <= 0f ? 1.0 : scale;
            double l = lengthMetres * scl;
            double r = radiusMetres > 0f ? radiusMetres : DefaultRadiusMetres;
            double w = (speedMetresPerSecond > 0f ? speedMetresPerSecond : DefaultSpeedMetresPerSecond) / r;

            double tMs = gameSeconds * 1000.0;
            double tt = gameSeconds;                    // the rig's t / 1000
            double a = w * tt;

            // The leader's lazy figure-eight, and the heading read off its OWN velocity. Atan2(vx, vy) —
            // the rig's bearing convention, 0 = north and clockwise, NOT Atan2's maths convention.
            double lx = r * System.Math.Sin(a);
            double ly = r * 0.55 * System.Math.Sin(2.0 * a);
            double vx = r * w * System.Math.Cos(a);
            double vy = r * 1.1 * w * System.Math.Cos(2.0 * a);
            double head = System.Math.Atan2(vx, vy);
            double ch = System.Math.Cos(head), sh = System.Math.Sin(head);

            var rng = new Mulberry32(InitialState(seed));

            for (int i = 0; i < count; i++)
            {
                // ⚠ SIX draws, in the rig's order. Reordering these silently rebuilds the whole shoal.
                double back = -(0.3 + rng.Next() * 1.6) * l * (1.0 + (double)i / n * 1.5);
                double side = (rng.Next() - 0.5) * l * 3.2 * (0.35 + (double)i / n);
                double lift = (rng.Next() - 0.5) * 0.08;
                double ph = rng.Next() * 6.28;
                double fr = 0.6 + rng.Next() * 0.8;
                double sc = scl * (0.85 + rng.Next() * 0.3);

                double fwd = back + System.Math.Cos(tt * fr * 0.7 + ph) * l * 0.15;
                double lat = side + System.Math.Sin(tt * fr + ph) * l * 0.25;
                double hd = head + System.Math.Sin(tt * fr * 1.3 + ph) * 0.18;

                double x = lx + fwd * sh + lat * ch;
                double y = ly + fwd * ch - lat * sh;
                double z = zMetres + lift + System.Math.Sin(tt * fr + ph) * 0.01;

                int frame = Mod((long)System.Math.Floor(tMs / SwimFrameMs * fr + ph * 2.0), SwimFrames);

                into[i] = new Swimmer((float)x, (float)y, (float)z, (float)hd, DirOf(hd), frame, (float)sc);
            }

            return count;
        }

        /// <summary>The rig's <c>mod</c> — a true modulus, never a negative remainder.</summary>
        private static int Mod(long a, int n)
        {
            if (n <= 0) return 0;
            int r = (int)(a % n);
            return r < 0 ? r + n : r;
        }
    }
}
