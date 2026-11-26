using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A group of offshore rocks the tide covers and uncovers.</b> The rocks themselves are rows of a key
    /// scene (one row a rock, each with its own stable id, its place and the height of its seat); this names
    /// which rows, and how they are drawn in the water. The sea covers a rock from its seat upward along the
    /// rock's own lower edge, and the band just above the water is drawn in the kit's wetter state. Where the
    /// water stands is the tide's, recomputed from the clock, so nothing here is saved (rule 5).
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Tidal Rock Field", fileName = "TidalRockField_")]
    public sealed class TidalRockFieldDef : ScriptableObject
    {
        [Tooltip("Stable id, rockfield.snake_case. Append-only.")]
        public string Id = "rockfield.example";

        [Tooltip("The name on the chart.")]
        public string DisplayName = "";

        [Tooltip("The key scene whose rows these rocks are.")]
        public string KeySceneId = "";

        [Tooltip("The rows that belong to this field: every rock row whose id starts with this.")]
        public string PiecePrefix = "";

        [Header("How the water meets them")]
        [Tooltip("How far above the water a rock is drawn wet, metres.")]
        [Min(0f)] public float WetBandMetres = 0.25f;

        [Tooltip("The kit's tide state the wet band is drawn in: dry, wet or awash.")]
        public string WetBandState = "awash";
    }
}
