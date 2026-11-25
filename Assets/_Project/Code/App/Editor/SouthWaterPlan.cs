#if UNITY_EDITOR
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// The South Water: one large open-water region south of Rice Point, St Peters and the East Water.
    /// It is a plan (this file) and a call to the shared water builder, like the East Water.
    ///
    /// <para><b>The bed.</b> Three things, all data here and nothing hand-placed:
    /// a shelf about twenty metres down along the whole north edge; the deep floor at trawling depth
    /// further out; and one bank that rises off the deep floor. At each of the three northern
    /// approaches the bed comes up to the NEIGHBOUR'S own floor, reached for by name where the
    /// neighbour has a constant, so no seam can step (the seam audit's elevation law).</para>
    ///
    /// <para><b>Tide and weather.</b> The island's tide verbatim, and the one deterministic sim for
    /// wind and sea state. This region has no weather of its own.</para>
    /// </summary>
    public static class SouthWaterPlan
    {
        public const string RegionId = "region.south_water";
        public const string RegionAssetName = "SouthWater";
        public const string SceneName = "SouthWater";
        public const string PlaceholderDisplayName = "The South Water (working name)";
        public const string Description =
            "The open water south of Rice Point, St Peters and the East Water: a shelf twenty metres " +
            "down under the north edge, then the bottom falls away to the deep mud where the draggers " +
            "work. Nothing out here to hit, and nothing to hide behind when it blows. NAME OWED: the " +
            "owner names the water scenes in PEI-variant style (world-map-plan 7 Q4); the id is stable " +
            "whatever he picks.";

        // ---- extent ---------------------------------------------------------------------------------
        public static readonly Vector2 RegionWorldCenter = new Vector2(0f, 0f);
        /// <summary>Three neighbours lie along the north edge, each 760 m wide, so this is far wider
        /// than an inshore scene.</summary>
        public static readonly Vector2 RegionWorldSize = new Vector2(2400f, 1200f);
        /// <summary>Half a texel a metre: the bed has no feature narrower than tens of metres, and the
        /// height bake stays at 1200 texels instead of 2400.</summary>
        public const float SeabedPixelsPerMetre = 0.5f;

        public static float NorthEdgeY => RegionWorldCenter.y + RegionWorldSize.y * 0.5f;

        // ---- tide: the island's, verbatim -----------------------------------------------------------
        public static float TideMean => StPetersBuilder.TideMean;
        public static float TideAmplitude => StPetersBuilder.TideAmplitude;
        public static float TidePhaseHours => StPetersBuilder.TidePhaseHours;
        public static float SpringLowWater => TideMean - TideAmplitude;

        // ---- the bed --------------------------------------------------------------------------------
        /// <summary>The deep mud, the draggers' water. Inside the Deep depth band (30 to 90 m).</summary>
        public const float DeepFloorElevation = -70f;
        /// <summary>The shelf under the north edge.</summary>
        public const float ShelfElevation = -20f;
        /// <summary>How far south of the north edge the shelf lies flat.</summary>
        public const float ShelfWidthMetres = 120f;
        /// <summary>How far the shelf's edge takes to fall to the deep floor.</summary>
        public const float ShelfFalloffMetres = 320f;

        /// <summary>The bank: hard ground standing up off the mud.</summary>
        public const float BankElevation = -35f;
        public static readonly Vector2 BankCenter = new Vector2(-500f, -150f);
        public static readonly Vector2 BankHalfSize = new Vector2(160f, 90f);
        public const float BankFalloffMetres = 150f;

        // ---- the three northern approaches ----------------------------------------------------------
        /// <summary>Where along the north edge each neighbour's water meets this region (x, metres).
        /// West to east as the neighbours lie: Rice Point, St Peters, the East Water.</summary>
        public const float RicePointApproachX = -900f;
        public const float StPetersApproachX = 250f;
        public const float EastWaterApproachX = 950f;

        /// <summary>The bed of the trawler channel Rice Point is dredging to its south edge. Rice
        /// Point has no constant for it on main yet; when it does, this reaches for that instead.</summary>
        public const float RicePointChannelBedElevation = -7f;
        public static float StPetersFloorElevation => StPetersBuilder.DeepHarbourElevation;
        public static float EastWaterFloorElevation => EastWaterPlan.BayFloorElevation;

        /// <summary>Each approach is a flat apron at the neighbour's floor, this wide and this deep
        /// from the north edge, falling away over <see cref="ApproachFalloffMetres"/>.</summary>
        public static readonly Vector2 ApproachHalfSize = new Vector2(130f, 40f);
        public const float ApproachFalloffMetres = 90f;

        public const float DoorInsetMetres = WestWaterPlan.DoorInsetMetres;
        public const float ArrivalClearanceMetres = WestWaterPlan.ArrivalClearanceMetres;

        static RectTidalTerrain.LandZone Approach(float x, float floor) =>
            new RectTidalTerrain.LandZone(new Vector2(x, NorthEdgeY), ApproachHalfSize, floor, ApproachFalloffMetres);

        /// <summary>The whole bed. The highest feature wins at any point.</summary>
        public static RectTidalTerrain.LandZone[] Zones => new[]
        {
            new RectTidalTerrain.LandZone(new Vector2(RegionWorldCenter.x, NorthEdgeY),
                new Vector2(RegionWorldSize.x * 0.5f, ShelfWidthMetres), ShelfElevation, ShelfFalloffMetres),
            new RectTidalTerrain.LandZone(BankCenter, BankHalfSize, BankElevation, BankFalloffMetres),
            Approach(RicePointApproachX, RicePointChannelBedElevation),
            Approach(StPetersApproachX, StPetersFloorElevation),
            Approach(EastWaterApproachX, EastWaterFloorElevation),
        };

        public static float ElevationAt(Vector2 worldPos) =>
            RectTidalTerrain.ElevationAtZones(worldPos, DeepFloorElevation, Zones);
        public static float ElevationAt(Vector3 worldPos) => ElevationAt(new Vector2(worldPos.x, worldPos.y));

        /// <summary>Still water over a point at the lowest tide of the year.</summary>
        public static float SpringLowDepthAt(Vector2 worldPos) => SpringLowWater - ElevationAt(worldPos);

        // ---- the three north seams ------------------------------------------------------------------
        // This region's halves only. Each door lies along the north edge, inset as every sea door is,
        // across the whole flat apron of its approach; each arrival is the island's clearance inside.
        // The matching exits live in the neighbours' scenes and are wired by THEIR builders.

        public const string RicePointRegionAssetName = "RicePoint";
        public const string StPetersRegionAssetName = "StPeters";

        /// <summary>The key every door here sends: the name the neighbour's anchor must resolve.</summary>
        public const string ArrivalKeySentToNeighbours = "south_water";
        /// <summary>The keys the neighbours' exits name when they send a boat here.</summary>
        public const string FromRicePointArrivalKey = "rice_point";
        public const string FromStPetersArrivalKey = "st_peters";
        public const string FromEastWaterArrivalKey = "east_water";

        public const string ToRicePointPassageName = "PassageToRicePoint";
        public const string ToStPetersPassageName = "PassageToStPeters";
        public const string ToEastWaterPassageName = "PassageToEastWater";

        /// <summary>A door's band: the apron's full width along the edge, the shared depth across it.</summary>
        public static Vector2 PassageBandSize =>
            new Vector2(ApproachHalfSize.x * 2f, Plan.EdgeBandDepthMetres);

        static Vector3 DoorAt(float x) => new Vector3(x, NorthEdgeY - DoorInsetMetres, 0f);
        static Vector3 ArrivalAt(float x) =>
            new Vector3(x, NorthEdgeY - DoorInsetMetres - ArrivalClearanceMetres, 0f);

        public static Vector3 ToRicePointPassagePos => DoorAt(RicePointApproachX);
        public static Vector3 ToStPetersPassagePos => DoorAt(StPetersApproachX);
        public static Vector3 ToEastWaterPassagePos => DoorAt(EastWaterApproachX);
        public static Vector3 FromRicePointArrivalPos => ArrivalAt(RicePointApproachX);
        public static Vector3 FromStPetersArrivalPos => ArrivalAt(StPetersApproachX);
        public static Vector3 FromEastWaterArrivalPos => ArrivalAt(EastWaterApproachX);

        public static (string Key, Vector3 Position)[] NamedArrivals => new[]
        {
            (FromRicePointArrivalKey, FromRicePointArrivalPos),
            (FromStPetersArrivalKey, FromStPetersArrivalPos),
            (FromEastWaterArrivalKey, FromEastWaterArrivalPos),
        };

        public static (string Name, Vector3 Position, string RegionAssetPath, string ArrivalKey)[] Passages => new[]
        {
            (ToRicePointPassageName, ToRicePointPassagePos,
                WaterSceneTemplate.RegionAssetPathFor(RicePointRegionAssetName), ArrivalKeySentToNeighbours),
            (ToStPetersPassageName, ToStPetersPassagePos,
                WaterSceneTemplate.RegionAssetPathFor(StPetersRegionAssetName), ArrivalKeySentToNeighbours),
            (ToEastWaterPassageName, ToEastWaterPassagePos,
                WaterSceneTemplate.RegionAssetPathFor(EastWaterPlan.RegionAssetName), ArrivalKeySentToNeighbours),
        };

        // ---- where a boat is put down ---------------------------------------------------------------
        /// <summary>Inside the St Peters approach, clear of the edge: the place a boat lies when the
        /// region is opened without a named arrival.</summary>
        public static Vector3 DefaultArrivalPos => FromStPetersArrivalPos;

        public static WaterScenePlan Plan => new WaterScenePlan
        {
            RegionId = RegionId,
            RegionAssetName = RegionAssetName,
            DisplayName = PlaceholderDisplayName,
            SceneName = SceneName,
            Description = Description,
            UnlockFlag = "",
            WorldCenter = RegionWorldCenter,
            WorldSize = RegionWorldSize,
            SeabedPixelsPerMetre = SeabedPixelsPerMetre,
            TideMean = TideMean,
            TideAmplitude = TideAmplitude,
            TidePhaseHours = TidePhaseHours,
            NominalDepthMetres = -ShelfElevation,
            DeepElevation = DeepFloorElevation,
            Zones = Zones,
            CameraWorldHeightMetres = CameraFollow.DefaultWorldHeightMeters,
            BackdropColor = new Color(0.08f, 0.16f, 0.24f),
            SpawnFishIds = new string[0],
        };
    }
}
#endif
