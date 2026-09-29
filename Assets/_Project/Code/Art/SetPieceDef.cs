using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// A key-scene set piece: one baked, placeable object from a key-scene kit — the St Peters landing's
    /// tide board and harbour vane, the cannery's boiler house, trolley line, trolley, conveyor, fallen
    /// sign and door notice. One asset per piece (ADR 0003). The id is the kit's; every other field is
    /// DERIVED from the kit's gameplay sidecar and the bake by <c>SetPieceDefBuilder</c>, so nothing here
    /// is tuned here: a change is a kit change, re-baked.
    ///
    /// <para><b>Frames are clockwise.</b> Frame k is rig dir (8 − k) % 8, the sheet order of every rig in
    /// the repo; the kit's own dirs turn counterclockwise (0 N, 1 NW, 2 W …). <see cref="RigDirForFrame"/>
    /// and <see cref="FrameForRigDir"/> are the one mapping between the two.</para>
    ///
    /// <para><b>Each frame keeps its own pivot.</b> The sheet is a lattice of equal cells, but a frame's
    /// ground point sits where the rig put it inside that frame's own box rather than at one pivot shared
    /// by all eight. A shared pivot would make the trolley line's cell 788×517, which fits no grid under
    /// the 2048 import cap. The sprite carries the pivot; <see cref="SetPieceFrame.Pivot"/> repeats it in
    /// cell pixels for the anchor maths.</para>
    ///
    /// <para><b>Words bake blank.</b> A board the game letters carries its corners and a string id
    /// (<see cref="SetPieceWordsAnchor"/>); the text lives in the strings table, so renaming a place
    /// changes a string, never a pixel.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Set Piece", fileName = "SetPiece")]
    public class SetPieceDef : ScriptableObject
    {
        /// <summary>Facings baked per piece. The kit renders all eight; no piece folds.</summary>
        public const int Facings = 8;

        [Header("Identity")]
        [Tooltip("Stable, append-only id from the kit's sidecar, e.g. \"prop.stp_slip_tide_board\". " +
                 "Never reuse or rename.")]
        public string Id = "";

        [Tooltip("Player-facing name, the sidecar's own. Flavour only; the id is canonical.")]
        public string DisplayName = "";

        [Header("Where it came from")]
        [Tooltip("The kit's piece key, verbatim, e.g. \"slipTideBoard\". What a re-bake matches against.")]
        public string RigKey = "";

        [Tooltip("The key scene the piece was drawn for, e.g. \"keyscene.stp_landing\".")]
        public string SceneId = "";

        [Tooltip("The options the frames were baked at, as the kit resolved them (JSON). Not decoration: " +
                 "the tide board's foot and the trolley line's path change the picture.")]
        public string BakedOptionsJson = "{}";

        [Header("How it sits")]
        [Tooltip("raised (y-sorted with figures) or floor (drawn under them).")]
        public string Layer = "raised";

        [Tooltip("What a walker does with it: behind (passes behind it), on (walks over it), under " +
                 "(walks under it).")]
        public string Walk = "behind";

        [Tooltip("ground, or wall for a piece that hangs on something (the door notice).")]
        public string Mount = "ground";

        [Tooltip("The model's extent in piece metres: +x right, +y the show face, +z up from the ground " +
                 "point (the pivot).")]
        public Vector3 BoundsMin;

        public Vector3 BoundsMax;

        [Header("Baked art")]
        [Tooltip("Eight frames, clockwise: frame k is rig dir (8 − k) % 8. Use Frame(k) or FrameForRigDir.")]
        public SetPieceFrame[] Frames = Array.Empty<SetPieceFrame>();

        [Tooltip("The lattice cell every frame on the sheet shares, in pixels.")]
        public Vector2Int CellSize;

        [Tooltip("The kit's scale, pixels per metre: the project's PPU.")]
        public float PixelsPerMetre = 32f;

        [Tooltip("The kit's camera elevation in degrees. A height z lifts a point z·cos(elev) screen " +
                 "metres; a depth y lifts it y·sin(elev).")]
        public float ElevationDeg = 40f;

        [Tooltip("The lamp level the frames were baked at. 0 for every St Peters piece: none of them " +
                 "emits in the bake.")]
        public float BakedLit;

        [Header("What it does to a walker")]
        [Tooltip("Footprints in piece metres (+x right, +y the show face). Turn them with the facing: " +
                 "ColliderAt.")]
        public SetPieceCollider[] Colliders = Array.Empty<SetPieceCollider>();

        [Header("Anchors (piece metres: +x right, +y the show face, +z up)")]
        [Tooltip("Named model points (top, foot, flue, door, lock, interact), sorted by name.")]
        public SetPieceAnchor[] Anchors = Array.Empty<SetPieceAnchor>();

        [Tooltip("The trolley line's path on the ground. Empty on every other piece.")]
        public Vector2[] Path = Array.Empty<Vector2>();

        [Tooltip("The tide board's marks. Empty on every other piece.")]
        public SetPieceTideMark[] TideMarks = Array.Empty<SetPieceTideMark>();

        [Tooltip("Boards the game letters: their corners (or a point) and the default string id.")]
        public SetPieceWordsAnchor[] Words = Array.Empty<SetPieceWordsAnchor>();

        [Header("Light (only where the sidecar carries a rule)")]
        public bool HasLight;

        public SetPieceLightRule Light = new SetPieceLightRule();

        [Header("Animated part (only where the sidecar carries one)")]
        public bool IsAnimated;

        public SetPieceAnimatedPart Animated = new SetPieceAnimatedPart();

        // ---- the one facing mapping -------------------------------------------------------------------

        /// <summary>The rig dir a clockwise frame shows. Its own inverse: frame and dir swap by (8 − x) % 8.</summary>
        public static int RigDirForFrame(int frame) => Wrap(Facings - Wrap(frame, Facings), Facings);

        /// <summary>The clockwise frame that shows a rig dir.</summary>
        public static int FrameForRigDir(int rigDir) => Wrap(Facings - Wrap(rigDir, Facings), Facings);

        internal static int Wrap(int i, int n) => ((i % n) + n) % n;

        public SetPieceFrame Frame(int frame)
        {
            if (Frames == null || Frames.Length == 0) return null;
            return Frames[Wrap(frame, Frames.Length)];
        }

        public Sprite SpriteFor(int frame) => Frame(frame)?.Sprite;

        public bool HasArt
        {
            get
            {
                if (Frames == null || Frames.Length != Facings) return false;
                foreach (var f in Frames)
                    if (f == null || f.Sprite == null) return false;
                return true;
            }
        }

        // ---- the kit's own geometry -------------------------------------------------------------------

        /// <summary>
        /// A piece-metre point on the ground turned to world metres (x east, y north) at a rig dir: the
        /// kit's own turn, dir·45° counterclockwise (<c>basis(dir)</c> in the rig).
        /// </summary>
        public static Vector2 ToWorld(Vector2 local, int rigDir)
        {
            double a = Wrap(rigDir, Facings) * Math.PI / 4.0;
            double ct = Math.Cos(a), st = Math.Sin(a);
            return new Vector2((float)(local.x * ct - local.y * st), (float)(local.x * st + local.y * ct));
        }

        /// <summary>A collider's footprint turned to world metres for the facing a frame shows.</summary>
        public Vector2[] ColliderAt(int collider, int frame)
        {
            var pts = Colliders[collider].Points ?? Array.Empty<Vector2>();
            int dir = RigDirForFrame(frame);
            var world = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++) world[i] = ToWorld(pts[i], dir);
            return world;
        }

        /// <summary>
        /// A model point's offset from the ground point on screen, in pixels, +y UP: the kit's
        /// <c>projRaw</c> at a rig dir, x·S and (y·sin e + z·cos e)·S after the turn.
        /// </summary>
        public Vector2 ScreenOffsetPx(Vector3 local, int rigDir)
        {
            double a = Wrap(rigDir, Facings) * Math.PI / 4.0;
            double ct = Math.Cos(a), st = Math.Sin(a);
            double e = ElevationDeg * Math.PI / 180.0;
            double xr = local.x * ct - local.y * st, yr = local.x * st + local.y * ct;
            return new Vector2((float)(xr * PixelsPerMetre),
                               (float)((yr * Math.Sin(e) + local.z * Math.Cos(e)) * PixelsPerMetre));
        }

        /// <summary>
        /// Where a model point lands in a facing frame's cell, in pixels from the cell's TOP-LEFT (the
        /// rig's own pixel frame, the one its <c>px</c> anchors are written in).
        /// </summary>
        public Vector2 AnchorPixel(Vector3 local, int frame)
        {
            var f = Frame(frame);
            if (f == null) return Vector2.zero;
            Vector2 off = ScreenOffsetPx(local, RigDirForFrame(frame));
            return new Vector2(f.Pivot.x + off.x, f.Pivot.y - off.y);
        }

        /// <summary>A named anchor, or false when the piece has none by that name.</summary>
        public bool TryGetAnchor(string anchorName, out Vector3 position)
        {
            if (Anchors != null)
                foreach (var a in Anchors)
                    if (a != null && a.Name == anchorName)
                    {
                        position = a.Position;
                        return true;
                    }
            position = default;
            return false;
        }
    }

    /// <summary>One baked frame: its sprite, the facing it shows, and where its ground point sits.</summary>
    [Serializable]
    public sealed class SetPieceFrame
    {
        public Sprite Sprite;

        [Tooltip("The kit's dir this frame was rendered at (0 N, 1 NW, 2 W … counterclockwise).")]
        public int RigDir;

        [Tooltip("The world direction the show face points, measured by the kit: N, NW, W …")]
        public string ShowFaces = "";

        [Tooltip("That direction as a compass bearing, degrees.")]
        public float BearingDeg;

        [Tooltip("What the camera sees at this facing: its back, the show face, or the show face " +
                 "edge-on or at a diagonal.")]
        public string CameraSees = "";

        [Tooltip("The kit's own cell for this dir, in pixels, before the bake cropped it.")]
        public Vector2Int NativeSize;

        [Tooltip("The ground point in the kit's own cell, pixels from its top-left.")]
        public Vector2Int NativePivot;

        [Tooltip("Where this frame's box starts in the kit's own cell. NativePivot − Crop = Pivot.")]
        public Vector2Int Crop;

        [Tooltip("The ground point in the lattice cell, pixels from its top-left. The sprite's pivot " +
                 "says the same.")]
        public Vector2Int Pivot;
    }

    /// <summary>A footprint that blocks, in piece metres.</summary>
    [Serializable]
    public sealed class SetPieceCollider
    {
        [Tooltip("The sidecar's shape word: polygon.")]
        public string Type = "polygon";

        [Tooltip("What it stops: walk.")]
        public string Blocks = "walk";

        public Vector2[] Points = Array.Empty<Vector2>();
    }

    /// <summary>A named model point.</summary>
    [Serializable]
    public sealed class SetPieceAnchor
    {
        public string Name = "";
        public Vector3 Position;
    }

    /// <summary>A mark on the tide board, and the water level that reaches it.</summary>
    [Serializable]
    public sealed class SetPieceTideMark
    {
        [Tooltip("The water level that reaches this mark, metres about mean sea level (springs ±2.20).")]
        public float Z;

        [Tooltip("The same level over chart datum (−2.20, the spring low): what the board reads.")]
        public float OverDatum;

        [Tooltip("Where the mark is painted, piece metres.")]
        public Vector3 At;

        [Tooltip("The numeral beside a whole-metre mark; empty on the others.")]
        public string Label = "";

        [Tooltip("The kit's word: half, metre, or spring high.")]
        public string Kind = "";
    }

    /// <summary>
    /// A board the game letters. The piece bakes it blank; the words are a string id the strings table
    /// resolves, so the Def carries where they go and never what they say.
    /// </summary>
    [Serializable]
    public sealed class SetPieceWordsAnchor
    {
        [Tooltip("The default string id, e.g. \"words.cannery_sign\".")]
        public string Id = "";

        [Tooltip("The body's string id when the board has one (\"notice.cannery_for_sale\"); empty otherwise.")]
        public string BodyId = "";

        [Tooltip("The board plane's four corners, piece metres. Empty when the words sit at a point.")]
        public Vector3[] Corners = Array.Empty<Vector3>();

        [Tooltip("True when the words sit at a point (the gauge numerals) rather than on a plane.")]
        public bool HasPoint;

        public Vector3 Point;

        [Tooltip("Letter height in metres, when the kit gives one; 0 otherwise.")]
        public float Size;
    }

    /// <summary>The sidecar's light rule: what would glow, and when.</summary>
    [Serializable]
    public sealed class SetPieceLightRule
    {
        public string[] Emitters = Array.Empty<string>();

        [Tooltip("When, in the kit's words.")]
        public string When = "";
    }

    /// <summary>
    /// A part that moves with a game input: the harbour vane's cod turns with the wind. Its headings are
    /// separate frames, rendered at one dir so the part is true to the compass.
    /// </summary>
    [Serializable]
    public sealed class SetPieceAnimatedPart
    {
        [Tooltip("The kit's name for the moving part, e.g. \"vane.cod\".")]
        public string Part = "";

        [Tooltip("The option that drives it, e.g. \"windFromDeg\" (the wind it points into, world compass).")]
        public string Input = "";

        [Tooltip("Headings baked per turn.")]
        public int Steps;

        [Tooltip("The input's value in the eight facing frames.")]
        public float BakedAtDeg;

        [Tooltip("The dir the headings were rendered at.")]
        public int RigDir;

        [Tooltip("Heading s shows the input at s·360/Steps degrees.")]
        public SetPieceFrame[] Headings = Array.Empty<SetPieceFrame>();

        public float StepDeg => Steps > 0 ? 360f / Steps : 0f;

        /// <summary>The heading frame nearest an input value, in degrees.</summary>
        public int HeadingFor(float degrees)
        {
            if (Steps <= 0) return 0;
            return SetPieceDef.Wrap(Mathf.FloorToInt(degrees / StepDeg + 0.5f), Steps);
        }
    }
}
