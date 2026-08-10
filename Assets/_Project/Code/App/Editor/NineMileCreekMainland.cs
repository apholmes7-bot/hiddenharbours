#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The authored geography of Nine Mile Creek — the MAINLAND.</b> One place where the region's
    /// rectangle, its tide, its coastline, its coast plan, the mainland half of the tidal crossing, the
    /// dredged basin, the ponds, the wharf's fill, the roads, the town lots and the landmark sites are
    /// written down, so the builder that pushes them into the scene and the EditMode tests that check
    /// them read the SAME numbers (the <c>StPetersBuilder</c> convention: a single source of truth, not a
    /// builder with a comment and a test with a literal).
    ///
    /// <para><b>PHASE A of two.</b> This is the GEOGRAPHY: terrain, coast, the crossing, roads, lots and
    /// markers. Phase B dresses the wharf and the town from the ISO wharf kit / wharf decor / utility
    /// pack once those rigs have baked sheets. Everything Phase B needs POSITIONED is positioned here —
    /// wharf walls, berth line, the winch, the shanty row, the utility-pole route, the building lots — so
    /// nothing gets built twice and nothing Phase B lands on has to be torn out first.</para>
    ///
    /// <para><b>Why the region is recreated rather than grown.</b> Nine Mile Creek shipped as a 120 × 120 m
    /// harbour island with a rectangular quay — a stand-in for Port Greywick, authored before the
    /// 2026-07-25 ruling split the two ("Nine Mile Creek IS the working wharf; the market town is one road
    /// up the hill"). The owner's reference photographs and the overhead are of a MAINLAND wharf on a
    /// low red spit, with fields behind it, a barachois pond, and St Peters lying offshore to the
    /// south-east. That is a different landform, not a bigger one.</para>
    ///
    /// <para><b>The frame.</b> Region-local metres, centre at the origin. <b>Water is EAST</b> (+x),
    /// <b>fields are WEST</b> (−x), the coast runs roughly <b>north–south</b>, and the crossing to
    /// St Peters leaves the shore heading <b>ESE</b>. Bearings quoted in the comments are compass degrees
    /// (N = 0, clockwise), the same convention <see cref="CoastPlan"/> and
    /// <see cref="MainlandCoast"/> measure in.</para>
    /// </summary>
    public static class NineMileCreekMainland
    {
        // =================================================================================================
        //  1. THE REGION RECTANGLE — sized by TIME TO CROSS, not by feel
        // =================================================================================================
        // 760 × 560 m. The long axis is the CROSSING (a corridor, sized by the tide window — scene-sizing
        // §1.3 says a corridor's length is set by drama, not by comfort); the short axis is the FOOT
        // REGION (the landing → wharf → town walk, 3:07 at the 3.0 m/s walk, just over the 2–3 min
        // guideline for exactly the reason the wharf doc gives for 600 × 400: it is two places, and the
        // walk between them is what makes them feel like two places).
        //
        // ⭐ 760 m is St Peters' own width, and that is not decoration: the two regions are the same water
        // 610 m apart across one sandbar, so they get the same scale, the same tide and the same seabed
        // resolution. A player who walks the bar should not feel the world change units halfway.
        public static readonly Vector2 RegionWorldCenter = new Vector2(0f, 0f);
        public static readonly Vector2 RegionWorldSize   = new Vector2(760f, 560f);

        /// <summary>Painted-seabed resolution in TEXELS PER METRE — 2 px/m, the ruled inshore figure
        /// (scene-sizing §6.1). 760 × 560 at 2 px/m is 1520 × 1120 texels = 1.62 MiB of R8, inside
        /// <see cref="RegionDef.MaxSeabedTexels"/> with room to spare.</summary>
        public const float SeabedPixelsPerMetre = 2f;

        /// <summary>The height-bake grid the water shader uses — derived from the extent, never a
        /// literal. Square, because <c>WaterSurface</c> bakes a square texture.</summary>
        public static int WaterHeightBakeResolution =>
            Mathf.Clamp(
                Mathf.CeilToInt(Mathf.Max(RegionWorldSize.x, RegionWorldSize.y) * SeabedPixelsPerMetre),
                64, RegionDef.MaxSeabedTexels);

        // =================================================================================================
        //  2. THE TIDE — St Peters', exactly, and this is a CHANGE
        // =================================================================================================
        // ⚠⚠ The shipped Nine Mile Creek authors mean 0, amplitude 0.8 m, phase 2 h — the "gentle market
        // harbour so business is never stranded" profile, written when this region was standing in for
        // Port Greywick. It CANNOT survive the recreation, for a reason that is geometry rather than
        // taste: the tidal bar is ONE bar spanning the seam between two regions, and its exposure is a
        // function of (crest, amplitude, phase). Two different tides either side of the seam means the
        // crossing is dry on one side and flooded on the other at the same instant — the crossing's whole
        // lesson, broken by arithmetic.
        //
        // So the mainland takes St Peters' tide verbatim. It is also simply true: these are the same
        // 610 m of water. What it costs is the "market harbour never strands you" convenience — which
        // the 2026-07-25 ruling already moved to Port Greywick, the town one road up the hill. A working
        // wharf on a big-tide coast should absolutely dry out under the fleet at spring low; the
        // reference photographs are of a wharf whose ladders and tyre fenders exist for precisely that.
        public const float TideMean = 0f;
        public const float TideAmplitude = 2.2f;
        public const float TidePhaseHours = 1f;

        /// <summary>Lowest water of the biggest spring tide (m above datum): −2.2 m.</summary>
        public static float SpringLowWater => TideMean - TideAmplitude;
        /// <summary>Highest water of the biggest spring tide (m above datum): +2.2 m.</summary>
        public static float SpringHighWater => TideMean + TideAmplitude;

        // =================================================================================================
        //  3. THE DEPTH LADDER
        // =================================================================================================
        // The wharf doc's ruled ladder is THREE HARBOURS, not three depths here: St Peters' dock ~0.6 m,
        // Nine Mile Creek ~1.6 m, Port Greywick 6 m dredged. Nine Mile Creek's number is 1.6 — it is the
        // LOBSTER-BOAT berth, which is exactly the owner's stated ceiling for the starter world ("this
        // will likely stop at the lobster boat in terms of progression"). Greywick keeps the 6 m.
        //
        // ⚠ CORRECTION TO THE HANDOFF, which reads "the NMC wharf's ruled depth ladder is 0.6 / 1.6 / 6 m
        // — keep it (the wharf memory: NMC is the deep-water berth)". Those two halves disagree: the
        // ladder puts NMC at 1.6 m and Greywick at 6 m. The repo wins (the handoff says it should), and
        // "the deep-water berth" resolves to the sense that survives — NMC is the deepest berth the
        // STARTER world has, the one the lobster boat comes home to.
        //
        // ⚠ AND THE LADDER'S "EXCLUDES THE SIDE DRAGGER" CLAIM DOES NOT SURVIVE THE TIDE CHANGE. Gating
        // is emergent (waterLevel − bed > draught; the wharf doc §4 says so itself), and against a
        // −1.6 m bed under a ±2.2 m swing the fleet lands as a FRACTION OF THE CYCLE AFLOAT:
        //     dory 0.30 m .......... 70.1%      lobster boat 1.30 m ... 54.4%
        //     fishing skiff 0.35 ... 69.2%      Cape Islander 1.40 .... 52.9%
        //     punt 0.50 ............ 66.7%      side dragger 2.90 ..... 29.9%
        // The ORDERING is right and the working hulls sit at "a little over half the cycle", which is the
        // nagging-constraint shape St Peters' berth already has. But a 4.4 m tide range is wider than the
        // 2.6 m draught spread, so no single bed can admit a 1.4 m hull for half the cycle AND exclude a
        // 2.9 m one outright. The dragger can enter near high water — and then take the ground under her
        // own weight for 70% of the cycle, which is a better and more honest exclusion than a wall. Owner
        // call if it should land harder; the lever is the bed, not the tide.
        public const float BayFloorElevation   = -6f;    // the open bay: never bares, nothing grounds
        public const float BasinBedElevation   = -1.6f;  // ⭐ the ruled gate — the lobster-boat berth
        public const float LandElevation       = 6f;     // the fields: dry at every tide (St Peters' plateau)

        // The foreshore: a broad shallow apron, because that is what the overhead SHOWS — the bay off
        // Nine Mile Creek is a wide brown shoal, not a deep-water frontage. These are HULL-DRAUGHT and
        // WADING numbers, so unlike the crest they deliberately do NOT scale with the tide.
        public const float ShoreFalloff        = 24f;    // the beach band the waterline sweeps
        public const float ShelfWidth          = 60f;
        public const float ShelfInnerElevation = -1f;
        public const float ShelfOuterElevation = -2.6f;

        // The cliff profile. Fractions of the amplitude, never metres (the 2026-08-01 lesson).
        public const float CliffPlungeWidth       = 3f;
        public const float CliffToeTideFraction   = 0.45f;   // toe −3.19 m: below the lowest water by more than a wade
        public const float LedgeBenchTideFraction = 0.25f;   // bench −1.65 m: a SPRING-tide bench
        public const float LedgeBenchWidth        = 4f;

        // =================================================================================================
        //  4. THE COASTLINE — an OPEN RUN, authored SOUTH → NORTH with the sea on the RIGHT (east)
        // =================================================================================================
        // Read off the overhead and the three aerial photographs, compressed to the region's scale:
        //   · a low red-sandstone coast running roughly N–S, water east into the bay;
        //   · the tidal crossing landing on it toward the SOUTH end, with beach south of the landing;
        //   · a working spit at the creek's mouth, further north, where the wharf is;
        //   · a barachois — a shallow lagoon behind the spit — with a narrow mouth (the NOTCH below);
        //   · beach again north of the creek, then the coast running on off the region's north edge.
        //
        // ⚠ The NOTCH (P7…P11) is the creek mouth and it is the one place the coast turns its back on the
        // camera: its two inner banks face NE and NNE. The cliff kit authors no north facing, so those
        // 41.8 m are the ONLY stretch of this coastline a cliff may not stand on — and they are exactly
        // where the marsh and the sand belong anyway. The law wearing the photographs' clothes.
        public static readonly Vector2[] CoastPoints =
        {
            new Vector2( 30f, -280f),   // s   0.00 — the region's south edge
            new Vector2( 52f, -200f),   // s  82.97
            new Vector2( 60f, -150f),   // s 133.61 — ⭐ THE BAR LANDING
            new Vector2( 50f, -100f),   // s 184.60
            new Vector2( 34f,  -40f),   // s 246.69
            new Vector2( 32f,   20f),   // s 306.73
            new Vector2( 44f,   70f),   // s 358.15
            new Vector2( 58f,  110f),   // s 400.53 — the spit's root
            new Vector2( 44f,  128f),   // s 423.33 — the creek mouth, south bank  (normal 52.1° — no cliff)
            new Vector2( 26f,  134f),   // s 442.30 — inside the mouth             (normal 18.4° — no cliff)
            new Vector2( 24f,  152f),   // s 460.41
            new Vector2( 40f,  168f),   // s 483.04 — back out to the shore
            new Vector2( 34f,  210f),   // s 525.47
            new Vector2( 30f,  260f),   // s 575.63
            new Vector2( 34f,  280f),   // s 596.02 — the region's north edge
        };

        /// <summary>Total length (m) of the coast run — derived, never typed in.</summary>
        public static float CoastRunLength => MainlandCoast.RunLength(CoastPoints);

        /// <summary>Distance along the run at which the crossing comes ashore (the third authored point).
        /// Derived so the landing and the sector table cannot drift apart.</summary>
        public static float BarLandingAlong => MainlandCoast.SegmentStart(CoastPoints, 2);

        // =================================================================================================
        //  5. THE COAST PLAN — which stretch of shoreline stands up
        // =================================================================================================
        // Distances are metres ALONG the run from its south end; each entry starts a sector that runs to
        // the next, and the last runs to the end of the coast. To retune the coast, move these numbers and
        // re-run the builder — NineMileCreekMainlandCoastTests re-derives every claim from this table
        // rather than restating it.
        //
        // ⭐ WHY IT LOOKS LIKE THIS — the constraints, in the order they bind:
        //
        // 1. THE ASPECT LAW. A cliff may stand only where its seaward normal snaps within 22.5° of one of
        //    the kit's five facings (W · SW · S · SE · E) — no north, because a north-facing wall shows
        //    its shadowed back to a ¾ top-down camera. An EAST-FACING MAINLAND IS ALMOST ENTIRELY LEGAL:
        //    measured, 554.2 m of 596.0 = 93.0% of this coastline, against St Peters' 55.6%. That is the
        //    single biggest difference between the two regions' coast plans and it is why the mainland
        //    could be given its own plan without touching St Peters' — which sits 0.02° inside its own
        //    ceiling and must not be nudged.
        //
        // 2. THE CROSSING'S LANDING MUST BE WALKABLE SHORE. The bar bares out to ±30 m of its centre-line,
        //    so it comes ashore across roughly s 104…164. The Beach sector runs 0…176, which covers it
        //    with 12 m of margin — a landing with no margin is a landing a later nudge silently walls off
        //    (St Peters' 252° note, learned there and applied here).
        //
        // 3. NO CLIFF RUN SHORTER THAN TWO FACE TILES (24 m). A shorter run cannot show the kit's periodic
        //    wall twice, so it reads as a texture accident rather than as rock.
        //
        // 4. THERE MUST BE A WAY DOWN. One Access gully in the cliff coast, or the whole foreshore north
        //    of the crossing is scenery you can see and never reach.
        //
        // WHAT THAT ADDS UP TO: 163.0 m of 596.0 = 27.35% authored cliff against a 93.0% ceiling. Unlike
        // St Peters there is enormous headroom here — the mainland can be given more rock later without
        // an argument with the aspect law. This plan spends it sparingly on purpose: the owner's photos
        // are of a LOW coast, and the drama belongs to the crossing and the working wharf.
        public static readonly CoastRunSector[] CoastSectors =
        {
            // The south beach — the arrival. 133.6 m of it lies SOUTH of the landing and 42.4 m north,
            // which is the owner's "beach to the SOUTH of the crossing point", measured.
            new CoastRunSector(  0f, CoastClass.Beach),

            // The marram top of the beach where the ground starts to rise to the bank.
            new CoastRunSector(176f, CoastClass.Dune),

            // The low red bank: a ledge whose bench bares on the big tides only — the first rock the
            // player meets, and the gentlest kind.
            new CoastRunSector(210f, CoastClass.LedgeCliff),

            // ⭐ THE GULLY. The ONE way down to the foreshore along the cliff coast, and the thing that
            // makes the ledges reachable at low water. A footpath spurs to it off the bar road.
            new CoastRunSector(268f, CoastClass.Access),

            // The weather face north of the crossing — the tall red bluff. This is the stretch the
            // handoff asks about: standing on it you look back SE across the bar to St Peters.
            new CoastRunSector(294f, CoastClass.Cliff),

            // The spit's root falls straight to the bay floor, so a hull lies close alongside the rock
            // at any state of tide — which is why the wharf is HERE and not somewhere prettier.
            new CoastRunSector(370f, CoastClass.DeepShoreCliff),

            // The creek mouth: both banks, the notch, and the north shore of the spit. Soft by law
            // (normals 52.1° and 18.4°) and soft by sense — this is marsh and sand.
            new CoastRunSector(399f, CoastClass.Beach),

            new CoastRunSector(521f, CoastClass.Dune),

            // The north beach — the wharf doc's "beach at the north end, the soft arrival".
            new CoastRunSector(551f, CoastClass.Beach),
        };

        /// <summary>Metres of run the sector joins are feathered over. Butted hard, a cliff meeting a
        /// beach is a 7 m step ACROSS the shore — a wall running out to sea. Every sector is at least
        /// twice this long, which the coast tests assert against the table above.</summary>
        public const float CoastFeatherMetres = 8f;

        /// <summary>The shortest a cliff run may be: two 12 m face tiles, so the kit's periodic wall
        /// shows twice and reads as rock rather than as an accident.</summary>
        public const float MinimumCliffRunMetres = 24f;

        // =================================================================================================
        //  6. THE CROSSING — this region's HALF of the bar, and the SEAM
        // =================================================================================================
        // ⭐ THE SEAM IS THE BAR'S MIDPOINT. St Peters owns 305.0 m of crossing (SandbarFrom (−45,0) →
        // SandbarTo (−350,0)); the mainland owns 305.0 m more. Total crossing 610 m — which lands, by
        // coincidence rather than design, within 10 m of the 600 m bar the sizing doc originally
        // recommended before the bar was shortened.
        //
        // WHY THE MIDPOINT and not the landing:
        //   · Regions do not share a coordinate frame — a "seam" is a passage band and an arrival point,
        //     not a shared line — so the split is free to sit wherever it reads best.
        //   · St Peters' bar today ENDS IN OPEN WATER at its region edge with a passage band 6 m past the
        //     tip; there is no landfall on that side to move. The mainland supplying the other half is
        //     what turns two half-crossings into one crossing.
        //   · A load at the LANDING would fire exactly when the player arrives — stepping ashore is the
        //     beat, and a scene swap on top of it wastes it. A load at the midpoint fires on flat open
        //     flats with nothing happening, which is the cheapest possible place to hide one.
        //
        // ⭐ THE TWO HALVES ARE MIRROR IMAGES, ON PURPOSE. Same crest (0.88 m = 0.4 × amplitude), same
        // 30 m half-width, same −4 m shoulder, and a passage band 6 m past each tip — so the ground at
        // the seam is 0.372 m on St Peters' side and 0.359 m on this one, and the flats bare 17.5 m
        // either side of the centre-line on BOTH. The crossing therefore bares at the same tide and to
        // the same width across the load, which is what makes a split crossing honest rather than merely
        // possible. NineMileCreekMainlandTerrainTests measures both and holds them together.
        public static readonly Vector2 BarFrom = new Vector2( 45f, -150f);  // 15 m inland — the ridge joins the beach
        public static readonly Vector2 BarTo   = new Vector2(346f, -199f);  // the seam end
        public const float BarHalfWidth = 30f;                              // St Peters' half-width, unchanged

        /// <summary>
        /// The bar's SHOULDER: what its flanks fall to at the half-width, before the bottom drops away to
        /// this region's own −6 m bay floor.
        ///
        /// <para>⭐ −4 m IS ST PETERS' DEEP-HARBOUR FLOOR, and copying it is the whole point. How wide a
        /// bar BARES at a given tide is decided by how fast its flanks fall, not by its half-width: the
        /// same 30 m half-width tapering straight into a −6 m bay bares 13.5 m either side at spring low
        /// where St Peters' bares 17.5 m. Left alone, the crossing would visibly NARROW by a quarter the
        /// instant you crossed the seam. Giving the mainland's bar St Peters' shoulder makes both halves
        /// bare 17.5 m either side at spring low — measured, and pinned by the seam test.</para>
        /// </summary>
        public const float BarFlankToeElevation = -4f;
        /// <summary>How far (m) past the half-width the shoulder takes to fall to the bay floor.</summary>
        public const float BarFlankFalloff = 40f;

        /// <summary>⚠ A TIDE FRACTION WEARING A METRE. 0.88 = 0.4 × <see cref="TideAmplitude"/>, which is
        /// St Peters' crest to the digit — and it must stay equal to it, because a bar with two crests is
        /// two bars. Everything the owner tuned about the crossing (that it floods at EVERY tide of the
        /// lunar month; how long it is dry at spring vs neap) depends only on crest/amplitude.</summary>
        public const float BarCrestElevation = 0.88f;

        // The gut: the boat passage cut through this half of the bar, so a hull can cross the crossing.
        // St Peters' numbers, because it is the same bar and the same hulls.
        public const float GutAlong        = 0.45f;
        public const float GutHalfWidth    = 15f;
        public const float GutBedElevation = -0.6f;

        /// <summary>
        /// The walk-out band at the mainland's bar tip: cross this heading ESE and the crossing hands over
        /// to St Peters. 6.1 m past <see cref="BarTo"/> along the bar's own axis — St Peters' own 6 m
        /// offset, which against an identical taper puts identical ground under both bands.
        /// </summary>
        public static readonly Vector3 ToStPetersPassagePos = new Vector3(352f, -200f, 0f);

        /// <summary>
        /// Where the player STANDS on arriving from St Peters, on foot, mid-crossing.
        ///
        /// <para>⚠ NOTHING CONSUMES THIS YET, and that is a reported gap rather than an oversight.
        /// <c>RegionAnchor</c> exposes ONE <c>DisembarkPoint</c> and <c>RegionTravelCoordinator</c>
        /// teleports the player to it on every arrival — which is right for the one-passage regions built
        /// so far and wrong the moment a region has both a wharf you sail into and a bar you walk in
        /// across. Arriving on foot would put the player on the wharf deck 400 m away. The fix is a
        /// per-passage arrival point on <c>RegionAnchor</c> (App/Core, so lead-architect's call); it is
        /// named here so Phase A's geometry is ready for it the day it lands.</para>
        /// </summary>
        public static readonly Vector3 WalkArrivalPos = new Vector3(350f, -200f, 0f);

        // --- ⭐ THE SEA DOOR: out east into THE WEST WATER (world-map-plan §6 step 1) -------------------
        // The wharf's way out to open water, and the half of the home arc that is not tide-gated: leave
        // the basin, pass the breakwater head, hold east, and the bay's first open-water scene takes over.
        //
        // ON THE BEACON'S OWN LATITUDE, derived rather than typed. HarbourBeaconPos marks the breakwater
        // head, which is the point every boat leaving this harbour actually rounds — so a door on that
        // line is a door you reach by steering the way the harbour already steers you. It sits 24 m inside
        // the region's east edge, 235 m clear of the tidal bar's nearest point (the bar comes ashore far
        // to the south, y ≈ −199) and 172 m beyond the breakwater's own tip, which puts it in open bay on
        // the flat −6 m floor — a seam in featureless water, clear of hazards (world-map-plan §4.4).
        //
        // ⚠ 120 m tall, not the full region: the east edge already carries the CROSSING's band at
        // y = −200, and two doors on one edge must not overlap. The gap between them is 158 m.
        //
        // ⚠ EXPRESSION-BODIED, not `static readonly`, and it has to be: HarbourBeaconPos is declared
        // further down this same file, and a static field initialiser reading a static field declared
        // BELOW it binds to that field's default — this door would have silently landed on y = 0, in the
        // middle of the region's east edge, with nothing failing. Same-file order is the one case the
        // file-header warning about cross-type initialisation order does not already cover.
        public static Vector3 ToWestWaterPassagePos =>
            new Vector3(RegionWorldCenter.x + RegionWorldSize.x * 0.5f - 24f, HarbourBeaconPos.y, 0f);
        public static readonly Vector2 WestWaterPassageBandSize = new Vector2(6f, 120f);

        /// <summary>The bar's total length across BOTH regions (m) — St Peters' half plus this one.
        /// Derived from the two builders rather than restated, so neither can move without the other
        /// noticing.</summary>
        public static float CrossingTotalMetres =>
            Vector2.Distance(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo) +
            Vector2.Distance(BarFrom, BarTo);

        // =================================================================================================
        //  7. CARVES — the ground the tide got to first
        // =================================================================================================
        // Carves compose DOWNWARD only: they can never lift a wharf. Both of them are freshwater-side
        // features — the two ponds behind the shore. The HARBOUR is not a carve at all; see §8.

        /// <summary>The barachois — the shallow lagoon behind the spit, drained through the coastline's
        /// notch. Bed −0.8 m, so it is a mirror at high water and a red mudflat at low: the brightest
        /// thing in the owner's first photograph, and free P1 texture from one carve.</summary>
        public static readonly MainlandZone BarachoisCarve =
            new MainlandZone(new Vector2(-10f, 132f), new Vector2(54f, 26f), -0.8f, 8f);

        /// <summary>The marsh pool south of Wharf Road — the second pond in the photographs. Bed −0.4 m.
        /// Its whole job is to leave a NECK: the road runs the 12 m of dry ground between the two ponds,
        /// which is why the causeway in the reference photo exists and why one is not authored here.</summary>
        public static readonly MainlandZone MarshPoolCarve =
            new MainlandZone(new Vector2(-26f, 58f), new Vector2(30f, 16f), -0.4f, 6f);

        public static readonly MainlandZone[] Carves =
        {
            BarachoisCarve, MarshPoolCarve,
        };

        // =================================================================================================
        //  8. FILLS — the built ground (and the wharf PLAN Phase B builds on)
        // =================================================================================================
        // ⭐ THE SPIT IS FILL, NOT A HEADLAND, and that is the single most important reading of the
        // photographs. The wharf at Nine Mile Creek stands on made ground: red earth and rock pushed out
        // onto a shoal, low enough that a big tide is a real event on it. Modelling it as natural land
        // would have given it the fields' +6 m plateau and turned a working spit into a bluff.
        //
        // ⭐ AND THE BASIN IS A SHOAL, NOT A DREDGING — the correction that fell out of measuring the
        // first draft. A basin authored as a CARVE to −1.6 m did nothing at all, because the open bay
        // floor here is −6 m and a carve can only cut: the "dredged" basin came out four and a half
        // metres deeper than its own gate, and every hull in the game could lie in it at any tide. The
        // truth the photographs show is the opposite of dredging — the wharf at Nine Mile Creek is built
        // out ONTO a shoal, and the shoal IS the gate. So the harbour's water is a FILL that raises the
        // bay to −1.6 m under the walls, the breakwater and the approach; the walls then stand on top of
        // it. One number, applied once, gates the berth and the entrance alike.

        /// <summary>Deck elevation (m above datum) of the wharf: 0.8 m of freeboard at spring high water.
        ///
        /// <para>⚠ FLAG FOR PHASE B / art-pipeline — <b>RESOLVED, and the terrain never moved.</b> The
        /// face this implies is <c>deck − basin</c> = 4.6 m of quay wall, and 5.2 m of it stands exposed
        /// at spring low. The flag as first written ("the ISO wharf kit bakes a 24 px overhanging face —
        /// 0.75 m at PPU 32 — so it must tile/stack the face vertically, or the deck must come down
        /// toward the water") quoted the RETIRED near-plan tile kit; the ISO pack baked 2.80 m, and #471
        /// re-measured the gap at 2.40 m. #477/#478 re-baked the pack at this coast's own tide and the
        /// wall is now drawn in one course. Phase A authored the honest terrain and reported the
        /// mismatch rather than quietly flattening a big-tide wharf to suit a sprite, and that is exactly
        /// what got the sprite fixed instead.</para></summary>
        public const float WharfDeckElevation = 3f;
        /// <summary>The yard: 0.6 m above the deck, so you step DOWN onto the wharf. Real wharves do.</summary>
        public const float SpitYardElevation = 3.6f;
        /// <summary>The crib breakwater's crest — awash only on the biggest tides.</summary>
        public const float BreakwaterCrestElevation = 3.4f;

        /// <summary>
        /// THE HARBOUR SHOAL: 116 × 60 m of ground raised to the ruled −1.6 m gate under the basin, the
        /// breakwater and the approach, easing back to the −6 m bay over 16 m. This is the harbour's
        /// water, and the ONE number every hull is measured against here.
        ///
        /// <para>Its west edge stops at x = 76 deliberately, short of the spit's root, so the
        /// DeepShoreCliff sector there keeps its own promise — a hull lies close alongside that rock at
        /// any state of tide, which is why the wharf grew where it did.</para>
        /// </summary>
        public static readonly MainlandZone HarbourShoalFill =
            new MainlandZone(new Vector2(134f, 64f), new Vector2(58f, 30f), BasinBedElevation, 16f);

        /// <summary>
        /// The made ground: 132 × 44 m of red-earth yard carrying the shanty row, the trap stacks, the
        /// parking, the buyers' trucks and — since the photograph pass — the boatyard. Its west end runs
        /// back into the natural shore.
        ///
        /// <para>⚠ <b>IT GREW 28 m EAST, and the reason is arithmetic rather than taste.</b> The owner's
        /// ruling puts Harris &amp; Sons on this yard at the shipyard kit's <c>smallYard</c> tier, and that
        /// tier's own contract measures 1230 × 953 px at 32 px/m — a 38.4 m DRAWN width, which at the
        /// pack's iso projection is a ground square of about 27 m a side. There was no 27 m of clear
        /// ground on the authored 104 × 44 m spit: every siting collided with the fish store, the trap
        /// store, the shanty row or Wharf Road's own 3 m corridor. Growing the made ground east onto the
        /// harbour shoal (which already reaches x = 192) is what the photograph shows anyway — the yard
        /// there is a compound of its own beside the wharf, not a shed squeezed between the others.</para>
        ///
        /// <para>East only, and deliberately: growing it NORTH would push made ground into the beach and
        /// dune sectors the coast plan authors at y &gt; 140, which is a coastline change and a different
        /// argument. The east edge stops at x = 185, inside the shoal's own 192.</para>
        /// </summary>
        public static readonly MainlandZone SpitFill =
            new MainlandZone(new Vector2(119f, 118f), new Vector2(66f, 22f), SpitYardElevation, 8f);

        /// <summary>The NORTH WALL: 84 × 10 m of deck. The fleet moors along its SOUTH face
        /// (y = 87) — the owner's arrangement, and the one edge the kit gives a tall face to.</summary>
        public static readonly MainlandZone NorthWallFill =
            new MainlandZone(new Vector2(128f, 92f), new Vector2(42f, 5f), WharfDeckElevation, 1.2f);

        /// <summary>The WEST WALL: 10 × 48 m, wider in the sense that matters — it carries the unloading
        /// apron, the winch and a truck. Its water side faces EAST, a curb-only edge at this camera,
        /// which is why the winch must be a tall legible object rather than a detail on a wall.
        /// <para>⚠ Its east face sits at x = 92 so the berth line (from x = 98) clears it. The first
        /// draft put the wall at x ∈ [85, 95] and the first berth at x = 92.5 — i.e. the first two boats
        /// were moored ON the deck, which the layout test now makes impossible to ship.</para></summary>
        public static readonly MainlandZone WestWallFill =
            new MainlandZone(new Vector2(87f, 68f), new Vector2(5f, 24f), WharfDeckElevation, 1.2f);

        /// <summary>The breakwater: a 92 m south arm of timber crib, shown unmistakably in the reference
        /// photographs as log boxes filled with stone. It shelters the basin's south side.</summary>
        public static readonly MainlandZone BreakwaterFill =
            new MainlandZone(new Vector2(140f, 38f), new Vector2(46f, 4f), BreakwaterCrestElevation, 3f);

        // ⚠ ORDER MATTERS WITHIN THE LIST ONLY IN THE SENSE THAT IT DOESN'T — fills max-compose, so the
        // shoal can never eat a wall and a wall can never eat the yard. The shoal is listed first because
        // it is the ground the rest of them stand on.
        public static readonly MainlandZone[] Fills =
        {
            HarbourShoalFill, SpitFill, NorthWallFill, WestWallFill, BreakwaterFill,
        };

        // --- the wharf PLAN, for Phase B ---------------------------------------------------------------

        /// <summary>Berths along the north wall's south face: 14 at 5.5 m spacing, which is the fleet in
        /// the photographs (twelve to fourteen boats, rafted two deep in places). Phase B lays the finger
        /// piers and the mooring hardware on this line; Phase A only promises the water is there.</summary>
        public const int BerthCount = 14;
        public const float BerthSpacingMetres = 5.5f;
        /// <summary>The first berth's centre — 2 m off the wall's face (y = 87), and east of the west
        /// wall's own deck so no boat is moored on dry land. The line runs x = 98 → 169.5, inside the
        /// north wall's 86 → 170.</summary>
        public static readonly Vector2 FirstBerthPos = new Vector2(98f, 85f);

        /// <summary>Centre of berth <paramref name="index"/> (0-based), along the north wall's face.</summary>
        public static Vector2 BerthPos(int index) =>
            FirstBerthPos + new Vector2(BerthSpacingMetres * Mathf.Clamp(index, 0, BerthCount - 1), 0f);

        // Where the working things stand. Phase A places greybox markers; Phase B swaps in the kit.
        public static readonly Vector3 WinchPos          = new Vector3( 87f,  84f, 0f);  // west wall, by the apron
        public static readonly Vector3 UnloadApronPos    = new Vector3( 87f,  74f, 0f);
        public static readonly Vector3 ParkingPos        = new Vector3( 92f, 118f, 0f);  // the buyers' trucks
        public static readonly Vector3 FishBuyerPos      = new Vector3( 88f, 110f, 0f);  // at his tailgate
        public static readonly Vector3 FishStorePos      = new Vector3(120f, 112f, 0f);  // holds; NO processing
        public static readonly Vector3 BaitShedPos       = new Vector3(136f, 132f, 0f);
        public static readonly Vector3 TrapStorePos      = new Vector3(148f, 130f, 0f);
        public static readonly Vector3 DerelictDoryPos   = new Vector3( 70f, 118f, 0f);  // on the hard (the 2026-07-25 rider)
        public static readonly Vector3 BoatRampHeadPos   = new Vector3( 66f, 100f, 0f);
        public static readonly Vector3 BoatRampToePos    = new Vector3( 78f,  90f, 0f);
        public static readonly Vector3 HarbourBeaconPos  = new Vector3(184f,  38f, 0f);  // breakwater head

        /// <summary>
        /// THE DORY YARD — the hard the damaged dory is bought off, and where the used-outboard man stands
        /// at his barrel. ⭐ <b>Added in Phase A-2.</b> A-1 positioned the derelict but not the ground you
        /// buy her from, and the two cannot be far apart: the boat you are SHOWN and the boat you are SOLD
        /// have to be the same boat, or §7.2's exit condition becomes two unrelated beats.
        ///
        /// <para>8.5 m west-south-west of her, at the spit's landward end — on the made ground, clear of
        /// her hull, of the boat ramp's head and of the shanty row, and (the constraint that actually
        /// binds) <b>off the sightline from where the player lands to the derelict</b>, which is what
        /// <c>NineMileCreekDoryTests</c> measures. It is a YARD, not a shed: the 2026-07-25 ruling says
        /// the man who sells her here is not a shipwright.</para>
        /// </summary>
        public static readonly Vector3 DoryYardPos       = new Vector3( 64f, 112f, 0f);

        /// <summary>The shanty row — five gable sheds side by side along the yard's north edge, which is
        /// how they stand in the photograph (eight there; five reads as the same place at this scale).</summary>
        public static readonly Vector3[] ShantyRow =
        {
            new Vector3( 66f, 132f, 0f),
            new Vector3( 80f, 132f, 0f),
            new Vector3( 94f, 132f, 0f),
            new Vector3(108f, 132f, 0f),
            new Vector3(122f, 132f, 0f),
        };

        /// <summary>The ground a wharf shed reserves, as a radius — the kit's biggest preset plus a
        /// working margin. Anything asking "is one of these in the way?" asks this.</summary>
        public const float WharfShedRadius = 5f;

        // =================================================================================================
        //  8b. WHAT THE PHOTOGRAPH SHOWS THAT THE PLAN DID NOT — floats, slipway, armour, lots
        // =================================================================================================
        // ⭐ THE SCALE, DERIVED RATHER THAN FELT (owner's satellite view, 2026-08-10). The moored hulls in
        // the basin are lobster boats, and this repo's own LobsterBoat.asset says a lobster boat is 12 m.
        // Measured in the close aerial (1271 × 865 px) a moored hull spans ~51 px stem to stern, so the
        // photograph runs at ≈ 4.25 px/m (the hulls read 48–55 px, so call it ±8 %). Against that:
        //
        //     the main quay arm ...... ~365 px → 86 m        the float run .......... ~200 px → 47 m
        //     the basin's water ...... ~370 × 325 px → 87 × 76 m
        //     the slipway ............ ~90 px wide → 21 m    Bait Masters' shed ..... ~125 × 95 px → 29 × 22 m
        //
        // ⭐ THE CHECK THAT MATTERS: the authored north wall is 84 m and the photograph's main arm is 86 —
        // within 2 %. The wharf's PRIMARY dimension was already right and does not move.
        //
        // ⚠ AND THE ONE FINDING THE PLAN DOES NOT ACT ON. The photograph's basin is ~76 m deep against the
        // authored 45 m. Deepening it means moving the breakwater south from y = 34 to about y = 15 — and
        // HarbourBeaconPos stands on the breakwater head, with ToWestWaterPassagePos deriving its whole
        // latitude from HarbourBeaconPos.y. So a deeper basin MOVES THE SEA DOOR, and the standing
        // constraint on this pass is that the bar and every passage stay put. The basin therefore keeps
        // its authored depth, the extra 30 m is REPORTED rather than taken, and everything below is fitted
        // INSIDE the existing envelope. The visible cost is real and named: the photograph shows two float
        // runs in the basin and this plan has room for one.

        /// <summary>
        /// THE FLOATING DOCK — the photograph's finger run inside the basin, where the small craft lie.
        /// One run, not the photograph's two: see the basin-depth finding above.
        ///
        /// <para>It hangs off the APRON, which is what the photograph shows and what the wharf already
        /// wants: the west wall is the service end (the fuel pump and the oil tank are both on it), and a
        /// float reached from the working mooring face would put a gangway across the fleet's own
        /// landing.</para>
        /// </summary>
        public static readonly float FloatRunY = 70f;
        /// <inheritdoc cref="FloatRunY"/>
        public static readonly float FloatRunWestX = 104f;
        /// <inheritdoc cref="FloatRunY"/>
        public const float FloatRunLengthMetres = 48f;
        /// <summary>The run's east end — derived, so lengthening it moves one number.</summary>
        public static float FloatRunEastX => FloatRunWestX + FloatRunLengthMetres;

        /// <summary>Where the gangway leaves the fixed wharf: the apron's east face, on the float's own
        /// latitude. DERIVED from the wall rather than typed, so re-siting the apron takes the brow with
        /// it instead of leaving it reaching for a wall that moved.</summary>
        public static Vector2 GangwayShoreEnd =>
            new Vector2(WestWallFill.Center.x + WestWallFill.HalfSize.x, FloatRunY);

        /// <summary>…and where it lands: the float run's west end.</summary>
        public static Vector2 GangwayFloatEnd => new Vector2(FloatRunWestX, FloatRunY);

        /// <summary>
        /// THE SLIPWAY — the photograph's broad ramp at the basin's landward corner, and the boatyard's
        /// way of getting a hull out of the water.
        ///
        /// <para>It runs EAST off the apron's face into the basin. 20 m of ramp falling the 4.6 m from the
        /// +3.0 m deck to the −1.6 m shoal is about 1:4.3 — steep for a real slipway (1:8 is the trade's
        /// number) and the honest consequence of a basin only 45 m deep. Flagged with the basin finding
        /// above rather than hidden by pretending the ramp is longer than the water allows.</para>
        /// </summary>
        public static Vector2 SlipwayHeadPos =>
            new Vector2(WestWallFill.Center.x + WestWallFill.HalfSize.x, 52f);
        /// <inheritdoc cref="SlipwayHeadPos"/>
        public static Vector2 SlipwayToePos => new Vector2(SlipwayHeadPos.x + 20f, SlipwayHeadPos.y);
        /// <summary>How wide the ramp is — a hull's beam plus her cradle, either side of the centre line.</summary>
        public const float SlipwayHalfWidthMetres = 4f;

        /// <summary>
        /// THE ROCK ARMOUR — the rubble line the photograph shows wherever made ground meets open water.
        /// Two runs, and only two, because those are the only faces of this wharf that are EXPOSED: the
        /// wharf head at the north wall's east end, and the spit's own east edge behind it. Everything
        /// else is either sheltered by the breakwater (which carries its own crib armour already) or is
        /// landward of the yard.
        ///
        /// <para>Each run is a pair of points, read as a line the armour is laid along — the same shape
        /// <c>NineMileCreekWharf.BreakwaterPoints</c> uses, and blocked out at the same cell width.</para>
        /// </summary>
        public static Vector2[] WharfHeadArmour => new[]
        {
            new Vector2(NorthWallFill.Center.x + NorthWallFill.HalfSize.x,
                        NorthWallFill.Center.y - NorthWallFill.HalfSize.y),
            new Vector2(NorthWallFill.Center.x + NorthWallFill.HalfSize.x,
                        NorthWallFill.Center.y + NorthWallFill.HalfSize.y),
        };

        /// <inheritdoc cref="WharfHeadArmour"/>
        public static Vector2[] SpitEastArmour => new[]
        {
            new Vector2(SpitFill.Center.x + SpitFill.HalfSize.x, SpitFill.Center.y - SpitFill.HalfSize.y),
            new Vector2(SpitFill.Center.x + SpitFill.HalfSize.x, SpitFill.Center.y + SpitFill.HalfSize.y),
        };

        // --- the lots the photograph puts along Wharf Road ---------------------------------------------
        // ⚠ NAMED RESERVED LOTS, NOT BUILDINGS, for the two types no kit bakes. The standing ruling is
        // that a building type with no kit gets a reserved lot and a reported gap — never a placeholder
        // asset, because a clapboard house standing in for a bait shed looks FINISHED while being wrong
        // (NineMileCreekFlavour's own argument, applied to the same problem one pass later). The BOATYARD
        // is the exception and is drawn: the shipyard kit is baked, 25 sheets.

        /// <summary>
        /// HARRIS &amp; SONS — the boatyard, at the owner's ruled <c>smallYard</c> tier. On the spit's new
        /// eastern ground, beside the wharf head, which is the only 27 m of clear yard the region has (see
        /// <see cref="SpitFill"/> for why it had to be made).
        /// </summary>
        /// <para>⚠ At y = 112 rather than 110, and the two metres are load-bearing: a 27 m square centred
        /// at 110 reaches down to y = 96.5 and the north wall's deck reaches up to y = 97, so the yard
        /// stood half a metre ON THE QUAY. Caught by
        /// <c>TheBoatyardStandsOnMadeGround_ClearOfTheRoadAndTheQuay</c>.</para>
        public static readonly Vector3 ShipyardPos = new Vector3(168f, 112f, 0f);

        /// <summary>The ground the yard occupies, a side of the square in metres — the shipyard kit's
        /// <c>smallYard</c> cell (1230 px at 32 px/m = 38.4 m drawn) read back through the pack's iso
        /// projection to the ground square that drew it. A RECTANGLE rather than the circle a shed gets,
        /// because at this size the difference is 8 m of yard and the road is 19 m away.</summary>
        public const float ShipyardSideMetres = 27f;

        /// <summary>The yard's footprint, for anything asking "is this in the boatyard?".</summary>
        public static Rect ShipyardFootprint() => new Rect(
            ShipyardPos.x - ShipyardSideMetres * 0.5f, ShipyardPos.y - ShipyardSideMetres * 0.5f,
            ShipyardSideMetres, ShipyardSideMetres);

        /// <summary>THE RESTAURANT — the photograph's one non-working building on the wharf front, near
        /// where the road arrives so it catches whoever comes down it. ⚠ NO KIT BAKES ONE; this is a
        /// reserved lot and a reported art-director gap.</summary>
        public static readonly Vector3 RestaurantLotPos = new Vector3(112f, 102f, 0f);

        /// <summary>
        /// THE BAIT SHEDS — the photograph's cluster, of which the plan already had one
        /// (<see cref="BaitShedPos"/>). These are the other two.
        ///
        /// <para>⚠ On the yard's SOUTH strip, not on the shed row: sited on the row they landed two metres
        /// off the owners' own shed lots at x = 164 and 178, which is the same building drawn twice. The
        /// south strip between the quay and Wharf Road is the last genuinely clear ground on this spit —
        /// clear of the road's corridor, of the boatyard's west edge, of the fish store and of the quay
        /// deck. <b>That is the wharf full</b>: the photograph shows more sheds than this yard has room
        /// for, and the remaining ones are the shanty row, the bait shed and the trap store the plan
        /// already had.</para>
        ///
        /// <para>⚠ Reserved lots: <c>wharfBuildingRig</c> has never been baked (M2-40/46).</para>
        /// </summary>
        public static readonly Vector3[] BaitShedCluster =
        {
            new Vector3(132f, 104f, 0f),
            new Vector3(146f, 104f, 0f),
        };

        // --- the owners' shed lots, DERIVED rather than listed ------------------------------------------

        /// <summary>How far apart the owners' sheds stand along the row.</summary>
        public const float OwnerShedSpacingMetres = 14f;

        /// <summary>The row the owners' sheds stand on — the shanty row's own latitude, because that IS
        /// the row of sheds in the photograph and a second row beside it would be a second wharf.</summary>
        public static float OwnerShedRowY => ShantyRow[0].y;

        /// <summary>
        /// The shed lot belonging to owner <paramref name="index"/>.
        ///
        /// <para><b>⭐ A RULE, NOT A TABLE.</b> The lots march east along the shed row from the shanty
        /// row's west end at <see cref="OwnerShedSpacingMetres"/>, and a step that would land inside a
        /// working site's reserved ground is SKIPPED rather than nudged — so the row cannot collide with
        /// the bait shed, the trap store or anything else the plan already put on the yard, and adding a
        /// working site later re-flows the row instead of silently overlapping it.</para>
        ///
        /// <para><b>⚠ THE YARD AFFORDS SEVEN, AND THAT IS WHAT SIZES THE REGISTER.</b> Walked, the row
        /// yields the shanty row's own five and two more east of the trap store: the bait shed and the
        /// trap store each eat a step, and the spit's east edge stops the walk. The buoy kit's eight paint
        /// schemes are therefore NOT the binding cap here — the ground is — and
        /// <c>NineMileCreekPhotographTests.TheRegisterFitsTheLotsTheYardAffords</c> measures the walk
        /// rather than trusting this paragraph, because the day a working site is added the row loses a
        /// lot and the last owner would otherwise fall to the clamp below and be drawn on a neighbour.</para>
        ///
        /// <para>Lots 0–4 land exactly on <see cref="ShantyRow"/>, and that is the intent rather than a
        /// coincidence: the photograph's shed row IS the shanty row, so an owner's shed and the shanty
        /// drawn there are one building.</para>
        /// </summary>
        public static Vector2 OwnerShedLot(int index)
        {
            var row = OwnerShedLots();
            if (row.Count == 0) return new Vector2(ShantyRow[0].x, OwnerShedRowY);
            return row[Mathf.Clamp(index, 0, row.Count - 1)];
        }

        /// <summary>
        /// How many shed lots this yard actually affords — the walk, counted.
        ///
        /// <para><b>⭐ THE REGISTER MAY NOT OUTGROW IT, and that is a real cap rather than a formality.</b>
        /// Walked today the row yields SEVEN: the shanty row's five, then east past the bait shed and the
        /// trap store (each of which eats a step) to the spit's east edge. An eighth owner's
        /// <c>LotIndex</c> clamps onto the seventh and the two sheds are drawn in one place — which is
        /// exactly what happened on the first pass, and is why
        /// <c>NineMileCreekPhotographTests.TheRegisterFitsTheLotsTheYardAffords</c> measures this rather
        /// than trusting the number in this paragraph.</para>
        /// </summary>
        public static int OwnerShedLotCount => OwnerShedLots().Count;

        /// <summary>The whole row, walked once. See <see cref="OwnerShedLot"/> for the rule.</summary>
        public static List<Vector2> OwnerShedLots()
        {
            var row = new List<Vector2>();
            float y = OwnerShedRowY;
            float west = ShantyRow[0].x;
            float east = SpitFill.Center.x + SpitFill.HalfSize.x - WharfShedRadius;

            for (int step = 0; step < 64; step++)
            {
                var candidate = new Vector2(west + step * OwnerShedSpacingMetres, y);
                if (candidate.x > east) break;                       // off the made ground
                if (IsWorkingSiteInTheWay(candidate)) continue;      // step around, never nudge
                row.Add(candidate);
            }
            return row;
        }

        /// <summary>Is one of the plan's own working sites standing on <paramref name="p"/>? The sites a
        /// shed row has to give way to, at the radius each of them reserves.</summary>
        public static bool IsWorkingSiteInTheWay(Vector2 p)
        {
            foreach (var site in new[] { BaitShedPos, TrapStorePos, FishStorePos, ParkingPos, FishBuyerPos })
                if (Vector2.Distance(p, new Vector2(site.x, site.y)) < WharfShedRadius * 2f) return true;
            // …and the boatyard, which is a rectangle rather than a circle.
            Rect yard = ShipyardFootprint();
            if (p.x > yard.xMin - WharfShedRadius && p.x < yard.xMax + WharfShedRadius &&
                p.y > yard.yMin - WharfShedRadius && p.y < yard.yMax + WharfShedRadius) return true;
            return false;
        }

        // =================================================================================================
        //  9. THE ROADS — blob-47 kit; these are the ROUTES, the tiles are the builder's
        // =================================================================================================
        // The owner's two named roads, plus the through-road the community is strung along (the overhead's
        // Route 19) and the footpath that makes the cliff foreshore reachable.

        /// <summary>THE BAR ROAD — "from the crossing, a road the character can follow leads to Nine Mile
        /// Creek". 271.9 m of red dirt from the landing north along the clifftop to the Wharf Road
        /// junction: 1:30 at the walk. Deliberately inland of the coast plan's cliff sectors — a coast
        /// road runs along the top of the bank, not the foot of it — and deliberately EAST of the marsh
        /// pool, which the first draft ran straight through (the road-on-dry-ground test is there because
        /// it caught that).</summary>
        public static readonly Vector2[] BarRoad =
        {
            new Vector2( 58f, -146f),   // the landing, at the head of the beach
            new Vector2( 36f, -112f),
            new Vector2( 20f,  -56f),
            new Vector2( 14f,    0f),   // ⭐ the gully path spurs east from here
            new Vector2( 20f,   78f),   // east of the marsh pool
            new Vector2(-16f,   92f),   // ⭐ the junction with Wharf Road
        };

        /// <summary>WHARF ROAD — "runs all the way to Nine Mile Creek's wharf front". 321.8 m of gravel
        /// from the town, east along the neck between the two ponds, onto the spit and into the yard:
        /// 1:47 at the walk.</summary>
        public static readonly Vector2[] WharfRoad =
        {
            new Vector2(-178f,  92f),   // the town, on the through-road
            new Vector2(-100f,  90f),
            new Vector2( -40f,  92f),
            new Vector2(  14f,  92f),   // the neck between the barachois and the marsh pool
            new Vector2(  56f, 104f),   // stepping onto the spit
            new Vector2( 100f, 116f),
            new Vector2( 140f, 122f),   // the wharf front
        };

        /// <summary>THE THROUGH-ROAD — the overhead's Route 19: the road the community is actually strung
        /// along, running the length of the region a few hundred metres inland. 564.4 m.</summary>
        public static readonly Vector2[] ThroughRoad =
        {
            new Vector2(-230f, -280f),
            new Vector2(-206f, -140f),
            new Vector2(-188f,  -20f),
            new Vector2(-178f,   92f),  // ⭐ the Wharf Road junction
            new Vector2(-176f,  180f),
            new Vector2(-186f,  280f),
        };

        /// <summary>THE GULLY PATH — 19 m of footpath off the bar road to the head of the Access sector,
        /// which is the only way down onto the ledges at low water. Small, and load-bearing: without it
        /// the whole cliff foreshore is scenery.</summary>
        public static readonly Vector2[] GullyPath =
        {
            new Vector2(14f,  0f),
            new Vector2(26f, -4f),
            new Vector2(32f, -6f),
        };

        /// <summary>The Wharf Road / bar road junction — named because the roads, the town and the tests
        /// all have to be able to ask where it is without a second copy of the number.</summary>
        public static readonly Vector2 RoadJunction = new Vector2(-16f, 92f);

        /// <summary>Half-width (m) of a road's cleared corridor — nothing may be sited inside it.</summary>
        public const float RoadHalfWidth = 3f;

        // =================================================================================================
        //  10. THE TOWN — lots, not buildings (Phase B dresses them)
        // =================================================================================================
        // Nine Mile Creek the COMMUNITY, not a market town: nine lots strung along the through-road and
        // the west end of Wharf Road, the way a rural PEI settlement actually sits. Port Greywick keeps
        // the auction, the chart shop, the tavern crowd and the shipwright's yard — this is the place you
        // walk up to for a licence and a rod.
        //
        // ⚠ THE BOAT SHED IS FLAGGED, NOT RESOLVED. The 2026-07-25 ruling says there is NO SHIPWRIGHT in
        // this region, but the shipped scene has a "shipwright shed" that sells the Punt and the pots and
        // the economy data hangs off it. A lot is reserved for it here under a neutral name so nothing
        // breaks; WHERE the shipwright's yard really lives is the coordinator's open question, not
        // world-content's to close.
        public static readonly Vector3 HarbourmasterPos = new Vector3(-150f, 118f, 0f);  // the cod licence
        public static readonly Vector3 ChandleryPos     = new Vector3(-206f, 116f, 0f);  // the rod
        public static readonly Vector3 GeneralStorePos  = new Vector3(-150f, 152f, 0f);
        public static readonly Vector3 TavernPos        = new Vector3(-206f, 152f, 0f);
        public static readonly Vector3 ParishHallPos    = new Vector3(-152f,  62f, 0f);
        public static readonly Vector3 HouseNorthPos    = new Vector3(-148f, 196f, 0f);
        public static readonly Vector3 HouseNorthWestPos = new Vector3(-206f, 196f, 0f);
        public static readonly Vector3 HouseSouthPos    = new Vector3(-208f,  60f, 0f);
        public static readonly Vector3 BoatShedPos      = new Vector3(-108f,  64f, 0f);  // ⚠ see the note above

        /// <summary>Every town lot, for anything that has to ask "is one of these in the way?".</summary>
        public static readonly Vector3[] TownLots =
        {
            HarbourmasterPos, ChandleryPos, GeneralStorePos, TavernPos, ParishHallPos,
            HouseNorthPos, HouseNorthWestPos, HouseSouthPos, BoatShedPos,
        };

        /// <summary>The ground a town lot reserves, as a RADIUS. The village kit's biggest house measures
        /// 7.0 × 8.7 m — a 5.59 m half-diagonal — so 6 m covers it at every facing with a margin, and a
        /// facing is the first thing that changes. A circle for the same reason St Peters' village
        /// reserves one.</summary>
        public const float TownLotRadius = 6f;

        // =================================================================================================
        //  11. ARRIVAL / DOCK GEOMETRY (the boat half; see WalkArrivalPos for the foot half)
        // =================================================================================================
        // The persistent ControlSwitcher disembarks by a pure DISTANCE test, so ArrivalPos MUST sit within
        // DockZoneRadius of DockZonePos or the player lands out of dock range and cannot get off the boat
        // (the owner-playtest gap #52 fixed — keep it).
        public const float DockZoneRadius = 3.5f;
        /// <summary>Dock here: in the basin, against the north wall's south face at its west end — beside
        /// the unloading apron, which is where a boat with fish aboard actually stops.</summary>
        public static readonly Vector3 DockZonePos  = new Vector3(98f, 84.5f, 0f);
        /// <summary>Park just east of the dock zone, 3.0 m off — inside the 3.5 m radius.</summary>
        public static readonly Vector3 ArrivalPos   = new Vector3(101f, 84.5f, 0f);
        /// <summary>Step ashore onto the north wall's deck (its south edge is y = 87).</summary>
        public static readonly Vector3 DisembarkPos = new Vector3(98f, 88f, 0f);

        /// <summary>The return passage to Coddle Cove: the cove lies EAST of Nine Mile Creek (canon), so
        /// you sail east out of the bay. Well clear of the crossing's flats to the south.</summary>
        public static readonly Vector3 ToCovePassagePos = new Vector3(370f, 60f, 0f);

        // =================================================================================================
        //  12. UTILITY POLES — the route Phase B strings wire along
        // =================================================================================================
        // Positioned now precisely so Phase B does not have to re-decide it: poles follow Wharf Road from
        // the town to the wharf, on the NORTH side of the road, at the ~40 m spacing the photographs show
        // (the line ends at the wharf's yard light, which is the only lit thing out there at night).
        public const float UtilityPoleOffsetMetres = 5f;    // north of the road's centre-line
        public const float UtilityPoleSpacingMetres = 40f;

        // =================================================================================================
        //  13. DERIVED FACTS — the arithmetic the docs quote, computed rather than restated
        // =================================================================================================

        /// <summary>Walking speed (m/s) — <c>PlayerWalkController</c>'s, mirrored here so the crossing
        /// arithmetic in the tests reads in one place. A test holds the two equal is not possible from an
        /// editor assembly, so this is quoted with its source rather than derived.</summary>
        public const float WalkSpeedMetresPerSecond = 3f;

        /// <summary>Seconds to walk the WHOLE crossing one way, both regions' halves together.</summary>
        public static float CrossingWalkSeconds => CrossingTotalMetres / WalkSpeedMetresPerSecond;

        /// <summary>Length (m) of a polyline route — the roads' own arithmetic, in one place.</summary>
        public static float RouteLength(Vector2[] route)
        {
            if (route == null || route.Length < 2) return 0f;
            float total = 0f;
            for (int i = 0; i < route.Length - 1; i++) total += Vector2.Distance(route[i], route[i + 1]);
            return total;
        }

        /// <summary>Shortest distance from a point to a polyline route — what "is this lot on the road?"
        /// and "is this road on dry ground?" both ask.</summary>
        public static float DistanceToRoute(Vector2[] route, Vector2 point)
        {
            if (route == null || route.Length == 0) return float.MaxValue;
            if (route.Length == 1) return Vector2.Distance(route[0], point);
            float best = float.MaxValue;
            for (int i = 0; i < route.Length - 1; i++)
            {
                float d = MainlandTidalTerrain.DistanceToSegment(point, route[i], route[i + 1]);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>
        /// Build the region's terrain exactly as the scene carries it — the ONE place the plan above is
        /// turned into a height field, shared by the builder's push and by every test, so a test can
        /// never assert a terrain the scene does not have.
        /// </summary>
        public static void ConfigureTerrain(MainlandTidalTerrain terrain)
        {
            if (terrain == null) return;
            terrain.Configure(
                BayFloorElevation,
                CoastPoints, CoastSectors, CoastFeatherMetres,
                LandElevation, ShoreFalloff,
                ShelfWidth, ShelfInnerElevation, ShelfOuterElevation,
                CliffPlungeWidth, CliffToeTideFraction,
                LedgeBenchTideFraction, LedgeBenchWidth,
                TideMean, TideAmplitude,
                BarFrom, BarTo, BarHalfWidth, BarCrestElevation,
                BarFlankToeElevation, BarFlankFalloff,
                GutAlong, GutHalfWidth, GutBedElevation,
                Carves, Fills);
        }
    }
}
#endif
