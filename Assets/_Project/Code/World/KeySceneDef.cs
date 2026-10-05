using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>ONE KEY SCENE, AS THE GAME PLACES IT.</b> One asset per scene (ADR 0003), keyed by a stable,
    /// append-only <see cref="Id"/> (<c>keyscene.snake_case</c>, e.g. <c>keyscene.stp_landing</c>): the
    /// concept director's key scene, holding the pieces the game places TODAY, each by the id CD gave it.
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
    /// test holds, and the step never draws with it.</para>
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

        /// <summary>The variant a scene shows today; the others (restored) are later boards.</summary>
        public const string Today = "today";

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
        [Tooltip("The pieces placed today, in CD's order. Ids are CD's, stable and append-only.")]
        public KeyScenePiece[] Pieces = Array.Empty<KeyScenePiece>();
    }

    /// <summary>One placed piece: which kit piece, where, facing which way, standing on what.</summary>
    [Serializable]
    public sealed class KeyScenePiece
    {
        [Tooltip("CD's id for this placement, e.g. prop.stp_slip_tide_board. A set piece's is its SetPieceDef's.")]
        public string Id = "";

        [Tooltip("The kit it comes from: stPetersSetPieces, wharfDecor or shoreFinds.")]
        public string Kit = "";

        [Tooltip("The kit's own key for the piece, e.g. slipTideBoard, trapStack, Driftwood.")]
        public string Piece = "";

        [Tooltip("A shore find's state (wet, dry, bleached); empty for everything else.")]
        public string State = "";

        [Tooltip("Where its ground point stands, in ground metres (x east, y north).")]
        public Vector2 At;

        [Tooltip("The height it stands at, metres above datum, as the game's height source reads it.")]
        public float Z;

        [Tooltip("What that height is read off: ground, deck or mount.")]
        public string StandsOn = KeySceneDef.StandsOnGround;

        [Tooltip("CD's facing, the kit's dir: 0 N, then counterclockwise (1 NW, 2 W … 4 S … 7 NE).")]
        public int Dir;

        [Tooltip("Set when CD gives the piece its own sort line.")]
        public bool HasSortY;

        [Tooltip("The world y it sorts by against figures, when HasSortY.")]
        public float SortY;

        [Tooltip("The scene variants it stands in. Every piece here stands in today's.")]
        public string[] Variants = { KeySceneDef.Today };

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
}
