using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A SALT PAN ON THE MARSH.</b> One asset per pan (<c>pan.snake_case</c>, e.g.
    /// <c>pan.stp_lagoon_marsh_a</c>). It fills to its lowest rim point, so its water is flat on a sloping
    /// platform; its dish is carved the plan's pan depth below that (terrain pass 9, part 1 §4).
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Terrain Plan/Salt Pan", fileName = "SaltPan")]
    public class SaltPanDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only (pan.snake_case).")]
        public string Id = "pan.example";
        public Vector2 Centre;
        [Tooltip("Radii (m): along, across.")]
        public Vector2 Radii = new Vector2(2f, 1.5f);
        public float RotationDeg;
    }
}
