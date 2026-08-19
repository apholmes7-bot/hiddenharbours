#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;    // GameConfig / ITidalTerrain
using HiddenHarbours.World;   // NavChannel / NavMarkPlan

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE MARKS OFF NINE MILE CREEK</b> — the channels a skipper reads coming home, as authored
    /// DATA plus a <see cref="Place"/> that places and does not draw.
    ///
    /// <para><b>⭐ THE ROUTES ARE PROPOSALS FOR THE OWNER'S SAIL.</b> Nothing here is surveyed. The
    /// polylines below are where the geography says a fairway ought to run — off the reference
    /// photographs and the harbour's own shape — and <see cref="NavMarkPlan"/> then makes the SEABED
    /// decide: each station slides to the deepest water in its corridor, and any mark that would stand
    /// on ground that bares is REFUSED rather than nudged. Sail them and move the waypoints; every
    /// mark on a route follows it.</para>
    ///
    /// <para><b>⚠ THIS HARBOUR DRIES, AND THAT IS WHY THERE ARE NO BUOYS IN IT.</b> The ruled gate is
    /// a shoal at <see cref="NineMileCreekMainland.BasinBedElevation"/> (−1.6 m) under a ±2.2 m tide,
    /// so the basin bares by 0.6 m at spring low — the region's own note says a working wharf on this
    /// coast should. A floating mark cannot stand there. So the laterals mark the APPROACH, which is
    /// where a skipper needs them anyway, and the plan reports every refusal on every build. If the
    /// owner wants the inner channel marked, the answer is a perch or a withy — a mark on a POLE, in
    /// ground that dries — and that is an art ask, not something to improvise out of a floating can.</para>
    ///
    /// <para><b>⚠ NO MOORING BUOYS IN v1, deliberately.</b> The handoff says "moorings where the wharf
    /// plan already says moorings", and the wharf plan says <i>berths</i>: fourteen of them alongside
    /// the north wall (<see cref="NineMileCreekMainland.BerthPos"/>), plus a float run. Nowhere in this
    /// region does a boat swing to a mooring, so there is nothing here to mark. The kit's
    /// <c>Mooring</c> type stays unplaced until a mooring field exists to place it on.</para>
    ///
    /// <para><b>IALA Region B</b> — this is Canada. Returning from seaward, red to starboard and green
    /// to port. The direction of buoyage is the WAYPOINT ORDER (index 0 is the seaward end), so every
    /// colour below is arithmetic rather than a typed choice; see
    /// <see cref="NavChannelGeometry.IsRed"/>.</para>
    /// </summary>
    public static class NineMileCreekNavMarks
    {
        /// <summary>The root every mark hangs under.</summary>
        public const string RootName = "NineMileCreekNavMarks";

        /// <summary>Where the mark defs live. One entity per file (rule 2).</summary>
        public const string DefFolder = "Assets/_Project/Data/NavBuoys";

        /// <summary>The one prefab a placement instantiates — root samples the wave field, child bobs.</summary>
        public const string PrefabPath = "Assets/_Project/Prefabs/Decor/NavBuoy.prefab";

        // =================================================================================================
        //  THE TUNING
        // =================================================================================================

        /// <summary>
        /// Spacing and floors for this region. Every number the scheme uses lives here (rule 6).
        ///
        /// <para>The floor is <b>0.6 m at spring low</b>: enough water that the mark is unmistakably
        /// afloat at the worst hour of the month, and no more — raising it would push the whole scheme
        /// out into the bay where it marks nothing.</para>
        /// </summary>
        public static NavMarkTuning Tuning => new NavMarkTuning
        {
            MinDepthAtSpringLowMetres = 0.6f,
            StraightSpacingMetres = 70f,
            TurnThresholdDegrees = 20f,
            SnapStepMetres = 2f,
            MaxSnapSlewMetres = 8f,
            FairwayProbeStepMetres = 4f,
        };

        // =================================================================================================
        //  THE CHANNELS
        // =================================================================================================

        /// <summary>
        /// <b>The entrance</b> — in from the deep bay, round the crib breakwater's head, and into the
        /// basin. This is the fleet's own approach: the sixteen boats in the photographs come home this
        /// way, so the channel is not invented, it is the line they already sail.
        ///
        /// <para>The claim is the region's ruled gate, stated as a claim so a test can check it: the
        /// <b>lobster boat</b> (1.30 m, the owner's stated ceiling for the starter world) at MEAN water.
        /// The basin bed is −1.6 m, so the fairway carries her with 0.30 m under the keel — tight, which
        /// is what a 1.6 m gate MEANS. Raise the shoal and the content test fails by name.</para>
        /// </summary>
        public static NavChannel Entrance => new NavChannel
        {
            Id = "channel.nmc_entrance",
            DisplayName = "Nine Mile Creek entrance",
            // ⭐ SEAWARD → HARBOUR. Reverse this list and every buoy on it changes hands.
            Waypoints = new[]
            {
                new Vector2(274f, -20f),   // the landfall, out in the −6 m bay
                new Vector2(216f,  20f),   // the turn, clear east of the breakwater head
                new Vector2(204f,  50f),   // the entrance, abeam the head
                new Vector2(182f,  62f),   // into the basin — inside here it dries; see the class note
            },
            HalfWidthMetres = 12f,
            SearchHalfWidthMetres = 18f,
            SizeId = "s18",                // 1.75 m, the kit's working default: a bay approach, not a creek
            LitLandfall = true,            // only the outermost pair — a lit mark every 40 m is a runway
            DeclaredDraughtMetres = 1.30f, // the lobster boat, the ruled ceiling
            NavigableTideFraction = 0f,    // …at MEAN water. A fraction, never a metre (the 2026-08-01 lesson)
            KeelClearanceMetres = 0.2f,
        };

        /// <summary>
        /// <b>The bar gut</b> — the gap in the tidal bar that carries the crossing to St Peters. The
        /// bar itself is the region's pacing lever; the gut is the one place through it, and marking
        /// its two mouths is the neap gap made visible.
        ///
        /// <para>⚠ The gut BED is −0.6 m and bares by 1.6 m at spring low, so nothing floats inside it.
        /// The route therefore runs from deep water south of the bar to deep water north of it, and the
        /// pairs land at the two mouths where a skipper actually needs to find the gap. The claim is
        /// the dory (0.30 m) at neap high water — which is the honest statement of what this gut is: a
        /// small-boat gap open around high water, not a channel.</para>
        ///
        /// <para><b>The seaward end is the SOUTH one</b>, and it is a declaration rather than a
        /// derivation: the bar has open bay on both sides and neither is "the sea". Declaring it in the
        /// data is what the handoff asks for, and it puts both regions' guts on the same convention.</para>
        /// </summary>
        public static NavChannel BarGut
        {
            get
            {
                Vector2 centre = GutCentre();
                Vector2 across = GutAxis();
                return new NavChannel
                {
                    Id = "channel.nmc_bar_gut",
                    DisplayName = "The bar gut (crossing to St Peters)",
                    Waypoints = new[]
                    {
                        centre - across * GutApproachMetres,   // seaward: south of the bar
                        centre + across * GutApproachMetres,   // harbour: north of the bar
                    },
                    HalfWidthMetres = 12f,
                    SearchHalfWidthMetres = 10f,
                    // ⚠ NO singles: one pair at each mouth and nothing between. The gut's middle
                    // bares by 1.6 m at spring low, so a mid-straight station could only ever be
                    // REFUSED — every build, forever — and a permanent refusal teaches the owner to
                    // skim the refusal list, which is exactly where the real findings live.
                    StraightSpacingMetres = 1000f,
                    SizeId = "s12",                 // 1.2 m, the harbour size — this is a small-boat gap
                    LitLandfall = false,
                    DeclaredDraughtMetres = 0.30f,  // the dory
                    NavigableTideFraction = GameConfig.DefaultNeapAmplitudeFraction,  // …at NEAP HIGH
                    KeelClearanceMetres = 0.15f,
                };
            }
        }

        /// <summary>How far either side of the bar the gut's route reaches, to get its mouths into water
        /// that does not bare. Derived reach, one number, tunable.</summary>
        public const float GutApproachMetres = 45f;

        /// <summary>Where the gut cuts the bar — read off the region's own bar plan, never re-typed.</summary>
        public static Vector2 GutCentre() =>
            Vector2.Lerp(NineMileCreekMainland.BarFrom, NineMileCreekMainland.BarTo,
                         NineMileCreekMainland.GutAlong);

        /// <summary>The unit vector ACROSS the bar (its left normal) — the way a boat transits the gut.
        /// Derived from the bar's own end points, so re-siting the bar turns the gut with it.</summary>
        public static Vector2 GutAxis()
        {
            Vector2 along = (NineMileCreekMainland.BarTo - NineMileCreekMainland.BarFrom).normalized;
            Vector2 across = NavChannelGeometry.PortNormal(along);
            // The bar runs roughly ESE, so its left normal points NNE — the harbour side. Guard the
            // sign anyway: if a future re-authoring flips the bar's direction, the gut must still run
            // south → north, because that is what the data DECLARES.
            return across.y >= 0f ? across : -across;
        }

        /// <summary>Every channel this region buoys, seaward end first in each.</summary>
        public static IReadOnlyList<NavChannel> Channels => new[] { Entrance, BarGut };

        // =================================================================================================
        //  THE DANGERS
        // =================================================================================================

        /// <summary>
        /// The cardinals, and they are two because the harbour has two corners a stranger can hurt
        /// herself on — both of them the same danger, the shoal the wharf is built out onto.
        ///
        /// <para><b>Nothing here is trusted.</b> <see cref="NavMarkPlan"/> probes the seabed either
        /// side of each mark and REFUSES any whose safe quadrant is not actually the deeper one, so a
        /// quadrant cannot survive a re-site of the thing it guards.</para>
        /// </summary>
        public static IReadOnlyList<NavCardinal> Cardinals => new[]
        {
            // The crib breakwater's head. You round it to the EAST — inside it is the arm itself,
            // standing 3.4 m proud and dry at every tide.
            new NavCardinal
            {
                Id = "mark.nmc_breakwater_head",
                DisplayName = "Breakwater head",
                Quadrant = NavCardinalQuadrant.East,
                At = new Vector2(206f, 38f),
                SizeId = "s12",
                ProbeMetres = 24f,
            },
            // The harbour shoal's north-east corner, for anyone coming down the coast from the north.
            new NavCardinal
            {
                Id = "mark.nmc_shoal_northeast",
                DisplayName = "Harbour shoal, north-east corner",
                Quadrant = NavCardinalQuadrant.North,
                At = new Vector2(200f, 104f),
                SizeId = "s12",
                ProbeMetres = 24f,
            },
        };

        // =================================================================================================
        //  THE PLACEMENT
        // =================================================================================================

        /// <summary>
        /// The plan for this region against a terrain — the seam an EditMode test reads.
        ///
        /// <para>⚠ It takes the <see cref="ITidalTerrain"/> INTERFACE, not the
        /// mainland class, so whatever elevation source the region ends up standing on — the analytic
        /// coast today, a <c>PaintedTidalTerrain</c> the day the owner adopts his paint — is the one
        /// the marks are derived from. Binding to the concrete class is how "paint = sail" quietly
        /// becomes "paint = sail, except the buoys".</para>
        /// </summary>
        public static NavMarkPlanResult Plan(ITidalTerrain terrain) =>
            terrain == null
                ? new NavMarkPlanResult()
                : NavMarkPlan.Plan(Channels, Cardinals, terrain.ElevationAt,
                                   NineMileCreekMainland.TideMean, NineMileCreekMainland.TideAmplitude,
                                   Tuning);

        /// <summary>
        /// Stand the marks up. Returns how many were placed.
        ///
        /// <para><b>⭐ IT PLACES; IT DOES NOT DECIDE.</b> Every position, type, size and facing comes
        /// from <see cref="NavMarkPlan"/> against the terrain the scene actually carries — the same
        /// <c>ElevationAt</c> the boat's depth gate reads — so "paint = sail" (ADR 0014) extends to
        /// "paint = sail = marked". Null-tolerant throughout: a missing def or prefab warns and the
        /// rest of the marks still go in.</para>
        /// </summary>
        public static int Place(ITidalTerrain terrain)
        {
            return NavMarkPlacer.Place(RootName, DefFolder, PrefabPath, Plan(terrain),
                                       "NineMileCreekNavMarks", NineMileCreekMainland.SpringLowWater);
        }
    }
}
#endif
