using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A BIOME'S ZONE.</b> One asset per biome (<c>biome.snake_case</c>, e.g.
    /// <c>biome.stp_blueberry_barren</c>). PR 5 lands the zone only, because the ground needs it: the
    /// barren's outcrops and the swale's hollow (terrain pass 9, part 1 §6).
    ///
    /// <para><b>No recipe field yet.</b> The scene recipe it grows, <c>BiomeRecipeDef</c>, and its species
    /// come with PR 5p, and the field comes with them. Until then it is absent, so no asset can hold a
    /// half-set recipe.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Terrain Plan/Biome", fileName = "Biome")]
    public class BiomeDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only (biome.snake_case).")]
        public string Id = "biome.example";
        public string DisplayName = "";
        [Tooltip("Which zone rule it takes.")]
        public BiomeKind Kind = BiomeKind.Meadow;
        [Tooltip("The rule in words, as the plan gives it.")]
        [TextArea] public string Rule = "";
        [Tooltip("Its polygon, for a barren or a swale; empty otherwise.")]
        public Vector2[] Poly = new Vector2[0];
        [Tooltip("The heights it holds (m): coast above x; marsh between x and y.")]
        public Vector2 Band;
        [Tooltip("A marsh grows where one of these sections dominates.")]
        public string[] SectionIds = new string[0];
        [Tooltip("A barren also takes plateau ground at or above this height (m)...")]
        public float CliffTopMinM;
        [Tooltip("...within this of a cliff wall (m).")]
        public float CliffDistanceM;
    }
}
