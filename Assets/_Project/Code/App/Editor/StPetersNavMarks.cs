#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;    // GameConfig — the neap fraction, not a re-typed 0.45
using HiddenHarbours.World;   // NavChannel / NavMarkPlan

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE MARKS OFF ST PETERS</b> — the way in through the reef, and the gap in the bar you walk
    /// to Nine Mile Creek over. Authored DATA plus a <see cref="Place"/> that places and does not draw.
    ///
    /// <para><b>⭐ THE ONE DOOR IN THE REEF IS THE WHOLE STORY HERE.</b> The island is ringed by a
    /// −1.0/−1.5 m apron with exactly one dredged slip through it (the §5.1a ruling), and that slip is
    /// the reason a boat can come home at all. So the entrance channel is not decoration: it is the
    /// buoyed line onto the only door, and its declared claim — the skiff tier, 0.6 m, at mean water —
    /// is the ruled reef gate written down where a test can check it.</para>
    ///
    /// <para><b>⚠ THE REEF STILL BARES — THE WAY IN NO LONGER DOES.</b> Spring low here is −2.2 m and
    /// the apron is −1.0 to −1.5 m, so the ring itself stands out of the water twice a month and
    /// nothing floats on it. What changed on 2026-08-19 is the door through it: the owner ruled the
    /// east dock holds water at every tide, and
    /// <see cref="StPetersBuilder.ApproachFrom"/>→<see cref="StPetersBuilder.ApproachTo"/> is the
    /// dredged channel that makes that true — 1.80 m of it at the LOWEST water, all the way to the
    /// wharf. So the entrance is buoyed in to the dock rather than stopped at the drop-off, and the
    /// perch/withy art ask is no longer load-bearing here (the beach slip at
    /// <see cref="StPetersBuilder.BerthTo"/> still dries, but nothing is asked to float on it).</para>
    ///
    /// <para><b>⚠ NO MOORING BUOYS IN v1.</b> This island moors one dory, and she lies alongside the
    /// wharf (<see cref="StPetersBuilder.DoryMooredPos"/>) rather than on a mooring. There is no
    /// mooring field here to mark, so the kit's <c>Mooring</c> type stays unplaced. (Her berth no
    /// longer dries, so the reason is now simply that there is no field — not that a buoy would sit
    /// on mud.)</para>
    ///
    /// <para><b>IALA Region B</b> — red to starboard returning from seaward. The direction of buoyage
    /// is the WAYPOINT ORDER; see <see cref="NavChannelGeometry.IsRed"/>.</para>
    /// </summary>
    public static class StPetersNavMarks
    {
        /// <summary>The root every mark hangs under.</summary>
        public const string RootName = "StPetersNavMarks";

        /// <summary>Where the mark defs live. One entity per file (rule 2).</summary>
        public const string DefFolder = "Assets/_Project/Data/NavBuoys";

        /// <summary>The one prefab a placement instantiates.</summary>
        public const string PrefabPath = "Assets/_Project/Prefabs/Decor/NavBuoy.prefab";

        // =================================================================================================
        //  THE TUNING
        // =================================================================================================

        /// <summary>
        /// Spacing and floors for this region (rule 6 — the knobs, all in one place).
        ///
        /// <para>The straight spacing is TIGHTER than Nine Mile Creek's: this approach crosses open
        /// water with no breakwater to read off, so the run between the landfall pair and the reef
        /// wants a mark in it. The floor is the same 0.6 m — it is a statement about what "afloat"
        /// looks like, not about a region.</para>
        /// </summary>
        public static NavMarkTuning Tuning => new NavMarkTuning
        {
            MinDepthAtSpringLowMetres = 0.6f,
            StraightSpacingMetres = 55f,
            TurnThresholdDegrees = 20f,
            SnapStepMetres = 2f,
            MaxSnapSlewMetres = 8f,
            FairwayProbeStepMetres = 4f,
        };

        // =================================================================================================
        //  THE CHANNELS
        // =================================================================================================

        /// <summary>
        /// <b>The entrance</b> — in from the open bay, round onto the dredged channel's line, and up
        /// to the dock. Both ends are READ from the region's plan rather than re-typed, so re-siting
        /// the dock or re-cutting the channel re-routes the fairway that serves them.
        ///
        /// <para>⭐ The claim is the DREDGE's, and it is a much stronger statement than the one this
        /// route used to make. It was <b>0.6 m at MEAN water</b> — the skiff tier, over a slip that
        /// bared twice a month. It is now <b>the arrival hull's 1.40 m at SPRING LOW</b>: the worst
        /// water this region ever has, carrying the biggest hull that comes home. The margin is thin
        /// on purpose — the open bay itself is only −4 m, so 1.80 m at spring low is the CEILING any
        /// route here could claim, and the declared 0.20 m clearance is the kit's standard rather
        /// than the 0.40 m the cut was dredged to (a route may not promise the whole of its own
        /// margin). EveryChannelCarriesTheDraughtItClaims walks every metre of it.</para>
        ///
        /// <para>⚠ The inner end is the DOCK, not <see cref="StPetersBuilder.BerthTo"/>. The beach
        /// slip still dries near spring low and a fairway may not be marked over it at a tide it
        /// does not hold; the dredged pocket alongside the wharf is where a hull that draws anything
        /// actually stops.</para>
        /// </summary>
        public static NavChannel Entrance => new NavChannel
        {
            Id = "channel.stpeters_entrance",
            DisplayName = "St Peters entrance",
            // ⭐ SEAWARD → HARBOUR. Reverse this list and every buoy on it changes hands.
            Waypoints = new[]
            {
                new Vector2(340f, 40f),          // the landfall, out in the −4 m bay
                new Vector2(262f, 26f),          // the turn onto the approach
                StPetersBuilder.ApproachFrom,    // square onto the channel's line, ON the −4 m contour
                new Vector2(StPetersBuilder.DockZonePos.x,
                            StPetersBuilder.DockZonePos.y),  // the dock — derived, never typed
            },
            HalfWidthMetres = 10f,               // the cut is 8 m each side; the marked fairway is a
                                                 // touch wider so a mark is not IN the cut
            SearchHalfWidthMetres = 14f,
            SizeId = "s12",                      // 1.2 m, the harbour rung — this is an island slip
            LitLandfall = true,
            DeclaredDraughtMetres = StPetersBuilder.ArrivalHullDraughtMetres,  // the cape islander
            NavigableTideFraction = -1f,         // …at SPRING LOW — the dredge's whole point
            KeelClearanceMetres = 0.2f,          // the kit's standard, NOT the 0.40 m dredged margin
        };

        /// <summary>
        /// <b>The bar gut</b> — the gap in the sandbar that the crossing to Nine Mile Creek runs over.
        /// Marking its two mouths is the neap gap made visible: the bar itself is the pacing lever, and
        /// this is the one place through it.
        ///
        /// <para>⚠ The gut bed is −0.6 m and bares by 1.6 m at spring low, so nothing floats inside it.
        /// The route runs from deep water south of the bar to deep water north of it and the pairs land
        /// at the mouths. The claim is the dory at NEAP HIGH water — the honest statement of what this
        /// gap is.</para>
        ///
        /// <para><b>The seaward end is the SOUTH one</b>, declared rather than derived, matching Nine
        /// Mile Creek's gut so the two ends of one crossing are buoyed on one convention.</para>
        /// </summary>
        public static NavChannel BarGut
        {
            get
            {
                Vector2 centre = GutCentre();
                Vector2 across = GutAxis();
                return new NavChannel
                {
                    Id = "channel.stpeters_bar_gut",
                    DisplayName = "The bar gut (crossing to Nine Mile Creek)",
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
                    SizeId = "s12",
                    LitLandfall = false,
                    DeclaredDraughtMetres = 0.30f,   // the dory
                    NavigableTideFraction = GameConfig.DefaultNeapAmplitudeFraction,  // …at neap high
                    KeelClearanceMetres = 0.15f,
                };
            }
        }

        /// <summary>How far either side of the bar the gut's route reaches, to get its mouths into
        /// water that does not bare.</summary>
        public const float GutApproachMetres = 45f;

        /// <summary>Where the gut cuts the bar — read off the region's own bar plan, never re-typed.</summary>
        public static Vector2 GutCentre() =>
            Vector2.Lerp(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo, StPetersBuilder.ChannelAlong);

        /// <summary>The unit vector ACROSS the bar — the way a boat transits the gut. Derived from the
        /// bar's own end points, and forced NORTHWARD so the declared direction of buoyage survives a
        /// future re-authoring that flips the bar's own direction.</summary>
        public static Vector2 GutAxis()
        {
            Vector2 along = (StPetersBuilder.SandbarTo - StPetersBuilder.SandbarFrom).normalized;
            Vector2 across = NavChannelGeometry.PortNormal(along);
            return across.y >= 0f ? across : -across;
        }

        /// <summary>Every channel this region buoys, seaward end first in each.</summary>
        public static IReadOnlyList<NavChannel> Channels => new[] { Entrance, BarGut };

        // =================================================================================================
        //  THE DANGERS
        // =================================================================================================

        /// <summary>
        /// <b>ONE cardinal, on the island's north shoulder — and the reason there is only one is the
        /// nicest thing this region's coast plan says.</b>
        ///
        /// <para>The danger worth marking here is the reef apron, and the apron only exists where the
        /// coast is soft. Read <see cref="StPetersBuilder.CoastSectors"/>: bearings 0–76° are
        /// <c>Beach</c> and <c>Dune</c> — an intertidal shore with a shelf a hull can find. Due south
        /// (172–205°) is <c>Cliff</c>, "the tallest, plainest wall, with its foot below the lowest
        /// water", and the west face is <c>DeepShoreCliff</c> that "falls straight to the harbour
        /// floor". <b>You can lie alongside the south of this island at any state of tide.</b> There is
        /// no shoal there, so there is nothing to guard, so no mark goes there.</para>
        ///
        /// <para>⚠ <b>This was learned the honest way, and it is why the probe exists.</b> The first
        /// draft authored a matching South cardinal off the island's other shoulder — symmetry, not
        /// geography — and <see cref="NavMarkPlan"/> REFUSED it: 24 m either side of it the bed reads
        /// −4.00 m and −4.00 m, because a cliff has no shelf to have a gradient across. A quadrant
        /// typed beside a coordinate would have shipped it.</para>
        ///
        /// <para>The position is derived from the island's own ellipse rather than typed, so the
        /// 2026-07-30-style re-ruling of the island's size moves the mark with it instead of leaving it
        /// stranded offshore.</para>
        /// </summary>
        public static IReadOnlyList<NavCardinal> Cardinals => new[]
        {
            new NavCardinal
            {
                Id = "mark.stpeters_reef_north",
                DisplayName = "St Peters reef, north side",
                Quadrant = NavCardinalQuadrant.North,
                At = StPetersBuilder.IslandCenter + Vector2.up * ReefStandOffMetres(),
                SizeId = "s12",
                ProbeMetres = 24f,
            },
        };

        /// <summary>
        /// How far north/south of the island's centre a cardinal has to stand to be clear of the reef
        /// and afloat at every tide — DERIVED from the island's own profile.
        ///
        /// <para>The apron ends <c>IslandRadius + IslandFalloff + ReefShelfWidth</c> out in ELLIPTICAL
        /// distance and the drop-off runs another <c>IslandFalloff</c> beyond it; on the N–S axis that
        /// distance is compressed by <c>IslandRadiusY / IslandRadius</c>, because the island is an
        /// ellipse and not a disc. Getting that ratio wrong is how a mark ends up 90 m out in open
        /// water guarding nothing.</para>
        /// </summary>
        public static float ReefStandOffMetres()
        {
            float ellipticalReach = StPetersBuilder.IslandRadius
                                  + StPetersBuilder.IslandFalloff
                                  + StPetersBuilder.ReefShelfWidth
                                  + StPetersBuilder.IslandFalloff;   // the drop-off's own width
            float northSouthScale = StPetersBuilder.IslandRadiusY / StPetersBuilder.IslandRadius;
            return ellipticalReach * northSouthScale;
        }

        // =================================================================================================
        //  THE PLACEMENT
        // =================================================================================================

        /// <summary>
        /// The plan for this region against a terrain — the seam an EditMode test reads.
        ///
        /// <para>⚠ It takes the <see cref="ITidalTerrain"/> INTERFACE, so whatever the region ends up
        /// standing on — the analytic island today, a <c>PaintedTidalTerrain</c> the day the owner
        /// adopts his paint — is what the marks are derived from. Binding to the concrete class is how
        /// "paint = sail" quietly becomes "paint = sail, except the buoys".</para>
        /// </summary>
        public static NavMarkPlanResult Plan(ITidalTerrain terrain) =>
            terrain == null
                ? new NavMarkPlanResult()
                : NavMarkPlan.Plan(Channels, Cardinals, terrain.ElevationAt,
                                   StPetersBuilder.TideMean, StPetersBuilder.TideAmplitude, Tuning);

        /// <summary>
        /// Stand the marks up. Returns how many were placed. Null-tolerant throughout: a missing def
        /// or prefab warns and the rest of the region is unaffected.
        /// </summary>
        public static int Place(ITidalTerrain terrain) =>
            NavMarkPlacer.Place(RootName, DefFolder, PrefabPath, Plan(terrain),
                                "StPetersNavMarks",
                                StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude);
    }
}
#endif
