namespace HiddenHarbours.World
{
    /// <summary>
    /// The rule numbers terrain pass 9's derivation (plan9_lib.py, p2_lib.py) carries in its code and no Def holds yet:
    /// the relief a section type adds, a pond's bowl, a channel's dish, the paint rules' bands. Named here, in one
    /// place, so they can move into the Defs without a hunt. Each changes the maps; none is a tunable to reach for.
    /// </summary>
    public static class TerrainPlanRules
    {
        // ---- the coast: the relief a section type adds to its profile -----------------------------------------------------
        public const double LedgeRough = 0.22, LedgeRoughLambda = 3.5, LedgeRough2 = 0.12, LedgeRough2Lambda = 1.6;
        public const double LedgeBenchLambda = 5.0, LedgeBenchSteps = 3.4, LedgeBenchStep = 0.14, LedgeBenchDrop = 0.2;
        public const double LedgeOnFrom = -1.6, LedgeOnTo = 1.2, LedgeBluff = 1.6;
        public const double MarshReliefAbove = 0.6, MarshRelief = 0.05, MarshReliefLambda = 6.0;
        public const double ShingleReliefAbove = 2.0, ShingleRelief = 0.15, ShingleReliefLambda = 5.0;
        public const int SeedWarpX = 1, SeedWarpY = 2, SeedEdge = 11, SeedEdgeStep = 7;
        public const int SeedLedge = 41, SeedLedge2 = 43, SeedBench = 45, SeedMarsh = 47, SeedShingle = 49;

        // ---- water: ponds, channels, pans ---------------------------------------------------------------------------------
        public const double PondReach = 2.6, PondWobble = 0.14, PondWobbleLambda = 3.0, PondBankSlope = 0.22;
        public const double PondBowlReach = 2.5, PondStillReach = 1.35;
        public const int SeedPond = 61, SeedPan = 71;
        public const double StreamWindow = 25, CreekWindow = 12, StreamSmooth = 0.45, CreekSmooth = 0.3;
        public const double ChannelDish = 0.05, ChannelEdge = 0.05, ChannelMinHalf = 1e-3, TidalDepthShare = 0.6;
        public const double PanReach = 1.6, PanRimFrom = 0.92, PanRimTo = 1.12, PanRimDrop = 0.02;
        public const double MouthShare = 0.6, MouthReach = 40, MouthBed = 12, MouthBedNoise = 6, MouthBedLambda = 4;
        public const int SeedMouth = 350;

        // ---- paint ----------------------------------------------------------------------------------------------------------
        public const int SeedPaintOnly = 500, SeedSection = 510, SeedFeature1 = 300, SeedFeature2 = 310, SeedPath = 600;
        public const double Feature1Lambda = 2.2, Feature2Lambda = 1.5, PaintOnlyDeep = -3.4;
        public const int PaintOnlyK = 90;
        public const double SectionDominant = 0.5;
        public const double BarrenGraniteLambda = 2.0, BarrenGranite2Lambda = 1.5, BarrenGranite2 = 0.25;
        public const double BarrenShelf = 0.66, BarrenTalus = 0.62, BarrenDirt = 0.74, BarrenDirtLambda = 1.5;
        public const double BarrenBank = 2.0, BarrenPathGap = 1.0, BarrenRise = 0.25, BarrenRiseMore = 0.4, BarrenRiseSpan = 0.13;
        public const int SeedGranite = 400, SeedGranite2 = 401, SeedDirt = 403;
        public const double SwaleBank = 2.0, SwaleBankNoise = 10.0, SwaleLambda = 3.0, SwaleMud = 0.7, SwaleMudLambda = 1.5;
        public const int SeedSwale = 410, SeedSwaleMud = 411;
        public const double FreshAbove = 2.4, FreshMud = 1.2, FreshSedge = 3.5, FreshSedgeNoise = 2.0, FreshLambda = 2.0;
        public const int SeedFresh = 412;
        public const double TidalMudAbove = -1.3, TidalSiltFrom = -0.8, TidalSiltTo = 2.4, TidalSiltBank = 1.0;
        public const double PathEdgeLambda = 2.0;
        public const double PathWindow = 4.0, DistanceCap = 30.0;
        public const string PaintOnlySkipsClass = "Access";

        // ---- what a section type carries at map scale (pass 9's features_zone): (from, to) heights, noise thresholds --
        public const int SeedSandRunnel = 320, SeedMudSilt = 330, SeedFlatsSilt = 340, SeedRockEdge = 355;
        public const double SandRunnelOffset = 13.0, SandRunnelWander = 2.0, SandRunnelLambda = 9.0, SandRunnelHalf = 1.1;
        public const double SandRunnelFrom = -1.9, SandRunnelTo = -0.4;
        public const double SandTalusFrom = -1.2, SandTalusTo = 0.5, SandTalusAbove = 0.74;
        public const double LedgeTalusHighFrom = 1.7, LedgeTalusHighTo = 3.2, LedgeTalusHighAbove = 0.52;
        public const double LedgeTalusLowFrom = -1.0, LedgeTalusLowTo = 0.3, LedgeTalusLowAbove = 0.66;
        public const double RockEdgeWander = 2.0, RockEdgeLambda = 5.0, RockEdgeEelgrassBelow = -2.0;
        public const double ShingleWeedFrom = -0.4, ShingleWeedTo = 0.8, ShingleWeedAbove = 0.6;
        public const double ShingleTalusFrom = 2.5, ShingleTalusTo = 3.3, ShingleTalusAbove = 0.62;
        public const double SectionMusselAbove = 0.55;
        public const double MudMusselFrom = -1.5, MudMusselTo = -0.7, MarshMusselFrom = -1.3, MarshMusselTo = -0.5;
        public const double MudSiltFrom = -1.8, MudSiltTo = 0.0, MudSiltLambda = 6.0, MudSiltHalf = 0.08;
        public const double FlatsSiltFrom = -1.3, FlatsSiltTo = -0.1, FlatsSiltLambda = 7.0, FlatsSiltHalf = 0.09;

        // ---- the crossing (part 2 §5, layout B) ---------------------------------------------------------------------------
        public const int SeedBarRecipe = 540, SeedRunnel = 541, SeedMussel = 542, SeedLobe = 545, SeedBarPool = 91;
        public const double RunnelLambda = 23.0, RunnelSkew = 0.6, RunnelNoise = 3.0, MusselLambda = 2.0, MusselAbove = 0.45;
        public const double LobeSideOffset = 400.0, PoolReach = 1.8, PoolEllipseLambda = 2.0;
        public const double MovedBy = 1e-9;

        // ---- the still water on a ground file (PR 5 B; amendment 2 §4.2) ----------------------------------------------------
        /// <summary>How many height-map steps a pool's flood may drop to hold (4096 steps of 16/65535 m: about a metre).</summary>
        public const int StillStepsDown = 4096;
        public const int SeedBay = 570;
        /// <summary>The village's routes wander by the plan's PathEdge, each on its own salt: this plus its index.</summary>
        public const int SeedRoute = 700;

        // ---- the key scenes ------------------------------------------------------------------------------------------------
        public const int SeedHeadTop = 560, SeedHeadNeck = 561, SeedHeadPlatform = 562, SeedHeadBar = 563, SeedHeadPlinth = 564;
        public const int KeyScenePieceSeedStep = 5;
        public const int SeedPlatformPool = 221, SeedPlatformPoolOff = 223, SeedPlatformPoolR = 225, SeedPlatformPoolRot = 227;
        public const int SeedPlatformPoolDepth = 229, SeedPlatformPoolCut = 131;
        public const double PlatformPoolEnds = 5.0, PlatformPoolJitter = 0.2, PlatformPoolJitterSpan = 0.6;
        public const double PlatformPoolLong = 1.25, PlatformPoolShort = 0.85, PlatformPoolRot = 60.0;
        public const double KeySceneMoved = 0.02;
    }
}
