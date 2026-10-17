using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A WAVE-CUT PLATFORM ROUND A HEADLAND'S FOOT.</b> One asset per platform (<c>ground.snake_case</c>, e.g.
    /// <c>ground.stp_ne_platform</c>). Out from its form's brow it falls from <see cref="Foot"/> by
    /// <see cref="Fall"/> over <see cref="Width"/> metres, then blends into the ground under it over
    /// <see cref="Blend"/>; it fades in from <see cref="RootBlend"/> about the root line and out between
    /// <see cref="Reach"/>.x and .y. It only raises the ground (the key scene's TerrainChangeDef).
    ///
    /// <para><b>Its rock pools</b> are small basins, laid as part 2 §7.5 lays the reefs': placed by hash of the
    /// plan's seed along the brow, at most <see cref="PoolDepth"/>.y deep, filled to their lowest rim. The
    /// derivation lists each in the manifest. Outside the basins the platform keeps its own heights.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Terrain Plan/Ground Platform", fileName = "GroundPlatform")]
    public class GroundPlatformDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only (ground.snake_case).")]
        public string Id = "ground.example";
        public string DisplayName = "";
        [Tooltip("The form whose foot it rings.")]
        public FormDef Form;

        [Header("The platform")]
        [Tooltip("Its height at the brow (m).")]
        public float Foot = 0.85f;
        [Tooltip("It falls this much (m)...")]
        public float Fall = 2.6f;
        [Tooltip("...over this width out from the brow (m).")]
        public float Width = 6.5f;
        [Tooltip("Then it blends into the ground under it over this (m).")]
        public float Blend = 7f;
        [Tooltip("It fades out between these distances from the brow (m).")]
        public Vector2 Reach = new Vector2(12f, 16f);
        [Tooltip("It fades in between these distances about the root line (m, + seaward).")]
        public Vector2 RootBlend = new Vector2(-3f, 1f);

        [Header("Rock pools (part 2 §7.5's rule)")]
        public float PoolsPer100m = 15f;
        [Tooltip("A pool's radius (m): min, max. Its outline is 1.25 x 0.85 of it.")]
        public Vector2 PoolRadius = new Vector2(0.8f, 1.5f);
        [Tooltip("A pool's depth below its spill (m): min, max.")]
        public Vector2 PoolDepth = new Vector2(0.2f, 0.3f);
        [Tooltip("A pool's centre lies this far out from the brow (m): min, max.")]
        public Vector2 PoolBand = new Vector2(1.5f, 5f);
        [Tooltip("A pool's outline wobbles by this fraction.")]
        public float PoolWobble = 0.14f;

        [Header("Paint")]
        [Tooltip("The platform's ground by height, as the key scene drew it.")]
        public CoastRecipeDef Recipe;
        [Tooltip("A rock pool's floor (the kit's slot).")]
        public string PoolZone = "irishmoss";

        [Header("Source")]
        public string SourceId = "";
        [TextArea] public string Why = "";
    }
}
