using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using HiddenHarbours.World;
using UnityEngine;
using static HiddenHarbours.World.TerrainPlanMath;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// What the terrain plan's guards judge: the plan, the frozen sources with today's heights and the ground file's import,
    /// a fresh derivation, the scene's placed things and its text, the village's buildings and yards, and the numbers the game
    /// keeps elsewhere. Every number is required: a missing one is refused, not guessed.
    /// </summary>
    public sealed class TerrainPlanGuardInput
    {
        public RegionTerrainPlanDef Plan;
        public TerrainPlanSources Sources;
        public TerrainPlanResult Result;
        public List<TerrainPlanSceneScan.Item> Items;
        public string SceneText;

        /// <summary>The region's ground file: the walls it keeps and its asks' Defs. Its import is <see cref="TerrainPlanSources.Import"/>.</summary>
        public GroundFileDef Ground;

        /// <summary>The village's buildings and yards (VillagePlanDerivation over its Defs): what the village's ground checks judge.</summary>
        public VillagePlanDerivation.Result Village;

        /// <summary>The WaterSurface and TerrainSplatSurface scripts' guids: the components whose range the sea's case reads.</summary>
        public string WaterScriptGuid, SplatScriptGuid;

        /// <summary>The PaintedHeightMap asset's range (its min and max elevation).</summary>
        public float MapMin = float.NaN, MapMax = float.NaN;

        /// <summary>The spring's low and high water: StPetersBuilder.TideMean ∓ TideAmplitude.</summary>
        public double SpringLow = double.NaN, SpringHigh = double.NaN;

        /// <summary>A nav mark's floor: StPetersNavMarks.Tuning.MinDepthAtSpringLowMetres (NavMarkPlan refuses a mark at or under it).</summary>
        public double NavFloor = double.NaN;

        /// <summary>A walker's wade, GameConfig.WadeDepth: a path is cut once the tide stands this far over its lowest ground.</summary>
        public double WadeDepth = double.NaN;

        /// <summary>The tide's period, GameConfig.TidalPeriodHours (h): how long a walk over the flats stays open.</summary>
        public double TidalPeriodHours = double.NaN;
    }

    /// <summary>One guard's verdict, what it measured and where.</summary>
    public sealed class TerrainPlanGuardCase
    {
        public string Name;
        public bool Pass;
        public string Detail;
        public readonly SortedDictionary<string, string> Numbers = new SortedDictionary<string, string>(StringComparer.Ordinal);

        public override string ToString() => (Pass ? "PASS " : "FAIL ") + Name + ": " + Detail;
    }

    /// <summary>
    /// <b>THE TERRAIN PLAN'S GUARDS</b>, the cases of StPetersTerrainPlanGuardTests (the charter's §4.3, amendment 1 §5,
    /// amendment 2 §4 and §5): part 1's, the crossing's, the key scenes', the ground file's and the village's. Pure: they read
    /// a fresh derivation against today's ground, pass 9's and the plan, never the engine, so a headless run and the EditMode
    /// test give the same verdicts. The test is a thin wrapper over <see cref="All"/>, one case per name in <see cref="Names"/>.
    ///
    /// <para>The map is the ground file's import (amendment 1 §4). "Unchanged" and "holds" mean within one R16 step at the
    /// plan's range, 16/65535 m over −4 to +12 (amendment 2 §4.8): the committed height map holds those codes (the determinism
    /// test pins it to a fresh import). The import holds its keep (the common keep, the cliffs outside the south sector, the
    /// far west strip) at today's ground but in amendment 2 §2's named places, and T1, the bar head's keep outside it, at pass
    /// 9's; the rest of pass 9's frozen mask is the file's. Each case also reports the largest difference in metres.</para>
    ///
    /// <para>Their own numbers are the prototype's checks' (terrain pass 9's plan9_check, part 2's p2_check), CD's placements
    /// (terrain.json), the file's (amendment 2) and the village plan's, each named below with its source. The plan's numbers
    /// come from the Defs.</para>
    /// </summary>
    public static class TerrainPlanGuards
    {
        // ---- the scene's roots and the Defs the guards name (ids are stable) ---------------------------------------------
        public const string ShorelineRoot = "Shoreline", NavMarksRoot = "StPetersNavMarks";
        public const string ShorePathId = "path.stp_shore_path", GinnysTrackId = "path.stp_ginnys_track", BarrenPathId = "path.stp_barren_path";
        public const string StackId = "form.stp_lantern_stack", BarId = "bar.stp_ne_bar", HeathBrookId = "stream.stp_heath_brook";

        // ---- CD's placements (terrain.json, R2 pass 3; the charter §6.6) --------------------------------------------------
        /// <summary>The lantern stack's centre.</summary>
        public static readonly PlanPoint StackCentre = new PlanPoint(224.2, 57.8);
        /// <summary>The NE bar's crest: its ends, and terrain.json's ground there (it falls from +0.75 to −2.16), reported beside the file's.</summary>
        public static readonly PlanPoint BarCrestFrom = new PlanPoint(219.8, 61.8), BarCrestTo = new PlanPoint(255.2, 101.8);
        public const double BarCrestRoot = 0.75, BarCrestTip = -2.16;
        /// <summary>The east cardinal's spot: aid.stp_ne_bar_east_cardinal (pass3/nePieces.js, BUOY).</summary>
        public static readonly PlanPoint EastCardinal = new PlanPoint(259.8, 105.2);

        // ---- the crossing (part 2's p2_check) -----------------------------------------------------------------------------
        /// <summary>CROSS_A, CROSS_B, CROSS_BOX: St Peters' half of the crossing, the bar's root to the Nine Mile Creek pass.</summary>
        public static readonly PlanPoint CrossA = new PlanPoint(-45, 0), CrossB = new PlanPoint(-356, 0);
        public const double CrossX0 = -362, CrossY0 = -38, CrossX1 = -40, CrossY1 = 38;
        /// <summary>bottleneck: 12 halvings of −2 to +1 (to 0.0007 m) over ground at or above z and under the (unused) cap; the
        /// sill's cells lie within 5 cm of it.</summary>
        public const double SillFrom = -2.0, SillTo = 1.0, SillCap = 99.0, SillNear = 0.05;
        public const int SillHalvings = 12;
        /// <summary>crest_band: the bar's crest, 5 m either side of its axis between its ends.</summary>
        public const double CrestBandHalf = 5.0;
        /// <summary>pool_mask: each pool's ellipse (no wobble) to 1.2 + 0.3 of its radius, read in a window of twice its radius.</summary>
        public const double PoolMaskRadius = 1.5, PoolMaskWindow = 2.0;
        /// <summary>crest_px_released: a crest cell a pool moved by more than a millimetre.</summary>
        public const double ReleasedBy = 1e-3;

        // ---- part 1's checks (terrain pass 9's plan9_check) ---------------------------------------------------------------
        /// <summary>ponds: the fill's window (4 × the radius + the basin), its level (the surface + 5 cm), the outlet's own gully
        /// (2.5 m of its line, past its first metre), and the radius a held pond's water never passes.</summary>
        public const double PondWindow = 4.0, PondFillAbove = 0.05, PondGully = 2.5, PondGullyFrom = 1.0, PondLeakRadius = 2.0;
        /// <summary>streams: a carved centreline never climbs more than 2 cm.</summary>
        public const double StreamClimb = 0.02;
        /// <summary>placed things: a thing's ground within 1 m of it; things on the map less its 10 m rim; the roots the plan
        /// re-derives (NOT_BUILT) left out.</summary>
        public const double PlacedThing = 1.0, PlacedX = 370, PlacedY = 250;
        public static readonly string[] NotBuilt =
            { "ShorePlants", "IslandShrubs", "IslandFlowers", "IslandUnderstorey", "Shoreline", "IslandWoods", "ClamHoles", "StPetersNavMarks" };

        // ---- the ground file (amendment 1 §4 and §5, amendment 2 §2, §4 and §5) ------------------------------------------------
        /// <summary>"Unchanged" allows one R16 step at the plan's range (amendment 2 §4.8).</summary>
        public const int UnchangedCodes = 1;

        /// <summary>The asks the cases name: the package's main beach, the fall's cut and the cannery's hold; the game's fixes 2 and 4.</summary>
        public const string MainBeachAskId = "ground.stp_main_beach", FallCutAskId = "ground.stp_alder_fall_cut", CanneryHoldAskId = "ground.stp_cannery_hold";
        public const string WestBlendAskId = "ground.stp_main_beach_west_blend", NeckFillAskId = "ground.stp_ne_neck_hollow_fill";

        /// <summary>A place where the import stands off today's ground inside its keep: its box (cell centres, inclusive), and
        /// amendment 2's count of its cells beyond a step and the most one moves (m).</summary>
        public sealed class NamedPlace
        {
            public string Name;
            public double X0, Y0, X1, Y1, Most;
            public int Cells;
        }

        /// <summary>Amendment 2 §2's places, 472 cells in all: the import's keep holds today's ground everywhere else.</summary>
        public static readonly NamedPlace[] NamedPlaces =
        {
            new NamedPlace { Name = "the west strip, north", X0 = -358.75, Y0 = 10.75, X1 = -356.25, Y1 = 28.75, Cells = 216, Most = -0.0328 },
            new NamedPlace { Name = "the west strip, south", X0 = -358.75, Y0 = -28.75, X1 = -356.25, Y1 = -10.75, Cells = 212, Most = -0.0160 },
            new NamedPlace { Name = "CliffWalls[6], by the bar's root", X0 = -44.25, Y0 = -20.75, X1 = -40.75, Y1 = -16.75, Cells = 17, Most = -0.5254 },
            new NamedPlace { Name = "pass 9's own cell", X0 = -45.75, Y0 = -16.75, X1 = -45.75, Y1 = -16.75, Cells = 1, Most = -0.0052 },
            new NamedPlace { Name = "main_beach's tree 1", X0 = 42.75, Y0 = -58.75, X1 = 46.75, Y1 = -57.75, Cells = 17, Most = 0.1835 },
            new NamedPlace { Name = "main_beach's tree 2", X0 = 51.25, Y0 = -60.75, X1 = 53.75, Y1 = -59.25, Cells = 9, Most = 0.1830 },
        };

        /// <summary>The fall's west rim, which Ginny's plot's case leaves out by name (amendment 2 §4.4, question 4 (a)): 66 cells
        /// beyond a step there, by up to +0.760 m.</summary>
        public const double GinnyRimX0 = 99.25, GinnyRimY0 = 58.75, GinnyRimX1 = 101.75, GinnyRimY1 = 68.25;

        /// <summary>The file's stack and bar (amendment 2 §4.7: the file wins): the plinth's core under its top at no more than 13
        /// of its cells, the lowest −0.823 m; the bar's root at +0.662 m and its tip at −2.179 m; each to half a millimetre.</summary>
        public const int StackCoreUnderTop = 13;
        public const double StackCoreLowest = -0.823, BarRootOnFile = 0.662, BarTipOnFile = -2.179, OnFile = 0.0005;

        /// <summary>A still pool lays at its Def's surface, to half a millimetre (amendment 1 §5).</summary>
        public const double StillLevelTolerance = 0.0005;

        /// <summary>The Heath Brook's mouth (amendment 2 §4.2): its Def ends where its bed crosses mean tide, so the sea owns the
        /// mouth. The ground at its end stands at or over the mean, and falls under it within 2 m past it (read every 5 cm).</summary>
        public const double MouthReach = 2.0, MouthStep = 0.05;

        /// <summary>The village plan's ground checks (docs/design/st-peters-village-plan.md:682 and :765; amendment 2 §4.4), in
        /// ground metres: 3 m from every trunk; the ground at least 5.9 m under a footprint and 5.5 m under a yard; pass 9's
        /// barren and shore paths keep half their width plus 0.5 m.</summary>
        public const double TrunkClear = 3.0, FootprintGround = 5.9, YardGround = 5.5, PathRoom = 0.5;

        // ---- part 2's guards on the patched walls (terrain PR 5w: amendment 1 §4.6, questions 3 to 6) --------------------------
        /// <summary>The toe sections whose walls stand in the sea at a spring low (part 2's p2_data: the South Arm, the Weather
        /// Cliff and the South-West Bluff). The ledges' toes stand on their own shelf: they are reported, not judged.</summary>
        public static readonly string[] WetSections = { "coast.stp_south_arm", "coast.stp_weather_cliff", "coast.stp_sw_bluff" };

        /// <summary>The new toe stations that stand dry by name, at the beach's banked end (question 4): 042 k23, 043 k0 to k2. A
        /// station St Peters' 1 m cut added between two of them (043 k0.5) is a slice of the face they draw, and is named with
        /// them (<see cref="DryByName"/>; terrain PR 5w).</summary>
        public static readonly string[] DryToesByName = { "042 k23", "043 k0", "043 k1", "043 k2" };

        /// <summary>Part 2's toe_field reads 12 m off a toe; its channel band (p2_check) is the cells within 3 m of a toe, under
        /// 0 m, in the flats' window (p2_lib.FLAT_WIN, south of y −5).</summary>
        public const double ToeReach = 12.0, ChannelReach = 3.0, ChannelUnder = 0.0;
        public const double FlatsX0 = -110, FlatsY0 = -160, FlatsX1 = 235, FlatsY1 = -5;

        /// <summary>The channel cells lost by name (question 5): the Weather Cliff's 42 by 042 and 043, where the beach banks; the
        /// South-West Bluff's 2 by 070, x −40.8 to −40.2, y −30.8 to −30.2.</summary>
        public static readonly string[] BeachLostBy = { "042", "043" };
        public const int BeachLostCells = 42;
        public const string BluffLostBy = "070";
        public const double BluffLostX0 = -40.8, BluffLostY0 = -30.8, BluffLostX1 = -40.2, BluffLostY1 = -30.2;
        public const int BluffLostCells = 2;

        /// <summary>A walk over the flats at a low (part 2's p2_check; its ends are checks2_B.json's): the box it is judged in, and
        /// its sill as Phase B measured it on the committed map (question 6), held to one R16 step.</summary>
        public sealed class FlatsWalk
        {
            public string Name;
            public PlanPoint A, B;
            public double X0, Y0, X1, Y1, Sill;
        }

        public static readonly FlatsWalk[] FlatsWalks =
        {
            new FlatsWalk { Name = "east_gap_to_west_gap", A = new PlanPoint(170.4, -52.9), B = new PlanPoint(4.8, -69.1),
                            X0 = -20, Y0 = -140, X1 = 180, Y1 = -35, Sill = -1.988006 },           // R16 code 8241, at (63.75, −77.75)
            new FlatsWalk { Name = "west_gap_to_storm_beach", A = new PlanPoint(4.8, -69.1), B = new PlanPoint(-62.5, -16.8),
                            X0 = -110, Y0 = -125, X1 = 25, Y1 = -5, Sill = -2.449195 },          // R16 code 6352, at (−51.75, −28.75)
        };

        /// <summary>A walker keeps 0.5 m off a wall's footprint (part 2's wall_mask) and to ground under +2 m (its SHORE_CAP).</summary>
        public const double WalkOffWalls = 0.5, WalkCap = 2.0;

        /// <summary>The cases, in the charter's order, then the ground file's, the village's and part 2's on the patched walls.</summary>
        public static readonly string[] Names =
        {
            "FrozenMaskHoldsToday", "HeldGroundUnchanged", "NavMarksDeepAtSpringLow", "ClamsInsideTheirBand", "PondsDoNotLeak",
            "StreamsNeverClimb", "CrossingSillIsTheGut", "CrestBandHoldsOutsideThePools", "WestOfTheCrossingHolds",
            "LandingAndCanneryHold", "HeadClearOfTheArrivalRoute", "HeadClearOfTheCannery", "StackAndBarWhereTerrainJsonPutsThem",
            "CutInsideItsBox", "PlungePoolHoldsItsSurface", "GinnysPlotHeldAtPart1", "ShorePathNeverCut",
            "EastCardinalDeepAtSpringLow", "NoShoreRockOnTheHead", "NoRockOrClamOnAKeyScenePath", "SeaRangeIsTheMapRange",
            "CanneryCircleIsPassNine", "ToeChannelsHoldAtSpringLow", "BeachWestEndBlends", "BeachDrySandStaysAboveTheSpringHigh",
            "DippingPoolHoldsItsLevel", "NeckHollowFilled", "HeathBrookNeverClimbsToTheShore", "CrossingWalkOffStillWater",
            "VillageClearOfTrunks", "VillageOnThePlateau", "VillagePathsKeepTheirRoom", "MainBeachSandByItsRecipe",
            "FlatsMoatOffThePatchedToes", "NewToesWetInTheWetSections", "ChannelCellsStayWet", "FlatsWindowsAtTheirSills",
        };

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Every case, in <see cref="Names"/>' order. A case that cannot read what it needs fails with the reason.</summary>
        public static List<TerrainPlanGuardCase> All(TerrainPlanGuardInput input)
        {
            var x = new Ctx(input);
            var cases = new Func<Ctx, TerrainPlanGuardCase>[]
            {
                FrozenMaskHoldsToday, HeldGroundUnchanged, NavMarksDeep, ClamsInBand, PondsHold, StreamsNeverClimb, CrossingSill,
                CrestBandHolds, WestHolds, LandingAndCannery, HeadClearOfRoute, HeadClearOfCannery, StackAndBarPlaced, CutInBox,
                PlungePoolHolds, GinnysPlotHeld, ShorePathNeverCut, EastCardinalDeep, NoShoreRockOnHead, NoRockOrClamOnKeyPaint,
                SeaRangeMatchesMap,
                CanneryCircle, ToeChannelsHold, BeachWestEndBlends, BeachDrySand, DippingPoolHolds, NeckHollowFilled, HeathBrook,
                CrossingWalk, VillageClearOfTrunks, VillageOnThePlateau, VillagePathsKeepTheirRoom, MainBeachSand,
                FlatsMoat, NewToesWet, ChannelCellsStayWet, FlatsWindows,
            };
            var o = new List<TerrainPlanGuardCase>();
            for (int n = 0; n < cases.Length; n++)
            {
                try { o.Add(cases[n](x)); }
                catch (Exception ex) when (!(ex is OutOfMemoryException))
                {
                    o.Add(new TerrainPlanGuardCase { Name = Names[n], Pass = false, Detail = "could not be judged: " + ex.GetType().Name + ": " + ex.Message });
                }
                if (o[n].Name != Names[n]) throw new InvalidOperationException("[TerrainPlanGuards] case " + n + " is " + o[n].Name + ", not " + Names[n] + ".");
            }
            return o;
        }

        // ---- part 1's cases ------------------------------------------------------------------------------------------------

        /// <summary>
        /// The import's keep holds today's ground within one R16 step, but in amendment 2 §2's named places, each within its
        /// count and its most (and a step); T1, the bar head's keep outside it, holds pass 9's (amendment 2 §4.1 item 8). The
        /// rest of pass 9's frozen mask is the file's: it is counted, the south sector and its cliffs by name.
        /// </summary>
        static TerrainPlanGuardCase FrozenMaskHoldsToday(Ctx x)
        {
            var k = New("FrozenMaskHoldsToday");
            var imp = x.Imp;
            var held = new HeldTally();
            var placeOff = new int[NamedPlaces.Length];
            var placeMost = new double[NamedPlaces.Length];
            var t1 = new Tally();
            int t1Pass9Off = 0, released = 0, releasedSouth = 0, releasedCliffs = 0;
            for (int i = 0; i < x.N; i++)
            {
                byte why = x.R.FrozenWhy[i];
                if (x.Held(i))
                {
                    if (!held.Add(x, i)) continue;
                    int p = x.PlaceOf(i);
                    if (p < 0) continue;
                    placeOff[p]++;
                    placeMost[p] = Math.Max(placeMost[p], Math.Abs(x.R.E[i] - x.Base[i]));
                    continue;
                }
                if ((why & TerrainPlanResult.WhyCrossing) != 0)
                {
                    t1.Add(i, x.CE, x.CI, x.R.E, imp.Base, UnchangedCodes);
                    if (Math.Abs(x.CI[i] - x.CB[i]) > UnchangedCodes) t1Pass9Off++;
                    continue;
                }
                if (x.R.Frozen[i] != 1) continue;
                released++;
                if ((why & TerrainPlanResult.WhySouth) != 0) releasedSouth++;
                if ((why & TerrainPlanResult.WhyCliffs) != 0) releasedCliffs++;
            }
            bool places = true;
            var parts = new List<string>();
            for (int p = 0; p < NamedPlaces.Length; p++)
            {
                var pl = NamedPlaces[p];
                bool ok = placeOff[p] <= pl.Cells && placeMost[p] <= Math.Abs(pl.Most) + x.Step;
                places &= ok;
                parts.Add(pl.Name + " " + placeOff[p] + " (A2 " + pl.Cells + "), at most " + F(placeMost[p]) + " m (A2 " + F(Math.Abs(pl.Most)) + ")" + (ok ? "" : " BEYOND"));
                Put(k, "place" + (p + 1) + ".off_cells", placeOff[p]); Put(k, "place" + (p + 1) + ".max_abs_m", placeMost[p]);
            }
            k.Pass = held.Cells > 0 && held.Outside == 0 && places && t1.Cells > 0 && t1.Off == 0;
            k.Detail = held.Cells + " cells of the import's keep (the common keep, the cliffs outside the south sector, the far west strip): " +
                       held.Off + " more than one R16 step off today's ground, " + held.Outside + " of them outside the named places" +
                       x.At(held.OutsideAt) + " (elsewhere at most " + F(held.MaxAbs) + " m" + x.At(held.MaxAt) + "); in them " +
                       string.Join("; ", parts) + ". T1, the bar head's keep outside it: " + t1.Cells + " cells, " + t1.Off +
                       " more than a step off pass 9's ground (at most " + F(t1.MaxAbs) + " m), pass 9 more than a step off today's at " +
                       t1Pass9Off + ". The file's: " + released + " more tier-1 cells, " + releasedSouth + " in the south sector and " +
                       releasedCliffs + " of its cliffs";
            Put(k, "held_cells", held.Cells); Put(k, "held_off", held.Off); Put(k, "held_off_outside_places", held.Outside);
            Put(k, "held_max_abs_outside_places_m", held.MaxAbs);
            Put(k, "t1_cells", t1.Cells); Put(k, "t1_off_pass9", t1.Off); Put(k, "t1_max_abs_pass9_m", t1.MaxAbs); Put(k, "t1_pass9_off_today", t1Pass9Off);
            Put(k, "released_cells", released); Put(k, "released_south", releasedSouth); Put(k, "released_south_cliffs", releasedCliffs);
            return k;
        }

        /// <summary>
        /// The berths, wharf, buildings, roads, arrival route, cliffs and woods, and every other placed thing: unchanged where the
        /// import holds its keep, but in the named places. Their cells outside the keep are the file's, and are reported.
        /// </summary>
        static TerrainPlanGuardCase HeldGroundUnchanged(Ctx x)
        {
            var k = New("HeldGroundUnchanged");
            var keep = x.Plan.Keep;
            var s = x.Src;
            var groups = new List<KeyValuePair<string, bool[]>>();

            var m = x.Mask();                                                   // the berths, widened as the keep widens them
            foreach (var cap in new[] { s.BerthSlip, s.ApproachCut, s.Pocket }) x.Capsule(m, cap.A, cap.B, cap.HalfWidth + Num(keep.BerthExtra));
            groups.Add(new KeyValuePair<string, bool[]>("berths", m));

            m = x.Mask();                                                       // the wharf: its box and its items
            x.Rect(m, s.WharfMin, s.WharfMax, Num(keep.WharfBuffer));
            foreach (var p in x.ItemsOf(keep.WharfRoot)) x.Disc(m, p, Num(keep.WharfItemBuffer));
            groups.Add(new KeyValuePair<string, bool[]>("wharf", m));

            m = x.Mask();                                                       // the buildings: 8 m, the large ones 12 m
            var large = new HashSet<string>(keep.LargeBuildings ?? new string[0], StringComparer.Ordinal);
            foreach (var b in s.Buildings) x.Disc(m, b.At, large.Contains(b.Id) ? Num(keep.LargeBuildingRadius) : Num(keep.BuildingRadius));
            groups.Add(new KeyValuePair<string, bool[]>("buildings", m));

            m = x.Mask();                                                       // the roads: the plan's paths on a builder's line
            foreach (var p in x.Plan.Paths)
            {
                if (p == null || string.IsNullOrEmpty(p.LineSource)) continue;
                if (!s.Lines.TryGetValue(p.LineSource, out var pts)) throw new InvalidOperationException(p.Id + "'s line " + p.LineSource + " is not in the sources");
                for (int j = 0; j + 1 < pts.Length; j++) x.Capsule(m, pts[j], pts[j + 1], Num(keep.RoadBuffer));
            }
            groups.Add(new KeyValuePair<string, bool[]>("roads", m));

            m = x.Mask();                                                       // the arrival route and the keep's margin
            for (int j = 0; j + 1 < s.Entrance.Length; j++) x.Capsule(m, s.Entrance[j], s.Entrance[j + 1], s.EntranceHalfWidth + Num(keep.EntranceExtra));
            groups.Add(new KeyValuePair<string, bool[]>("arrival_route", m));

            m = x.Mask();
            foreach (var p in x.ItemsOf(keep.CliffRoot)) x.Disc(m, p, Num(keep.CliffBuffer));
            groups.Add(new KeyValuePair<string, bool[]>("cliffs", m));

            m = x.Mask();
            foreach (var p in x.ItemsOf(keep.WoodsRoot)) x.Disc(m, p, Num(keep.WoodsBuffer));
            groups.Add(new KeyValuePair<string, bool[]>("woods", m));

            m = x.Mask();                                                       // every other placed thing (part 1's check)
            var notBuilt = new HashSet<string>(NotBuilt, StringComparer.Ordinal);
            foreach (var it in x.In.Items)
            {
                if (notBuilt.Contains(it.Root) || (it.X == 0 && it.Y == 0) || !(-PlacedX < it.X && it.X < PlacedX && -PlacedY < it.Y && it.Y < PlacedY)) continue;
                x.Disc(m, new PlanPoint(it.X, it.Y), PlacedThing);
            }
            groups.Add(new KeyValuePair<string, bool[]>("placed_things", m));

            bool pass = true;
            var parts = new List<string>();
            foreach (var g in groups)
            {
                var t = new HeldTally();
                var rel = new Tally();
                for (int i = 0; i < x.N; i++)
                {
                    if (!g.Value[i]) continue;
                    if (x.Held(i)) t.Add(x, i);
                    else rel.Add(i, x.CE, x.CB, x.R.E, x.Base, UnchangedCodes);
                }
                pass &= t.Cells + rel.Cells > 0 && t.Outside == 0;
                parts.Add(g.Key + " " + t.Outside + "/" + t.Cells + (t.Outside > 0 ? " (first" + x.At(t.OutsideAt) + ")" : "") +
                          (t.Off > t.Outside ? " and " + (t.Off - t.Outside) + " in the named places" : "") + ", the file's " + rel.Off + "/" + rel.Cells +
                          (rel.Off > 0 ? " (at most " + F(rel.MaxAbs) + " m" + x.At(rel.MaxAt) + ")" : ""));
                Put(k, g.Key + ".held_cells", t.Cells); Put(k, g.Key + ".held_off", t.Outside); Put(k, g.Key + ".held_off_in_places", t.Off - t.Outside);
                Put(k, g.Key + ".held_max_abs_m", t.MaxAbs); Put(k, g.Key + ".file_cells", rel.Cells); Put(k, g.Key + ".file_off", rel.Off);
                Put(k, g.Key + ".file_max_abs_m", rel.MaxAbs);
            }
            k.Pass = pass;
            k.Detail = "cells more than one R16 step off today's ground, of those the import holds (and of the file's): " + string.Join("; ", parts);
            return k;
        }

        /// <summary>Every nav mark has more water than its floor at a spring low.</summary>
        static TerrainPlanGuardCase NavMarksDeep(Ctx x)
        {
            var k = New("NavMarksDeepAtSpringLow");
            int n = 0, bad = 0;
            double least = double.PositiveInfinity;
            string leastAt = "";
            foreach (var it in x.In.Items)
            {
                if (it.Root != NavMarksRoot || (it.X == 0 && it.Y == 0)) continue;
                n++;
                double d = x.In.SpringLow - x.G.Sample(x.R.E, it.X, it.Y);
                if (d <= x.In.NavFloor) bad++;
                if (d < least) { least = d; leastAt = it.Name + " (" + F(it.X) + ", " + F(it.Y) + ")"; }
            }
            k.Pass = n > 0 && bad == 0;
            k.Detail = n + " nav mark items, " + bad + " at or under the " + F(x.In.NavFloor) + " m floor at a spring low (" + F(x.In.SpringLow) +
                       " m); the shallowest has " + F(least) + " m, " + leastAt;
            Put(k, "marks", n); Put(k, "under_floor", bad); Put(k, "least_depth_m", least);
            return k;
        }

        /// <summary>The clam holes stay inside the scatter's band.</summary>
        static TerrainPlanGuardCase ClamsInBand(Ctx x)
        {
            var k = New("ClamsInsideTheirBand");
            double lo = Num(x.Plan.ClamBand.x), hi = Num(x.Plan.ClamBand.y);
            int n = 0, outside = 0;
            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            foreach (var p in x.ItemsOf(x.Plan.Keep.ClamRoot))
            {
                n++;
                double e = x.G.Sample(x.R.E, p.X, p.Y);
                if (e < lo || e > hi) outside++;
                min = Math.Min(min, e); max = Math.Max(max, e);
            }
            k.Pass = n > 0 && outside == 0;
            k.Detail = n + " clam holes, " + outside + " outside " + F(lo) + " to " + F(hi) + " m; their ground runs " + F(min) + " to " + F(max) + " m";
            Put(k, "holes", n); Put(k, "outside", outside); Put(k, "min_m", min); Put(k, "max_m", max);
            return k;
        }

        /// <summary>The ponds do not leak: part 1's fill, from each centre over ground under its surface + 5 cm.</summary>
        static TerrainPlanGuardCase PondsHold(Ctx x)
        {
            var k = New("PondsDoNotLeak");
            bool pass = x.Plan.Ponds.Length > 0;
            var parts = new List<string>();
            for (int n = 0; n < x.Plan.Ponds.Length; n++)
            {
                bool ok = PondHolds(x, n, out string what);
                pass &= ok;
                parts.Add(what);
                Put(k, x.Plan.Ponds[n].Id + ".holds", ok ? 1 : 0);
            }
            k.Pass = pass;
            k.Detail = string.Join("; ", parts);
            return k;
        }

        /// <summary>
        /// The streams never climb: along each carved centreline, no rise over its lowest point upstream beyond 2 cm. A pond, a
        /// still pool or a fall's plunge pool on the line reads as its water, not its bed (amendment 2 §4.2: the dipping pool).
        /// A fall's own reach, from its box's south edge to the chute's top, is skipped by the fall's id: its water climbs in its
        /// bowl and past its spill by design (amendment 2 §4.2, the Alder Run), and its pool's case judges it.
        /// </summary>
        static TerrainPlanGuardCase StreamsNeverClimb(Ctx x)
        {
            var k = New("StreamsNeverClimb");
            bool pass = x.Plan.Streams.Length > 0;
            var parts = new List<string>();
            foreach (var st in x.Plan.Streams)
            {
                var falls = new List<WaterfallDef>();
                foreach (var f in x.Plan.Falls) if (f != null && f.Stream != null && f.Stream.Id == st.Id) falls.Add(f);
                var line = Catmull(Num(st.Points), Num(x.Plan.LineStep));
                double climb = Climb(x, st, line, q =>
                {
                    foreach (var f in falls) if (q.Y >= Num(f.BoxMin.y) && q.Y <= Num(f.ChuteTo)) return true;
                    return false;
                }, out int at, out int skipped);
                pass &= climb <= StreamClimb;
                string what = st.Id + " climbs " + F(climb) + " m" + (at >= 0 ? " at (" + F(line.R[at].X) + ", " + F(line.R[at].Y) + ")" : "");
                foreach (var f in falls) what += " (" + f.Id + "'s reach, y " + F(Num(f.BoxMin.y)) + " to " + F(Num(f.ChuteTo)) + ", skipped: " + skipped + " stations)";
                parts.Add(what);
                Put(k, st.Id + ".max_climb_m", climb);
                if (falls.Count > 0) Put(k, st.Id + ".skipped_stations", skipped);
            }
            k.Pass = pass;
            k.Detail = string.Join("; ", parts) + " (at most " + F(StreamClimb) + " m)";
            return k;
        }

        /// <summary>
        /// A stream's climb: along its line, the largest rise over its lowest point upstream, a pond, still pool or the stream's
        /// fall's pool read as its water at its surface. Stations <paramref name="skip"/> takes are left out.
        /// </summary>
        static double Climb(Ctx x, StreamDef st, PlanLine line, Func<PlanPoint, bool> skip, out int at, out int skipped)
        {
            var water = new List<PondDef>();
            foreach (var p in x.Plan.Ponds) if (p != null) water.Add(p);
            if (x.Plan.StillPools != null) foreach (var p in x.Plan.StillPools) if (p != null) water.Add(p);
            foreach (var f in x.Plan.Falls) if (f != null && f.Pool != null && f.Stream != null && f.Stream.Id == st.Id) water.Add(f.Pool);
            double low = double.PositiveInfinity, climb = double.NegativeInfinity;
            at = -1;
            skipped = 0;
            bool first = true;
            for (int j = 0; j < line.R.Length; j++)
            {
                double X = line.R[j].X, Y = line.R[j].Y;
                if (skip != null && skip(line.R[j])) { skipped++; continue; }
                double e = x.G.Sample(x.R.E, X, Y);
                foreach (var p in water) if (PondQ(p, X, Y) < 1) e = Math.Max(e, Num(p.Surface));
                if (!first && e - low > climb) { climb = e - low; at = j; }
                low = Math.Min(low, e);
                first = false;
            }
            return climb;
        }

        /// <summary>A pond's radius at a point, no wobble (1 on its rim): the fall's PoolQ.</summary>
        static double PondQ(PondDef p, double X, double Y)
        {
            double rot = Radians(Num(p.RotationDeg));
            return EllipseRadius(X, Y, Num(p.Centre), Num(p.Radii.x), Num(p.Radii.y), Math.Cos(rot), Math.Sin(rot), 0, 0.0, 0.0);
        }

        // ---- the crossing -----------------------------------------------------------------------------------------------------

        /// <summary>The crossing's sill stays the gut's bed, so the walk is cut above the sill plus a wade.</summary>
        static TerrainPlanGuardCase CrossingSill(Ctx x)
        {
            var k = New("CrossingSillIsTheGut");
            var s = x.Src;
            bool ends = Same(CrossA, s.Sandbar.A) && s.Passages.Exists(p => Same(p, CrossB));
            double sill = Sill(x, x.R.E, out string at), today = Sill(x, x.Base, out string atToday);
            var gut = new PlanPoint((s.BarGut.A.X + s.BarGut.B.X) / 2, (s.BarGut.A.Y + s.BarGut.B.Y) / 2);
            double bed = x.G.Sample(x.R.E, gut.X, gut.Y);
            double step = (SillTo - SillFrom) / (1 << SillHalvings);
            k.Pass = ends && Math.Abs(sill - today) <= step && Math.Abs(sill - bed) <= step;
            k.Detail = "sill " + F(PyRound(sill, 3)) + " m (today " + F(PyRound(today, 3)) + "), its cells about " + at + " (today " + atToday + "); the gut's bed " + F(bed) +
                       " m at (" + F(gut.X) + ", " + F(gut.Y) + "); the walk cut above " + F(PyRound(sill + x.In.WadeDepth, 3)) + " m" +
                       (ends ? "" : "; the crossing's ends are no longer the sources' bar root and pass");
            Put(k, "sill_m", sill); Put(k, "sill_today_m", today); Put(k, "gut_bed_m", bed); Put(k, "cut_above_m", sill + x.In.WadeDepth);
            Put(k, "halving_step_m", step);
            return k;
        }

        /// <summary>The crest band holds part 1's ground outside layout B's pools, within one R16 step.</summary>
        static TerrainPlanGuardCase CrestBandHolds(Ctx x)
        {
            var k = New("CrestBandHoldsOutsideThePools");
            var bar = x.Src.Sandbar;
            double x0 = Math.Min(bar.A.X, bar.B.X), x1 = Math.Max(bar.A.X, bar.B.X), axis = bar.A.Y;
            var pools = x.Mask();
            foreach (var p in x.Plan.Pools)
            {
                if (p == null) continue;
                var cc = Num(p.Centre);
                double ra = Num(p.Radii.x), rb = Num(p.Radii.y), R = Math.Max(ra, rb) * PoolMaskWindow;
                double rot = Radians(Num(p.RotationDeg)), cr = Math.Cos(rot), sr = Math.Sin(rot);
                if (!x.G.Win(cc.X - R, cc.Y - R, cc.X + R, cc.Y + R, out int r0, out int r1, out int c0, out int c1)) continue;
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                    if (EllipseRadius(x.Xs[c], x.Ys[r], cc, ra, rb, cr, sr, 0, 0.0, 0.0) < PoolMaskRadius) pools[r * x.W + c] = true;
            }
            var outside = new Tally();
            var vsToday = new Tally();
            int band = 0, inPools = 0, released = 0;
            double releasedMax = 0;
            for (int r = 0; r < x.H; r++)
            for (int c = 0; c < x.W; c++)
            {
                if (x.Xs[c] < x0 || x.Xs[c] > x1 || Math.Abs(x.Ys[r] - axis) > CrestBandHalf) continue;
                int i = r * x.W + c;
                band++;
                if (pools[i])
                {
                    inPools++;
                    double d = Math.Abs(x.R.E[i] - x.R.E1[i]);
                    if (d > ReleasedBy) { released++; releasedMax = Math.Max(releasedMax, d); }
                    continue;
                }
                outside.Add(i, x.CE, x.C1, x.R.E, x.R.E1, UnchangedCodes);
                vsToday.Add(i, x.CE, x.CB, x.R.E, x.Base, UnchangedCodes);
            }
            double cell = x.G.Mpp * x.G.Mpp;
            k.Pass = band > 0 && outside.Cells > 0 && outside.Off == 0;
            k.Detail = band + " crest cells, " + inPools + " in the pools' reach: " + released + " released (" + F(released * cell) + " m², up to " +
                       F(releasedMax) + " m); outside the pools " + outside.Off + " of " + outside.Cells + " more than a step off part 1's ground (at most " +
                       F(outside.MaxAbs) + " m), " + vsToday.Off + " off today's";
            Put(k, "band_cells", band); Put(k, "in_pools", inPools); Put(k, "released_cells", released); Put(k, "released_m2", released * cell);
            Put(k, "released_max_m", releasedMax); Put(k, "outside_off_part1", outside.Off); Put(k, "outside_max_abs_part1_m", outside.MaxAbs);
            Put(k, "outside_off_today", vsToday.Off); Put(k, "outside_max_abs_today_m", vsToday.MaxAbs);
            return k;
        }

        /// <summary>The ground west of the crossing's edge holds today's, within one R16 step, but in the west strip's named places.</summary>
        static TerrainPlanGuardCase WestHolds(Ctx x)
        {
            var k = New("WestOfTheCrossingHolds");
            double west = Num(x.Plan.Crossing.XRange.x);
            var t = new HeldTally();
            for (int r = 0; r < x.H; r++)
            for (int c = 0; c < x.W && x.Xs[c] < west; c++)
                t.Add(x, r * x.W + c);
            k.Pass = t.Cells > 0 && t.Outside == 0;
            k.Detail = t.Cells + " cells west of x " + F(west) + ", " + t.Off + " more than one R16 step off today's ground, " + (t.Off - t.Outside) +
                       " of them in the named places and " + t.Outside + " elsewhere" + x.At(t.OutsideAt) + "; elsewhere at most " + F(t.MaxAbs) + " m" + x.At(t.MaxAt);
            Put(k, "cells", t.Cells); Put(k, "off_cells", t.Off); Put(k, "off_outside_places", t.Outside); Put(k, "max_abs_outside_places_m", t.MaxAbs);
            return k;
        }

        /// <summary>The Landing (its four points, 8 m round each) and the cannery's 12 m: unchanged, within one R16 step.</summary>
        static TerrainPlanGuardCase LandingAndCannery(Ctx x)
        {
            var k = New("LandingAndCanneryHold");
            var keep = x.Plan.Keep;
            var landing = x.Mask();
            foreach (var p in x.Src.DockPoints) x.Disc(landing, p, Num(keep.DockPointBuffer));
            var cannery = x.Mask();
            var large = new HashSet<string>(keep.LargeBuildings ?? new string[0], StringComparer.Ordinal);
            int buildings = 0;
            foreach (var b in x.Src.Buildings) if (large.Contains(b.Id)) { x.Disc(cannery, b.At, Num(keep.LargeBuildingRadius)); buildings++; }
            var tl = new Tally();
            var tc = new Tally();
            for (int i = 0; i < x.N; i++)
            {
                if (landing[i]) tl.Add(i, x.CE, x.CB, x.R.E, x.Base, UnchangedCodes);
                if (cannery[i]) tc.Add(i, x.CE, x.CB, x.R.E, x.Base, UnchangedCodes);
            }
            k.Pass = x.Src.DockPoints.Count > 0 && buildings > 0 && tl.Off == 0 && tc.Off == 0;
            k.Detail = "the Landing: " + tl.Off + " of " + tl.Cells + " cells more than a step off today's ground (at most " + F(tl.MaxAbs) + " m); the cannery's " +
                       F(Num(keep.LargeBuildingRadius)) + " m: " + tc.Off + " of " + tc.Cells + " (at most " + F(tc.MaxAbs) + " m)";
            Put(k, "landing_cells", tl.Cells); Put(k, "landing_off", tl.Off); Put(k, "landing_max_abs_m", tl.MaxAbs);
            Put(k, "cannery_cells", tc.Cells); Put(k, "cannery_off", tc.Off); Put(k, "cannery_max_abs_m", tc.MaxAbs);
            return k;
        }

        // ---- the Head ------------------------------------------------------------------------------------------------------------

        /// <summary>Nothing of the Head inside the arrival route's capsule (its half-width and the keep's margin, 30 m).</summary>
        static TerrainPlanGuardCase HeadClearOfRoute(Ctx x)
        {
            var k = New("HeadClearOfTheArrivalRoute");
            var route = x.Src.Entrance;
            double reach = x.Src.EntranceHalfWidth + Num(x.Plan.Keep.EntranceExtra);
            int moved = 0, inside = 0, near = -1;
            double nearest = double.PositiveInfinity;
            for (int i = 0; i < x.N; i++)
            {
                if (!x.HeadMoved(i)) continue;
                moved++;
                double X = x.Xs[i % x.W], Y = x.Ys[i / x.W], d = double.PositiveInfinity;
                for (int j = 0; j + 1 < route.Length; j++) d = Math.Min(d, Seg(X, Y, route[j], route[j + 1]));
                if (d <= reach) inside++;
                if (d < nearest) { nearest = d; near = i; }
            }
            k.Pass = moved > 0 && route.Length > 1 && inside == 0;
            k.Detail = moved + " cells of new ground, " + inside + " within " + F(reach) + " m of the arrival route; the nearest is " + F(nearest) + " m" + x.At(near);
            Put(k, "head_cells", moved); Put(k, "inside", inside); Put(k, "nearest_m", nearest); Put(k, "capsule_m", reach);
            return k;
        }

        /// <summary>Nothing of the Head within the neck's guard of the cannery's pivot (21.06 m of (170, 16)).</summary>
        static TerrainPlanGuardCase HeadClearOfCannery(Ctx x)
        {
            var k = New("HeadClearOfTheCannery");
            int guards = 0, inside = 0, near = -1, others = 0, othersByKeyScenes = 0;
            double nearest = double.PositiveInfinity, radius = 0;
            foreach (var ramp in x.Plan.Ramps)
            {
                if (ramp == null || !(ramp.GuardRadius > 0)) continue;
                guards++;
                var g = Num(ramp.Guard);
                double gr = Num(ramp.GuardRadius);
                radius = gr;
                for (int i = 0; i < x.N; i++)
                {
                    double d = Hypot(x.Xs[i % x.W] - g.X, x.Ys[i / x.W] - g.Y);
                    if (!x.HeadMoved(i))
                    {
                        if (d <= gr && x.CE[i] != x.CB[i])
                        {
                            others++;
                            if (Math.Abs(x.R.E[i] - x.R.E2[i]) > TerrainPlanRules.MovedBy) othersByKeyScenes++;
                        }
                        continue;
                    }
                    if (d <= gr) inside++;
                    if (d < nearest) { nearest = d; near = i; }
                }
                Put(k, ramp.Id + ".guard_m", gr);
            }
            k.Pass = guards > 0 && inside == 0;
            k.Detail = guards + " guard(s): " + inside + " cells of new ground within " + F(radius) + " m of the pivot; the nearest is " + F(nearest) + " m" +
                       x.At(near) + ". There " + others + " other cells differ from today's ground, " + othersByKeyScenes +
                       " of them moved by the key scenes (the rest are part 1's and the crossing's ground)";
            Put(k, "inside", inside); Put(k, "nearest_m", nearest); Put(k, "other_cells_off_today", others);
            Put(k, "other_cells_moved_by_key_scenes", othersByKeyScenes);
            return k;
        }

        /// <summary>
        /// The stack and the bar where terrain.json puts them, at the file's heights: the Defs carry CD's placements, and the
        /// import lays them as the file does (amendment 2 §4.7, the file wins), terrain.json's heights reported beside.
        /// </summary>
        static TerrainPlanGuardCase StackAndBarPlaced(Ctx x)
        {
            var k = New("StackAndBarWhereTerrainJsonPutsThem");
            FormDef stack = null;
            foreach (var f in x.Plan.Forms) if (f != null && f.Id == StackId) stack = f;
            TidalBarDef bar = null;
            foreach (var b in x.Plan.Bars) if (b != null && b.Id == BarId) bar = b;
            if (stack == null || bar == null) throw new InvalidOperationException("the plan has no " + (stack == null ? StackId : BarId));

            // the stack: its Def at CD's centre; its plinth's core (to PlinthSkirt.x of its radius) under its top at no more cells,
            // and no lower, than the file's; every cell the plinth moved inside its skirt (PlinthSkirt.y)
            var centre = Num(stack.Centre);
            bool stackDef = Same(centre, StackCentre);
            double rot = Radians(Num(stack.RotationDeg)), co = Math.Cos(rot), si = Math.Sin(rot);
            double px = Num(stack.PlinthRadii.x), py = Num(stack.PlinthRadii.y), top = Num(stack.PlinthTop);
            double k0 = Num(stack.PlinthSkirt.x), k1 = Num(stack.PlinthSkirt.y), reach = Math.Max(px, py) * k1;
            int core = 0, coreLow = 0, cells = 0, outside = 0, low = -1;
            double coreMin = double.PositiveInfinity;
            if (x.G.Win(centre.X - reach, centre.Y - reach, centre.X + reach, centre.Y + reach, out int r0, out int r1, out int c0, out int c1))
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    int i = r * x.W + c;
                    if (EllipseRadius(x.Xs[c], x.Ys[r], centre, px, py, co, si, 0, 0.0, 0.0) > k0) continue;
                    core++;
                    if (x.R.E[i] < coreMin) { coreMin = x.R.E[i]; low = i; }
                    if (x.R.E[i] < top - TerrainPlanRules.MovedBy) coreLow++;
                }
            for (int i = 0; i < x.N; i++)
            {
                if (x.OwnerId(i) != StackId) continue;
                cells++;
                if (EllipseRadius(x.Xs[i % x.W], x.Ys[i / x.W], centre, px, py, co, si, 0, 0.0, 0.0) >= k1) outside++;
            }
            bool stackOk = stackDef && core > 0 && coreLow <= StackCoreUnderTop && coreMin >= StackCoreLowest - OnFile && cells > 0 && outside == 0;

            // the bar: its Def on CD's crest, and the file's ground at the crest's root and tip
            var crest = Num(bar.Crest);
            bool barDef = crest.Length >= 2 && Same(crest[0], BarCrestFrom) && Same(crest[crest.Length - 1], BarCrestTo);
            double eRoot = x.G.Sample(x.R.E, BarCrestFrom.X, BarCrestFrom.Y), eTip = x.G.Sample(x.R.E, BarCrestTo.X, BarCrestTo.Y);
            int barCells = 0;
            for (int i = 0; i < x.N; i++) if (x.OwnerId(i) == BarId) barCells++;
            bool barOk = barDef && barCells > 0 && Math.Abs(eRoot - BarRootOnFile) <= OnFile && Math.Abs(eTip - BarTipOnFile) <= OnFile;

            k.Pass = stackOk && barOk;
            k.Detail = "the stack: Def " + (stackDef ? "at" : "NOT at") + " (" + F(StackCentre.X) + ", " + F(StackCentre.Y) + "); its plinth's core " +
                       core + " cells, " + coreLow + " under its " + F(top) + " m top (the file's " + StackCoreUnderTop + "), the lowest " + F(coreMin) + " m" +
                       x.At(low) + " (the file's " + F(StackCoreLowest) + "); " + cells + " cells the plinth moved, " + outside + " outside its skirt. The bar: Def " +
                       (barDef ? "on" : "NOT on") + " CD's crest; " + barCells + " cells; ground " + F(eRoot) + " m at the root (the file's " + F(BarRootOnFile) +
                       ", terrain.json's " + F(BarCrestRoot) + "), " + F(eTip) + " m at the tip (the file's " + F(BarTipOnFile) + ", terrain.json's " + F(BarCrestTip) + ")";
            Put(k, "stack_core_cells", core); Put(k, "stack_core_under_top", coreLow); Put(k, "stack_core_min_m", coreMin);
            Put(k, "stack_cells", cells); Put(k, "stack_cells_outside_skirt", outside);
            Put(k, "bar_cells", barCells); Put(k, "bar_root_m", eRoot); Put(k, "bar_tip_m", eTip);
            return k;
        }

        // ---- the cut --------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Every cell an ask moved lies inside its reach, as the file gives it (amendment 2 §4.8): the cells more than one R16 step
        /// off pass 9's ground, by the ask that laid them last, each inside that ask's box or window, the fall's cut among them by
        /// name. A cell off pass 9's ground that no ask laid fails the case too.
        /// </summary>
        static TerrainPlanGuardCase CutInBox(Ctx x)
        {
            var k = New("CutInsideItsBox");
            var imp = x.Imp;
            int n = imp.Asks.Count;
            var cells = new int[n];
            var outside = new int[n];
            var first = new int[n];
            for (int a = 0; a < n; a++) first[a] = -1;
            int orphans = 0, orphan = -1;
            for (int i = 0; i < x.N; i++)
            {
                if (Math.Abs(x.CE[i] - x.CI[i]) <= UnchangedCodes) continue;
                int o = imp.Owner[i];
                if (o < 0) { orphans++; if (orphan < 0) orphan = i; continue; }
                cells[o]++;
                var rec = imp.Asks[o];
                double X = x.Xs[i % x.W], Y = x.Ys[i / x.W];
                if (X < rec.X0 || X > rec.X1 || Y < rec.Y0 || Y > rec.Y1) { outside[o]++; if (first[o] < 0) first[o] = i; }
            }
            bool pass = orphans == 0, cut = false;
            var parts = new List<string>();
            for (int a = 0; a < n; a++)
            {
                var rec = imp.Asks[a];
                bool isCut = rec.Id == FallCutAskId;
                pass &= outside[a] == 0 && (!isCut || cells[a] > 0);
                cut |= isCut;
                parts.Add((isCut ? "the fall's cut, " : "") + rec.Id + " " + cells[a] + ", " + outside[a] + " outside x " + F(rec.X0) + ".." + F(rec.X1) +
                          ", y " + F(rec.Y0) + ".." + F(rec.Y1) + x.At(first[a]));
                Put(k, rec.Id + ".cells", cells[a]); Put(k, rec.Id + ".outside", outside[a]);
            }
            k.Pass = pass && cut;
            k.Detail = "cells more than one R16 step off pass 9's ground, by the ask that laid them last: " + string.Join("; ", parts) + ". " + orphans +
                       " that no ask laid" + x.At(orphan) + (cut ? "" : "; the file has no " + FallCutAskId);
            Put(k, "orphan_cells", orphans);
            return k;
        }

        /// <summary>
        /// The plunge pool holds its water by flood (amendment 2 §4.2): the level the derivation laid (its surface and the flow's
        /// water) stands at or under its spill, the lowest level at which its water reaches a leak.
        /// </summary>
        static TerrainPlanGuardCase PlungePoolHolds(Ctx x)
        {
            var k = New("PlungePoolHoldsItsSurface");
            bool pass = x.Plan.Falls.Length > 0;
            var parts = new List<string>();
            foreach (var f in x.Plan.Falls)
            {
                if (f == null || f.Pool == null) { pass = false; parts.Add((f == null ? "a fall" : f.Id) + " has no pool"); continue; }
                pass &= StillHolds(x, k, f.Pool, out string what, out _);
                parts.Add(what);
            }
            k.Pass = pass;
            k.Detail = string.Join("; ", parts);
            return k;
        }

        // ---- Ginny's plot, the paths, the east cardinal ---------------------------------------------------------------------------

        /// <summary>
        /// Ginny's plot: the key scenes' frozen pieces (tier 2) and her track at the heights Phase A froze them at, part 1's,
        /// within one R16 step, but the fall's west rim, by name (amendment 2 §4.4, question 4 (a)). Part 1 there is pass 9's
        /// ground, the file's base: the plan's own part 1 no longer is where a held Def's work is gone (pond.stp_fen_pool's
        /// basin reaches the plot), so the case reads the base, and reports how far the plan's part 1 now stands off it.
        /// </summary>
        static TerrainPlanGuardCase GinnysPlotHeld(Ctx x)
        {
            var k = New("GinnysPlotHeldAtPart1");
            var imp = x.Imp;
            var tier2 = new Tally();
            var rim = new Tally();
            var part1 = new Tally();
            for (int i = 0; i < x.N; i++)
            {
                if (x.R.Frozen[i] != 2) continue;
                part1.Add(i, x.C1, x.CI, x.R.E1, imp.Base, UnchangedCodes);
                if (x.InGinnyRim(i)) rim.Add(i, x.CE, x.CI, x.R.E, imp.Base, UnchangedCodes);
                else tier2.Add(i, x.CE, x.CI, x.R.E, imp.Base, UnchangedCodes);
            }

            PathDef track = null;
            foreach (var p in x.Plan.Paths) if (p != null && p.Id == GinnysTrackId) track = p;
            if (track == null) throw new InvalidOperationException("the plan has no " + GinnysTrackId);
            var line = TerrainPlanDerivation.PathLineOf(x.Plan, x.Src, track);
            var onTrack = x.Mask();
            x.Along(onTrack, line, Num(track.Width) / 2);
            var tt = new Tally();
            int trackRim = 0;
            for (int i = 0; i < x.N; i++)
            {
                if (!onTrack[i]) continue;
                if (x.InGinnyRim(i)) { trackRim++; continue; }
                tt.Add(i, x.CE, x.CI, x.R.E, imp.Base, UnchangedCodes);
            }
            double sampled = 0;
            foreach (var q in line.R)
                if (!InGinnyRim(q.X, q.Y)) sampled = Math.Max(sampled, Math.Abs(x.G.Sample(x.R.E, q.X, q.Y) - x.G.Sample(imp.Base, q.X, q.Y)));

            double pieceMax = 0;
            foreach (var p in x.R.Pieces) pieceMax = Math.Max(pieceMax, p.MaxMove);
            k.Pass = tier2.Cells > 0 && tier2.Off == 0 && tt.Cells > 0 && tt.Off == 0;
            k.Detail = x.R.Pieces.Count + " frozen pieces, " + tier2.Cells + " tier-2 cells outside the fall's west rim: " + tier2.Off +
                       " more than a step off part 1's ground, pass 9's (at most " + F(tier2.MaxAbs) + " m" + x.At(tier2.MaxAt) + "); the track's " + tt.Cells +
                       " cells: " + tt.Off + " off (at most " + F(tt.MaxAbs) + " m; " + F(sampled) + " m along its line). The rim (x " + F(GinnyRimX0) + ".." +
                       F(GinnyRimX1) + ", y " + F(GinnyRimY0) + ".." + F(GinnyRimY1) + ", left out by name): " + rim.Off + " of its " + rim.Cells +
                       " tier-2 cells off, by up to " + F(rim.MaxAbs) + " m; " + trackRim + " of the track's cells. The plan's own part 1 stands off pass 9's at " +
                       part1.Off + " of the plot's " + part1.Cells + " cells, by up to " + F(part1.MaxAbs) + " m" + x.At(part1.MaxAt) + " (the held Defs' work gone)";
            Put(k, "tier2_cells", tier2.Cells); Put(k, "tier2_off", tier2.Off); Put(k, "tier2_max_abs_m", tier2.MaxAbs);
            Put(k, "track_cells", tt.Cells); Put(k, "track_off", tt.Off); Put(k, "track_max_abs_m", tt.MaxAbs); Put(k, "track_line_max_abs_m", sampled);
            Put(k, "rim_tier2_cells", rim.Cells); Put(k, "rim_tier2_off", rim.Off); Put(k, "rim_max_abs_m", rim.MaxAbs); Put(k, "rim_track_cells", trackRim);
            Put(k, "plan_part1_off_pass9", part1.Off); Put(k, "plan_part1_max_abs_pass9_m", part1.MaxAbs);
            Put(k, "pieces_max_move_m", pieceMax);
            return k;
        }

        /// <summary>
        /// The shore path is never cut: its lowest ground plus a wade stands above the spring's high water. Where it crosses the
        /// fall's cut it walks the fall's stones, so those stations are left out, by the cut's id (amendment 2 §4.8).
        /// </summary>
        static TerrainPlanGuardCase ShorePathNeverCut(Ctx x)
        {
            var k = New("ShorePathNeverCut");
            PathDef path = null;
            foreach (var p in x.Plan.Paths) if (p != null && p.Id == ShorePathId) path = p;
            if (path == null) throw new InvalidOperationException("the plan has no " + ShorePathId);
            var cut = x.AskRecord(FallCutAskId);
            var line = TerrainPlanDerivation.PathLineOf(x.Plan, x.Src, path);
            double low = double.PositiveInfinity, lowCut = double.PositiveInfinity;
            int at = -1, atCut = -1, skipped = 0;
            for (int j = 0; j < line.R.Length; j++)
            {
                double X = line.R[j].X, Y = line.R[j].Y, e = x.G.Sample(x.R.E, X, Y);
                if (X >= cut.X0 && X <= cut.X1 && Y >= cut.Y0 && Y <= cut.Y1)
                {
                    skipped++;
                    if (e < lowCut) { lowCut = e; atCut = j; }
                    continue;
                }
                if (e < low) { low = e; at = j; }
            }
            double cutAbove = low + x.In.WadeDepth;
            k.Pass = at >= 0 && cutAbove > x.In.SpringHigh;
            k.Detail = "its lowest ground " + F(low) + " m" + (at >= 0 ? " at (" + F(line.R[at].X) + ", " + F(line.R[at].Y) + ")" : "") + ", so it is cut above " +
                       F(cutAbove) + " m; the spring's high water is " + F(x.In.SpringHigh) + " m. On the fall's stones (" + FallCutAskId + ", x " + F(cut.X0) + ".." +
                       F(cut.X1) + ", y " + F(cut.Y0) + ".." + F(cut.Y1) + "): " + skipped + " stations left out, the lowest " + F(lowCut) + " m" +
                       (atCut >= 0 ? " at (" + F(line.R[atCut].X) + ", " + F(line.R[atCut].Y) + ")" : "");
            Put(k, "lowest_m", low); Put(k, "cut_above_m", cutAbove); Put(k, "length_m", line.Length);
            Put(k, "stones_stations", skipped); Put(k, "stones_lowest_m", lowCut);
            return k;
        }

        /// <summary>The east cardinal's spot has more water than a mark's floor at a spring low.</summary>
        static TerrainPlanGuardCase EastCardinalDeep(Ctx x)
        {
            var k = New("EastCardinalDeepAtSpringLow");
            double d = x.In.SpringLow - x.G.Sample(x.R.E, EastCardinal.X, EastCardinal.Y);
            k.Pass = d > x.In.NavFloor;
            k.Detail = F(d) + " m at a spring low at (" + F(EastCardinal.X) + ", " + F(EastCardinal.Y) + "); the floor is " + F(x.In.NavFloor) + " m";
            Put(k, "depth_m", d);
            return k;
        }

        // ---- the rocks and clams, the sea's range ---------------------------------------------------------------------------------

        /// <summary>No shore rock inside the Head's brow or the stack's plinth.</summary>
        static TerrainPlanGuardCase NoShoreRockOnHead(Ctx x)
        {
            var k = New("NoShoreRockOnTheHead");
            var found = new List<string>();
            int rocks = 0;
            foreach (var it in x.In.Items)
            {
                if (it.Root != ShorelineRoot || it.Name == ShorelineRoot || (it.X == 0 && it.Y == 0)) continue;
                rocks++;
                foreach (var f in x.Plan.Forms)
                {
                    if (f == null) continue;
                    bool inBrow = f.Brow != null && f.Brow.Length >= 3 && PointInPoly(it.X, it.Y, Num(f.Brow));
                    bool onPlinth = false;
                    if (f.PlinthRadii.x > 0 && f.PlinthRadii.y > 0)
                    {
                        double rot = Radians(Num(f.RotationDeg));
                        onPlinth = EllipseRadius(it.X, it.Y, Num(f.Centre), Num(f.PlinthRadii.x), Num(f.PlinthRadii.y), Math.Cos(rot), Math.Sin(rot), 0, 0.0, 0.0) <= 1;
                    }
                    if (inBrow || onPlinth) { found.Add(it.Name + " (" + F(it.X) + ", " + F(it.Y) + ") in " + f.Id + (inBrow ? "'s brow" : "'s plinth")); break; }
                }
            }
            k.Pass = found.Count == 0;
            k.Detail = rocks + " shore rocks; " + (found.Count == 0 ? "none on the Head" : found.Count + " on it: " + string.Join(", ", found));
            Put(k, "rocks", rocks); Put(k, "on_head", found.Count);
            return k;
        }

        /// <summary>No shore rock or clam hole on a key scene's path or lot (its paint where tier 1 does not repaint it).</summary>
        static TerrainPlanGuardCase NoRockOrClamOnKeyPaint(Ctx x)
        {
            var k = New("NoRockOrClamOnAKeyScenePath");
            double west = Num(x.Plan.Crossing.XRange.x), keepAbove = Num(x.Plan.Keep.KeepPaintAbove);
            string clams = x.Plan.Keep.ClamRoot;
            var found = new List<string>();
            int n = 0;
            foreach (var it in x.In.Items)
            {
                bool rock = it.Root == ShorelineRoot && it.Name != ShorelineRoot, clam = it.Root == clams && it.Name != clams;
                if (!(rock || clam) || (it.X == 0 && it.Y == 0)) continue;
                n++;
                int c = x.G.ColOf(it.X), r = x.G.RowOf(it.Y);
                if (c < 0 || c >= x.W || r < 0 || r >= x.H) continue;
                int i = r * x.W + c;
                if (x.R.KeyPaint[i] && !(x.R.Pp1[i] > keepAbove || x.Xs[c] < west))
                    found.Add(it.Name + " (" + F(it.X) + ", " + F(it.Y) + ")");
            }
            k.Pass = found.Count == 0;
            k.Detail = n + " rocks and clam holes; " + (found.Count == 0 ? "none on a key scene's path or lot" : found.Count + " on one: " + string.Join(", ", found));
            Put(k, "items", n); Put(k, "on_key_paint", found.Count);
            return k;
        }

        /// <summary>The sea's and the splat's range equal the height map's (the region validator's rule), and the map's the plan's.</summary>
        static TerrainPlanGuardCase SeaRangeMatchesMap(Ctx x)
        {
            var k = New("SeaRangeIsTheMapRange");
            var water = Ranges(x.In.SceneText, x.In.WaterScriptGuid);
            var splat = Ranges(x.In.SceneText, x.In.SplatScriptGuid);
            float pMin = x.Plan.HeightRange.x, pMax = x.Plan.HeightRange.y, mMin = x.In.MapMin, mMax = x.In.MapMax;
            bool ok = water.Count == 1 && splat.Count == 1;
            foreach (var v in water) ok &= Mathf.Approximately(v.Key, mMin) && Mathf.Approximately(v.Value, mMax);
            foreach (var v in splat) ok &= Mathf.Approximately(v.Key, mMin) && Mathf.Approximately(v.Value, mMax);
            bool plan = Mathf.Approximately(mMin, pMin) && Mathf.Approximately(mMax, pMax);
            k.Pass = ok && plan;
            k.Detail = "the sea " + Show(water) + ", the splat " + Show(splat) + ", the map " + F(mMin) + " to " + F(mMax) + ", the plan " + F(pMin) + " to " + F(pMax);
            Put(k, "water_components", water.Count); Put(k, "splat_components", splat.Count);
            Put(k, "map_min", mMin); Put(k, "map_max", mMax); Put(k, "plan_min", pMin); Put(k, "plan_max", pMax);
            return k;
        }

        // ---- the ground file's cases (amendment 1 §5, amendment 2 §4 and §5) ----------------------------------------------------

        /// <summary>
        /// The cannery's circle is pass 9's ground (amendment 2 §4.4, question 16): within the hold's reach of its centre the map
        /// stands within one R16 step of pass 9's. Between the keep's 12 m and the reach, how far it stands off today's is reported.
        /// </summary>
        static TerrainPlanGuardCase CanneryCircle(Ctx x)
        {
            var k = New("CanneryCircleIsPassNine");
            var imp = x.Imp;
            var hold = x.AskDef(CanneryHoldAskId);
            var cc = Num(hold.Centre);
            double reach = Num(hold.Reach), inner = Num(x.Plan.Keep.LargeBuildingRadius);
            var t = new Tally();
            var ring = new Tally();
            if (x.G.Win(cc.X - reach, cc.Y - reach, cc.X + reach, cc.Y + reach, out int r0, out int r1, out int c0, out int c1))
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    double d = Hypot(x.Xs[c] - cc.X, x.Ys[r] - cc.Y);
                    if (d > reach) continue;
                    int i = r * x.W + c;
                    t.Add(i, x.CE, x.CI, x.R.E, imp.Base, UnchangedCodes);
                    if (d > inner) ring.Add(i, x.CE, x.CB, x.R.E, x.Base, UnchangedCodes);
                }
            k.Pass = reach > 0 && t.Cells > 0 && t.Off == 0;
            k.Detail = CanneryHoldAskId + ": " + t.Cells + " cells within " + F(reach) + " m of (" + F(cc.X) + ", " + F(cc.Y) + "), " + t.Off +
                       " more than one R16 step off pass 9's ground (at most " + F(t.MaxAbs) + " m" + x.At(t.MaxAt) + "). Between " + F(inner) + " and " + F(reach) +
                       " m: " + ring.Off + " of " + ring.Cells + " cells off today's ground, by up to " + F(ring.MaxAbs) + " m";
            Put(k, "cells", t.Cells); Put(k, "off_pass9", t.Off); Put(k, "max_abs_pass9_m", t.MaxAbs);
            Put(k, "ring_cells", ring.Cells); Put(k, "ring_off_today", ring.Off); Put(k, "ring_max_abs_today_m", ring.MaxAbs);
            return k;
        }

        /// <summary>
        /// The kept walls' channels hold water at a spring low (amendment 1 §4.5): at each toe station of every wall the file
        /// keeps (by real id, its CliffWallSurface's _toePlan in the scene), where pass 9's ground stands under the spring's low
        /// water, so does the map's. A kept wall missing from the scene, or two walls with one id, fail the case.
        /// </summary>
        static TerrainPlanGuardCase ToeChannelsHold(Ctx x)
        {
            var k = New("ToeChannelsHoldAtSpringLow");
            var imp = x.Imp;
            var ground = x.In.Ground ?? throw new InvalidOperationException("the input names no ground file");
            var kept = ground.KeptWalls ?? new string[0];
            var twice = new List<string>();
            var toes = Toes(x.In.SceneText, twice);
            var missing = new List<string>();
            int stations = 0, wet = 0, dry = 0;
            double least = double.PositiveInfinity;
            string leastAt = "", dryAt = "";
            foreach (var id in kept)
            {
                if (!toes.TryGetValue(id, out var pts)) { missing.Add(id); continue; }
                for (int j = 0; j < pts.Count; j++)
                {
                    stations++;
                    if (!(imp.BaseAt(pts[j].X, pts[j].Y) < x.In.SpringLow)) continue;
                    wet++;
                    double margin = x.In.SpringLow - imp.Bilinear(x.R.E, pts[j].X, pts[j].Y);
                    string where = "wall " + id + " station " + j + " (" + F(pts[j].X) + ", " + F(pts[j].Y) + ")";
                    if (!(margin > 0)) { dry++; if (dryAt == "") dryAt = where; }
                    if (margin < least) { least = margin; leastAt = where; }
                }
            }
            k.Pass = kept.Length > 0 && missing.Count == 0 && twice.Count == 0 && wet > 0 && dry == 0;
            k.Detail = kept.Length + " kept walls (" + ground.KeptWallsFrom + ") of the scene's " + toes.Count + ": " + stations + " toe stations, " + wet +
                       " under the spring's low water (" + F(x.In.SpringLow) + " m) on pass 9's ground, " + dry + " of them dry on the map" +
                       (dry > 0 ? ", first " + dryAt : "") + "; the least margin " + F(least) + " m, " + leastAt +
                       (missing.Count > 0 ? "; MISSING from the scene: " + string.Join(", ", missing) : "") +
                       (twice.Count > 0 ? "; ids two walls share: " + string.Join(", ", twice) : "");
            Put(k, "kept_walls", kept.Length); Put(k, "scene_walls", toes.Count); Put(k, "missing", missing.Count); Put(k, "stations", stations);
            Put(k, "wet_at_pass9", wet); Put(k, "dry_on_map", dry); Put(k, "least_margin_m", least);
            return k;
        }

        /// <summary>
        /// Fix 2, the main beach's west end (amendment 1 §4.4): the blend only raises, never ground that stood dry at the spring's
        /// high water, inside its window; no slope on or beside it is steeper than the beach's own (A2's rule: central differences,
        /// the north-south one over 0.5 × 1.556 m), and no still water stands there.
        /// </summary>
        static TerrainPlanGuardCase BeachWestEndBlends(Ctx x)
        {
            var k = New("BeachWestEndBlends");
            var imp = x.Imp;
            var rec = x.AskRecord(WestBlendAskId);
            int beach = x.AskIndex(MainBeachAskId);
            var near = x.Beside(rec.Changed);
            int lowered = 0, wasDry = 0, outside = 0, mostAt = -1;
            double most = 0;
            for (int j = 0; j < rec.Changed.Length; j++)
            {
                int i = rec.Changed[j];
                double before = rec.Before[j], after = x.R.E[i], X = x.Xs[i % x.W], Y = x.Ys[i / x.W];
                if (after < before - x.Step) lowered++;
                if (before >= x.In.SpringHigh) wasDry++;
                if (X < rec.X0 || X > rec.X1 || Y < rec.Y0 || Y > rec.Y1) outside++;
                if (after - before > most) { most = after - before; mostAt = i; }
            }
            double steepNear = 0, steepBeach = 0;
            int steepNearAt = -1, steepBeachAt = -1, wet = 0, beachCells = 0;
            for (int i = 0; i < x.N; i++)
            {
                bool isNear = near[i], isBeach = !isNear && imp.Owner[i] == beach;
                if (!isNear && !isBeach) continue;
                double s = x.Slope(i);
                if (isNear)
                {
                    if (!double.IsNaN(x.R.Still[i])) wet++;
                    if (s > steepNear) { steepNear = s; steepNearAt = i; }
                }
                else
                {
                    beachCells++;
                    if (s > steepBeach) { steepBeach = s; steepBeachAt = i; }
                }
            }
            k.Pass = rec.Changed.Length > 0 && lowered == 0 && wasDry == 0 && outside == 0 && beachCells > 0 && steepNear <= steepBeach && wet == 0;
            k.Detail = WestBlendAskId + ": " + rec.Changed.Length + " cells, raised by up to " + F(most) + " m" + x.At(mostAt) + "; " + lowered + " lowered, " +
                       wasDry + " that stood at or over the spring's high water, " + outside + " outside its window. The steepest slope on and beside it " +
                       F(steepNear) + x.At(steepNearAt) + ", the beach's own " + F(steepBeach) + x.At(steepBeachAt) + " (" + beachCells + " cells); " + wet +
                       " cells on and beside it under still water";
            Put(k, "cells", rec.Changed.Length); Put(k, "max_raise_m", most); Put(k, "lowered", lowered); Put(k, "was_dry", wasDry);
            Put(k, "outside", outside); Put(k, "steepest_near", steepNear); Put(k, "steepest_beach", steepBeach); Put(k, "still_near", wet);
            return k;
        }

        /// <summary>
        /// The beach's dry sand stays dry (amendment 1 §4.4): inside the main beach's box, every cell that stood at or over the
        /// spring's high water before the game's asks still does, within one R16 step.
        /// </summary>
        static TerrainPlanGuardCase BeachDrySand(Ctx x)
        {
            var k = New("BeachDrySandStaysAboveTheSpringHigh");
            var beach = x.AskRecord(MainBeachAskId);
            var pre = x.BeforeTheGameAsks();
            int dry = 0, sank = 0, sankAt = -1, leastAt = -1;
            double least = double.PositiveInfinity;
            if (x.G.Win(beach.X0, beach.Y0, beach.X1, beach.Y1, out int r0, out int r1, out int c0, out int c1))
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    double X = x.Xs[c], Y = x.Ys[r];
                    if (X < beach.X0 || X > beach.X1 || Y < beach.Y0 || Y > beach.Y1) continue;
                    int i = r * x.W + c;
                    if (!(pre[i] >= x.In.SpringHigh)) continue;
                    dry++;
                    double m = x.R.E[i] - x.In.SpringHigh;
                    if (m < least) { least = m; leastAt = i; }
                    if (x.R.E[i] < x.In.SpringHigh - x.Step) { sank++; if (sankAt < 0) sankAt = i; }
                }
            k.Pass = dry > 0 && sank == 0;
            k.Detail = MainBeachAskId + "'s box x " + F(beach.X0) + ".." + F(beach.X1) + ", y " + F(beach.Y0) + ".." + F(beach.Y1) + ": " + dry +
                       " cells at or over the spring's high water (" + F(x.In.SpringHigh) + " m) before the game's asks, " + sank + " of them under it now" +
                       x.At(sankAt) + "; the least stands " + F(least) + " m over it" + x.At(leastAt);
            Put(k, "dry_cells", dry); Put(k, "sank", sank); Put(k, "least_over_m", least);
            return k;
        }

        /// <summary>
        /// The dipping pool holds its level by flood (amendment 2 §4.2): the derivation lays it at its Def's surface (to half a
        /// millimetre), at or under its spill.
        /// </summary>
        static TerrainPlanGuardCase DippingPoolHolds(Ctx x)
        {
            var k = New("DippingPoolHoldsItsLevel");
            var pools = x.Plan.StillPools ?? new PondDef[0];
            bool pass = pools.Length > 0;
            var parts = new List<string>();
            foreach (var p in pools)
            {
                if (p == null) { pass = false; parts.Add("a missing pool"); continue; }
                bool ok = StillHolds(x, k, p, out string what, out double laid);
                double off = Math.Abs(laid - Num(p.Surface));
                pass &= ok && off <= StillLevelTolerance;
                parts.Add(what + "; " + F(off) + " m off its surface");
                Put(k, p.Id + ".off_surface_m", off);
            }
            k.Pass = pass;
            k.Detail = string.Join("; ", parts) + " (at most " + F(StillLevelTolerance) + " m off)";
            return k;
        }

        /// <summary>
        /// Fix 4, the hollow by the Head's neck (amendment 2 §4.3): filled to its level (within its tolerance), one 4-connected
        /// patch raised to that level within one R16 step, none of it in the cannery's circle; no still water on or beside it, and
        /// the still deriver finds no hollow there: none centred within the patch's reach of the ask's seed (its farthest cell,
        /// and a cell). Other hollows in its window are named, not judged.
        /// </summary>
        static TerrainPlanGuardCase NeckHollowFilled(Ctx x)
        {
            var k = New("NeckHollowFilled");
            var def = x.AskDef(NeckFillAskId);
            var rec = x.AskRecord(NeckFillAskId);
            double want = Num(def.Level), tol = Num(def.LevelTolerance);
            bool levelOk = Math.Abs(rec.Level - want) <= tol;
            GroundAskDef keep = string.IsNullOrEmpty(def.KeepOutOf) ? null : x.AskDef(def.KeepOutOf);
            double cellM2 = x.G.Mpp * x.G.Mpp / HiddenHarbours.Core.IsoGround.GroundDepthScale;
            int notAtLevel = 0, notRaised = 0, inKeep = 0;
            double deepest = 0, volume = 0;
            for (int j = 0; j < rec.Changed.Length; j++)
            {
                int i = rec.Changed[j];
                double after = x.R.E[i], before = rec.Before[j];
                if (Math.Abs(after - rec.Level) > x.Step) notAtLevel++;
                if (!(after > before)) notRaised++;
                deepest = Math.Max(deepest, after - before);
                volume += (after - before) * cellM2;
                if (keep != null && Hypot(x.Xs[i % x.W] - keep.Centre.x, x.Ys[i / x.W] - keep.Centre.y) <= Num(keep.Reach)) inKeep++;
            }
            int blob = Blob4(rec.Changed, x.W);
            var near = x.Beside(rec.Changed);
            int wet = 0;
            for (int i = 0; i < x.N; i++) if (near[i] && !double.IsNaN(x.R.Still[i])) wet++;
            var seed = Num(def.Centre);
            double reach = 0;
            foreach (int i in rec.Changed) reach = Math.Max(reach, Hypot(x.Xs[i % x.W] - seed.X, x.Ys[i / x.W] - seed.Y));
            reach += x.G.Mpp;
            var left = new List<string>();
            var elsewhere = new List<string>();
            foreach (var h in x.R.Hollows)
            {
                if (Hypot(h.CentreX - seed.X, h.CentreY - seed.Y) <= reach) left.Add(h.Id);
                else if (h.CentreX >= rec.X0 && h.CentreX <= rec.X1 && h.CentreY >= rec.Y0 && h.CentreY <= rec.Y1)
                    elsewhere.Add(h.Id + " (" + F(h.CentreX) + ", " + F(h.CentreY) + ")");
            }
            int n = rec.Changed.Length;
            k.Pass = levelOk && n > 0 && blob == n && notAtLevel == 0 && notRaised == 0 && inKeep == 0 && wet == 0 && left.Count == 0;
            k.Detail = NeckFillAskId + ": level " + F(rec.Level) + " m (its Def " + F(want) + " ± " + F(tol) + "); " + n + " cells (" + F(n * cellM2) + " m²), " +
                       blob + " in one 4-connected patch, " + notAtLevel + " off the level by more than a step, " + notRaised + " not raised; up to " + F(deepest) +
                       " m deep, " + F(volume) + " m³; " + inKeep + " in " + (keep != null ? keep.Id : "no keep") + ", where " + rec.Held +
                       " of its basin's cells are held as they were; " + wet + " on and beside it under still water; hollows within " + F(reach) + " m of (" +
                       F(seed.X) + ", " + F(seed.Y) + "): " + (left.Count == 0 ? "none" : string.Join(", ", left)) + "; elsewhere in its window: " +
                       (elsewhere.Count == 0 ? "none" : string.Join(", ", elsewhere));
            Put(k, "level_m", rec.Level); Put(k, "cells", n); Put(k, "area_m2", n * cellM2); Put(k, "deepest_m", deepest); Put(k, "volume_m3", volume);
            Put(k, "blob_cells", blob); Put(k, "in_keep", inKeep); Put(k, "held", rec.Held); Put(k, "still_near", wet); Put(k, "hollows_left", left.Count);
            Put(k, "hollows_elsewhere_in_window", elsewhere.Count); Put(k, "reach_m", reach);
            return k;
        }

        /// <summary>
        /// The Heath Brook never climbs to the shore (amendment 2 §4.2): no climb over its whole line, with no skip; its Def ends
        /// at the shore, where the ground stands at or over mean tide and falls under it within 2 m past the end (the sea owns its
        /// mouth).
        /// </summary>
        static TerrainPlanGuardCase HeathBrook(Ctx x)
        {
            var k = New("HeathBrookNeverClimbsToTheShore");
            StreamDef st = null;
            foreach (var s in x.Plan.Streams) if (s != null && s.Id == HeathBrookId) st = s;
            if (st == null) throw new InvalidOperationException("the plan has no " + HeathBrookId);
            var line = Catmull(Num(st.Points), Num(x.Plan.LineStep));
            double climb = Climb(x, st, line, null, out int at, out _);
            double mean = (x.In.SpringLow + x.In.SpringHigh) / 2;
            int n = line.R.Length;
            var end = line.R[n - 1];
            double dx = end.X - line.R[n - 2].X, dy = end.Y - line.R[n - 2].Y, len = Hypot(dx, dy);
            dx /= len; dy /= len;
            double eEnd = x.G.Sample(x.R.E, end.X, end.Y), under = double.NaN;
            for (int j = 1; j * MouthStep <= MouthReach + 1e-9; j++)
                if (x.G.Sample(x.R.E, end.X + dx * j * MouthStep, end.Y + dy * j * MouthStep) < mean) { under = j * MouthStep; break; }
            k.Pass = climb <= StreamClimb && eEnd >= mean && !double.IsNaN(under);
            k.Detail = HeathBrookId + " climbs " + F(climb) + " m" + (at >= 0 ? " at (" + F(line.R[at].X) + ", " + F(line.R[at].Y) + ")" : "") + " (at most " +
                       F(StreamClimb) + "); its end (" + F(end.X) + ", " + F(end.Y) + ") stands " + F(eEnd - mean) + " m over mean tide, and the ground falls under it " +
                       (double.IsNaN(under) ? "NOT within " + F(MouthReach) + " m" : F(under) + " m past the end");
            Put(k, "max_climb_m", climb); Put(k, "end_over_mean_m", eEnd - mean); Put(k, "under_mean_past_end_m", under);
            return k;
        }

        /// <summary>
        /// The crossing's walk stays off still water (amendment 1 §5; amendment 2's C2): none of its line's stations on still water,
        /// and every crest pool's water at least the plan's WalkClear off its line (world units; ground metres reported).
        /// </summary>
        static TerrainPlanGuardCase CrossingWalk(Ctx x)
        {
            var k = New("CrossingWalkOffStillWater");
            PathDef walk = null;
            foreach (var p in x.Plan.Paths)
            {
                if (p == null || p.Kind != PathKind.TidalCrossing) continue;
                if (walk != null) throw new InvalidOperationException("the plan has two tidal crossings, " + walk.Id + " and " + p.Id);
                walk = p;
            }
            if (walk == null) throw new InvalidOperationException("the plan has no tidal crossing");
            var line = TerrainPlanDerivation.PathLineOf(x.Plan, x.Src, walk);
            int onWater = 0, wetAt = -1;
            for (int j = 0; j < line.R.Length; j++)
            {
                int c = x.G.ColOf(line.R[j].X), r = x.G.RowOf(line.R[j].Y);
                if (c < 0 || c >= x.W || r < 0 || r >= x.H || double.IsNaN(x.R.Still[r * x.W + c])) continue;
                onWater++;
                if (wetAt < 0) wetAt = j;
            }
            double clear = Num(x.Plan.WalkClear), least = double.PositiveInfinity, leastGround = double.PositiveInfinity;
            string leastPool = "";
            int pools = 0, water = 0;
            foreach (var p in x.Plan.Pools)
            {
                if (p == null) continue;
                pools++;
                var cc = Num(p.Centre);
                double ra = Num(p.Radii.x), rb = Num(p.Radii.y), R = Math.Max(ra, rb) * PoolMaskWindow;
                double rot = Radians(Num(p.RotationDeg)), cr = Math.Cos(rot), sr = Math.Sin(rot);
                if (!x.G.Win(cc.X - R, cc.Y - R, cc.X + R, cc.Y + R, out int r0, out int r1, out int c0, out int c1)) continue;
                int hint = 0;
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    int i = r * x.W + c;
                    if (double.IsNaN(x.R.Still[i]) || !(EllipseRadius(x.Xs[c], x.Ys[r], cc, ra, rb, cr, sr, 0, 0.0, 0.0) < PoolMaskRadius)) continue;
                    water++;
                    line.Query(x.Xs[c], x.Ys[r], ref hint, out double d, out _, out _);
                    if (d < least) { least = d; leastPool = p.Id + x.At(i); }
                    leastGround = Math.Min(leastGround, GroundToLine(x.Xs[c], x.Ys[r], line));
                }
            }
            k.Pass = line.R.Length > 1 && onWater == 0 && pools > 0 && least >= clear;
            k.Detail = walk.Id + ": " + onWater + " of its " + line.R.Length + " stations on still water" +
                       (wetAt >= 0 ? ", first (" + F(line.R[wetAt].X) + ", " + F(line.R[wetAt].Y) + ")" : "") + "; " + pools + " crest pools, " + water +
                       " cells of their water, the nearest " + F(least) + " m off the line (" + F(leastGround) + " m of ground), " + leastPool + "; at least " + F(clear);
            Put(k, "stations", line.R.Length); Put(k, "on_still_water", onWater); Put(k, "pool_water_cells", water);
            Put(k, "least_m", least); Put(k, "least_ground_m", leastGround);
            return k;
        }

        // ---- the village's ground checks (amendment 2 §4.4, V1's decision 3) ------------------------------------------------------

        /// <summary>Every V1 footprint and yard stands at least 3 m of ground from every trunk (the woods' items in the scene).</summary>
        static TerrainPlanGuardCase VillageClearOfTrunks(Ctx x)
        {
            var k = New("VillageClearOfTrunks");
            var v = x.Village;
            var trunks = x.ItemsOf(x.Plan.Keep.WoodsRoot);
            var lots = Lots(v);
            double least = double.PositiveInfinity;
            string leastAt = "";
            var fails = new List<string>();
            foreach (var lot in lots)
            {
                double d = double.PositiveInfinity;
                PlanPoint nearest = default;
                foreach (var t in trunks)
                {
                    var p = V(t);
                    double e = lot.Footprint ? VillageGeometry.DistanceToBounds(p, lot.Bounds) : ToPolygon(lot.Outline, p);
                    if (e < d) { d = e; nearest = t; }
                }
                string what = lot.Name + " " + F(d) + " m from the trunk at (" + F(nearest.X) + ", " + F(nearest.Y) + ")";
                if (d < least) { least = d; leastAt = what; }
                if (d < TrunkClear) fails.Add(what);
            }
            k.Pass = trunks.Count > 0 && lots.Count > 0 && fails.Count == 0;
            k.Detail = lots.Count + " footprints and yards, " + trunks.Count + " trunks: " + fails.Count + " nearer than " + F(TrunkClear) + " m" +
                       (fails.Count > 0 ? " (" + string.Join("; ", fails) + ")" : "") + "; the least margin " + F(least - TrunkClear) + " m, " + leastAt;
            Put(k, "lots", lots.Count); Put(k, "trunks", trunks.Count); Put(k, "nearer", fails.Count); Put(k, "least_m", least);
            return k;
        }

        /// <summary>
        /// The village on the plateau: the ground at least 5.9 m under every V1 footprint and 5.5 m under every yard, read at the
        /// cells whose centres lie inside it. Each outline's corners, read bilinearly, are reported, not judged: the village plan's
        /// own least margins (5.91 and 5.62 m) are not its corners'.
        /// </summary>
        static TerrainPlanGuardCase VillageOnThePlateau(Ctx x)
        {
            var k = New("VillageOnThePlateau");
            var lots = Lots(x.Village);
            double leastF = double.PositiveInfinity, leastY = double.PositiveInfinity, corner = double.PositiveInfinity;
            string atF = "", atY = "", cornerAt = "";
            var fails = new List<string>();
            foreach (var lot in lots)
            {
                double low = double.PositiveInfinity;
                int lowAt = -1;
                var poly = lot.Outline;
                float x0 = float.PositiveInfinity, y0 = float.PositiveInfinity, x1 = float.NegativeInfinity, y1 = float.NegativeInfinity;
                foreach (var q in poly) { x0 = Math.Min(x0, q.x); y0 = Math.Min(y0, q.y); x1 = Math.Max(x1, q.x); y1 = Math.Max(y1, q.y); }
                if (x.G.Win(x0, y0, x1, y1, out int r0, out int r1, out int c0, out int c1))
                    for (int r = r0; r < r1; r++)
                    for (int c = c0; c < c1; c++)
                    {
                        var p = new Vector2((float)x.Xs[c], (float)x.Ys[r]);
                        if (lot.Footprint ? !VillageGeometry.InBounds(lot.Bounds, p) : !VillageGeometry.Contains(poly, p)) continue;
                        int i = r * x.W + c;
                        if (x.R.E[i] < low) { low = x.R.E[i]; lowAt = i; }
                    }
                double floor = lot.Footprint ? FootprintGround : YardGround;
                foreach (var q in poly)
                {
                    double e = x.G.Sample(x.R.E, q.x, q.y);
                    if (e - floor < corner) { corner = e - floor; cornerAt = lot.Name + "'s corner (" + F(q.x) + ", " + F(q.y) + "), " + F(e) + " m"; }
                }
                if (lowAt < 0) { fails.Add(lot.Name + " has no cell under it"); continue; }
                string what = lot.Name + " " + F(low) + " m" + x.At(lowAt);
                if (lot.Footprint) { if (low < leastF) { leastF = low; atF = what; } }
                else if (low < leastY) { leastY = low; atY = what; }
                if (!(low >= floor)) fails.Add(what + " (at least " + F(floor) + ")");
            }
            k.Pass = lots.Count > 0 && fails.Count == 0;
            k.Detail = lots.Count + " footprints and yards: " + fails.Count + " under their floor" + (fails.Count > 0 ? " (" + string.Join("; ", fails) + ")" : "") +
                       "; the lowest footprint " + atF + " (at least " + F(FootprintGround) + "), the lowest yard " + atY + " (at least " + F(YardGround) +
                       "). Their corners, not judged: the least " + F(corner) + " m over its floor, " + cornerAt;
            Put(k, "lots", lots.Count); Put(k, "under_floor", fails.Count); Put(k, "least_footprint_m", leastF); Put(k, "least_yard_m", leastY);
            Put(k, "least_corner_over_floor_m", corner);
            return k;
        }

        /// <summary>
        /// Pass 9's barren and shore paths keep their room: every V1 footprint and yard at least half the path's width plus 0.5 m
        /// of ground from its line.
        /// </summary>
        static TerrainPlanGuardCase VillagePathsKeepTheirRoom(Ctx x)
        {
            var k = New("VillagePathsKeepTheirRoom");
            var lots = Lots(x.Village);
            var fails = new List<string>();
            var parts = new List<string>();
            int paths = 0;
            foreach (var id in new[] { BarrenPathId, ShorePathId })
            {
                PathDef path = null;
                foreach (var p in x.Plan.Paths) if (p != null && p.Id == id) path = p;
                if (path == null) throw new InvalidOperationException("the plan has no " + id);
                paths++;
                var line = TerrainPlanDerivation.PathLineOf(x.Plan, x.Src, path);
                var pts = new Vector2[line.R.Length];
                for (int j = 0; j < pts.Length; j++) pts[j] = V(line.R[j]);
                double room = Num(path.Width) / 2 + PathRoom, least = double.PositiveInfinity;
                string leastAt = "";
                foreach (var lot in lots)
                {
                    double d = LineToPolygon(pts, lot.Outline);
                    if (d < least) { least = d; leastAt = lot.Name; }
                    if (d < room) fails.Add(lot.Name + " " + F(d) + " m from " + id + " (at least " + F(room) + ")");
                }
                parts.Add(id + " (room " + F(room) + " m): the nearest " + leastAt + ", " + F(least) + " m, margin " + F(least - room));
                Put(k, id + ".room_m", room); Put(k, id + ".least_m", least);
            }
            k.Pass = paths == 2 && lots.Count > 0 && fails.Count == 0;
            k.Detail = lots.Count + " footprints and yards: " + fails.Count + " inside a path's room" + (fails.Count > 0 ? " (" + string.Join("; ", fails) + ")" : "") +
                       "; " + string.Join("; ", parts);
            Put(k, "lots", lots.Count); Put(k, "inside_room", fails.Count);
            return k;
        }

        /// <summary>
        /// The main beach's sand by its recipe (amendment 2 §4.5, CD's paint rule as data): where the bay paints, a cell further
        /// than the noise inside one band carries that band's zone, and its back zone stands only over its BackAbove (there is
        /// some back).
        /// </summary>
        static TerrainPlanGuardCase MainBeachSand(Ctx x)
        {
            var k = New("MainBeachSandByItsRecipe");
            var paint = x.R.BayPaint ?? throw new InvalidOperationException("the derivation painted no bay");
            var g = x.Src.Ground ?? throw new InvalidOperationException("the sources carry no ground file's ground");
            bool pass = x.Plan.Bays.Length > 0;
            var parts = new List<string>();
            foreach (var bay in x.Plan.Bays)
            {
                if (bay == null || bay.Recipe == null || bay.Recipe.Bands == null || bay.Recipe.Bands.Length == 0)
                    throw new InvalidOperationException("a bay has no recipe");
                var bands = bay.Recipe.Bands;
                var below = new double[bands.Length];
                var zone = new byte[bands.Length];
                var count = new int[bands.Length];
                for (int b = 0; b < bands.Length; b++) { below[b] = Num(bands[b].Below); zone[b] = Zone(bands[b].Zone); }
                byte back = Zone(bay.BackZone);
                bool backIsBand = Array.IndexOf(zone, back) >= 0;
                double noise = Num(bay.Noise.x), backAbove = Num(bay.BackAbove);
                double bx0 = Num(bay.BoxMin.x), by0 = Num(bay.BoxMin.y), bx1 = Num(bay.BoxMax.x), by1 = Num(bay.BoxMax.y);
                int painted = 0, backs = 0, backLow = 0, sure = 0, wrong = 0, wrongAt = -1;
                if (x.G.Win(bx0, by0, bx1, by1, out int r0, out int r1, out int c0, out int c1))
                    for (int r = r0; r < r1; r++)
                    for (int c = c0; c < c1; c++)
                    {
                        int i = r * x.W + c;
                        if (paint[i] == TerrainPlanZones.Unpainted || x.Xs[c] < bx0 || x.Xs[c] > bx1 || x.Ys[r] < by0 || x.Ys[r] > by1) continue;
                        painted++;
                        double h = g[i];
                        if (paint[i] == back && !backIsBand) { backs++; if (!(h > backAbove)) backLow++; continue; }
                        int band = 0;
                        while (band < below.Length - 1 && !(h < below[band])) band++;
                        double lo = band == 0 ? double.NegativeInfinity : below[band - 1];
                        if (!(h - lo > noise) || !(below[band] - h > noise)) continue;
                        sure++;
                        if (paint[i] == zone[band]) count[band]++;
                        else { wrong++; if (wrongAt < 0) wrongAt = i; }
                    }
                pass &= painted > 0 && backs > 0 && backLow == 0 && sure > 0 && wrong == 0;
                var byBand = new List<string>();
                for (int b = 0; b < bands.Length; b++) byBand.Add(bands[b].Zone + " " + count[b]);
                parts.Add(bay.Id + " (" + bay.Recipe.Id + "): " + painted + " cells painted; " + sure + " further than " + F(noise) + " m inside a band: " +
                          string.Join(", ", byBand) + ", " + wrong + " not its band's" + x.At(wrongAt) + "; " + backs + " at its back (" + bay.BackZone + "), " +
                          backLow + " of them at or under " + F(backAbove) + " m");
                Put(k, bay.Id + ".painted", painted); Put(k, bay.Id + ".sure", sure); Put(k, bay.Id + ".wrong", wrong);
                Put(k, bay.Id + ".back", backs); Put(k, bay.Id + ".back_low", backLow);
            }
            k.Pass = pass;
            k.Detail = string.Join("; ", parts);
            return k;
        }

        // ---- part 2's guards on the patched walls (terrain PR 5w: amendment 1 §4.6) -------------------------------------------------

        /// <summary>
        /// The flats keep their moat off the patched toes (question 3): no cell the south lays flats on (its weight over 0) lies
        /// within the flats' moat (FlatsDef.Moat.x) of a scene wall's toe line.
        /// </summary>
        static TerrainPlanGuardCase FlatsMoat(Ctx x)
        {
            var k = New("FlatsMoatOffThePatchedToes");
            var south = x.R.South ?? throw new InvalidOperationException("the derivation laid no south");
            var flats = x.Plan.Flats ?? throw new InvalidOperationException("the plan has no flats");
            var pw = x.Walls;
            double moat = Num(flats.Moat.x);
            int cells = 0, inside = 0, insideAt = -1, nearestAt = -1;
            double nearest = double.PositiveInfinity;
            for (int i = 0; i < x.N; i++)
            {
                if (!(south.FlatsW[i] > 0)) continue;
                cells++;
                double d = pw.ToeDistance[i];
                if (d < nearest) { nearest = d; nearestAt = i; }
                if (d < moat) { inside++; if (insideAt < 0) insideAt = i; }
            }
            k.Pass = pw.Problem == null && moat > 0 && cells > 0 && inside == 0;
            k.Detail = flats.Id + ": " + cells + " cells of flats, " + inside + " of them within its " + F(moat) + " m moat of the scene's " + pw.Ids.Count +
                       " walls' toes" + (inside > 0 ? ", first" + x.At(insideAt) : "") + "; the nearest " +
                       (nearestAt < 0 ? "more than " + F(ToeReach) + " m off any" : F(nearest) + " m off " + pw.Ids[pw.NearestWall[nearestAt]] + "'s toe" + x.At(nearestAt)) +
                       (pw.Problem != null ? "; " + pw.Problem : "");
            Put(k, "flats_cells", cells); Put(k, "inside_moat", inside); Put(k, "moat_m", moat); Put(k, "nearest_m", nearest);
            return k;
        }

        /// <summary>
        /// A new toe station in a wet section stands wet at a spring low (question 4): each toe station of a wall the patch
        /// changed (not kept) in the South Arm, the Weather Cliff or the South-West Bluff stands under the spring's low water on the
        /// map, but the beach's banked end, by name (042 k23, 043 k0 to k2). A station is named by its parent (a split wall's
        /// SplitFrom) and its Def's station, and counts once; one cut between two whole stations is named where both of
        /// them are.
        /// </summary>
        static TerrainPlanGuardCase NewToesWet(Ctx x)
        {
            var k = New("NewToesWetInTheWetSections");
            var pw = x.Walls;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var walls = new List<string>();
            var named = new List<string>();
            var other = new List<string>();
            var unpaired = new List<string>();
            int stations = 0, wet = 0;
            double least = double.PositiveInfinity;
            string leastAt = "";
            foreach (string id in pw.Ids)
            {
                if (!pw.Defs.TryGetValue(id, out var d) || d.Status == CliffWallStatus.Kept || Array.IndexOf(WetSections, pw.Section[id]) < 0) continue;
                var toes = pw.Toes[id];
                if (d.Stations == null || d.Stations.Length != toes.Count) { unpaired.Add(id); continue; }
                walls.Add(id);
                string parent = string.IsNullOrEmpty(d.SplitFrom) ? id : RealIdOf(d.SplitFrom);
                for (int j = 0; j < toes.Count; j++)
                {
                    string at = parent + " k" + d.Stations[j].ToString(Inv);
                    if (!seen.Add(at)) continue;
                    stations++;
                    var p = toes[j];
                    double margin = x.In.SpringLow - x.Imp.Bilinear(x.R.E, p.X, p.Y);
                    if (margin > 0)
                    {
                        wet++;
                        if (margin < least) { least = margin; leastAt = at; }
                        continue;
                    }
                    (DryByName(parent, d.Stations[j]) ? named : other).Add(at + " (dry by " + F(-margin) + " m)");
                }
            }
            k.Pass = pw.Problem == null && stations > 0 && unpaired.Count == 0 && other.Count == 0;
            k.Detail = stations + " new toe stations in the wet sections (walls " + string.Join(", ", walls) + "), " + wet + " under the spring's low water (" +
                       F(x.In.SpringLow) + " m) on the map, the least by " + F(least) + " m at " + leastAt + "; dry by name: " +
                       (named.Count > 0 ? string.Join(", ", named) : "none") + "; dry otherwise: " + (other.Count > 0 ? string.Join(", ", other) : "none") +
                       (unpaired.Count > 0 ? "; toes that do not pair with their Def's stations: " + string.Join(", ", unpaired) : "") +
                       (pw.Problem != null ? "; " + pw.Problem : "");
            Put(k, "stations", stations); Put(k, "wet", wet); Put(k, "dry_named", named.Count); Put(k, "dry_other", other.Count);
            Put(k, "least_margin_m", least);
            return k;
        }

        /// <summary>A station named dry (<see cref="DryToesByName"/>), or one cut between two whole stations of its parent that
        /// both are: St Peters' 1 m cut adds stations as slices of the face between whole ones (terrain PR 5w).</summary>
        static bool DryByName(string parent, float station)
        {
            if (Array.IndexOf(DryToesByName, parent + " k" + station.ToString(Inv)) >= 0) return true;
            double below = Math.Floor(station), above = Math.Ceiling(station);
            return below != station && Array.IndexOf(DryToesByName, parent + " k" + below.ToString(Inv)) >= 0 &&
                   Array.IndexOf(DryToesByName, parent + " k" + above.ToString(Inv)) >= 0;
        }

        /// <summary>
        /// A channel cell wet today stays wet (question 5): of the cells within 3 m of a patched toe and under 0 m in the flats'
        /// window, one today's ground holds under the spring's low water stands under it on the map, by section (its nearest toe's
        /// wall's), in the wet sections; but two named sets, the Weather Cliff's 42 by 042 and 043 (the beach) and the South-West
        /// Bluff's 2 by 070. A cell is named by its nearest toe's wall's parent (a split wall's SplitFrom), as
        /// <see cref="NewToesWet"/> names a station: St Peters' 1 m cut leaves pieces of 070 nearer one of the bluff's two. The
        /// ledges' cells are reported, not judged.
        /// </summary>
        static TerrainPlanGuardCase ChannelCellsStayWet(Ctx x)
        {
            var k = New("ChannelCellsStayWet");
            var pw = x.Walls;
            double low = x.In.SpringLow;
            var tally = new SortedDictionary<string, int[]>(StringComparer.Ordinal);     // section: cells, wet today, wet now, lost
            var lostBy = new SortedDictionary<string, int>(StringComparer.Ordinal);
            int beach = 0, bluff = 0, other = 0, otherAt = -1;
            if (!x.G.Win(FlatsX0, FlatsY0, FlatsX1, FlatsY1, out int r0, out int r1, out int c0, out int c1))
                throw new InvalidOperationException("the flats' window is off the map");
            for (int r = r0; r < r1; r++)
            for (int c = c0; c < c1; c++)
            {
                int i = r * x.W + c;
                if (!(pw.ToeDistance[i] < ChannelReach) || !(x.R.E[i] < ChannelUnder) || !(x.Ys[r] < FlatsY1)) continue;
                string id = pw.Ids[pw.NearestWall[i]];
                string sec = pw.Section.TryGetValue(id, out var s) ? s : "(no section)";
                if (!tally.TryGetValue(sec, out var t)) tally[sec] = t = new int[4];
                bool wasWet = x.Base[i] < low, isWet = x.R.E[i] < low;
                t[0]++;
                if (wasWet) t[1]++;
                if (isWet) t[2]++;
                if (!wasWet || isWet) continue;
                t[3]++;
                lostBy.TryGetValue(id, out int n);
                lostBy[id] = n + 1;
                if (Array.IndexOf(WetSections, sec) < 0) continue;
                string parent = pw.Defs.TryGetValue(id, out var d) && !string.IsNullOrEmpty(d.SplitFrom) ? RealIdOf(d.SplitFrom) : id;
                if (Array.IndexOf(BeachLostBy, parent) >= 0) beach++;
                else if (parent == BluffLostBy && x.Xs[c] >= BluffLostX0 && x.Xs[c] <= BluffLostX1 && x.Ys[r] >= BluffLostY0 && x.Ys[r] <= BluffLostY1) bluff++;
                else { other++; if (otherAt < 0) otherAt = i; }
            }
            bool everyWetSection = true;
            foreach (string sec in WetSections) everyWetSection &= tally.TryGetValue(sec, out var t) && t[1] > 0;
            k.Pass = pw.Problem == null && everyWetSection && other == 0 && beach <= BeachLostCells && bluff <= BluffLostCells;
            var bySection = new List<string>();
            foreach (var kv in tally)
            {
                bySection.Add(kv.Key + " " + kv.Value[0] + " cells, wet today " + kv.Value[1] + ", wet now " + kv.Value[2] + ", lost " + kv.Value[3] +
                              (Array.IndexOf(WetSections, kv.Key) < 0 ? " (not judged)" : ""));
                Put(k, kv.Key + ".cells", kv.Value[0]); Put(k, kv.Key + ".wet_today", kv.Value[1]);
                Put(k, kv.Key + ".wet_now", kv.Value[2]); Put(k, kv.Key + ".lost", kv.Value[3]);
            }
            var by = new List<string>();
            foreach (var kv in lostBy) by.Add(kv.Key + " " + kv.Value);
            k.Detail = "channel cells (within " + F(ChannelReach) + " m of the scene's toes, under " + F(ChannelUnder) + " m, in the flats' window) against the spring's " +
                       "low water (" + F(low) + " m): " + string.Join("; ", bySection) + ". Lost, by wall: " + (by.Count > 0 ? string.Join(", ", by) : "none") +
                       ". In the wet sections " + beach + " by name at the beach (042, 043; at most " + BeachLostCells + "), " + bluff + " by name at " + BluffLostBy +
                       " (at most " + BluffLostCells + "), " + other + " otherwise" + (other > 0 ? ", first" + x.At(otherAt) : "") +
                       (pw.Problem != null ? "; " + pw.Problem : "");
            Put(k, "lost_beach", beach); Put(k, "lost_bluff", bluff); Put(k, "lost_other", other);
            return k;
        }

        /// <summary>
        /// The flats' two windows (question 6): each walk's sill, the highest level that joins its ends over ground under +2 m and
        /// 0.5 m off every scene wall's footprint, 4-connected (part 2's bottleneck, exactly), stands within one R16 step of Phase
        /// B's measure on the committed map. Reported beside it, the hours the walk stays open (its sill under water by less than
        /// the wade) at a spring low and at a neap low.
        /// </summary>
        static TerrainPlanGuardCase FlatsWindows(Ctx x)
        {
            var k = New("FlatsWindowsAtTheirSills");
            var pw = x.Walls;
            var barred = pw.Footprints(x, WalkOffWalls);
            double mean = 0.5 * (x.In.SpringHigh + x.In.SpringLow), spring = 0.5 * (x.In.SpringHigh - x.In.SpringLow);
            double neap = spring * Num(HiddenHarbours.Core.GameConfig.DefaultNeapAmplitudeFraction), period = x.In.TidalPeriodHours;
            bool pass = pw.Problem == null && period > 0;
            var parts = new List<string>();
            foreach (var wk in FlatsWalks)
            {
                double sill = WidestPath(x, x.R.E, barred, wk, out int at);
                double off = Math.Abs(sill - wk.Sill);
                bool held = off <= x.Step * UnchangedCodes;
                pass &= held;
                double cut = sill + x.In.WadeDepth - mean;
                double openSpring = period - HoursAbove(cut, spring, period), openNeap = period - HoursAbove(cut, neap, period);
                parts.Add(wk.Name + ": sill " + F(sill) + " m" + x.At(at) + " against Phase B's " + F(wk.Sill) + " m, off by " + F(off * 1000) + " mm (" +
                          (held ? "within" : "MORE than") + " one R16 step); open " + F(openSpring) + " h of a spring low, " + F(openNeap) + " h of a neap");
                Put(k, wk.Name + ".sill_m", sill); Put(k, wk.Name + ".off_m", off);
                Put(k, wk.Name + ".spring_open_h", openSpring); Put(k, wk.Name + ".neap_open_h", openNeap);
            }
            k.Pass = pass;
            k.Detail = string.Join("; ", parts) + (pw.Problem != null ? "; " + pw.Problem : "");
            return k;
        }

        // ---- the rules the cases share ----------------------------------------------------------------------------------------------

        /// <summary>
        /// Part 1's pond rule (plan9_check): fill from the centre over ground under the surface + 5 cm, the outlet's own gully
        /// left out; the pond leaks when the fill reaches its window's edge or passes twice its (wobbled) radius.
        /// </summary>
        static bool PondHolds(Ctx x, int n, out string what)
        {
            var p = x.Plan.Ponds[n];
            var cc = Num(p.Centre);
            double ra = Num(p.Radii.x), rb = Num(p.Radii.y), R = Math.Max(ra, rb) * PondWindow + Num(p.Basin.x), surface = Num(p.Surface);
            double rot = Radians(Num(p.RotationDeg)), cr = Math.Cos(rot), sr = Math.Sin(rot);
            long seed = x.Plan.Seed + TerrainPlanRules.SeedPond + n;
            if (!x.G.Win(cc.X - R, cc.Y - R, cc.X + R, cc.Y + R, out int r0, out int r1, out int c0, out int c1))
            {
                what = p.Id + " is off the map";
                return false;
            }
            int w = c1 - c0, h = r1 - r0;
            var outlet = p.Outlet != null ? Catmull(Num(p.Outlet.Points), Num(x.Plan.LineStep)) : null;
            var allowed = new bool[w * h];
            int hint = 0;
            for (int r = r0; r < r1; r++)
            for (int c = c0; c < c1; c++)
            {
                bool gully = false;
                if (outlet != null)
                {
                    outlet.Query(x.Xs[c], x.Ys[r], ref hint, out double d, out _, out double u);
                    gully = d <= PondGully && u > PondGullyFrom;
                }
                allowed[(r - r0) * w + (c - c0)] = x.R.E[r * x.W + c] < surface + PondFillAbove && !gully;
            }
            int sr0 = (int)((x.G.Y1 - cc.Y) / x.G.Mpp) - r0, sc0 = (int)((cc.X - x.G.X0) / x.G.Mpp) - c0;
            var f = Flood4(allowed, w, h, sr0, sc0);
            int filled = 0;
            bool edge = false, beyond = false;
            double far = 0;
            for (int r = 0; r < h; r++)
            for (int c = 0; c < w; c++)
            {
                if (!f[r * w + c]) continue;
                filled++;
                if (r == 0 || r == h - 1 || c == 0 || c == w - 1) edge = true;
                double q = EllipseRadius(x.Xs[c0 + c], x.Ys[r0 + r], cc, ra, rb, cr, sr, seed, TerrainPlanRules.PondWobble, TerrainPlanRules.PondWobbleLambda);
                far = Math.Max(far, q);
                if (q > PondLeakRadius) beyond = true;
            }
            bool holds = !edge && !beyond;
            what = p.Id + " at +" + F(surface) + " m: " + filled + " cells fill, out to " + F(far) + " of its radius" +
                   (holds ? ", held" : edge ? ", LEAKS to its window's edge" : ", LEAKS past " + F(PondLeakRadius) + " of its radius");
            return holds;
        }

        /// <summary>
        /// A pond or pool by the derivation's flood rule: the level it laid (its record) against its spill, the lowest level at
        /// which water from its centre's cell reaches a leak (its window's edge, or past FloodLeak of its radius), by a minimax
        /// flood over the map. It holds when it recorded no leak and its spill stands at or over the level it laid.
        /// </summary>
        static bool StillHolds(Ctx x, TerrainPlanGuardCase k, PondDef p, out string what, out double laid)
        {
            laid = x.Number(p.Id + ".ground_level");
            bool leaked = x.R.Numbers.ContainsKey(p.Id + ".ground_leaks");
            long steps = x.R.Numbers.TryGetValue(p.Id + ".ground_steps_down", out var s) ? long.Parse(s, Inv) : 0;
            double spill = Spill(x, p, out int notch);
            bool ok = !leaked && !double.IsNaN(laid) && spill >= laid;
            what = p.Id + ": laid at " + F(laid) + " m (its surface " + F(Num(p.Surface)) + ", " + steps + " steps down" + (leaked ? ", LEAKS" : "") +
                   "); it spills at " + F(spill) + " m, its notch" + x.At(notch) + ", " + F(spill - laid) + " m over the level";
            Put(k, p.Id + ".laid_m", laid); Put(k, p.Id + ".spill_m", spill); Put(k, p.Id + ".margin_m", spill - laid); Put(k, p.Id + ".steps_down", steps);
            return ok;
        }

        /// <summary>
        /// A pond's spill on the map: the least, over the leak cells of its window, of the highest ground on the best path to it
        /// from its centre's cell (4-neighbour), and the cell that sets it (its notch).
        /// </summary>
        static double Spill(Ctx x, PondDef p, out int notch)
        {
            var cc = Num(p.Centre);
            double ra = Num(p.Radii.x), rb = Num(p.Radii.y), rot = Radians(Num(p.RotationDeg)), co = Math.Cos(rot), si = Math.Sin(rot);
            double R = Math.Max(ra, rb) * Num(x.Plan.FloodWindow) + Num(p.Basin.x), leakQ = Num(x.Plan.FloodLeak);
            if (!x.G.Win(cc.X - R, cc.Y - R, cc.X + R, cc.Y + R, out int r0, out int r1, out int c0, out int c1))
                throw new InvalidOperationException(p.Id + " is off the map");
            int w = c1 - c0, h = r1 - r0, sr = x.G.RowOf(cc.Y) - r0, sc = x.G.ColOf(cc.X) - c0;
            if (sr < 0 || sr >= h || sc < 0 || sc >= w) throw new InvalidOperationException(p.Id + "'s centre is off its window");
            var best = new double[w * h];
            var via = new int[w * h];
            var done = new bool[w * h];
            for (int j = 0; j < best.Length; j++) best[j] = double.PositiveInfinity;
            double E(int j) => x.R.E[(r0 + j / w) * x.W + c0 + j % w];
            var heap = new MinHeap();
            int s0 = sr * w + sc;
            best[s0] = E(s0);
            via[s0] = s0;
            heap.Push(best[s0], s0);
            while (heap.Count > 0)
            {
                heap.Pop(out double lv, out int j);
                if (done[j]) continue;
                done[j] = true;
                int r = j / w, c = j % w;
                if (r == 0 || r == h - 1 || c == 0 || c == w - 1 || EllipseRadius(x.Xs[c0 + c], x.Ys[r0 + r], cc, ra, rb, co, si, 0, 0.0, 0.0) > leakQ)
                {
                    int v = via[j];
                    notch = (r0 + v / w) * x.W + c0 + v % w;
                    return lv;
                }
                for (int d = 0; d < 4; d++)
                {
                    int nr = r + (d == 0 ? -1 : d == 1 ? 1 : 0), nc = c + (d == 2 ? -1 : d == 3 ? 1 : 0);
                    if (nr < 0 || nr >= h || nc < 0 || nc >= w) continue;
                    int n = nr * w + nc;
                    if (done[n]) continue;
                    double e = E(n), nl = Math.Max(lv, e);
                    if (!(nl < best[n])) continue;
                    best[n] = nl;
                    via[n] = e > lv ? n : via[j];
                    heap.Push(nl, n);
                }
            }
            notch = -1;
            return double.PositiveInfinity;
        }

        /// <summary>The size of the 4-connected patch of <paramref name="cells"/> that holds the first of them.</summary>
        static int Blob4(int[] cells, int w)
        {
            if (cells.Length == 0) return 0;
            var set = new HashSet<int>(cells);
            var seen = new HashSet<int> { cells[0] };
            var q = new Queue<int>();
            q.Enqueue(cells[0]);
            while (q.Count > 0)
            {
                int i = q.Dequeue(), c = i % w;
                foreach (int j in new[] { i - w, i + w, c > 0 ? i - 1 : -1, c < w - 1 ? i + 1 : -1 })
                    if (j >= 0 && set.Contains(j) && seen.Add(j)) q.Enqueue(j);
            }
            return seen.Count;
        }

        /// <summary>A point's distance to a line in ground metres (y over IsoGround.GroundDepthScale).</summary>
        static double GroundToLine(double X, double Y, PlanLine line)
        {
            double s = HiddenHarbours.Core.IsoGround.GroundDepthScale, py = Y / s, best = double.PositiveInfinity;
            for (int j = 0; j + 1 < line.R.Length; j++)
            {
                double ax = line.R[j].X, ay = line.R[j].Y / s, bx = line.R[j + 1].X, by = line.R[j + 1].Y / s;
                double dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
                double t = l2 == 0 ? 0 : Math.Max(0, Math.Min(1, ((X - ax) * dx + (py - ay) * dy) / l2));
                best = Math.Min(best, Hypot(X - ax - t * dx, py - ay - t * dy));
            }
            return best;
        }

        /// <summary>A V1 footprint or yard: its outline (a footprint's rectangle), and its name for the report.</summary>
        sealed class Lot
        {
            public string Name;
            public bool Footprint;
            public Vector4 Bounds;
            public Vector2[] Outline;
        }

        static List<Lot> Lots(VillagePlanDerivation.Result v)
        {
            var o = new List<Lot>();
            foreach (var b in v.Buildings ?? new VillagePlanDerivation.Building[0])
                o.Add(new Lot { Name = b.Id + "'s footprint", Footprint = true, Bounds = b.Footprint, Outline = VillageGeometry.Rectangle(b.Footprint) });
            foreach (var y in v.Yards ?? new VillagePlanDerivation.Yard[0])
                o.Add(new Lot { Name = y.Id + "'s yard", Outline = y.Outline });
            return o;
        }

        static Vector2 V(PlanPoint p) => new Vector2((float)p.X, (float)p.Y);

        /// <summary>A point's distance to a polygon in ground metres; 0 inside it.</summary>
        static double ToPolygon(Vector2[] poly, Vector2 p)
        {
            if (VillageGeometry.Contains(poly, p)) return 0;
            double d = double.PositiveInfinity;
            for (int j = 0; j < poly.Length; j++) d = Math.Min(d, VillageGeometry.PointSegment(p, poly[j], poly[(j + 1) % poly.Length]));
            return d;
        }

        /// <summary>A line's distance to a polygon in ground metres; 0 where it enters it.</summary>
        static double LineToPolygon(Vector2[] line, Vector2[] poly)
        {
            double d = double.PositiveInfinity;
            for (int j = 0; j < line.Length; j++)
            {
                if (VillageGeometry.Contains(poly, line[j])) return 0;
                if (j == 0) continue;
                for (int e = 0; e < poly.Length; e++)
                    d = Math.Min(d, VillageGeometry.SegmentDistance(line[j - 1], line[j], poly[e], poly[(e + 1) % poly.Length]));
            }
            return d;
        }

        static byte Zone(string name)
        {
            int z = TerrainPlanZones.IndexOf(name);
            if (z < 0) throw new InvalidOperationException("no ground zone is called '" + name + "'");
            return (byte)z;
        }

        static bool InGinnyRim(double X, double Y) => X >= GinnyRimX0 && X <= GinnyRimX1 && Y >= GinnyRimY0 && Y <= GinnyRimY1;

        const string ToeKey = "\n  _toePlan:", BrowKey = "\n  _browPlan:";

        /// <summary>
        /// The scene's cliff walls by real id (the last three digits of each CliffWall_ GameObject's name): each one's toe stations,
        /// its CliffWallSurface's _toePlan (world units). An id two walls share goes in <paramref name="twice"/>.
        /// </summary>
        static Dictionary<string, List<PlanPoint>> Toes(string scene, List<string> twice) => Lines(scene, ToeKey, twice);

        /// <summary>The scene's cliff walls by real id: each one's stations under <paramref name="key"/> (its CliffWallSurface's
        /// _toePlan or _browPlan, world units). An id two walls share goes in <paramref name="twice"/>.</summary>
        static Dictionary<string, List<PlanPoint>> Lines(string scene, string key, List<string> twice)
        {
            const string block = "\n--- !u!", owner = "\n  m_GameObject: {fileID: ", head = "\n--- !u!1 &";
            var byObject = new Dictionary<string, List<PlanPoint>>(StringComparer.Ordinal);
            for (int at = scene.IndexOf(key, StringComparison.Ordinal); at >= 0; at = scene.IndexOf(key, at + key.Length, StringComparison.Ordinal))
            {
                int start = Math.Max(0, scene.LastIndexOf(block, at, StringComparison.Ordinal));
                int o = scene.IndexOf(owner, start, at - start, StringComparison.Ordinal);
                if (o < 0) throw new InvalidOperationException("a " + key.Trim() + " has no GameObject");
                int oEnd = scene.IndexOf('}', o + owner.Length);
                string go = scene.Substring(o + owner.Length, oEnd - o - owner.Length).Trim();
                var pts = new List<PlanPoint>();
                for (int eol = scene.IndexOf('\n', at + key.Length); eol >= 0;)
                {
                    int next = scene.IndexOf('\n', eol + 1);
                    var m = StationLine.Match(scene.Substring(eol + 1, (next < 0 ? scene.Length : next) - eol - 1).TrimEnd('\r'));
                    if (!m.Success) break;
                    pts.Add(new PlanPoint(double.Parse(m.Groups[1].Value, NumberStyles.Float, Inv), double.Parse(m.Groups[2].Value, NumberStyles.Float, Inv)));
                    eol = next;
                }
                byObject[go] = pts;
            }
            var walls = new Dictionary<string, List<PlanPoint>>(StringComparer.Ordinal);
            for (int at = scene.IndexOf(head, StringComparison.Ordinal); at >= 0; at = scene.IndexOf(head, at + head.Length, StringComparison.Ordinal))
            {
                int idEnd = at + head.Length;
                while (idEnd < scene.Length && char.IsDigit(scene[idEnd])) idEnd++;
                if (!byObject.TryGetValue(scene.Substring(at + head.Length, idEnd - at - head.Length), out var pts)) continue;
                int end = scene.IndexOf(block, idEnd, StringComparison.Ordinal);
                var name = Regex.Match(scene.Substring(at, (end < 0 ? scene.Length : end) - at), @"\n  m_Name: ([^\r\n]*)");
                string n = name.Success ? name.Groups[1].Value.Trim() : "";
                if (!n.StartsWith("CliffWall_", StringComparison.Ordinal)) continue;
                string real = n.Substring(n.LastIndexOf('_') + 1);
                if (walls.ContainsKey(real)) twice.Add(real);
                else walls[real] = pts;
            }
            return walls;
        }

        static readonly Regex StationLine = new Regex(@"^  - \{x: ([^,]+), y: ([^}]+)\}$");

        /// <summary>
        /// The crossing's sill (p2_check.bottleneck): the highest level z at which the bar's root and the pass stay joined over
        /// ground at or above z, by halving; and where its cells lie (their mean, to 0.1 m). The level is the prototype's to
        /// the bit. Its "where" is not: there the mask is integers (~False is −1), so X[near] picks the window's first two rows
        /// and always gives the window's middle, (−200.8, 37.7). Here it is the mean of the near cells.
        /// </summary>
        static double Sill(Ctx x, double[] e, out string at)
        {
            if (!x.G.Win(CrossX0, CrossY0, CrossX1, CrossY1, out int r0, out int r1, out int c0, out int c1))
                throw new InvalidOperationException("the crossing's box is off the map");
            int w = c1 - c0, h = r1 - r0;
            int ar = (int)((x.G.Y1 - CrossA.Y) / x.G.Mpp) - r0, ac = (int)((CrossA.X - x.G.X0) / x.G.Mpp) - c0;
            int br = (int)((x.G.Y1 - CrossB.Y) / x.G.Mpp) - r0, bc = (int)((CrossB.X - x.G.X0) / x.G.Mpp) - c0;
            var allowed = new bool[w * h];
            double lo = SillFrom, hi = SillTo;
            for (int n = 0; n < SillHalvings; n++)
            {
                double z = 0.5 * (lo + hi);
                Allow(x, e, allowed, r0, c0, w, h, z);
                if (Flood4(allowed, w, h, ar, ac)[br * w + bc]) lo = z;
                else hi = z;
            }
            Allow(x, e, allowed, r0, c0, w, h, lo);
            var f = Flood4(allowed, w, h, ar, ac);
            double sx = 0, sy = 0;
            int near = 0;
            for (int r = 0; r < h; r++)
            for (int c = 0; c < w; c++)
            {
                if (!f[r * w + c] || !(e[(r0 + r) * x.W + c0 + c] < lo + SillNear)) continue;
                near++; sx += x.Xs[c0 + c]; sy += x.Ys[r0 + r];
            }
            at = near > 0 ? "(" + F(PyRound(sx / near, 1)) + ", " + F(PyRound(sy / near, 1)) + ")" : "nowhere";
            return lo;
        }

        static void Allow(Ctx x, double[] e, bool[] allowed, int r0, int c0, int w, int h, double z)
        {
            for (int r = 0; r < h; r++)
            for (int c = 0; c < w; c++)
            {
                double v = e[(r0 + r) * x.W + c0 + c];
                allowed[r * w + c] = v >= z && v <= SillCap;
            }
        }

        /// <summary>
        /// The prototype's flood_fill: from the seed and its four neighbours where allowed, over allowed cells, 4-neighbour,
        /// to stability; the window's edge bounds it.
        /// </summary>
        static bool[] Flood4(bool[] allowed, int w, int h, int sr, int sc)
        {
            var f = new bool[w * h];
            var q = new Queue<int>();
            void Try(int r, int c)
            {
                if (r < 0 || r >= h || c < 0 || c >= w) return;
                int i = r * w + c;
                if (!allowed[i] || f[i]) return;
                f[i] = true;
                q.Enqueue(i);
            }
            Try(sr, sc); Try(sr - 1, sc); Try(sr + 1, sc); Try(sr, sc - 1); Try(sr, sc + 1);
            while (q.Count > 0)
            {
                int i = q.Dequeue(), r = i / w, c = i % w;
                Try(r - 1, c); Try(r + 1, c); Try(r, c - 1); Try(r, c + 1);
            }
            return f;
        }

        static readonly int[] StepR = { 1, -1, 0, 0 }, StepC = { 0, 0, 1, -1 };

        /// <summary>
        /// A walk's sill over the flats (part 2's bottleneck, exactly: PR 5w's sill_exact): the highest level z at which its
        /// ends stay joined, 4-neighbour, over cells at or above z, under <see cref="WalkCap"/> and not barred; seeded at the
        /// first end and its four neighbours, as the prototype's flood. <paramref name="at"/> is the lowest cell on the widest
        /// path, the one that sets it; −1 (and −∞) when the ends do not join.
        /// </summary>
        static double WidestPath(Ctx x, double[] e, bool[] barred, FlatsWalk wk, out int at)
        {
            if (!x.G.Win(wk.X0, wk.Y0, wk.X1, wk.Y1, out int r0, out int r1, out int c0, out int c1))
                throw new InvalidOperationException(wk.Name + "'s box is off the map");
            int w = c1 - c0, h = r1 - r0;
            int ar = (int)((x.G.Y1 - wk.A.Y) / x.G.Mpp) - r0, ac = (int)((wk.A.X - x.G.X0) / x.G.Mpp) - c0;
            int br = (int)((x.G.Y1 - wk.B.Y) / x.G.Mpp) - r0, bc = (int)((wk.B.X - x.G.X0) / x.G.Mpp) - c0;
            if (ar < 0 || ar >= h || ac < 0 || ac >= w || br < 0 || br >= h || bc < 0 || bc >= w)
                throw new InvalidOperationException(wk.Name + "'s ends are not inside its box");
            var best = new double[w * h];
            var from = new int[w * h];
            for (int j = 0; j < best.Length; j++) { best[j] = double.NegativeInfinity; from[j] = -1; }
            bool Ok(int r, int c)
            {
                if (r < 0 || r >= h || c < 0 || c >= w) return false;
                int i = (r0 + r) * x.W + c0 + c;
                return !barred[i] && e[i] <= WalkCap;
            }
            double Level(int j) => e[(r0 + j / w) * x.W + c0 + j % w];
            var heap = new MinHeap();                                   // keyed by the level's negative: the highest first
            for (int s = -1; s < 4; s++)
            {
                int r = ar + (s < 0 ? 0 : StepR[s]), c = ac + (s < 0 ? 0 : StepC[s]);
                if (!Ok(r, c)) continue;
                int j = r * w + c;
                if (!(Level(j) > best[j])) continue;
                best[j] = Level(j);
                heap.Push(-best[j], j);
            }
            int goal = br * w + bc;
            while (heap.Count > 0)
            {
                heap.Pop(out double key, out int v);
                double lv = -key;
                if (lv < best[v]) continue;
                if (v == goal) break;
                int r = v / w, c = v % w;
                for (int s = 0; s < 4; s++)
                {
                    int rr = r + StepR[s], cc = c + StepC[s];
                    if (!Ok(rr, cc)) continue;
                    int j = rr * w + cc;
                    double nv = Math.Min(lv, Level(j));
                    if (!(nv > best[j])) continue;
                    best[j] = nv; from[j] = v;
                    heap.Push(-nv, j);
                }
            }
            at = -1;
            if (double.IsNegativeInfinity(best[goal])) return double.NegativeInfinity;
            int low = goal;
            for (int v = goal; v >= 0; v = from[v]) if (Level(v) < Level(low)) low = v;
            at = (r0 + low / w) * x.W + c0 + low % w;
            return best[goal];
        }

        /// <summary>How long in a period the tide stands over z (m about its mean) at an amplitude (plan9_check's hours_above).</summary>
        static double HoursAbove(double z, double amp, double period) =>
            z >= amp ? 0 : z <= -amp ? period : period * Math.Acos(z / amp) / Math.PI;

        /// <summary>A wall's real id: its Def id's last three digits.</summary>
        static string RealIdOf(string id) => id != null && id.Length >= 3 ? id.Substring(id.Length - 3) : "";

        /// <summary>Each component with the script's guid: its _heightMin and _heightMax, from the scene's text.</summary>
        static List<KeyValuePair<float, float>> Ranges(string scene, string guid)
        {
            var o = new List<KeyValuePair<float, float>>();
            if (string.IsNullOrEmpty(scene) || string.IsNullOrEmpty(guid)) return o;
            string key = "m_Script: {fileID: 11500000, guid: " + guid + ", type: 3}";
            for (int at = scene.IndexOf(key, StringComparison.Ordinal); at >= 0; at = scene.IndexOf(key, at + key.Length, StringComparison.Ordinal))
            {
                int start = scene.LastIndexOf("\n--- !u!", at, StringComparison.Ordinal);
                int end = scene.IndexOf("\n--- !u!", at, StringComparison.Ordinal);
                string block = scene.Substring(start < 0 ? 0 : start, (end < 0 ? scene.Length : end) - (start < 0 ? 0 : start));
                var lo = Regex.Match(block, @"\n\s*_heightMin: (\S+)");
                var hi = Regex.Match(block, @"\n\s*_heightMax: (\S+)");
                o.Add(new KeyValuePair<float, float>(lo.Success ? float.Parse(lo.Groups[1].Value, Inv) : float.NaN,
                                                     hi.Success ? float.Parse(hi.Groups[1].Value, Inv) : float.NaN));
            }
            return o;
        }

        static string Show(List<KeyValuePair<float, float>> ranges)
        {
            if (ranges.Count == 0) return "(no component)";
            var s = new List<string>();
            foreach (var r in ranges) s.Add(F(r.Key) + " to " + F(r.Value));
            return string.Join(" and ", s);
        }

        static bool Same(PlanPoint a, PlanPoint b) => Math.Abs(a.X - b.X) <= 1e-9 && Math.Abs(a.Y - b.Y) <= 1e-9;

        static TerrainPlanGuardCase New(string name) => new TerrainPlanGuardCase { Name = name };

        static void Put(TerrainPlanGuardCase k, string key, double v) => k.Numbers[key] = v.ToString("R", Inv);

        static void Put(TerrainPlanGuardCase k, string key, long v) => k.Numbers[key] = v.ToString(Inv);

        static string F(double v) => double.IsNaN(v) ? "none" : double.IsInfinity(v) ? (v > 0 ? "inf" : "-inf") : v.ToString("0.####", Inv);

        /// <summary>Cells against a reference: how many, how many off it by more than the allowance in R16 codes, the largest move.</summary>
        sealed class Tally
        {
            public int Cells, Off, MaxAt = -1;
            public double MaxAbs;

            /// <summary>Counts the cell; true when its code is off the reference's by more than <paramref name="allow"/>.</summary>
            public bool Add(int i, ushort[] codes, ushort[] refCodes, double[] e, double[] eRef, int allow)
            {
                Cells++;
                double d = Math.Abs(e[i] - eRef[i]);
                if (d > MaxAbs) { MaxAbs = d; MaxAt = i; }
                if (Math.Abs(codes[i] - refCodes[i]) <= allow) return false;
                Off++;
                return true;
            }
        }

        /// <summary>
        /// Held cells against today's ground: how many stand more than one R16 step off it, how many of those lie outside the named
        /// places, and the largest move outside them.
        /// </summary>
        sealed class HeldTally
        {
            public int Cells, Off, Outside, OutsideAt = -1, MaxAt = -1;
            public double MaxAbs;

            /// <summary>Counts the cell; true when it stands more than a step off today's ground.</summary>
            public bool Add(Ctx x, int i)
            {
                Cells++;
                int p = x.PlaceOf(i);
                double d = Math.Abs(x.R.E[i] - x.Base[i]);
                if (p < 0 && d > MaxAbs) { MaxAbs = d; MaxAt = i; }
                if (Math.Abs(x.CE[i] - x.CB[i]) <= UnchangedCodes) return false;
                Off++;
                if (p < 0) { Outside++; if (OutsideAt < 0) OutsideAt = i; }
                return true;
            }
        }

        /// <summary>
        /// The scene's walls after the patch (terrain PR 5w), by real id: each one's brow and toe stations (its CliffWallSurface's
        /// _browPlan and _toePlan), its Def and its section. The plan's toe sections list their live walls in run order, the
        /// sections in the coast's, so <see cref="Ids"/> runs as the patch's chunks do; a wall no section lists comes last, and
        /// is a problem. Each cell's distance to the nearest toe line out to <see cref="ToeReach"/>, with that wall (part 2's
        /// toe_field: the first in that order keeps a tie).
        /// </summary>
        sealed class PatchedWalls
        {
            public readonly List<string> Ids = new List<string>();
            public readonly Dictionary<string, List<PlanPoint>> Toes, Brows;
            public readonly Dictionary<string, CliffWallDef> Defs = new Dictionary<string, CliffWallDef>(StringComparer.Ordinal);
            public readonly Dictionary<string, string> Section = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly double[] ToeDistance;
            public readonly int[] NearestWall;

            /// <summary>Where the scene's walls and the sections' lists disagree, or null.</summary>
            public readonly string Problem;

            public PatchedWalls(Ctx x)
            {
                var twice = new List<string>();
                Toes = Lines(x.In.SceneText, ToeKey, twice);
                Brows = Lines(x.In.SceneText, BrowKey, new List<string>());
                var listedTwice = new List<string>();
                var missing = new List<string>();
                foreach (var s in x.Plan.Sections2)
                {
                    if (s == null || s.Mode != CoastSectionMode.Toe || s.Walls == null) continue;
                    foreach (var d in s.Walls)
                    {
                        if (d == null) continue;
                        if (Defs.ContainsKey(d.RealId)) { listedTwice.Add(d.RealId); continue; }
                        Defs[d.RealId] = d;
                        Section[d.RealId] = s.Id;
                        if (Toes.ContainsKey(d.RealId)) Ids.Add(d.RealId);
                        else missing.Add(d.RealId);
                    }
                }
                var unlisted = new List<string>();
                foreach (var id in Toes.Keys) if (!Defs.ContainsKey(id)) unlisted.Add(id);
                unlisted.Sort(StringComparer.Ordinal);
                Ids.AddRange(unlisted);
                var unpaired = new List<string>();
                foreach (var id in Ids) if (!Brows.TryGetValue(id, out var b) || b.Count != Toes[id].Count) unpaired.Add(id);
                var problems = new List<string>();
                if (Toes.Count == 0) problems.Add("the scene holds no walls");
                if (missing.Count > 0) problems.Add("listed walls the scene lacks: " + string.Join(", ", missing));
                if (unlisted.Count > 0) problems.Add("scene walls no toe section lists: " + string.Join(", ", unlisted));
                if (listedTwice.Count > 0) problems.Add("walls two sections list: " + string.Join(", ", listedTwice));
                if (twice.Count > 0) problems.Add("ids two scene walls share: " + string.Join(", ", twice));
                if (unpaired.Count > 0) problems.Add("walls whose brows and toes do not pair: " + string.Join(", ", unpaired));
                Problem = problems.Count > 0 ? string.Join("; ", problems) : null;

                ToeDistance = new double[x.N];
                NearestWall = new int[x.N];
                for (int i = 0; i < x.N; i++) { ToeDistance[i] = double.PositiveInfinity; NearestWall[i] = -1; }
                for (int n = 0; n < Ids.Count; n++)
                {
                    var toe = Toes[Ids[n]];
                    if (toe.Count == 0) continue;
                    double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
                    foreach (var p in toe) { x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); }
                    if (!x.G.Win(x0 - ToeReach, y0 - ToeReach, x1 + ToeReach, y1 + ToeReach, out int r0, out int r1, out int c0, out int c1)) continue;
                    for (int r = r0; r < r1; r++)
                    for (int c = c0; c < c1; c++)
                    {
                        double d = toe.Count == 1 ? Hypot(x.Xs[c] - toe[0].X, x.Ys[r] - toe[0].Y) : double.PositiveInfinity;
                        for (int j = 1; j < toe.Count; j++) d = Math.Min(d, Seg(x.Xs[c], x.Ys[r], toe[j - 1], toe[j]));
                        int i = r * x.W + c;
                        if (d <= ToeReach && d < ToeDistance[i]) { ToeDistance[i] = d; NearestWall[i] = n; }
                    }
                }
            }

            /// <summary>The cells inside a wall's footprint (its brow, then its toe back) or within <paramref name="off"/> of it
            /// (part 2's wall_mask).</summary>
            public bool[] Footprints(Ctx x, double off)
            {
                var m = x.Mask();
                foreach (var id in Ids)
                {
                    if (!Brows.TryGetValue(id, out var brow)) continue;
                    var poly = new List<PlanPoint>(brow);
                    var toe = Toes[id];
                    for (int j = toe.Count - 1; j >= 0; j--) poly.Add(toe[j]);
                    if (poly.Count < 3) continue;
                    double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
                    foreach (var p in poly) { x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); }
                    double pad = off + 1.0;
                    if (!x.G.Win(x0 - pad, y0 - pad, x1 + pad, y1 + pad, out int r0, out int r1, out int c0, out int c1)) continue;
                    for (int r = r0; r < r1; r++)
                    for (int c = c0; c < c1; c++)
                    {
                        int i = r * x.W + c;
                        if (m[i]) continue;
                        if (PointInPoly(x.Xs[c], x.Ys[r], poly) || PolyEdgeDist(x.Xs[c], x.Ys[r], poly) <= off) m[i] = true;
                    }
                }
                return m;
            }
        }

        /// <summary>A binary min-heap of (level, cell), for the minimax flood.</summary>
        sealed class MinHeap
        {
            double[] _k = new double[64];
            int[] _v = new int[64];
            public int Count;

            public void Push(double k, int v)
            {
                if (Count == _k.Length) { Array.Resize(ref _k, Count * 2); Array.Resize(ref _v, Count * 2); }
                int i = Count++;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (!(k < _k[p])) break;
                    _k[i] = _k[p]; _v[i] = _v[p]; i = p;
                }
                _k[i] = k; _v[i] = v;
            }

            public void Pop(out double k, out int v)
            {
                k = _k[0]; v = _v[0];
                double lk = _k[--Count];
                int lv = _v[Count], i = 0;
                while (true)
                {
                    int a = 2 * i + 1;
                    if (a >= Count) break;
                    if (a + 1 < Count && _k[a + 1] < _k[a]) a++;
                    if (!(_k[a] < lk)) break;
                    _k[i] = _k[a]; _v[i] = _v[a]; i = a;
                }
                _k[i] = lk; _v[i] = lv;
            }
        }

        /// <summary>One judging's reading of the input: the grid, the codes at the plan's range, the owners' kinds.</summary>
        sealed class Ctx
        {
            public readonly TerrainPlanGuardInput In;
            public readonly RegionTerrainPlanDef Plan;
            public readonly TerrainPlanSources Src;
            public readonly TerrainPlanResult R;
            public readonly TerrainPlanGrid G;
            public readonly int W, H, N;
            public readonly double[] Xs, Ys, Base;

            /// <summary>One R16 step at the plan's range (m).</summary>
            public readonly double Step;
            readonly float _min, _max;
            readonly bool[] _headOwner, _fallOwner;
            readonly string[] _ownerId;
            ushort[] _ce, _cb, _c1, _ci;

            public Ctx(TerrainPlanGuardInput input)
            {
                In = input ?? throw new ArgumentNullException(nameof(input));
                Plan = input.Plan ?? throw new ArgumentException("[TerrainPlanGuards] no plan");
                Src = input.Sources ?? throw new ArgumentException("[TerrainPlanGuards] no sources");
                R = input.Result ?? throw new ArgumentException("[TerrainPlanGuards] no derivation");
                if (input.Items == null || input.SceneText == null) throw new ArgumentException("[TerrainPlanGuards] no scene");
                if (double.IsNaN(input.SpringLow) || double.IsNaN(input.SpringHigh) || double.IsNaN(input.NavFloor) || double.IsNaN(input.WadeDepth) ||
                    double.IsNaN(input.TidalPeriodHours) || float.IsNaN(input.MapMin) || float.IsNaN(input.MapMax))
                    throw new ArgumentException("[TerrainPlanGuards] a required number is missing (the tide, its period, the mark's floor, the wade or the map's range)");
                G = R.Grid;
                W = G.W; H = G.H; N = G.Count;
                Base = Src.Base ?? throw new ArgumentException("[TerrainPlanGuards] the sources carry no today's ground");
                if (Base.Length != N || R.E == null || R.E.Length != N || R.E1 == null || R.E2 == null || R.Frozen == null || R.KeyOwner == null ||
                    R.FrozenWhy == null)
                    throw new ArgumentException("[TerrainPlanGuards] the derivation did not run to its end on this grid");
                Xs = new double[W]; Ys = new double[H];
                for (int c = 0; c < W; c++) Xs[c] = G.XG(c);
                for (int r = 0; r < H; r++) Ys[r] = G.YG(r);
                _min = Plan.HeightRange.x; _max = Plan.HeightRange.y;
                Step = (Num(Plan.HeightRange.y) - Num(Plan.HeightRange.x)) / TerrainPlanMaps.CodeCount;

                // an owner is named by its Def's id, then what of it ("form.stp_ne_head top")
                var head = new HashSet<string>(StringComparer.Ordinal);
                foreach (var f in Plan.Forms) if (f != null) head.Add(f.Id);
                foreach (var f in Plan.Ramps) if (f != null) head.Add(f.Id);
                foreach (var f in Plan.Platforms) if (f != null) head.Add(f.Id);
                foreach (var f in Plan.Bars) if (f != null) head.Add(f.Id);
                var fall = new HashSet<string>(StringComparer.Ordinal);
                foreach (var f in Plan.Falls) if (f != null) fall.Add(f.Id);
                int n = R.KeyOwners.Count;
                _headOwner = new bool[n]; _fallOwner = new bool[n]; _ownerId = new string[n];
                for (int k = 1; k < n; k++)
                {
                    string name = R.KeyOwners[k] ?? "";
                    int sp = name.IndexOf(' ');
                    _ownerId[k] = sp < 0 ? name : name.Substring(0, sp);
                    _headOwner[k] = head.Contains(_ownerId[k]);
                    _fallOwner[k] = fall.Contains(_ownerId[k]);
                }
                _ownerId[0] = "";
            }

            /// <summary>The ground file's import; a case that reads it fails without one.</summary>
            public GroundFileImport.Result Imp => Src.Import ?? throw new InvalidOperationException("the sources carry no ground file's import");

            /// <summary>The village's buildings and yards; a case that reads them fails without them.</summary>
            public VillagePlanDerivation.Result Village => In.Village ?? throw new InvalidOperationException("the input carries no village");

            PatchedWalls _walls;

            /// <summary>The scene's walls after the patch, read once for part 2's guards.</summary>
            public PatchedWalls Walls => _walls ?? (_walls = new PatchedWalls(this));

            /// <summary>The R16 codes of the final ground, today's, part 1's and pass 9's, at the plan's range (the map writer's rule).</summary>
            public ushort[] CE => _ce ?? (_ce = Codes(R.E));
            public ushort[] CB => _cb ?? (_cb = Codes(Base));
            public ushort[] C1 => _c1 ?? (_c1 = Codes(R.E1));
            public ushort[] CI => _ci ?? (_ci = Codes(Imp.Base));

            ushort[] Codes(double[] e)
            {
                var r01 = new float[e.Length];
                for (int i = 0; i < e.Length; i++) r01[i] = PaintedHeightField.EncodeElevation((float)e[i], _min, _max);
                return TerrainPlanMaps.Codes(r01);
            }

            /// <summary>A cell of the import's keep: the parts it holds at today's ground (amendment 1 §4.7).</summary>
            public bool Held(int i) => (R.FrozenWhy[i] & TerrainPlanResult.WhyHeldOnTheImport) != 0;

            /// <summary>The named place a cell's centre lies in, or −1.</summary>
            public int PlaceOf(int i)
            {
                double X = Xs[i % W], Y = Ys[i / W];
                for (int p = 0; p < NamedPlaces.Length; p++)
                {
                    var pl = NamedPlaces[p];
                    if (X >= pl.X0 - 1e-6 && X <= pl.X1 + 1e-6 && Y >= pl.Y0 - 1e-6 && Y <= pl.Y1 + 1e-6) return p;
                }
                return -1;
            }

            public bool InGinnyRim(int i) => TerrainPlanGuards.InGinnyRim(Xs[i % W], Ys[i / W]);

            public GroundFileImport.AskRecord AskRecord(string id) => Imp.Asks[AskIndex(id)];

            public int AskIndex(string id)
            {
                for (int k = 0; k < Imp.Asks.Count; k++) if (Imp.Asks[k].Id == id) return k;
                throw new InvalidOperationException("the import has no ask " + id);
            }

            public GroundAskDef AskDef(string id)
            {
                var g = In.Ground ?? throw new InvalidOperationException("the input names no ground file");
                foreach (var a in g.Asks) if (a != null && a.Id == id) return a;
                throw new InvalidOperationException("the ground file has no ask " + id);
            }

            /// <summary>A number the derivation recorded, or NaN.</summary>
            public double Number(string key) => R.Numbers.TryGetValue(key, out var s) ? double.Parse(s, NumberStyles.Float, Inv) : double.NaN;

            /// <summary>
            /// The import's ground before the game's asks (plan order): its pre-encode ground where none of them moved it, else
            /// the height just before the first that did.
            /// </summary>
            public double[] BeforeTheGameAsks()
            {
                var o = (double[])Imp.E.Clone();
                for (int k = Imp.Asks.Count - 1; k >= 0; k--)
                {
                    var a = Imp.Asks[k];
                    for (int j = 0; j < a.Changed.Length; j++) o[a.Changed[j]] = a.Before[j];
                }
                return o;
            }

            /// <summary>The cells and their eight neighbours.</summary>
            public bool[] Beside(int[] cells)
            {
                var m = Mask();
                foreach (int i in cells)
                {
                    int r = i / W, c = i % W;
                    for (int dr = -1; dr <= 1; dr++)
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        int rr = r + dr, cc = c + dc;
                        if (rr >= 0 && rr < H && cc >= 0 && cc < W) m[rr * W + cc] = true;
                    }
                }
                return m;
            }

            /// <summary>A cell's slope by amendment 2's rule (A2's fix2.py): central differences over the map, the north-south one over 0.5 × 1.556.</summary>
            public double Slope(int i)
            {
                int r = i / W, c = i % W;
                if (r < 1 || r >= H - 1 || c < 1 || c >= W - 1) return 0;
                double gx = (R.E[i + 1] - R.E[i - 1]) / (2 * G.Mpp);
                double gy = (R.E[i - W] - R.E[i + W]) / (2 * G.Mpp * HiddenHarbours.Core.IsoGround.GroundDepthScale);
                return Hypot(gx, gy);
            }

            public string OwnerId(int i) => _ownerId[R.KeyOwner[i]];
            public bool HeadOwned(int i) => _headOwner[R.KeyOwner[i]];
            public bool FallOwned(int i) => _fallOwner[R.KeyOwner[i]];

            /// <summary>A cell a Head piece owns and that stands off the crossing's ground: the Head's new ground.</summary>
            public bool HeadMoved(int i) => _headOwner[R.KeyOwner[i]] && Math.Abs(R.E[i] - R.E2[i]) > TerrainPlanRules.MovedBy;

            public bool[] Mask() => new bool[N];

            /// <summary>The scene's items under a root, as points (the root at the origin left out, as the sources' scan leaves it).</summary>
            public List<PlanPoint> ItemsOf(string root)
            {
                var o = new List<PlanPoint>();
                foreach (var it in In.Items) if (it.Root == root && !(it.X == 0 && it.Y == 0)) o.Add(new PlanPoint(it.X, it.Y));
                return o;
            }

            public void Disc(bool[] m, PlanPoint p, double radius)
            {
                if (!G.Win(p.X - radius, p.Y - radius, p.X + radius, p.Y + radius, out int r0, out int r1, out int c0, out int c1)) return;
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                    if (Hypot(Xs[c] - p.X, Ys[r] - p.Y) <= radius) m[r * W + c] = true;
            }

            public void Capsule(bool[] m, PlanPoint a, PlanPoint b, double halfWidth)
            {
                if (!G.Win(Math.Min(a.X, b.X) - halfWidth, Math.Min(a.Y, b.Y) - halfWidth, Math.Max(a.X, b.X) + halfWidth, Math.Max(a.Y, b.Y) + halfWidth,
                           out int r0, out int r1, out int c0, out int c1)) return;
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                    if (Seg(Xs[c], Ys[r], a, b) <= halfWidth) m[r * W + c] = true;
            }

            /// <summary>The cells within <paramref name="buffer"/> of a box.</summary>
            public void Rect(bool[] m, PlanPoint lo, PlanPoint hi, double buffer)
            {
                if (!G.Win(lo.X - buffer, lo.Y - buffer, hi.X + buffer, hi.Y + buffer, out int r0, out int r1, out int c0, out int c1)) return;
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    double dx = Math.Max(Math.Max(lo.X - Xs[c], 0), Xs[c] - hi.X), dy = Math.Max(Math.Max(lo.Y - Ys[r], 0), Ys[r] - hi.Y);
                    if (Hypot(dx, dy) <= buffer) m[r * W + c] = true;
                }
            }

            /// <summary>The cells within <paramref name="halfWidth"/> of a line.</summary>
            public void Along(bool[] m, PlanLine line, double halfWidth)
            {
                if (!G.Win(line.MinX - halfWidth, line.MinY - halfWidth, line.MaxX + halfWidth, line.MaxY + halfWidth, out int r0, out int r1, out int c0, out int c1))
                    return;
                int hint = 0;
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    line.Query(Xs[c], Ys[r], ref hint, out double d, out _, out _);
                    if (d <= halfWidth) m[r * W + c] = true;
                }
            }

            public string At(int i) => i < 0 ? "" : " at (" + F(Xs[i % W]) + ", " + F(Ys[i / W]) + ")";
        }
    }
}
