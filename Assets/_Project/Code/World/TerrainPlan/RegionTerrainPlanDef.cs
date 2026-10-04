using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A REGION'S TERRAIN PLAN.</b> One asset per region (<c>terrain_plan.snake_case</c>, e.g.
    /// <c>terrain_plan.st_peters</c>). It holds the seed, the shore reference line, the plan-wide rules
    /// and the lists of the plan's Defs, in the order the derivation reads them: a list's order is part of
    /// the data, because each Def's noise salt is its index (terrain pass 9, part 1 §8.1, Appendix A).
    ///
    /// <para><c>TerrainPlanDerivation</c> reads it with the analytic terrain, today's splat and the
    /// protected set, and derives the height, splat and still-water maps. The maps are outputs; this
    /// asset and the Defs it lists are the source.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Terrain Plan/Region Terrain Plan", fileName = "RegionTerrainPlan")]
    public class RegionTerrainPlanDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (terrain_plan.snake_case).")]
        public string Id = "terrain_plan.example";
        [Tooltip("The RegionDef id this plan shapes.")]
        public string RegionId = "region.example";
        [Tooltip("Every noise and hash in the plan is a function of (cell, Seed + a fixed salt).")]
        public int Seed = 9109;

        [Header("The kit's tide frame (part 1 §5)")]
        [Tooltip("The kit's heights run 0 to this across a spring range.")]
        public float KitSpringRangeM = 4f;
        [Tooltip("The region's spring amplitude (m): kit m = (E + spring) * KitSpringRangeM / (2 * spring).")]
        public float SpringM = 2.2f;

        [Header("The maps' height range (decision 1a)")]
        [Tooltip("The painted height map's range (m): its R16 codes run from x to y, and the crossing floors its kinds at x. " +
                 "The PaintedHeightMap asset, the splat's and the sea's _heightMin/_heightMax and the manifest take it together.")]
        public Vector2 HeightRange = new Vector2(-4f, 12f);

        [Header("The ground file (PR 5 B)")]
        [Tooltip("The region's ground file, or none. With one, the height map is the file's import (GroundFileImport); the " +
                 "derivation's own heights become a check, and its paint and still water read the import.")]
        public GroundFileDef Ground;

        [Header("The shore reference line")]
        public ShorePoint[] ShoreLine = new ShorePoint[0];
        [Tooltip("The line's resampling step (m).")]
        public float ShoreStep = 1f;
        [Tooltip("Straight run-outs past both ends (m), so a point beyond an end fades along a straight edge.")]
        public float ShoreRunOut = 60f;
        [Tooltip("The coast step's window: the line's bounds plus this (m).")]
        public float ShoreWindow = 90f;
        [Tooltip("A section's feather at the shore (degrees of bearing).")]
        public float SectionFeather = 3f;
        [Tooltip("The feather widens by this many degrees per metre offshore, past FeatherGrowthFrom.")]
        public float FeatherGrowth = 0.22f;
        public float FeatherGrowthFrom = 8f;
        [Tooltip("The line's ends fade out over this many degrees.")]
        public float EndFeather = 6f;
        [Tooltip("Domain warp of the shore distance: amplitude (m), wavelength (m).")]
        public Vector2 Warp = new Vector2(1.4f, 7f);
        [Tooltip("A fill section stops raising the land this far inland (m), over LandFadeFeather.")]
        public float LandFade = 70f;
        public float LandFadeFeather = 10f;
        [Tooltip("A cut section fades out over this many metres past its last knot.")]
        public float CutFade = 6f;

        [Header("Beds and water (part 1 §4)")]
        [Tooltip("A channel's bed sits at least this far below the ground along its line (m).")]
        public float Incise = 0.22f;
        [Tooltip("A side creek's or a pond outlet's incision ramps in over this (m).")]
        public float JoinRamp = 3f;
        [Tooltip("A salt pan's dish depth below its lowest rim (m).")]
        public float PanDepth = 0.12f;
        [Tooltip("Streams, creeks and paths are resampled at this step (m).")]
        public float LineStep = 0.25f;

        [Header("Paint")]
        [Tooltip("A recipe's band edge wanders by this (m) over this wavelength (m).")]
        public Vector2 RecipeJitter = new Vector2(0.16f, 2.5f);
        [Tooltip("Ground at or above this is the plateau (m).")]
        public float PlateauM = 3.8f;
        [Tooltip("Plateau ground within this of a tree is the woods' floor (m).")]
        public float WoodsFloorM = 7f;
        [Tooltip("A section repaints where its weight passes this.")]
        public float SectionDominance = 0.05f;
        [Tooltip("A section repaints above this height (m)...")]
        public float SectionPaintFloor = -2.6f;
        [Tooltip("...and below this, or wherever its ground moved more than SectionPaintMoved (m).")]
        public float SectionPaintCeiling = 4.3f;
        public float SectionPaintMoved = 0.02f;
        [Tooltip("A new path's tread sits this far below the ground (m).")]
        public float PathDip = 0.03f;
        [Tooltip("A path's edge wanders by this (m) over this wavelength (m).")]
        public Vector2 PathEdge = new Vector2(0.12f, 2f);

        [Header("Part 1: the coast")]
        [Tooltip("Cut and fill sections, west to east along the line.")]
        public CoastSectionDef[] Sections = new CoastSectionDef[0];
        [Tooltip("Paint-only sections: the ground keeps its height.")]
        public CoastSectionDef[] PaintOnly = new CoastSectionDef[0];
        public CoastRecipeDef[] Recipes = new CoastRecipeDef[0];

        [Header("Part 1: the water")]
        public PondDef[] Ponds = new PondDef[0];
        public StreamDef[] Streams = new StreamDef[0];
        public TidalCreekDef[] Creeks = new TidalCreekDef[0];
        public SaltPanDef[] Pans = new SaltPanDef[0];

        [Header("Still water on the ground file (PR 5 B; ADR 0046)")]
        [Tooltip("A key scene's pools that stand by flood at their surface (pool.snake_case PondDefs, no outlet), laid after the " +
                 "ponds. A pool whose flood escapes drops by whole height-map steps until it holds.")]
        public PondDef[] StillPools = new PondDef[0];
        [Tooltip("A pond's or pool's flood runs inside its larger radius times this, plus its basin's reach (m)...")]
        public float FloodWindow = 4f;
        [Tooltip("...and leaks if it reaches the window's edge or passes this many radii.")]
        public float FloodLeak = 2f;
        [Tooltip("A pond's outlet is left out of its flood: within x (m) of the outlet's line, past y (m) along it.")]
        public Vector2 OutletGully = new Vector2(2.5f, 1f);
        [Tooltip("A hollow (part 2 §7.4: a closed basin above the spring low) keeps its water at its spill when it is at " +
                 "least this deep (m)...")]
        public float HollowMinDepth = 0.1f;
        [Tooltip("...and holds at least this much ground (m², a cell being Mpp² / IsoGround.GroundDepthScale of ground).")]
        public float HollowMinAreaM2 = 4f;

        [Header("The ground file's bays (PR 5 B)")]
        [Tooltip("Beaches a ground ask laid, each painted by its own bands on the import (amendment 2 §4.5).")]
        public BayDef[] Bays = new BayDef[0];

        [Header("Paths and biomes")]
        [Tooltip("Part 1's seven first, in their order; new paths are appended, never inserted.")]
        public PathDef[] Paths = new PathDef[0];
        public BiomeDef[] Biomes = new BiomeDef[0];

        [Header("The village's routes (PR 5 B)")]
        [Tooltip("The village plan's lanes and walks (V1's RouteDefs), painted in one pass after the keep, as the roads are, " +
                 "each by its class's label (TerrainPlanValidation.RouteClasses), its width in ground metres as the village measures it.")]
        public RouteDef[] Routes = new RouteDef[0];

        [Header("Part 2: the crossing (layout B)")]
        [Tooltip("Part 2's noise and hashes are a function of (cell, Part2Seed + a fixed salt).")]
        public int Part2Seed = 9209;
        public CoastSectionDef Crossing;
        [Tooltip("The crossing's pools: layout A's twelve, then B's eleven.")]
        public TidalPoolDef[] Pools = new TidalPoolDef[0];
        [Tooltip("A pool's dish is flat inside this fraction of its radius, then rises to its lip.")]
        public float PoolLip = 0.5f;
        [Tooltip("The bar walk passes each crest pool this far off its water (m).")]
        public float WalkClear = 1f;
        [Tooltip("A clam hole the kinds carry out of its band pulls back: radius (m), feather (m), margin (m).")]
        public Vector3 ClamPull = new Vector3(1.5f, 4f, 0.03f);
        [Tooltip("The clams' tide band (m): the spring range less the builder's clam margin.")]
        public Vector2 ClamBand = new Vector2(-1.8f, 1.8f);

        [Header("The key scenes' ground (§4.1 tier 3)")]
        public FormDef[] Forms = new FormDef[0];
        public GroundRampDef[] Ramps = new GroundRampDef[0];
        public GroundPlatformDef[] Platforms = new GroundPlatformDef[0];
        public TidalBarDef[] Bars = new TidalBarDef[0];
        public WaterfallDef[] Falls = new WaterfallDef[0];
        [Tooltip("Lots: paint only.")]
        public GroundPatchDef[] Patches = new GroundPatchDef[0];

        [Header("The key scenes' frozen pieces (§4.1 tier 2): part 1's plan heights")]
        public FrozenPiece[] FrozenPieces = new FrozenPiece[0];
        public FrozenBox[] FrozenBoxes = new FrozenBox[0];
        [Tooltip("A frozen path holds within its half width plus this (m)...")]
        public float FrozenPathMargin = 0.5f;
        [Tooltip("...feathered over this (m).")]
        public float FrozenPathFeather = 1f;

        [Header("The protected set")]
        public TerrainPlanKeep Keep = new TerrainPlanKeep();
    }
}
