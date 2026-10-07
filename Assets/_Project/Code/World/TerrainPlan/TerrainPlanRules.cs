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

        // ---- part 2's south (PR 5w; p2_lib's coast2, flats, plinths and paint), on Part2Seed ----------------------------------
        public const double StrandReliefAbove = 2.0, StrandRelief = 0.14, StrandReliefLambda = 5.0;
        public const int SeedStrandRelief = 49;
        public const double CoveKeepFeather = 0.4;
        public const double SpitWander = 0.8, SpitWanderLambda = 6.0, SpitCrestNoise = 0.08, SpitCrestLambda = 3.0;
        public const double SpitTipStretch = 1.4, SpitRootBack = 4.0;
        public const int SeedSpitWander = 31, SeedSpitCrest = 33;
        public const double GullyFirst = 6.0, GullyEndKeep = 4.0, GullyLean = 20.0, GullyReach = 1.6;
        public const int SeedGullyEvery = 211, SeedGullyLean = 213, SeedGullyWidth = 215;
        public const double ReefEdgeNoise = 1.6, ReefEdgeLambda = 11.0, ReefCrestLambda = 7.0, ReefRough = 0.12, ReefRoughLambda = 2.2;
        public const int SeedReefEdge = 201, SeedReefCrest = 203, SeedReefRough = 205, SeedReefWidth = 207;
        public const double FlatsLobeRamp = 12.0, FlatsWidthRamp = 6.0, FlatsShoreIn = 2.0, FlatsShoreSpan = 4.0;
        public const double FlatsRidgeIn = 0.15, FlatsRidgeOutFrom = 0.8, FlatsRidgeOut = 0.2;
        public const double FlatsRidgeBreakLambda = 22.0, FlatsRidgeBreakShift = 0.2, FlatsRidgeBreakSpan = 0.5;
        public const double FlatsRidgePhase = 2.0, FlatsRidgePhaseLambda = 40.0;
        public const double RibWanderLambda = 30.0, RibBreakLambda = 7.0, RibFrom = 0.12, RibTo = 0.95;
        public const int SeedFlatsLobe = 801, SeedFlatsSwell = 803, SeedFlatsRidgeBreak = 805, SeedFlatsRidgePhase = 807;
        public const int SeedFlatsRipple = 809, SeedRibWander = 821, SeedRibBreak = 823, SeedFlatsOuter = 841;
        public const int SeedFlatsRunnelEvery = 811, SeedFlatsRunnelWidth = 813, SeedFlatsRunnelDepth = 815;
        public const int SeedFlatsRunnel = 817, SeedFlatsMeander = 831, SeedFlatsBranch = 833;
        public const double RunnelReachPad = 1.0, BranchReachWidth = 62.0, RunnelStartMargin = 0.02, RunnelFirstQ = 0.03;
        public const double RunnelLastQ = 1.05, RunnelFadeIn = 0.1, BranchFirstQ = 0.05, BranchFadeOut = 0.25, BranchMarkTo = 0.9;
        public const double RunnelHalfFrom = 0.65, RunnelHalfGrowth = 0.7, RunnelDepthFrom = 0.5, RunnelDepthGrowth = 0.7;
        public const double BranchDepthFrom = 0.4, BranchDepthGrowth = 0.6, BranchWander = 0.8, BranchWanderLambda = 6.0;
        public const double RunnelMeanderLane = 53.0, RunnelMeanderLaneOff = 11.0, BranchLane = 91.0, BranchLaneStep = 13.0;
        public const double RunnelMarked = 0.5;
        public const double FootReach = 12.0, FootWobble = 0.12, FootWobbleLambda = 3.0;
        public const int SeedFoot = 81, SeedFootStep = 7;
        public const double SouthToeFloor = -4.6, SouthToeReach = 25.0;
        public const int SeedFormRecipe = 530;
        public const double FreshBankCap = 50.0;
        public const double StrandRunnelOffset = 14.0, StrandRunnelWander = 2.0, StrandRunnelLambda = 9.0, StrandRunnelHalf = 1.0;
        public const int SeedStrandRunnel = 320, SeedApron = 330;
        public const double StrandSiltFrom = -1.9, StrandSiltTo = -0.4, StrandTalusFrom = -1.2, StrandTalusTo = 0.5, StrandTalusAbove = 0.76;
        public const double LandingWeedFrom = 1.95, LandingWeedTo = 2.35, LandingWeedAbove = 0.34;
        public const double LandingTalusFrom = -0.2, LandingTalusTo = 1.2, LandingTalusAbove = 0.7;
        public const double ApronReach = 6.0, ApronWander = 2.0, ApronLambda = 5.0, ApronRippleBelow = -2.3, ApronTalusBelow = -1.4;
        public const double ReefToeTalus = 5.5, ReefToeTalusBelow = -1.4, ReefToeTalusAbove = 0.58;
        public const double ReefCrestTalusW = 0.3, ReefCrestTalusFrom = -2.2, ReefCrestTalusTo = -1.3, ReefCrestTalusAbove = 0.5;
        public const double ReefEelgrassW = 0.05, ReefEelgrassOff = 17.0, ReefEelgrassBelow = -2.3;
        public const double SpitShingleFrom = -0.5, SpitShingleTo = 2.4, SpitWeedFrom = -1.5;
        public const double CoveTalusFrom = -1.2, CoveTalusTo = 0.5, CoveTalusAbove = 0.76;
        public const double StormWeedFrom = -0.4, StormWeedTo = 0.8, StormWeedAbove = 0.6;
        public const double StormTalusFrom = 2.5, StormTalusTo = 3.4, StormTalusAbove = 0.62;
        public const double FlatsPaintBelow = 0.6, FlatsPaintAbove = -4.6, FlatsMarked = 0.5;
        public const int SeedFlatsSand = 850, SeedFlatsMud = 851, SeedFlatsF1 = 853, SeedFlatsF2 = 855, SeedFlatsRib = 857;
        public const int SeedFlatsToeTalus = 859, SeedFlatsMussel = 861;
        public const double FlatsF1Lambda = 3.0, FlatsF2Lambda = 1.8, FlatsMusselF1 = 0.42;
        public const double FlatsMudSiltFrom = -1.75, FlatsMudSiltTo = -1.0, FlatsMudSiltAbove = 0.7, FlatsRunnelSiltBelow = -0.3;
        public const double FlatsBoulderAbove = 0.64, FlatsBoulderFrom = -1.9;
        public const double FlatsToeTalus = 9.0, FlatsToeTalusWander = 2.5, FlatsToeTalusLambda = 6.0, FlatsToeTalusFrom = -2.6;
        public const double CreekPaintReach = 8.0, CreekMudBank = 1.4, CreekMudFrom = -2.2;

        // ---- the still water on a ground file (PR 5 B; amendment 2 §4.2) ----------------------------------------------------
        /// <summary>How many height-map steps a pool's flood may drop to hold (4096 steps of 16/65535 m: about a metre).</summary>
        public const int StillStepsDown = 4096;
        public const int SeedBay = 570;
        /// <summary>A bay's edge wanders on its own salt (PR 5w): this plus its index.</summary>
        public const int SeedBayEdge = 580;
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
