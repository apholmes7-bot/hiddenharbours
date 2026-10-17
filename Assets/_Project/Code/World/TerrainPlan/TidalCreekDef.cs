using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A TIDAL SIDE CREEK.</b> One asset per creek (<c>creek.snake_case</c>, e.g.
    /// <c>creek.stp_marsh_east</c>), drawn from its junction with <see cref="Joins"/>. It holds no still
    /// water: the tide fills and drains it from both ends (terrain pass 9, part 1 §4).
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Terrain Plan/Tidal Creek", fileName = "TidalCreek")]
    public class TidalCreekDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only (creek.snake_case).")]
        public string Id = "creek.example";
        public string DisplayName = "";
        [Tooltip("The stream it joins at its first point.")]
        public StreamDef Joins;
        public Vector2[] Points = new Vector2[0];
        [Tooltip("The designed bed at each point (m).")]
        public float[] BedZ = new float[0];
        [Tooltip("The channel's width at each point (m).")]
        public float[] Widths = new float[0];
        [Tooltip("The bank's slope (m per m).")]
        public float Bank = 0.6f;
    }
}
