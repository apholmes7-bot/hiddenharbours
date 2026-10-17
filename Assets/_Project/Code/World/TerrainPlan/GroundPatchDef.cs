using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A LOT: A PATCH OF WORKED GROUND.</b> One asset per lot (<c>patch.snake_case</c>, e.g.
    /// <c>patch.stp_ginnys_garden</c>). Paint only: the box is painted in <see cref="Material"/> at
    /// <see cref="Weight"/>, after the plan's paths; its ground keeps its height.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Terrain Plan/Ground Patch", fileName = "GroundPatch")]
    public class GroundPatchDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only (patch.snake_case).")]
        public string Id = "patch.example";
        public string DisplayName = "";
        [Tooltip("The lot's box (m): min...")]
        public Vector2 Min;
        [Tooltip("...max.")]
        public Vector2 Max;
        [Tooltip("The kit's slot it paints (the lots are earth: dirt).")]
        public string Material = "dirt";
        [Range(0f, 1f)] public float Weight = 1f;
        [Tooltip("The key scene's fence round it, or empty (the key scene's own piece; no ground).")]
        public string Fence = "";

        [Header("Source")]
        [Tooltip("The key scene's id for it.")]
        public string SourceId = "";
        [TextArea] public string Why = "";
    }
}
