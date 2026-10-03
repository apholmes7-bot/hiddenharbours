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
        [TextArea] public string Why = "";
    }
}
