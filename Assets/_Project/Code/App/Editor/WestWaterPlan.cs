#if UNITY_EDITOR
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The authored geography of THE WEST WATER</b> — the bay's first open-water scene, and the first
    /// sail (world-map-plan §2.2 / §6 step 1). One place where the region's rectangle, its tide, its bed,
    /// its two doors and its arrival points are written down, so the builder that pushes them into the
    /// scene and the EditMode tests that check them read the SAME numbers (the
    /// <see cref="NineMileCreekMainland"/> convention: a single source of truth, not a builder with a
    /// comment and a test with a literal).
    ///
    /// <para><b>⚠ THE NAME IS OWED.</b> The owner rules names in PEI-variant style and has not named the
    /// three water scenes yet (world-map-plan §5 and §7 Q4: "names owed when the scenes are built"). The
    /// id <c>region.west_water</c> is append-only and stable whatever he picks (ADR 0009) — ids are code
    /// and saves, display names are a seam — so the region ships with a DisplayName that says out loud
    /// that it is a working name. Changing it later is one string on one asset and nothing else.</para>
    ///
    /// <para><b>The frame.</b> Region-local metres, centre at the origin, and it is water end to end.
    /// <b>St Peters is EAST</b> (+x), <b>Nine Mile Creek is WEST</b> (−x), and <b>the bar is the SOUTH
    /// boundary</b> (−y): the run north of the crossing, which is exactly the water the plan describes —
    /// "the bar, St Peters' lee, the run up to Nine Mile Creek". Regions do not share a coordinate frame
    /// (a seam is a passage band and an arrival point, never a shared line), so this frame owes nothing
    /// to its neighbours' — only its SEAM ELEVATIONS do, and those are reached for below rather than
    /// re-typed.</para>
    ///
    /// <para><b>What this scene deliberately does NOT have.</b> No painted seabed: open water is deep
    /// enough that the bottom is never visible, and paint is a look while data is the world (owner's
    /// ruling 2026-08-07, world-map-plan §2.2). No hazard: the west water is ruled "sheltered, forgiving,
    /// the dory's water" and the bay's standing hazard is Governors Island, which belongs to the mid-bay
    /// (§2.2, §6 step 4). No spawn table: the home grounds are the mid-bay's too. Building any of those
    /// here would be a later phase smuggled into this one (rule 8).</para>
    /// </summary>
    public static class WestWaterPlan
    {
        // =================================================================================================
        //  1. IDENTITY — the id is forever, the name is on loan
        // =================================================================================================

        /// <summary>Stable, append-only region id (ADR 0009). Saves and unlock state key off it, so it
        /// must outlive whatever the owner ends up calling the place.</summary>
        public const string RegionId = "region.west_water";

        /// <summary>File name of the committed <see cref="RegionDef"/> under <c>Data/Regions</c>.</summary>
        public const string RegionAssetName = "WestWater";

        /// <summary>Scene asset name (no path/extension), registered in Build Settings by the builder.</summary>
        public const string SceneName = "WestWater";

        /// <summary>⚠ PLACEHOLDER, and it says so on the card. The owner owes this region a real
        /// PEI-variant name (world-map-plan §7 Q4); until then the parenthetical is the marker that this
        /// string is data awaiting a decision rather than a name anybody chose.</summary>
        public const string PlaceholderDisplayName = "The West Water (working name)";

        // ASCII on purpose: this string is DATA that round-trips through the committed .asset's YAML, and
        // the committed copy and this one have to be comparable by eye. The typography lives in the
        // comments, where nothing serialises it.
        public const string Description =
            "The sheltered run between the island and the mainland, north of the tidal bar: St Peters' " +
            "lee at one end, the reach up to Nine Mile Creek at the other. The dory's water - the first " +
            "sail, and the one crossing you can make without waiting on the tide. NAME OWED: the owner " +
            "names the three water scenes in PEI-variant style (world-map-plan 7 Q4); the id is stable " +
            "whatever he picks.";

        // =================================================================================================
        //  2. THE REGION RECTANGLE — sized by TIME TO CROSS in the dory
        // =================================================================================================
        // 760 × 520 m, and both numbers are St Peters' own — the same water, so the same scale.
        //
        // THE SIZING, per scene-sizing §1.3 ("inshore water: 3–5 min in the gating boat"). The gating boat
        // is the DORY, which is the whole point of this region: it is the first hull the player owns and
        // the one this water was ruled forgiving for. Rowed terminal speed 3.00 m/s (scene-sizing §1.2):
        //
        //   · edge to edge, the long axis      760 m ÷ 3.00 m/s = 4:13   ← inside the 3–5 min band
        //   · arrival to the far door          672 m ÷ 3.00 m/s = 3:44   ← the sail as actually played
        //   · edge to edge, the short axis     520 m ÷ 3.00 m/s = 2:53
        //
        // ⭐ AND IT WANTS TO BE READ AGAINST THE WALK, because this region's job is to be the alternative
        // to one. The crossing on foot is 610 m of bared flats (305 m each side of the St Peters ↔ Nine
        // Mile Creek seam) — 3:23 at the 3.0 m/s walk, and only when the tide lets you. The sail is 3:44
        // and always open. Going round is slightly the longer trip and always available, which is the
        // right shape for both: the bar stays the dramatic route (P1, P5) and the boat stays the freedom
        // (P2). A shorter water scene would have made the dory strictly better than the tide.
        public static readonly Vector2 RegionWorldCenter = new Vector2(0f, 0f);
        public static readonly Vector2 RegionWorldSize   = new Vector2(760f, 520f);

        /// <summary>
        /// 1 px/m — the ruled OFFSHORE figure (scene-sizing §6.1), against St Peters' and Nine Mile
        /// Creek's 2 px/m inshore.
        ///
        /// <para>The west water paints NO seabed (world-map-plan §2.2), so this buys no texture here; it
        /// is the region's height-data resolution, and the coarser figure is right because there is
        /// nothing fine to resolve — a bed that runs from −6 m to −4 m across 760 m has no cave mouth or
        /// cove edge for a 0.5 m texel to catch. It is also what removes the paint-resolution ceiling
        /// from open-water scene size entirely, which is the ruling's other half.</para>
        /// </summary>
        public const float SeabedPixelsPerMetre = 1f;

        // =================================================================================================
        //  3. THE TIDE — not chosen here, INHERITED
        // =================================================================================================
        // ⚠ world-map-plan §4.3: adjacent regions sharing terrain must share a tide profile. This region
        // touches BOTH land scenes and shares the bar's shoulder with them, so it has no freedom at all —
        // it takes their profile, and takes it by REFERENCE so a retune of the crossing moves all three
        // together. St Peters is the source because Nine Mile Creek already copies it verbatim for the
        // same reason (NineMileCreekMainland §2: "the tidal bar SPANS the seam"), and a test holds the
        // three equal so the day somebody re-tunes one, the other two are not left behind.

        public static float TideMean => StPetersBuilder.TideMean;
        public static float TideAmplitude => StPetersBuilder.TideAmplitude;
        public static float TidePhaseHours => StPetersBuilder.TidePhaseHours;

        /// <summary>Spring low water — the tide at which this bed is shallowest. Derived, so it follows a
        /// retune.</summary>
        public static float SpringLowWater => TideMean - TideAmplitude;

        // =================================================================================================
        //  4. THE BED — full height data everywhere, and every seam number borrowed
        // =================================================================================================
        // ⭐ THE ONE-HEIGHT-MAP LAW, offshore. The owner ruled that open-water scenes carry no painted
        // seabed but DO carry height data everywhere, because the depth sounder and the fish finder read
        // real depth (world-map-plan §2.2). So the bed is analytic — RectTidalTerrain's deep floor plus
        // max-composing plateaus — and it is a real bed, not a placeholder: it shoals two metres from the
        // mainland side to the island side, and the bar's shoulder is its southern boundary.
        //
        // ⭐ EVERY NUMBER BELOW THAT A NEIGHBOUR ALSO KNOWS IS THE NEIGHBOUR'S OWN. Not a copy of it — the
        // constant itself, reached through the neighbouring plan. This is the crossing's lesson applied a
        // second time: Nine Mile Creek's half of the bar had to borrow St Peters' shoulder elevation or
        // the flats would have narrowed by a quarter the instant you crossed the load. A water scene
        // touching both of them has the same exposure at twice the seams, and a re-typed −4 that drifts
        // later would put a step in the seabed exactly where the depth sounder is watching.

        /// <summary>The floor at the Nine Mile Creek end: <b>the mainland's own bay floor</b>. Nothing
        /// grounds out here at any tide — spring low is −2.2 m, and this is 3.8 m below that.</summary>
        public static float BayFloorElevation => NineMileCreekMainland.BayFloorElevation;     // −6 m

        /// <summary>The floor at the St Peters end: <b>the island's own deep-harbour floor</b>. The bed
        /// shoals to meet it, which is what "St Peters' lee" means as bathymetry — you are coming up onto
        /// the shelf the island stands on.</summary>
        public static float LeeFloorElevation => StPetersBuilder.DeepHarbourElevation;        // −4 m

        /// <summary>The bar's SHOULDER — what the crossing's flanks fall to at their half-width, and
        /// therefore what this region's southern boundary IS. <b>The mainland's constant, which is itself
        /// St Peters' deep floor</b>, borrowed for exactly the reason its own docstring gives.</summary>
        public static float BarShoulderElevation => NineMileCreekMainland.BarFlankToeElevation;   // −4 m

        /// <summary>How far (m) the bar's shoulder takes to fall away to the bay floor — the mainland's
        /// own flank falloff, so the shoulder this region shows and the shoulder the crossing has are the
        /// same slope.</summary>
        public static float BarShoulderFalloff => NineMileCreekMainland.BarFlankFalloff;      // 40 m

        /// <summary>How far (m) into the region the bar's shoulder reaches from the south edge, before it
        /// starts falling away. The bar's own half-width, so the shoulder is as wide here as it is where
        /// you walk it.</summary>
        public static float BarShoulderReach => NineMileCreekMainland.BarHalfWidth;           // 30 m

        /// <summary>How far (m) west of the east edge the island's shelf stays flat before it begins to
        /// fall away toward the mainland's deeper bay floor. Wide enough that the door at the east edge
        /// stands squarely ON the shelf rather than on its slope, which is what makes the seam elevation
        /// exact rather than nearly right.</summary>
        public const float LeeShelfHalfWidth = 60f;

        /// <summary>Over how many metres the shelf falls the two metres from the island's floor to the
        /// mainland's. A long, quiet ramp — this is a bay bottom, not a bank: 2 m over 160 m is a 1.25%
        /// grade, which reads as a shoaling colour on the sounder and never as an obstacle.</summary>
        public const float LeeShelfFalloff = 160f;

        /// <summary>
        /// The bed, as zones over <see cref="BayFloorElevation"/>. Two features and no more, both of them
        /// a neighbour's fact rather than an invention of this region's:
        /// <list type="number">
        /// <item><description><b>the lee shelf</b> along the east edge, at the island's own floor;</description></item>
        /// <item><description><b>the bar's shoulder</b> along the south edge, at the crossing's own flank toe.</description></item>
        /// </list>
        /// Both span the full width of the edge they sit on, because both are the edge — a shoal that
        /// stopped short would put a step in the seabed where the region simply ends.
        /// </summary>
        public static RectTidalTerrain.LandZone[] Zones => new[]
        {
            // The lee shelf: the east edge, full height, shoaling west into the bay.
            new RectTidalTerrain.LandZone(
                new Vector2(RegionWorldCenter.x + RegionWorldSize.x * 0.5f, RegionWorldCenter.y),
                new Vector2(LeeShelfHalfWidth, RegionWorldSize.y * 0.5f),
                LeeFloorElevation, LeeShelfFalloff),

            // The bar's shoulder: the south edge, full width, falling away north at the bar's own slope.
            new RectTidalTerrain.LandZone(
                new Vector2(RegionWorldCenter.x, RegionWorldCenter.y - RegionWorldSize.y * 0.5f),
                new Vector2(RegionWorldSize.x * 0.5f, BarShoulderReach),
                BarShoulderElevation, BarShoulderFalloff),
        };

        /// <summary>The seabed this region authors at a world position — the same pure composition the
        /// scene's <see cref="RectTidalTerrain"/> evaluates at run time.</summary>
        public static float ElevationAt(Vector2 worldPos) =>
            RectTidalTerrain.ElevationAtZones(worldPos, BayFloorElevation, Zones);

        /// <inheritdoc cref="ElevationAt(Vector2)"/>
        public static float ElevationAt(Vector3 worldPos) => ElevationAt(new Vector2(worldPos.x, worldPos.y));

        // =================================================================================================
        //  5. THE TWO DOORS — and the arrivals on the far side of each
        // =================================================================================================
        // ⭐ IN OPEN WATER THE REGION'S EDGE IS THE DOOR, and that is a difference from both land regions
        // rather than a convenience. A bar or a channel steers a hull toward a passage band; nothing out
        // here does. So each band spans the full height of the region (WaterScenePlan.EdgeBandSize) and
        // sits 24 m inside its edge — sail off the east side and you are in St Peters, sail off the west
        // side and you are at Nine Mile Creek, with no line to find. A band you can miss by holding forty
        // metres north of it is a crossing that silently does not fire, which is the worst failure a seam
        // has; RegionPassage's leave-then-enter latch is what makes a generous band cost nothing.
        //
        // ⚠ Both bands sit in FEATURELESS water clear of anything (world-map-plan §4.4): the doors are on
        // the mid-line, the bar's shoulder is 220 m south of them, and the load fires with nothing
        // happening — the same reason the crossing's own seam sits at the bar's midpoint rather than at
        // the landing.

        /// <summary>How far (m) inside its edge a door sits — enough that a band's near face is still
        /// inside the region and the camera's bounds clamp has something to hold.</summary>
        public const float DoorInsetMetres = 24f;

        /// <summary>How far (m) an arrival point stands clear of the door it came through. The boat is
        /// stopped on arrival, so this is not braking distance — it is the margin that keeps a fisher who
        /// has just landed from being inside the band they arrived by.</summary>
        public const float ArrivalClearanceMetres = 40f;

        /// <summary>Sail east across this and you are at St Peters. On the mid-line, on the lee shelf.</summary>
        public static Vector3 ToStPetersPassagePos =>
            new Vector3(RegionWorldCenter.x + RegionWorldSize.x * 0.5f - DoorInsetMetres,
                        RegionWorldCenter.y, 0f);

        /// <summary>Sail west across this and you are at Nine Mile Creek. On the mid-line, over the bay
        /// floor.</summary>
        public static Vector3 ToNineMileCreekPassagePos =>
            new Vector3(RegionWorldCenter.x - RegionWorldSize.x * 0.5f + DoorInsetMetres,
                        RegionWorldCenter.y, 0f);

        // --- the names the NEIGHBOURS ask for when they send a boat in here (#456) --------------------
        // A passage names WHICH WAY IN it lands at, and the destination's RegionAnchor resolves the name
        // against its own table. This region is the first with two doors of equal standing — neither is a
        // "wharf you sail into" the other falls back to — so BOTH are named, and the day the mid-bay adds
        // a third door it adds a third name rather than re-pointing one of these.
        //
        // ⚠ The MATCHING halves are passages in the neighbours' scenes pointing here with these keys, and
        // they are wired by the NEIGHBOURS' builders (StPetersBuilder / NineMileCreekBuilder), never by
        // this one: a region builder may not reach into another region's scene.

        /// <summary>The key St Peters' passage names when it sends a boat west into this region.</summary>
        public const string FromStPetersArrivalKey = "st_peters";

        /// <summary>The key Nine Mile Creek's passage names when it sends a boat east into this region.</summary>
        public const string FromNineMileCreekArrivalKey = "nine_mile_creek";

        /// <summary>Where the boat is parked arriving FROM St Peters — inside the east door, heading
        /// west, with the whole run ahead of her.</summary>
        public static Vector3 FromStPetersArrivalPos =>
            ToStPetersPassagePos + new Vector3(-ArrivalClearanceMetres, 0f, 0f);

        /// <summary>Where the boat is parked arriving FROM Nine Mile Creek — inside the west door,
        /// heading east.</summary>
        public static Vector3 FromNineMileCreekArrivalPos =>
            ToNineMileCreekPassagePos + new Vector3(ArrivalClearanceMetres, 0f, 0f);

        /// <summary>
        /// Where a KEYLESS passage lands — the Nine Mile Creek end.
        ///
        /// <para>It is the front door because that is where the arc starts: the dory is bought and
        /// repaired at Nine Mile Creek and rowed out from there, so a region that has not yet named its
        /// way in should land a fisher at the beginning of the run rather than the end of it. Nothing
        /// currently relies on the fallback — both neighbours name their doors — which is exactly when a
        /// fallback should be chosen, rather than the first time something needs it.</para>
        /// </summary>
        public static Vector3 DefaultArrivalPos => FromNineMileCreekArrivalPos;

        // =================================================================================================
        //  6. THE PLAN, ASSEMBLED
        // =================================================================================================

        /// <summary>
        /// Everything above, in the shape <see cref="WaterSceneTemplate"/> takes. The builder and every
        /// EditMode test go through HERE, so a test can never assert a region the scene does not have.
        /// </summary>
        public static WaterScenePlan Plan => new WaterScenePlan
        {
            RegionId = RegionId,
            RegionAssetName = RegionAssetName,
            DisplayName = PlaceholderDisplayName,
            SceneName = SceneName,
            Description = Description,
            UnlockFlag = "",                       // the first sail — always reachable, no gate

            WorldCenter = RegionWorldCenter,
            WorldSize = RegionWorldSize,
            SeabedPixelsPerMetre = SeabedPixelsPerMetre,

            TideMean = TideMean,
            TideAmplitude = TideAmplitude,
            TidePhaseHours = TidePhaseHours,
            NominalDepthMetres = -BayFloorElevation,

            DeepElevation = BayFloorElevation,
            Zones = Zones,

            // Reviewed from the boat this water was ruled for, not on foot.
            CameraWorldHeightMetres = CameraFollow.DefaultWorldHeightMeters,
            BackdropColor = new Color(0.13f, 0.24f, 0.31f),

            // ⚠ EMPTY ON PURPOSE. The home grounds — fish, mussels, lobster, crab — are the MID-BAY's
            // (world-map-plan §2.2), and the mussel fishery arrives with it at §6 step 4. The west water
            // is the sheltered run, and filling it with a fishery now would be a later phase smuggled
            // into this one (rule 8).
            SpawnFishIds = new string[0],
        };
    }
}
#endif
