using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>ONE STRETCH OF A REGION'S SHORE.</b> One asset per section (<c>coast.snake_case</c>, e.g.
    /// <c>coast.stp_north_cove</c>). A cut or fill section lays a cross-shore profile along its part of
    /// the reference line (terrain pass 9, part 1 §3). A paint-only section keeps the ground and repaints
    /// it inside a polygon, or in a bearing sector below a height. The crossing (part 2 §5) is the bar:
    /// its runnels, lobes, pools and paint.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Terrain Plan/Coast Section", fileName = "CoastSection")]
    public class CoastSectionDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (coast.snake_case).")]
        public string Id = "coast.example";
        public string DisplayName = "";
        [Tooltip("The section's type: sand_beach, shingle_cobble, ledge_platform, salt_marsh, mud_flat, sandy_flats or the_bar.")]
        public string Type = "sand_beach";
        public CoastSectionMode Mode = CoastSectionMode.Fill;
        [Tooltip("The ground material by elevation.")]
        public CoastRecipeDef Recipe;

        [Header("Cut or fill: the profile")]
        [Tooltip("Edge wobble of the shore distance: amplitude (m), wavelength (m).")]
        public Vector2 Edge = new Vector2(2f, 10f);
        [Tooltip("The cross-shore profile: (signed distance from the line, + inland (m), elevation (m)).")]
        public Vector2[] Knots = new Vector2[0];
        [Tooltip("The profile fades into the sea over this many metres past its last seaward knot.")]
        public float SeaTail = 12f;
        [Tooltip("A ledge's rock ends this far out (signed distance, m); beyond it the bed is sand again.")]
        public bool HasRockEdge;
        public float RockEdge;

        [Header("Paint only")]
        [Tooltip("The polygon it repaints, or empty for a sector.")]
        public Vector2[] Poly = new Vector2[0];
        [Tooltip("The bearing sector it repaints (degrees), when it has no polygon.")]
        public Vector2 Sector;
        [Tooltip("The sector repaints below this height (m).")]
        public float Below;

        [Header("The crossing (part 2 §5)")]
        [Tooltip("The bar's west and east ends (x, m).")]
        public Vector2 XRange;
        public float HalfWidth;
        [Tooltip("The kinds run this much further west than XRange.x (m), to the map's pass.")]
        public float WindowWest = 30f;
        public float GutX;
        [Tooltip("No runnel or lobe within this of the gut (m).")]
        public float GutKeep;
        [Tooltip("Runnel spacing (m): its mean is the spacing.")]
        public Vector2 RunnelEvery;
        public float RunnelWidth;
        public float RunnelDepth;
        [Tooltip("Runnels cut ground between these heights (m).")]
        public Vector2 RunnelBand = new Vector2(-1.8f, 0.4f);
        [Tooltip("Runnels cut between these distances off the crest line (m).")]
        public Vector2 RunnelAcross = new Vector2(5.5f, 30f);
        public float MusselNearGut;
        public Vector2 MusselBand;
        public float LobeAmp;
        public float LobeLambda;
        [Tooltip("The lobes rise over (x, y) and fall over (z, w): distance off the crest line (m).")]
        public Vector4 LobeBand;
        [Tooltip("The bar's own paint runs below this height (m).")]
        public float PaintBelow = 2.4f;
        [Tooltip("The crest band's freeze: half width (m), feather (m).")]
        public Vector2 CrestKeep = new Vector2(5f, 1.5f);
        [Tooltip("Part 1's bar-head flats keep their ground inside this polygon...")]
        public Vector2[] HeadKeep = new Vector2[0];
        [Tooltip("...feathered over this (m).")]
        public float HeadKeepFeather = 3f;
        [Tooltip("A pool releases the freeze inside (radius factor, feather).")]
        public Vector2 PoolRelease = new Vector2(1.2f, 0.3f);
        [Tooltip("A pool's outline wobbles by this fraction.")]
        public float PoolWobble = 0.06f;

        [TextArea] public string Why = "";
    }
}
