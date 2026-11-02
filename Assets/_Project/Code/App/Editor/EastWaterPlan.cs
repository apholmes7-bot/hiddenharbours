#if UNITY_EDITOR
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The authored geography of THE EAST WATER</b> — the open water off St Peters' east wall, and
    /// the sea the game will OPEN on. The owner ruled the intro out here (2026-09-06): you start on
    /// deck with the captain at six in the morning, you are told the tradition, you fish, and then you
    /// sail west into St Peters. He also ruled that it is not a cutscene set — <i>"a fully functional
    /// and returnable scene to the east that lines up with St Peters' east wall"</i> — so it is a real
    /// region with a real door, built in the <see cref="WestWaterPlan"/> shape.
    ///
    /// <para><b>The frame.</b> Region-local metres, centre at the origin, water end to end.
    /// <b>St Peters is WEST</b> (−x) and <b>the open sea is EAST</b> (+x) — the mirror of the west
    /// water, which has St Peters at +x. Regions do not share a coordinate frame (a seam is a passage
    /// band and an arrival point, never a shared line), so this frame owes its neighbour nothing except
    /// its SEAM ELEVATION, and that one is reached for rather than re-typed.</para>
    ///
    /// <para>The intro-at-sea charter's Decision 8 keeps the bed flat at St Peters' own −4 m.
    /// Fish eligibility is data on FishSpeciesDef.RegionIds; <see cref="RegionDef.SpawnFishIds"/>
    /// stays empty. The scene is built by <see cref="EastWaterBuilder"/> and committed with its
    /// Unity-written meta (ADR 0011). The opening itself arrives in a later PR.</para>
    /// </summary>
    public static class EastWaterPlan
    {
        // =================================================================================================
        //  1. IDENTITY — the id is forever, the name is on loan
        // =================================================================================================

        /// <summary>Stable, append-only region id (ADR 0009). Saves and the intro's own state key off
        /// it, so it must outlive whatever the owner ends up calling the place.</summary>
        public const string RegionId = "region.east_water";

        /// <summary>File name of the committed <see cref="RegionDef"/> under <c>Data/Regions</c>.</summary>
        public const string RegionAssetName = "EastWater";

        /// <summary>Committed scene asset name (no path/extension), as requested by the loader.</summary>
        public const string SceneName = "EastWater";

        /// <summary>⚠ PLACEHOLDER, and it says so on the card — the same standing the west water's name
        /// has. The owner names the water scenes in PEI-variant style (world-map-plan §7 Q4).</summary>
        public const string PlaceholderDisplayName = "The East Water (working name)";

        // ASCII on purpose: this string is DATA that round-trips through the committed .asset's YAML, and
        // the committed copy and this one have to be comparable by eye. The typography lives in the
        // comments, where nothing serialises it.
        public const string Description =
            "The open water off St Peters' east wall, and where the game begins - out with the captain " +
            "at six in the morning, a rod stowed on her deck and the island a mile to the west. Sail " +
            "west and you are home; the sea the other way is the mid-bay's, and that is later. NAME " +
            "OWED: the owner names the water scenes in PEI-variant style (world-map-plan 7 Q4); the id " +
            "is stable whatever he picks.";

        // =================================================================================================
        //  2. THE REGION RECTANGLE — St Peters' own, because it is St Peters' own water
        // =================================================================================================
        // 760 × 520 m. Not sized afresh: this is the same bay at the same scale, and the west water took
        // these two numbers from the island for exactly that reason. The owner's word for what he wants
        // here was "just open water like the west water for now", and the cheapest way to be like the
        // west water is to be the same size as it.
        //
        // ⭐ AND THE INTRO IS WHAT IT HAS TO HOLD. The opening is a hull lying stopped in open water
        // while the player casts off her deck, and then a passage west. At the rowed 3.00 m/s the long
        // axis is 4:13 end to end; under the cape's own power the run from the arrival to the door is
        // shorter still. Nothing in the intro needs the far half of it — which is the point: the water
        // she can see herself sailing out into is water that is really there, and she can come back out
        // to it any time after (the owner's "returnable").
        public static readonly Vector2 RegionWorldCenter = new Vector2(0f, 0f);
        public static readonly Vector2 RegionWorldSize   = new Vector2(760f, 520f);

        /// <summary>1 px/m — the ruled OFFSHORE figure (scene-sizing §6.1). The east water paints no
        /// seabed, so this buys no texture; it is the region's height-data resolution, and the coarser
        /// figure is right because a flat floor has nothing fine to resolve.</summary>
        public const float SeabedPixelsPerMetre = 1f;

        // =================================================================================================
        //  3. THE TIDE — not chosen here, INHERITED
        // =================================================================================================
        // ⚠ world-map-plan §4.3: adjacent regions sharing terrain must share a tide profile. This region
        // touches St Peters along her whole east wall, so it has no freedom at all — it takes the
        // island's profile, and takes it BY REFERENCE so a retune moves both together. The west water
        // borrows the same three numbers from the same source for the same reason.

        public static float TideMean => StPetersBuilder.TideMean;
        public static float TideAmplitude => StPetersBuilder.TideAmplitude;
        public static float TidePhaseHours => StPetersBuilder.TidePhaseHours;

        /// <summary>Spring low water — the tide at which this bed is shallowest. Derived, so it follows a
        /// retune.</summary>
        public static float SpringLowWater => TideMean - TideAmplitude;

        // =================================================================================================
        //  4. THE BED — one elevation, and it is the neighbour's
        // =================================================================================================
        // ⭐ THE SEAM IS THE WHOLE BED. A water scene's job is to sit between places, and the one thing
        // it can get wrong that nothing else would catch is the depth it reports where it meets them:
        // sail across a seam whose two sides disagree and the sounder STEPS, a metre of seabed appearing
        // under a hull at the exact instant the player is watching an instrument. The west water learned
        // that at two seams. This region has ONE, and it answers it by being flat at the neighbour's own
        // number rather than by shoaling to meet it.
        //
        // Decision 8 was ruled 2026-10-29: this whole region keeps the flat 4 m bed. A shelf or
        // deep step needs its own later ruling; the seam continues to follow the neighbour.

        /// <summary>The floor, everywhere: <b>St Peters' own deep-harbour floor</b>, borrowed rather than
        /// copied. At the tide the two regions share this is 1.8 m of water at spring low — the honest
        /// figure the island's own entrance channel already declares for the open bay.</summary>
        public static float BayFloorElevation => StPetersBuilder.DeepHarbourElevation;      // −4 m

        /// <summary>
        /// The bed, as zones over <see cref="BayFloorElevation"/>. <b>Empty on purpose</b> — a flat
        /// floor at the neighbour's elevation is the only bed that makes the seam exact at every point
        /// of it rather than at the one point somebody measured. Decision 8 authors no shelf or step.
        /// </summary>
        public static RectTidalTerrain.LandZone[] Zones => System.Array.Empty<RectTidalTerrain.LandZone>();

        /// <summary>The seabed this region authors at a world position — the same pure composition the
        /// scene's <see cref="RectTidalTerrain"/> evaluates at run time.</summary>
        public static float ElevationAt(Vector2 worldPos) =>
            RectTidalTerrain.ElevationAtZones(worldPos, BayFloorElevation, Zones);

        /// <inheritdoc cref="ElevationAt(Vector2)"/>
        public static float ElevationAt(Vector3 worldPos) => ElevationAt(new Vector2(worldPos.x, worldPos.y));

        // =================================================================================================
        //  5. THE ONE DOOR — and the arrival on the far side of it
        // =================================================================================================
        // ⭐ IN OPEN WATER THE REGION'S EDGE IS THE DOOR. Nothing out here steers a hull toward a band
        // the way a bar or a channel does inshore, so the band spans the full height of the region
        // (WaterScenePlan.EdgeBandSize) and sits 24 m inside the west edge: sail west and you are at St
        // Peters, with no line to find. A band you can miss by holding forty metres north of it is a
        // crossing that silently does not fire, which is the worst failure a seam has.
        //
        // ⚠ ONE DOOR, NOT TWO. The west water has two of equal standing because it lies between two
        // places; this region lies between one place and the open sea, and the mid-bay that would put a
        // door on the east wall is a later phase (world-map-plan §6 step 4). So the west door is also
        // the DEFAULT arrival, and the day the mid-bay arrives it adds a name rather than re-pointing
        // this one.

        /// <summary>How far (m) inside its edge a door sits — enough that a band's near face is still
        /// inside the region and the camera's bounds clamp has something to hold. The west water's own
        /// figure, and the inset St Peters' sea doors already stand at.</summary>
        public const float DoorInsetMetres = WestWaterPlan.DoorInsetMetres;

        /// <summary>How far (m) an arrival point stands clear of the door it came through. The boat is
        /// stopped on arrival, so this is not braking distance — it is the margin that keeps a fisher
        /// who has just landed from being inside the band they arrived by. <b>One number for both sides
        /// of a sea door</b>: St Peters' own east arrival stands off by it too.</summary>
        public const float ArrivalClearanceMetres = WestWaterPlan.ArrivalClearanceMetres;

        /// <summary>Sail west across this and you are at St Peters. On the mid-line, over the island's
        /// own floor.</summary>
        public static Vector3 ToStPetersPassagePos =>
            new Vector3(RegionWorldCenter.x - RegionWorldSize.x * 0.5f + DoorInsetMetres,
                        RegionWorldCenter.y, 0f);

        // --- the name ST PETERS asks for when she sends a boat out here (#456) ------------------------
        // A passage names WHICH WAY IN it lands at, and the destination's RegionAnchor resolves the name
        // against its own table. The key names where you came FROM, which is why this region and the
        // west water both call their St Peters arrival the same thing and neither collides: a key is
        // resolved inside one region's table and nowhere else.
        //
        // ⚠ The MATCHING half is a passage in ST PETERS' scene pointing here with this key, and it is
        // wired by HER builder, never by this one: a region builder may not reach into another region's
        // scene.

        /// <summary>The key St Peters' east door names when it sends a boat east into this region.</summary>
        public const string FromStPetersArrivalKey = "st_peters";

        /// <summary>Where the boat is parked arriving FROM St Peters — inside the west door, heading
        /// east, with the open water ahead of her.</summary>
        public static Vector3 FromStPetersArrivalPos =>
            ToStPetersPassagePos + new Vector3(ArrivalClearanceMetres, 0f, 0f);

        /// <summary>Where a KEYLESS passage lands. The region has one door, so the default IS that door
        /// — stated rather than left implied, because the mid-bay will one day add a second and the
        /// fallback should already have been chosen by then.</summary>
        public static Vector3 DefaultArrivalPos => FromStPetersArrivalPos;

        /// <summary>Candidate lying point for the opening's cape, in region-local metres. It leaves
        /// 136 m to the west door and room to fish clear of its trigger. PR 2 reaches for this point;
        /// the ordinary seed/time/weather school model determines which water holds fish.</summary>
        public static readonly Vector3 OpeningLiePos = new Vector3(-220f, 0f, 0f);
        /// <summary>The fresh-game way in, distinct from an ordinary boat arriving from St Peters.</summary>
        public const string OpeningArrivalKey = "opening";
        /// <summary>A steering target beyond the west band, so the sea leg never brakes at the door.</summary>
        public static Vector2 OpeningExitTarget =>
            (Vector2)ToStPetersPassagePos - Vector2.right * ArrivalClearanceMetres;

        // =================================================================================================
        //  6. THE PLAN, ASSEMBLED
        // =================================================================================================

        /// <summary>
        /// Everything above, in the shape <see cref="WaterSceneTemplate"/> takes. The builder, the
        /// committed <see cref="RegionDef"/> and every EditMode test go through HERE, so a test can
        /// never assert a region the scene does not have.
        /// </summary>
        public static WaterScenePlan Plan => new WaterScenePlan
        {
            RegionId = RegionId,
            RegionAssetName = RegionAssetName,
            DisplayName = PlaceholderDisplayName,
            SceneName = SceneName,
            Description = Description,
            UnlockFlag = "",                       // the game OPENS here — nothing could gate it

            WorldCenter = RegionWorldCenter,
            WorldSize = RegionWorldSize,
            SeabedPixelsPerMetre = SeabedPixelsPerMetre,

            TideMean = TideMean,
            TideAmplitude = TideAmplitude,
            TidePhaseHours = TidePhaseHours,
            NominalDepthMetres = -BayFloorElevation,

            DeepElevation = BayFloorElevation,
            Zones = Zones,

            // Reviewed from the boat, like the water it copies.
            CameraWorldHeightMetres = CameraFollow.DefaultWorldHeightMeters,
            BackdropColor = new Color(0.13f, 0.24f, 0.31f),

            // The live pool is the species assets' RegionIds, shared by schools and catches.
            SpawnFishIds = new string[0],
        };
    }
}
#endif
