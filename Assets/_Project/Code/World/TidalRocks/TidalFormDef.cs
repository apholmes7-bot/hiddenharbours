using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>One named piece of coast standing in the sea</b>: a stack or a ledge, drawn as its own stone rather
    /// than picked from a kit. Its place and its name are the ones the key scene's ground gives the form; its
    /// pictures (dry, wet, and the height of every pixel) are drawn by a recipe and named here by their stem.
    /// The sea cuts the pictures where it stands, so the tide covers and bares the form along its own shape,
    /// and the band just above the water is the wet picture. The level is the tide's, recomputed from the
    /// clock, so nothing here is saved (rule 5).
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Tidal Form", fileName = "TidalForm_")]
    public sealed class TidalFormDef : ScriptableObject
    {
        [Tooltip("Stable id, tidalform.snake_case, named after the key scene's form but kept apart from its ground id. Append-only.")]
        public string Id = "tidalform.example";

        [Tooltip("The name on the chart.")]
        public string DisplayName = "";

        [Tooltip("The key scene whose ground this form belongs to.")]
        public string KeySceneId = "";

        [Tooltip("Where its centre stands, world metres.")]
        public Vector2 At;

        [Tooltip("The stem of its pictures in the forms' art folder (stem.png, stem_wet.png, stem_height.png).")]
        public string ArtStem = "";

        [Header("How the water meets it")]
        [Tooltip("How far above the water it is drawn wet, metres.")]
        [Min(0f)] public float WetBandMetres = 0.3f;

        [Tooltip("A hull cannot pass through its foot. Off for a ledge the tide covers: there a hull meets the ground's own depth.")]
        public bool BlocksBoats;
    }
}
