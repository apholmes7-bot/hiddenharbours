using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>ONE KEY SCENE, AS THE GAME PLACES IT.</b> One asset per scene (ADR 0003), keyed by a stable,
    /// append-only <see cref="Id"/> (<c>keyscene.snake_case</c>, e.g. <c>keyscene.stp_landing</c>): the
    /// concept director's key scene, holding every piece of it by the id CD gave it: the pieces the step
    /// places TODAY, and, as data it never places, what comes and goes with the tide, the hour and the
    /// weather, the later boards' variants, and what other code places (<see cref="KeyScenePiece.Owner"/>).
    ///
    /// <para><b>Why the placements live here and not on the piece.</b> A <c>SetPieceDef</c> says what a
    /// piece IS: its frames, its colliders, how it sorts. Where it stands belongs to the scene, because one
    /// piece type can stand in many places (a trap stack on the Landing's deck, another by the cannery), and
    /// the kits' iso props (<c>wharfDecor</c>, <c>shoreFinds</c>) have no Def of their own to carry a place
    /// at all. So a placement names its kit and piece, and the scene holds where.</para>
    ///
    /// <para><b>Ground metres.</b> <see cref="KeyScenePiece.At"/> is x east and y north on the unsquashed
    /// ground plane, which is world XY (ADR 0042, regime 2), and the pivot of every piece is its ground
    /// point, so the place IS the position. <see cref="KeyScenePiece.Z"/> is what it stands on, in metres
    /// above the game's datum, measured on the game's own height source (#890); it is the record the z table
    /// test holds, and ground placements do not draw with it. A mount draws only its rise above its host ground.</para>
    ///
    /// <para><b>One reading of y (O3, lead-architect).</b> Every position is its plan's own number,
    /// unconverted: a siting is world metres, never squashed (ADR 0042, regime 2). A piece that spans
    /// between positions (a trolley line's path) takes its picture's ground size at 1.556 m per unit north,
    /// the bake's squash (regime 1); a free-standing piece keeps its own size.</para>
    ///
    /// <para>Plain data, no behaviour: the World module holds it, and the App editor step
    /// (<c>StPetersLayerRefresh</c>'s key scene pieces) writes its pieces into the scene. Nothing at runtime
    /// reads it.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Key Scene", fileName = "KeyScene")]
    public class KeySceneDef : ScriptableObject
    {
        /// <summary>What a piece stands on: the ground's own height.</summary>
        public const string StandsOnGround = "ground";

        /// <summary>What a piece stands on: the wharf's deck (one level height, <c>StPetersWharf</c>).</summary>
        public const string StandsOnDeck = "deck";

        /// <summary>What a piece stands on: something it hangs from (a wall, a door).</summary>
        public const string StandsOnMount = "mount";

        /// <summary>What a piece stands on: the water, riding the tide (a buoy, a gull afloat). The step
        /// places nothing that rides.</summary>
        public const string StandsOnWater = "water";

        /// <summary>The variant a scene shows today; the others (restored) are later boards.</summary>
        public const string Today = "today";

        /// <summary>The kinds of piece (lead-architect's type map, O3), from the package's defType: a set
        /// piece (SetPieceDef, PropDef, or none), a gull (the seagull kit), a building (BuildingDef, a rig
        /// catalog preset), a nav mark (NavBuoyDef), a boat (BoatDef), a rock (RockFormDef), a tree, a plant,
        /// a waterfall and a wild animal.</summary>
        public const string KindSetPiece = "setPiece", KindGull = "gull", KindBuilding = "building",
                            KindNavMark = "navMark", KindBoat = "boat", KindRock = "rock", KindTree = "tree",
                            KindPlant = "plant", KindWaterfall = "waterfall", KindWildlife = "wildlife";

        [Header("Identity")]
        [Tooltip("Stable id, append-only (keyscene.snake_case, e.g. keyscene.stp_landing).")]
        public string Id = "keyscene.example";

        [Tooltip("Player-facing name, CD's own. Flavour only; the id is canonical.")]
        public string DisplayName = "";

        [Tooltip("The region the scene stands in, by its RegionDef id (e.g. region.st_peters).")]
        public string RegionId = "";

        [Header("Where it came from")]
        [Tooltip("The drop and file the placements were read from, with its sha256.")]
        public string Source = "";

        [Header("Today's pieces")]
        [Tooltip("Whether the step places this scene. A new scene comes in off, as data, and its wave turns it " +
                 "on when its art is in the game; PlacementIds can hold an explicitly staged subset.")]
        public bool Placed = true;

        [Tooltip("Every piece of the scene, in CD's order. Ids are CD's, stable and append-only. The step places " +
                 "those that stand in today's variant and have no Owner; the rest are kept as data.")]
        public KeyScenePiece[] Pieces = Array.Empty<KeyScenePiece>();

        [Header("Kept, never placed")]
        [Tooltip("The scene's lights, each by the piece that carries it. The step places none.")]
        public KeySceneLight[] Lights = Array.Empty<KeySceneLight>();

        [Tooltip("Ids a later package no longer holds. Kept so that none is ever used again.")]
        public string[] RetiredIds = Array.Empty<string>();

        [Tooltip("Optional wave selection by id. Empty places every eligible row. Unselected rows remain recorded until their host or ground is ready.")]
        public string[] PlacementIds = Array.Empty<string>();
    }

    /// <summary>One placed piece: which kit piece, where, facing which way, standing on what.</summary>
    [Serializable]
    public sealed class KeyScenePiece
    {
        [Tooltip("CD's id for this placement, e.g. prop.stp_slip_tide_board. A set piece's is its SetPieceDef's.")]
        public string Id = "";

        [Tooltip("What it is, by the type map: setPiece, gull, building, navMark, boat, rock, tree, plant, " +
                 "waterfall or wildlife.")]
        public string Kind = KeySceneDef.KindSetPiece;

        [Tooltip("Set when other code places it (a frozen piece's owner, e.g. StPetersCannery, or the nav marks): " +
                 "the step never places it, so it is never placed twice.")]
        public string Owner = "";

        [Tooltip("The kit that draws it: stPetersSetPieces, wharfDecor, shoreFinds, wharfBuilding2, seagull and on.")]
        public string Kit = "";

        [Tooltip("The kit's own key for the piece, e.g. slipTideBoard, trapStack, Driftwood.")]
        public string Piece = "";

        [Tooltip("A shore find's state (wet, dry, bleached); empty for everything else.")]
        public string State = "";

        [Tooltip("Where its ground point stands, in ground metres (x east, y north).")]
        public Vector2 At;

        [Tooltip("The height it stands at, metres above datum, as the game's height source reads it.")]
        public float Z;

        [Tooltip("What that height is read off: ground, deck, mount or water.")]
        public string StandsOn = KeySceneDef.StandsOnGround;

        [Tooltip("A mounted piece's host, by id (structure.stp_cannery); empty when CD names none by id.")]
        public string MountHost = "";

        [Tooltip("The host's anchor it stands at (door, ridge, top); the anchor's height is Z.")]
        public string MountAnchor = "";

        [Tooltip("Set when the piece's height is read at its foot, not at its place (the beach steps).")]
        public bool HasFoot;

        [Tooltip("Where its foot meets the ground, x east and y north, when HasFoot.")]
        public Vector2 Foot;

        [Tooltip("CD's facing, the kit's dir: 0 N, then counterclockwise (1 NW, 2 W … 4 S … 7 NE).")]
        public int Dir;

        [Tooltip("Set when CD gives the piece its own sort line.")]
        public bool HasSortY;

        [Tooltip("The world y it sorts by against figures, when HasSortY.")]
        public float SortY;

        [Tooltip("CD's layer for it (raised, floor), as the package gives it; empty when it gives none.")]
        public string Layer = "";

        [Tooltip("How a figure meets it (behind, on, under), in CD's words; empty when the package says nothing.")]
        public string Walk = "";

        [Tooltip("CD's collider for it, in CD's words (the kit's footprint, a line along the run).")]
        public string Collider = "";

        [Tooltip("The scene variants it stands in. The step places only the pieces that stand in today's.")]
        public string[] Variants = { KeySceneDef.Today };

        [Tooltip("The tide it shows at, CD's record as compact JSON; empty when it shows at every tide.")]
        public string WhenTide = "";

        [Tooltip("String ids the piece's boards show; the text is in the words table, never here.")]
        public string[] Words = Array.Empty<string>();

        [Tooltip("CD's options for the piece, verbatim, for the record. Iso props bake at the kit's defaults.")]
        public KeySceneOption[] Options = Array.Empty<KeySceneOption>();
    }

    /// <summary>One of CD's options for a piece, kept as text: <c>variant 1</c>, <c>weather 0.35</c>.</summary>
    [Serializable]
    public sealed class KeySceneOption
    {
        public string Key = "";
        public string Value = "";
    }

    /// <summary>One of a scene's lights, kept as data: the step places none. A lamp post lights its own, and a
    /// keeper's lamp waits for the keeper's routine.</summary>
    [Serializable]
    public sealed class KeySceneLight
    {
        [Tooltip("The piece that carries it, by id.")]
        public string PieceId = "";

        [Tooltip("CD's preset for it (lanternPost, keeperLamp, hurricaneLantern).")]
        public string Preset = "";

        [Tooltip("Set when it throws a pool of light on the ground.")]
        public bool Pool;

        [Tooltip("How far its light reaches, in metres.")]
        public float ReachMetres;

        [Tooltip("Where it hangs from its piece's place: x east, y north, z up.")]
        public Vector3 At;

        [Tooltip("Its colour, #rrggbb; empty for its preset's.")]
        public string Colour = "";

        [Tooltip("How bright it is; 0 for its preset's.")]
        public float Intensity;

        [Tooltip("When it is lit (dusk).")]
        public string When = "";

        [Tooltip("Who lights it, in CD's words.")]
        public string WhoLights = "";

        [Tooltip("What it counts against the scene's light budget.")]
        public int Budget;

        [Tooltip("CD's note on it, when it carries one (what is not ruled yet).")]
        public string Note = "";
    }
}
