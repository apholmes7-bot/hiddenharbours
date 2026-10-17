using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A POOL THE TIDE LEAVES.</b> One asset per pool (<c>pool.snake_case</c>, e.g.
    /// <c>pool.stp_bar_b03</c>). Its water stands at its lowest rim: the spill is derived from the ground,
    /// never typed. Its dish is carved <see cref="Depth"/> below that (terrain pass 9, part 2 §5, §7).
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Terrain Plan/Tidal Pool", fileName = "TidalPool")]
    public class TidalPoolDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only (pool.snake_case).")]
        public string Id = "pool.example";
        public Vector2 Centre;
        [Tooltip("Radii (m): along, across.")]
        public Vector2 Radii = new Vector2(3f, 2f);
        public float RotationDeg;
        [Tooltip("The dish's depth below the spill (m): 0.45 or less wades, 0.6 or more swims.")]
        public float Depth = 0.35f;
        [Tooltip("The crossing's layout it belongs to: \"A and B\", or \"B\".")]
        public string Layout = "A and B";
    }
}
