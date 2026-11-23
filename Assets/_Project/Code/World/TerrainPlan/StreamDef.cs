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
        [Tooltip("The width (m) the run-out's water may lay; its bed's dish sets the thread's real edge.")]
        public float RunOutWidth = 2.4f;
        [TextArea] public string Why = "";

        /// <summary>The brook's points from <paramref name="from"/> on, then its run-out: the one line a run-out and a bed follow.</summary>
        public Vector2[] LineWithRunOut(int from)
        {
            int f = Mathf.Clamp(from, 0, Mathf.Max(0, Points.Length - 1));
            int runOut = RunOut == null ? 0 : RunOut.Length;
            var o = new Vector2[Points.Length - f + runOut];
            for (int k = f; k < Points.Length; k++) o[k - f] = Points[k];
            for (int k = 0; k < runOut; k++) o[Points.Length - f + k] = RunOut[k];
            return o;
        }
    }
}
