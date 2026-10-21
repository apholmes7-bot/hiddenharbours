using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A SHORE TYPE'S GROUND MATERIALS BY ELEVATION.</b> One asset per type
    /// (<c>coast_recipe.&lt;type&gt;</c>, e.g. <c>coast_recipe.sand_beach</c>). The bands are read first
    /// match from below: a cell takes the zone of the lowest band whose top it sits under (terrain pass 9,
    /// part 1 §5, Appendix A).
    ///
    /// <para>A bay a ground file laid carries its own (<c>coast_recipe.stp_main_beach</c>: CD's bands for the main
    /// beach, amendment 2 §4.5), which <see cref="BayDef"/> reads with no jitter of its own.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Terrain Plan/Coast Recipe", fileName = "CoastRecipe")]
    public class CoastRecipeDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only (coast_recipe.<type>).")]
        public string Id = "coast_recipe.example";
        [Tooltip("The section type it paints.")]
        public string Type = "sand_beach";
        [Tooltip("(top elevation m, zone), lowest first. The last band's top is above any ground.")]
        public RecipeBand[] Bands = new RecipeBand[0];
    }
}
