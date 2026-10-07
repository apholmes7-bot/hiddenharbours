using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A PATH ON THE GROUND.</b> One asset per path (<c>path.snake_case</c>, e.g.
    /// <c>path.stp_shore_path</c>). It is painted along its line, <see cref="Width"/> wide, in
    /// <see cref="Material"/> at <see cref="Weight"/>; a weight of 0 is a line that is never painted, such
    /// as the bar walk (terrain pass 9, part 1 §5; the key scenes' labels, handoff §4.3).
    ///
    /// <para>An existing road names the builder's own line in <see cref="LineSource"/> and carries no
    /// points: the deriver takes the line from that source, so the village keeps one. Where it runs on into a
    /// key scene's line, it carries that line in <see cref="JoinPoints"/> (the bar head road, amendment 2 §4.6);
    /// the protected ground along the road stays the builder's line's.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Terrain Plan/Path", fileName = "Path")]
    public class PathDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only (path.snake_case).")]
        public string Id = "path.example";
        public string DisplayName = "";
        public PathKind Kind = PathKind.Footpath;
        [Tooltip("The painted width (m).")]
        public float Width = 1f;
        [Tooltip("The line (m). Empty for an existing road whose line is the builder's.")]
        public Vector2[] Points = new Vector2[0];
        [Tooltip("An existing road: the builder's line it follows (class.member).")]
        public string LineSource = "";
        [Tooltip("An existing road that joins a key scene's line: the builder's line runs to where it first reaches the first " +
                 "of these points' x, then on through them (m). Empty: the builder's line, whole.")]
        public Vector2[] JoinPoints = new Vector2[0];

        [Header("Paint")]
        [Tooltip("The kit's slot it paints: path (default) or another zone.")]
        public string Material = "path";
        [Tooltip("How much of the cell it paints: 1 = all; 0 = never painted.")]
        [Range(0f, 1f)] public float Weight = 1f;
        [Tooltip("The key scene's label for it (road, cart track, path, worn grass, dirt).")]
        public string Label = "";
        [Tooltip("Ruts are laid along it (road and cart track).")]
        public bool Ruts;

        [Header("Key scenes")]
        [Tooltip("The key scene's id for it, where it came from one.")]
        public string SourceId = "";
        [Tooltip("A key scene's frozen path (§4.1 tier 2): its ground holds part 1's plan heights.")]
        public bool Frozen;
        [Tooltip("Paint only: a key scene's path has no heights, so its tread is not dipped.")]
        public bool PaintOnly;

        [TextArea] public string Why = "";
    }
}
