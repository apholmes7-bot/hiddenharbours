using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A BROOK.</b> One asset per stream (<c>stream.snake_case</c>, e.g. <c>stream.stp_alder_run</c>),
    /// drawn source first. Its bed follows <see cref="BedZ"/> at each point, pulled down to the ground less
    /// the plan's incision, and never climbs downstream; a pond-fed brook starts at its pond's sill (terrain
    /// pass 9, part 1 §4).
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Terrain Plan/Stream", fileName = "Stream")]
    public class StreamDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only (stream.snake_case).")]
        public string Id = "stream.example";
        public string DisplayName = "";
        [Tooltip("The pond it drains, or none for a seep.")]
        public PondDef Source;
        [Tooltip("Control points, source first (m).")]
        public Vector2[] Points = new Vector2[0];
        [Tooltip("The designed bed at each point (m).")]
        public float[] BedZ = new float[0];
        [Tooltip("The channel's width at each point (m).")]
        public float[] Widths = new float[0];
        [Tooltip("The water's depth over the bed (m).")]
        public float Depth = 0.12f;
        [Tooltip("The bank's slope beside the channel: fresh reach, tidal reach (m per m).")]
        public Vector2 Bank = new Vector2(0.3f, 0.55f);
        [Tooltip("Where its fresh water runs on past its last point, down the beach to below the spring low (m), or empty. " +
                 "The thread of water is laid on the ground along it at the brook's depth, so a low tide still shows the stream " +
                 "reaching the sea; a higher tide covers it.")]
        public Vector2[] RunOut = new Vector2[0];
        [Tooltip("The width (m) the run-out's water may lay at its start; its bed's dish sets the thread's real edge.")]
        public float RunOutWidth = 2.4f;
        [Tooltip("The width (m) the run-out's water may lay at its end, linear by length from RunOutWidth; 0 keeps RunOutWidth. " +
                 "A stream spreads as it crosses the flat sand to the sea.")]
        public float RunOutWidthEnd;
        [Tooltip("The run-out meanders: it swings to each side of its drawn line by up to x m, over a wavelength of y m along it " +
                 "(seeded noise, zero at its two ends so it leaves the brook and meets the sea where drawn). (0, 0) runs it as drawn.")]
        public Vector2 RunOutMeander;
        [Tooltip("The meander's seed.")]
        public int RunOutSeed;
        [TextArea] public string Why = "";

        /// <summary>The brook's points from <paramref name="from"/> on, then its run-out: the one line a run-out and a bed follow.</summary>
        public Vector2[] LineWithRunOut(int from)
        {
            int f = Mathf.Clamp(from, 0, Mathf.Max(0, Points.Length - 1));
            var run = RunOutLine();
            var o = new Vector2[Points.Length - f + run.Length];
            for (int k = f; k < Points.Length; k++) o[k - f] = Points[k];
            for (int k = 0; k < run.Length; k++) o[Points.Length - f + k] = run[k];
            return o;
        }

        /// <summary>
        /// The run-out as it lies (past the brook's last point): as drawn, or, with a <see cref="RunOutMeander"/>, its drawn line
        /// walked at an eighth of the wavelength and each station swung across it by the amplitude × seeded noise × sin(π t), t its
        /// share of the run-out's length, so it leaves the brook's last point and ends at its own last point as drawn.
        /// </summary>
        public Vector2[] RunOutLine()
        {
            int n = RunOut == null ? 0 : RunOut.Length;
            if (n == 0) return new Vector2[0];
            double amp = RunOutMeander.x, lam = RunOutMeander.y;
            if (!(amp > 0) || !(lam > 0) || Points == null || Points.Length == 0) return (Vector2[])RunOut.Clone();
            var drawn = new Vector2[n + 1];
            drawn[0] = Points[Points.Length - 1];
            for (int k = 0; k < n; k++) drawn[k + 1] = RunOut[k];
            var cum = new double[n + 1];
            for (int k = 1; k <= n; k++) cum[k] = cum[k - 1] + Vector2.Distance(drawn[k - 1], drawn[k]);
            double L = cum[n];
            if (!(L > 0)) return (Vector2[])RunOut.Clone();
            int m = Mathf.Max(n, (int)System.Math.Ceiling(L / (lam / 8.0)));
            var o = new Vector2[m];
            int seg = 0;
            for (int j = 1; j <= m; j++)
            {
                double a = L * j / m;
                while (seg < n - 1 && cum[seg + 1] < a) seg++;
                double sl = cum[seg + 1] - cum[seg];
                double t = sl > 0 ? (a - cum[seg]) / sl : 0;
                Vector2 p0 = drawn[seg], p1 = drawn[seg + 1];
                Vector2 dir = (p1 - p0).normalized, nrm = new Vector2(-dir.y, dir.x);
                double sway = amp * System.Math.Sin(System.Math.PI * a / L) * Meander(a, lam, RunOutSeed);
                o[j - 1] = new Vector2((float)(p0.x + (p1.x - p0.x) * t + nrm.x * sway), (float)(p0.y + (p1.y - p0.y) * t + nrm.y * sway));
            }
            o[m - 1] = RunOut[n - 1];
            return o;
        }

        /// <summary>
        /// A meander's swing (about -1 to 1) at <paramref name="a"/> m along: a bend every half wavelength, its wavelength and
        /// phase drifting by the seed (two sines of seeded phase, the second at 0.53 of the wavelength and a third of the weight), so
        /// no two bends repeat.
        /// </summary>
        public static double Meander(double a, double lam, int seed)
        {
            double p1 = 2 * System.Math.PI * TerrainPlanMath.Hash01(seed, 1, seed), p2 = 2 * System.Math.PI * TerrainPlanMath.Hash01(seed, 2, seed);
            return 0.75 * System.Math.Sin(2 * System.Math.PI * a / lam + p1) + 0.25 * System.Math.Sin(2 * System.Math.PI * a / (0.53 * lam) + p2);
        }

        /// <summary>The width (m) the run-out's water may lay at a share <paramref name="t"/> (0 to 1) of its length.</summary>
        public float RunOutWidthAt(float t) =>
            Mathf.Lerp(RunOutWidth, RunOutWidthEnd > 0 ? RunOutWidthEnd : RunOutWidth, Mathf.Clamp01(t));
    }
}
