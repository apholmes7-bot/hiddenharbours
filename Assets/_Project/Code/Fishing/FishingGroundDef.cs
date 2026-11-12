using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// A named fishing ground: a rectangle of a region's water, the depth and bottom it has, and the
    /// fish it holds. One asset per ground (ADR 0003). The ground says nothing about gear or catch
    /// rates; whatever fishes it reads the depth, the bottom and the species from here.
    ///
    /// <para>The region is named by id, never by reference: Fishing does not know World's
    /// <c>RegionDef</c> (rule 4).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Fishing/Fishing Ground", fileName = "FishingGround")]
    public class FishingGroundDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (fishing_ground.snake_case).")]
        public string Id = "fishing_ground.";
        public string DisplayName = "";
        [TextArea] public string Description = "";

        [Header("Where")]
        [Tooltip("The region this ground lies in (region.snake_case).")]
        public string RegionId = "region.";
        [Tooltip("Centre of the ground, the region's own metres.")]
        public Vector2 Center;
        [Tooltip("Half-size of the ground (x = half-width, y = half-height), metres.")]
        public Vector2 HalfSize = new Vector2(100f, 100f);

        [Header("The bottom")]
        [Tooltip("Shallowest water over the ground at mean tide, metres.")]
        [Min(0f)] public float MinDepthMetres;
        [Tooltip("Deepest water over the ground at mean tide, metres.")]
        [Min(0f)] public float MaxDepthMetres;
        [Tooltip("What the bottom is made of.")]
        public EcologyHabitat Bottom = EcologyHabitat.Mud;

        [Header("The fish")]
        [Tooltip("Species this ground holds (fish.snake_case). Each must also list the region.")]
        public string[] FishIds = new string[0];

        public Rect WorldRect => new Rect(Center - HalfSize, HalfSize * 2f);

        public bool Contains(Vector2 worldPos) =>
            Mathf.Abs(worldPos.x - Center.x) <= HalfSize.x && Mathf.Abs(worldPos.y - Center.y) <= HalfSize.y;

        public bool Holds(string fishId)
        {
            if (FishIds == null) return false;
            for (int i = 0; i < FishIds.Length; i++)
                if (FishIds[i] == fishId) return true;
            return false;
        }
    }
}
