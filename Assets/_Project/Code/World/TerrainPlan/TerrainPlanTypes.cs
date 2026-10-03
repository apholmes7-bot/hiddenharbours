using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The ground materials the terrain plan paints, in the TerrainSplat shader's slot order: the twenty
    /// slots of <c>_SplatA</c> to <c>_SplatE</c> (four channels each), then <c>path</c>, which is
    /// <c>_SplatF</c>'s red channel. A zone's index is its slot. This is the shader's contract, not content.
    /// </summary>
    public static class TerrainPlanZones
    {
        public static readonly string[] Names =
        {
            "grass", "marram", "sand", "shingle", "ripple", "shelf", "silt", "dirt", "marsh", "sedge",
            "foreshore", "talus", "ledge", "rockweed", "musselbed", "oysterreef", "eelgrass", "irishmoss", "lawn", "mud",
            "path",
        };

        /// <summary>The slot count in the five RGBA splat maps A to E.</summary>
        public const int SplatSlots = 20;

        /// <summary>The <c>path</c> slot: <c>_SplatF</c>'s red channel.</summary>
        public const int Path = 20;

        /// <summary>A cell no rule paints: the shader's own height bands show there.</summary>
        public const byte Unpainted = 255;

        /// <summary>The zone's slot, or -1 when the name is not one of <see cref="Names"/>.</summary>
        public static int IndexOf(string zone)
        {
            for (int i = 0; i < Names.Length; i++)
                if (Names[i] == zone) return i;
            return -1;
        }
    }

    /// <summary>A recipe band: below <see cref="Below"/> metres (first match from below), paint <see cref="Zone"/>.</summary>
    [Serializable]
    public struct RecipeBand
    {
        public float Below;
        public string Zone;

        public RecipeBand(float below, string zone)
        {
            Below = below;
            Zone = zone;
        }
    }

    /// <summary>A point of the shore reference line. A point that starts a section names it.</summary>
    [Serializable]
    public struct ShorePoint
    {
        public Vector2 At;
        [Tooltip("The CoastSectionDef id this point starts, or empty.")]
        public string SectionId;
    }

    /// <summary>
    /// A key scene's frozen piece (§4.1 tier 2): its ground holds part 1's plan heights within
    /// <see cref="Radius"/>, feathered over <see cref="Feather"/>.
    /// </summary>
    [Serializable]
    public struct FrozenPiece
    {
        [Tooltip("The piece's id in the plan (its key scene's id).")]
        public string Id;
        [Tooltip("The key scene's file the piece comes from.")]
        public string Source;
        public Vector2 At;
        public float Radius;
        public float Feather;
    }

    /// <summary>A key scene's frozen box (§4.1 tier 2), such as a dooryard: part 1's plan heights inside it.</summary>
    [Serializable]
    public struct FrozenBox
    {
        public string Id;
        public string Source;
        public Vector2 Min;
        public Vector2 Max;
        public float Feather;
    }

    /// <summary>How a coast section changes the ground.</summary>
    public enum CoastSectionMode
    {
        /// <summary>Raises the sea side only: land keeps max(profile, today).</summary>
        Fill = 0,
        /// <summary>Lays the profile on both sides of the line.</summary>
        Cut = 1,
        /// <summary>Keeps the ground and repaints it: a polygon, or a bearing sector below a height.</summary>
        PaintOnly = 2,
        /// <summary>The bar's crossing (part 2 §5): runnels, lobes, pools and its own paint.</summary>
        Crossing = 3,
    }

    /// <summary>What a path is (part 1 §5).</summary>
    public enum PathKind
    {
        Footpath = 0,
        CartTrack = 1,
        TidalCrossing = 2,
    }

    /// <summary>The biome's zone rule (part 1 §6). The recipe it grows waits for PR 5p.</summary>
    public enum BiomeKind
    {
        Coast = 0,
        Marsh = 1,
        Barren = 2,
        Meadow = 3,
        Swale = 4,
    }

    /// <summary>
    /// The protected set's rules: what must not move and what keeps today's paint (part 1 §3, the
    /// prototype's <c>protection</c>; part 2 adds the crest band on the crossing's own Def). Each shape
    /// holds fully within its buffer and fades over its feather. The roots are the scene's.
    /// </summary>
    [Serializable]
    public class TerrainPlanKeep
    {
        [Header("The frozen mask")]
        [Tooltip("Protection at or above this is the frozen mask: the ground is the analytic model's, to the bit.")]
        public float FrozenAt = 0.999f;
        [Tooltip("Paint protection above this keeps today's splat.")]
        public float KeepPaintAbove = 0.5f;

        [Header("Buildings (the builder's positions)")]
        public float BuildingRadius = 8f;
        public float LargeBuildingRadius = 12f;
        [Tooltip("The buildings (the gatherer's ids) that hold the large radius: the cannery.")]
        public string[] LargeBuildings = { "b_cannery" };
        public float BuildingFeather = 6f;
        [Tooltip("The paint keep is the building's radius less this.")]
        public float BuildingPaintInset = 2f;
        public float PaintFeather = 2f;

        [Header("The scene's roots")]
        public string YardsRoot = "Yards";
        public float YardLink = 6f;
        public int YardMinPoints = 4;
        public float YardBuffer = 3f;
        public float YardFeather = 6f;
        public float YardPaintBuffer = 1.5f;
        public string[] BuiltRoots =
        {
            "GinnyPlot", "IslandVillage", "IslandShops", "Tools", "IslandInhabitants", "NedsLetter", "GeneralStoreCounter",
            "UtilityQuadAtTheStore", "Trike200AtTheStore", "Enduro250AtTheStore", "AuntGinny", "WetBucketSpot",
        };
        public float BuiltBuffer = 3f;
        public float BuiltFeather = 6f;
        public float BuiltPaintBuffer = 1.5f;
        public string WharfRoot = "StPetersWharf";
        public float WharfItemBuffer = 4f;
        public float WharfItemFeather = 4f;
        public string CliffRoot = "CliffWalls";
        public float CliffBuffer = 4f;
        public float CliffFeather = 4f;
        public float CliffPaintBuffer = 1f;
        public string WoodsRoot = "IslandWoods";
        public float WoodsBuffer = 3f;
        public float WoodsFeather = 2f;
        public string ClamRoot = "ClamHoles";

        [Header("The builder's lines")]
        public float RoadBuffer = 3f;
        public float RoadFeather = 3f;
        public float WharfBuffer = 6f;
        public float WharfFeather = 6f;
        public float WharfPaintBuffer = 3f;
        [Tooltip("Added to a berth's, the approach's and the pocket's half width.")]
        public float BerthExtra = 4f;
        public float BerthFeather = 6f;
        public float DockPointBuffer = 8f;
        public float DockPointFeather = 6f;
        [Tooltip("Added to the arrival route's half width.")]
        public float EntranceExtra = 20f;
        public float EntranceFeather = 6f;
        [Tooltip("Part 1's bar capsule. The crossing replaces it with its crest band (CoastSectionDef.CrestKeep).")]
        public float BarBuffer = 20f;
        public float BarFeather = 6f;
        [Tooltip("Added to the gut's half width.")]
        public float GutExtra = 5f;
        public float GutFeather = 6f;
        public float PassageBuffer = 10f;
        public float PassageFeather = 6f;
        public float FleetBuffer = 0f;
        public float FleetFeather = 6f;

        [Header("The map's edge and the south half")]
        public float EdgeBuffer = 20f;
        public float EdgeFeather = 6f;
        [Tooltip("Bearings (degrees) the south blanket holds: every cliff; part 2 (PR 5w) re-derives them.")]
        public Vector2 SouthSector = new Vector2(96.6f, 252f);
        public float SouthFeatherDeg = 3f;
    }
}
