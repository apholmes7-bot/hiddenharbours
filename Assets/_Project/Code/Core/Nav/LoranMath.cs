using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The Loran-C arithmetic</b>, pure and deterministic: world position to a pair of time
    /// differences (TDs) and back, the geometry that says how good a pair is at a spot, and the
    /// repeatable error a real chain carried.
    ///
    /// <para><b>A TD.</b> The master transmits; each secondary transmits when the master's pulse
    /// reaches it plus its coding delay. A receiver at P measures the secondary's arrival minus the
    /// master's: <c>TD = (|S - M| + |P - S| - |P - M|) / c + codingDelay</c>, in microseconds. One TD
    /// is a hyperbola with the two stations as foci (a line of position); two cross at a fix.</para>
    ///
    /// <para><b>The geometry measure.</b> <see cref="GeometryMetresPerMicrosecond"/> is the
    /// position error one microsecond of TD error buys at P: the Frobenius norm of the inverse of
    /// the 2x2 Jacobian of the two TDs. It is about 212 m/us at best (perpendicular lines on the
    /// baselines), grows as the lines cross more shallowly far from the chain, and blows up on a
    /// baseline extension, where a TD stops changing as the boat moves. Applying the error to the
    /// TDs and letting this geometry carry it into position is what makes the error honestly larger
    /// where real Loran was worse; nothing is painted on.</para>
    ///
    /// <para><b>Determinism (rule 5).</b> Every function is a pure function of its arguments. The
    /// error is value noise hashed from (worldSeed, chain id, secondary, position cell, time
    /// bucket); there is no <c>System.Random</c> and no state.</para>
    /// </summary>
    public static class LoranMath
    {
        /// <summary>Seconds in an in-game hour: a unit conversion for the drift bucket, not a
        /// tunable (the game clock counts in-game seconds).</summary>
        public const double SecondsPerGameHour = 3600.0;

        // Hash stream offsets that keep the repeatable and drifting error fields of the same
        // secondary independent. Labels, not tunables.
        private const int RepeatableStream = 0;
        private const int DriftStream = 1;
        private const int StreamsPerSecondary = 2;

        // ---- forward: position to TD ------------------------------------------------------------

        /// <summary>Distance between two world points, in double precision.</summary>
        public static double Distance(Vector2 a, Vector2 b)
        {
            double dx = (double)b.x - a.x, dy = (double)b.y - a.y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>The ideal (error-free) TD of one secondary at <paramref name="pos"/>, in
        /// microseconds. <paramref name="metresPerMicrosecond"/> is the receiver's propagation
        /// speed.</summary>
        public static double TimeDifference(LoranChainDef chain, int secondary, Vector2 pos,
                                            double metresPerMicrosecond)
        {
            LoranSecondary s = chain.Secondaries[secondary];
            double baseline = Distance(chain.Master, s.Position);
            return (baseline + Distance(pos, s.Position) - Distance(pos, chain.Master)) / metresPerMicrosecond
                   + s.CodingDelayMicroseconds;
        }

        /// <summary>The farthest station of the chain from <paramref name="pos"/>, as a fraction of
        /// the chain's range. Above 1 the chain cannot be used there.</summary>
        public static double RangeFraction(LoranChainDef chain, Vector2 pos)
        {
            double far = Distance(pos, chain.Master);
            for (int i = 0; i < chain.Secondaries.Length; i++)
                far = Math.Max(far, Distance(pos, chain.Secondaries[i].Position));
            return far / chain.RangeMetres;
        }

        /// <summary>The factor the chain's TD error is multiplied by at this range fraction:
        /// <c>1 + FringeErrorGain x f^2</c>.</summary>
        public static double RangeGain(LoranChainDef chain, double rangeFraction)
            => 1.0 + chain.FringeErrorGain * rangeFraction * rangeFraction;

        /// <summary>Metres of position error per microsecond of TD error for the secondary pair
        /// (<paramref name="a"/>, <paramref name="b"/>) at <paramref name="pos"/>.
        /// <see cref="double.PositiveInfinity"/> where the two lines are parallel or the position is
        /// on a station.</summary>
        public static double GeometryMetresPerMicrosecond(LoranChainDef chain, int a, int b, Vector2 pos,
                                                          double metresPerMicrosecond)
        {
            if (!Jacobian(chain, a, b, pos.x, pos.y, metresPerMicrosecond,
                          out double ja, out double jb, out double jc, out double jd))
                return double.PositiveInfinity;
            double det = ja * jd - jb * jc;
            if (det == 0.0 || double.IsNaN(det)) return double.PositiveInfinity;
            return Math.Sqrt(ja * ja + jb * jb + jc * jc + jd * jd) / Math.Abs(det);
        }

        // ---- inverse: TD pair to position -------------------------------------------------------

        /// <summary>
        /// The position whose ideal TDs on the pair (<paramref name="a"/>, <paramref name="b"/>) are
        /// (<paramref name="tdA"/>, <paramref name="tdB"/>): Newton's method from
        /// <paramref name="guess"/>, with the step capped by the receiver's solver settings.
        ///
        /// <para>Two hyperbolas can cross twice; Newton from a guess finds the crossing nearest the
        /// guess, which is the one a skipper means (the set is seeded with the boat's own fix).
        /// Returns false when the pair is singular near the guess or the solver does not settle.</para>
        /// </summary>
        public static bool TryPositionFromTds(LoranChainDef chain, int a, int b, double tdA, double tdB,
                                              Vector2 guess, LoranReceiverDef receiver, out Vector2 position)
        {
            position = default;
            if (chain == null || receiver == null || !ValidPair(chain, a, b) || !NavMath.IsFinite(guess)
                || double.IsNaN(tdA) || double.IsNaN(tdB) || double.IsInfinity(tdA) || double.IsInfinity(tdB))
                return false;

            double c = receiver.PropagationMetresPerMicrosecond;
            double x = guess.x, y = guess.y;
            double maxStep = receiver.SolverMaxStepMetres, tolerance = receiver.SolverToleranceMetres;
            for (int i = 0; i < receiver.SolverIterations; i++)
            {
                double fa = TdAt(chain, a, x, y, c) - tdA;
                double fb = TdAt(chain, b, x, y, c) - tdB;
                if (!Jacobian(chain, a, b, x, y, c, out double ja, out double jb, out double jc, out double jd))
                    return false;
                double det = ja * jd - jb * jc;
                if (det == 0.0 || double.IsNaN(det)) return false;

                // Solve J * step = -F.
                double sx = -(jd * fa - jb * fb) / det;
                double sy = -(-jc * fa + ja * fb) / det;
                double len = Math.Sqrt(sx * sx + sy * sy);
                if (double.IsNaN(len)) return false;
                if (len > maxStep) { sx *= maxStep / len; sy *= maxStep / len; }
                x += sx; y += sy;
                if (len < tolerance)
                {
                    position = new Vector2((float)x, (float)y);
                    return NavMath.IsFinite(position);
                }
            }
            return false;
        }

        // ---- the deterministic error -----------------------------------------------------------

        /// <summary>
        /// The TD error (microseconds) one secondary of the chain carries at this place and time for
        /// this world: the repeatable field plus the drifting field, scaled by the range gain at
        /// <paramref name="rangeFraction"/>. Smooth in space (bilinear value noise over
        /// <see cref="LoranChainDef.ErrorCellMetres"/> cells) and in time (a blend between
        /// <see cref="LoranChainDef.DriftBucketHours"/> buckets), and bounded by
        /// <c>(repeatable + drift) x gain</c>.
        /// </summary>
        public static double ErrorMicroseconds(LoranChainDef chain, int secondary, int worldSeed, Vector2 pos,
                                               double gameTimeSeconds, double rangeFraction)
        {
            uint chainHash = StableIdHash(chain.Id);
            double cell = chain.ErrorCellMetres;
            double gx = pos.x / cell, gy = pos.y / cell;
            int streamBase = secondary * StreamsPerSecondary;

            double repeatable = ValueNoise(worldSeed, chainHash, streamBase + RepeatableStream, gx, gy, 0);

            double bucketSeconds = chain.DriftBucketHours * SecondsPerGameHour;
            double tb = gameTimeSeconds / bucketSeconds;
            double tFloor = Math.Floor(tb);
            int bucket = (int)tFloor;
            double tf = Smooth(tb - tFloor);
            double drift0 = ValueNoise(worldSeed, chainHash, streamBase + DriftStream, gx, gy, bucket);
            double drift1 = ValueNoise(worldSeed, chainHash, streamBase + DriftStream, gx, gy, bucket + 1);
            double drift = drift0 + (drift1 - drift0) * tf;

            return (chain.RepeatableErrorMicroseconds * repeatable + chain.DriftErrorMicroseconds * drift)
                   * RangeGain(chain, rangeFraction);
        }

        /// <summary>Round a TD to the readout's step (half away from zero), as the set shows it and
        /// a skipper writes it down.</summary>
        public static double Quantize(double td, double resolution)
            => Math.Round(td / resolution, MidpointRounding.AwayFromZero) * resolution;

        /// <summary>FNV-1a over the id's characters: stable across runs and platforms, unlike
        /// <c>string.GetHashCode</c>.</summary>
        public static uint StableIdHash(string id)
        {
            uint h = 2166136261u;
            if (id == null) return h;
            unchecked
            {
                for (int i = 0; i < id.Length; i++)
                {
                    h ^= id[i];
                    h *= 16777619u;
                }
            }
            return h;
        }

        // ---- internals --------------------------------------------------------------------------

        internal static bool ValidPair(LoranChainDef chain, int a, int b)
            => chain.Secondaries != null && a >= 0 && b >= 0 && a != b
               && a < chain.Secondaries.Length && b < chain.Secondaries.Length;

        private static double TdAt(LoranChainDef chain, int secondary, double x, double y, double c)
        {
            LoranSecondary s = chain.Secondaries[secondary];
            double baseline = Distance(chain.Master, s.Position);
            double ds = Dist(x, y, s.Position), dm = Dist(x, y, chain.Master);
            return (baseline + ds - dm) / c + s.CodingDelayMicroseconds;
        }

        private static double Dist(double x, double y, Vector2 p)
        {
            double dx = x - p.x, dy = y - p.y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // Rows are the gradients of TD_a and TD_b (microseconds per metre):
        // grad TD = (unit(P - S) - unit(P - M)) / c.
        private static bool Jacobian(LoranChainDef chain, int a, int b, double x, double y, double c,
                                     out double ja, out double jb, out double jc, out double jd)
        {
            ja = jb = jc = jd = 0.0;
            if (!Unit(x, y, chain.Master, out double mx, out double my)) return false;
            if (!Unit(x, y, chain.Secondaries[a].Position, out double ax, out double ay)) return false;
            if (!Unit(x, y, chain.Secondaries[b].Position, out double bx, out double by)) return false;
            ja = (ax - mx) / c; jb = (ay - my) / c;
            jc = (bx - mx) / c; jd = (by - my) / c;
            return true;
        }

        private static bool Unit(double x, double y, Vector2 from, out double ux, out double uy)
        {
            double dx = x - from.x, dy = y - from.y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (!(len > 0.0)) { ux = uy = 0.0; return false; }
            ux = dx / len; uy = dy / len;
            return true;
        }

        // Bilinear value noise in [-1, 1] over integer lattice corners, smoothstep weights.
        private static double ValueNoise(int seed, uint chainHash, int stream, double gx, double gy, int bucket)
        {
            double fx0 = Math.Floor(gx), fy0 = Math.Floor(gy);
            int cx = (int)fx0, cy = (int)fy0;
            double tx = Smooth(gx - fx0), ty = Smooth(gy - fy0);
            double v00 = Corner(seed, chainHash, stream, cx, cy, bucket);
            double v10 = Corner(seed, chainHash, stream, cx + 1, cy, bucket);
            double v01 = Corner(seed, chainHash, stream, cx, cy + 1, bucket);
            double v11 = Corner(seed, chainHash, stream, cx + 1, cy + 1, bucket);
            double bottom = v00 + (v10 - v00) * tx;
            double top = v01 + (v11 - v01) * tx;
            return bottom + (top - bottom) * ty;
        }

        private static double Smooth(double t) => t * t * (3.0 - 2.0 * t);

        private static double Corner(int seed, uint chainHash, int stream, int cx, int cy, int bucket)
        {
            uint h = Mix((uint)seed ^ 0x9E3779B9u);
            h = Combine(h, unchecked((int)chainHash));
            h = Combine(h, stream);
            h = Combine(h, cx);
            h = Combine(h, cy);
            h = Combine(h, bucket);
            return h / (double)uint.MaxValue * 2.0 - 1.0;
        }

        private static uint Combine(uint h, int v)
            => Mix(h ^ unchecked((uint)v + 0x9E3779B9u + (h << 6) + (h >> 2)));

        // A 32-bit avalanche finalizer (lowbias32).
        private static uint Mix(uint h)
        {
            unchecked
            {
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
