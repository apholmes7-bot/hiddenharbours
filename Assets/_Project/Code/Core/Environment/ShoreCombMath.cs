using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// 🔴 <b>THE INSTRUMENT FOR REGISTER ROWS 9 + 10 — the comb and the wall, as numbers.</b>
    ///
    /// <para>Row 9 is <i>"the wet edge is a comb"</i>: 1–3 m teeth along the dry/wet boundary, aligned
    /// with one axis. Row 10 is <i>"the deep/shallow boundary is a wall"</i>. Both are properties of
    /// <b>where the drawn edge sits relative to the water's TRUE contour</b>, so both are measured here
    /// as a signal indexed by arc length ALONG that contour, in metres.</para>
    ///
    /// <para><b>Why this shape, and not a transect across the picture.</b> A first attempt sampled a
    /// horizontal row and called it "shore-parallel". The shoreline in that frame ran vertically, so the
    /// transect crossed the shore instead of following it and every arm returned the same number — a
    /// measurement of nothing. Indexing by the contour's own arc length and measuring PERPENDICULAR to
    /// its local tangent removes the idea of "horizontal" from the instrument altogether: there is no
    /// orientation left to get wrong.</para>
    ///
    /// <para>Pure and static — no clock, no RNG, no scene. The caller supplies the deviations; this
    /// turns them into the two numbers the register asks for, and its own tests feed it synthetic combs
    /// of known tooth length so the instrument is shown to see the defect before a slot is spent on it.</para>
    /// </summary>
    public static class ShoreCombMath
    {
        /// <summary>
        /// 🔴 <b>THE TOOTH LENGTH.</b> Given the drawn edge's perpendicular deviation from the true
        /// contour, sampled at a uniform <paramref name="sampleSpacingMetres"/> along it, answer the mean
        /// length of a RUN over which that deviation does not meaningfully change.
        ///
        /// <para>A comb is exactly that: the drawn edge holds still for one texel of the height map and
        /// then jumps, so the run length IS the texel pitch. A smooth edge moves a little at every
        /// sample and its runs are one sample long.</para>
        ///
        /// <para><paramref name="stepTolerance"/> is what counts as "did not change" — metres of
        /// deviation. It must be well below the step a comb makes and above the render's own noise; the
        /// caller states it rather than this function guessing.</para>
        /// </summary>
        public static float MeanToothLengthMetres(float[] deviations, float sampleSpacingMetres,
                                                  float stepTolerance)
        {
            if (deviations == null || deviations.Length < 4 || sampleSpacingMetres <= 0f) return 0f;
            int steps = 0;
            for (int i = 1; i < deviations.Length; i++)
                if (Mathf.Abs(deviations[i] - deviations[i - 1]) > stepTolerance) steps++;

            // ⚠️ NO STEPS IS NOT ONE ENORMOUS TOOTH. A smooth edge wanders by less than the tolerance
            // between neighbours, so counting runs alone reported a 400 m tooth for a clean shore — the
            // instrument confusing "moves slowly" with "does not move". Its own dead control caught it.
            // A comb is a sequence of STEPS; with none there is no comb, and 0 says so.
            if (steps == 0) return 0f;
            return deviations.Length * sampleSpacingMetres / (steps + 1);
        }

        /// <summary>
        /// The comb's <b>dominant period</b> along the contour, by autocorrelation — the second opinion
        /// on <see cref="MeanToothLengthMetres"/>, and the one that survives a deviation which drifts as
        /// well as steps. Returns 0 when the signal carries no repeat.
        ///
        /// <para>The mean is removed first, so a shoreline that simply sits offset from its true contour
        /// (a bias, not a comb) reports no period.</para>
        /// </summary>
        public static float DominantPeriodMetres(float[] deviations, float sampleSpacingMetres)
        {
            if (deviations == null || deviations.Length < 16 || sampleSpacingMetres <= 0f) return 0f;
            int n = deviations.Length;

            double mean = 0;
            for (int i = 0; i < n; i++) mean += deviations[i];
            mean /= n;

            var v = new double[n];
            double energy = 0;
            for (int i = 0; i < n; i++) { v[i] = deviations[i] - mean; energy += v[i] * v[i]; }
            if (energy <= 1e-12) return 0f;

            // The first LOCAL MAXIMUM of the autocorrelation is the repeat length. A global maximum is
            // wrong here: autocorrelation of a step train has ever-larger peaks at multiples, and taking
            // the largest would report a harmonic rather than the tooth.
            double previous = double.MaxValue;
            double rising = 0;
            int bestLag = 0;
            for (int lag = 1; lag < n / 3; lag++)
            {
                double acc = 0;
                for (int i = 0; i + lag < n; i++) acc += v[i] * v[i + lag];
                acc /= (n - lag);
                if (acc > previous && rising <= 0) rising = 1;              // started climbing
                if (rising > 0 && acc < previous) { bestLag = lag - 1; break; }   // just crested
                previous = acc;
            }
            return bestLag * sampleSpacingMetres;
        }

        /// <summary>
        /// 🔴 <b>ROW 10's WALL.</b> Across a profile sampled perpendicular to the contour at a uniform
        /// <paramref name="sampleSpacingMetres"/>, the width in metres over which the value falls from
        /// 90 % to 10 % of its range — and, through <paramref name="plateaus"/>, how many flat steps it
        /// crosses on the way, which is the posterisation the register names.
        /// </summary>
        public static float WallWidthMetres(float[] profile, float sampleSpacingMetres,
                                            float stepTolerance, out int plateaus)
        {
            plateaus = 0;
            if (profile == null || profile.Length < 4 || sampleSpacingMetres <= 0f) return 0f;

            float lo = float.MaxValue, hi = float.MinValue;
            foreach (float p in profile) { lo = Mathf.Min(lo, p); hi = Mathf.Max(hi, p); }
            float range = hi - lo;
            if (range <= 1e-6f) return 0f;

            int first = -1, last = -1;
            for (int i = 0; i < profile.Length; i++)
            {
                float t = (profile[i] - lo) / range;
                if (t <= 0.9f && first < 0) first = i;
                if (t >= 0.1f) last = i;
            }

            plateaus = 1;
            for (int i = 1; i < profile.Length; i++)
                if (Mathf.Abs(profile[i] - profile[i - 1]) > stepTolerance) plateaus++;

            return first >= 0 && last > first ? (last - first) * sampleSpacingMetres : 0f;
        }
    }
}
