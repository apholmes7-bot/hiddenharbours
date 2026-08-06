using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The C# face of the <b>Cliff Face &amp; Terrain Ledge kit v10</b>
    /// (<c>docs/art/rigs/cliff-face-kit/</c>) — the one place the rig's vocabulary, its canvas sizes and
    /// its import contract are written down for the rest of the project, so no baker, shader, catalog or
    /// test re-derives them from the README by eye.
    ///
    /// <para><b>⭐ THE PNGs ARE OUTPUT, NOT SOURCE — and unlike every sibling kit, they are NOT
    /// COMMITTED.</b> The kit is parametric: 3 rocks × 5 aspects × 4 batters × 3 wear steps = 180 face
    /// sets, each shipping four co-registered channels. The rig's own README opens with the ruling —
    /// <i>"This kit is parametric, so it ships as the rig, not as 800 PNGs… keeping it in the repo is 90
    /// MB that goes stale the moment a coefficient changes."</i> That is not a preference, it is the
    /// arithmetic: MEASURED on this drop, even the narrow St Peters subset (one rock, five aspects,
    /// three batters, one wear step, three of the four channels) is <b>16.0 MB across 63 PNGs</b>. So the
    /// pipeline here inverts the sibling kits' one: <see cref="HiddenHarbours.Tools.RigBaking.CliffBaker"/>
    /// writes the sheets into <see cref="BakeRoot"/> on demand (one menu item, seconds), that folder is
    /// <c>.gitignore</c>d, and what the repository carries is the RIG plus a contract test that drives it
    /// through V8 on every CI run. A pinned byte in a committed sheet would only ever pin the encoder.</para>
    ///
    /// <para><b>Faces are TEXTURES, not sprites</b> — the one place this kit breaks the sibling pattern.
    /// Every other rig in the repo bakes sprites with a pivot; a cliff face is a periodic material tiled
    /// across a wall quad with Repeat/Point, addressed by surface metres. Only the 32 px ledge pieces and
    /// the brow/toe decals are sprite-shaped. <see cref="IsSpriteAsset"/> is the split.</para>
    /// </summary>
    public static class CliffCatalog
    {
        // =====================================================================================
        //  SOURCE
        // =====================================================================================

        /// <summary>The rig, verbatim as the art director shipped it (immutable source — CLAUDE.md §6
        /// art-director lane). C#, shaders and import settings are PORTS of this file; when they
        /// disagree, this file wins and the port is the bug.</summary>
        public const string RigScriptPath = "docs/art/rigs/cliff-face-kit/bake/cliffRig.js";

        /// <summary>The kit's own manifest — names, sizes, wrap rules, channel meanings. Read by the
        /// contract test so a re-drop that changes a size fails CI rather than shipping a stretched
        /// wall.</summary>
        public const string ManifestPath = "docs/art/rigs/cliff-face-kit/cliff.json";

        /// <summary>The JS global the rig installs.</summary>
        public const string RigGlobalName = "CliffRig";

        /// <summary>Where <see cref="HiddenHarbours.Tools.RigBaking.CliffBaker"/> writes. Gitignored on
        /// purpose (see the class note): a bake is a local, reproducible artefact, not a commit.</summary>
        public const string BakeRoot = "Assets/_Project/Art/Terrain/Cliffs";

        // =====================================================================================
        //  THE VOCABULARY (mirrors cliff.json "axes"; CliffRigBakeTests asserts the two agree)
        // =====================================================================================

        /// <summary>Pixels per metre, throughout the kit — the project's PPU 32, so a face texel is a
        /// world texel and the wall never resamples.</summary>
        public const int PxPerMetre = 32;

        /// <summary>The three rocks the rig bakes. <b>St Peters is sandstone</b> — the region's docs call
        /// the weather coast "red sandstone cliffs" — so that is the only rock
        /// <see cref="HiddenHarbours.Tools.RigBaking.CliffBaker"/> ships by default; till and basalt are
        /// legal and cost nothing but a re-bake.</summary>
        public static readonly string[] Rocks = { "sandstone", "till", "basalt" };

        /// <summary>
        /// The aspect law — the five outward-facing directions the kit authors, W · SW · S · SE · E.
        ///
        /// <para><b>⚠ N IS NOT AUTHORED, AND THAT IS A DESIGN CONSTRAINT ON THE COAST, NOT A GAP IN THE
        /// KIT.</b> "N faces the camera's back": a north-facing wall would present its shadowed back to a
        /// ¾ top-down camera and read as a hole. The composer snaps each coast segment's outward normal
        /// to the nearest of these five, so a cliff can only honestly stand where its seaward normal
        /// bears within about 22.5° of E (90°) through S (180°) round to W (270°). That arc — roughly
        /// 67.5° to 292.5° — is 62% of the compass, which is what makes a "half the shoreline is cliff"
        /// island expressible at all: the cliff share has room to reach ~50% and still leave the honest
        /// gaps the beaches need.</para>
        /// </summary>
        public static readonly string[] Aspects = { "W", "SW", "S", "SE", "E" };

        /// <summary>Compass azimuth (degrees, N = 0, clockwise) of each <see cref="Aspects"/> entry — the
        /// direction the face LOOKS. Mirrors the rig's <c>ASPECT[a].az</c>; the contract test reads both
        /// and compares, because a mislabelled compass is this repo's most-repeated art defect (five
        /// kits).</summary>
        public static readonly float[] AspectAzimuths = { 270f, 225f, 180f, 135f, 90f };

        /// <summary>
        /// The TANGENT-SPACE key light each <see cref="Aspects"/> entry was BAKED at — the rig's own
        /// <c>ASPECT[a].L</c>, verbatim, index-aligned.
        ///
        /// <para><b>What it is for, and why it cannot be guessed.</b> <c>mask.R</c> holds
        /// <c>N·Lbake × castShadow</c>, so the shader recovers the cast shadow by DIVIDING by the bake's
        /// own <c>N·Lbake</c> (<c>HiddenHarboursCliffFace.shader</c>, <c>_BakeL</c>). Feed it the wrong
        /// aspect's key and the division leaves a residue of the wrong bake's shading — a face that is
        /// subtly, uniformly mis-lit, which reads as "the rock looks a bit flat" rather than as an error.
        /// The shader's PROPERTY DEFAULT is the S row below, which is the standing proof that the rig's
        /// frame and the shader's tangent frame are the same one and no flip is owed.</para>
        ///
        /// <para><c>CliffRigBakeTests</c> reads <c>CliffRig.ASPECT</c> through V8 and holds these equal,
        /// because a table copied out of a rig by eye is a table that drifts on the next drop.</para>
        /// </summary>
        public static readonly Vector3[] AspectBakeLights =
        {
            new Vector3(-0.24f, -0.52f, 0.82f),   // W
            new Vector3(-0.44f, -0.56f, 0.70f),   // SW
            new Vector3(-0.60f, -0.55f, 0.58f),   // S  — the shader's own _BakeL default
            new Vector3(-0.22f, -0.74f, 0.64f),   // SE
            new Vector3(-0.10f, -0.82f, 0.56f),   // E
        };

        /// <summary>The four named batters and their angles from horizontal. 90° is the wall the owner
        /// asked for; the shallower three exist so a coast can transition instead of stepping.</summary>
        public static readonly string[] Batters = { "wall", "steep", "ramp", "bank" };

        /// <summary>Angle (degrees from horizontal) of each <see cref="Batters"/> entry.</summary>
        public static readonly float[] BatterAngles = { 90f, 76f, 62f, 48f };

        /// <summary>The file-name suffix each batter carries. <b>The vertical bake carries none</b> — the
        /// wall is the base case, so <c>Sandstone_S.png</c> IS the 90° face.</summary>
        public static readonly string[] BatterSuffixes = { "", "_S76", "_S62", "_S48" };

        /// <summary>The wear ladder. <b>The base step carries no suffix</b>; the rig's <c>step</c>
        /// argument is 0/1/2 for Lo/base/Hi.</summary>
        public static readonly string[] StepSuffixes = { "_Lo", "", "_Hi" };

        /// <summary>The rig's step index for <see cref="StepSuffixes"/>[i] — i itself. Named so the
        /// baker's calls read as intent rather than as a bare 1.</summary>
        public const int BaseStep = 1;

        // =====================================================================================
        //  THE CHANNELS — and which path this project takes
        // =====================================================================================

        /// <summary>
        /// The <b>live-lit</b> channel set: the three the shader samples.
        ///
        /// <para><b>⭐ WE TAKE THE LIVE PATH AND WE DO NOT SHIP THE PRE-LIT ONE.</b> The kit bakes
        /// directional light into the plain <c>&lt;name&gt;.png</c> albedo — legitimately, because a
        /// cliff face never rotates relative to the camera — and offers <c>_unlit</c> + <c>_normal</c> +
        /// <c>_mask</c> as the alternative for a project whose sun moves. The README's ruling is
        /// explicit: <i>"Pick a path per project; do not mix them on one wall."</i> Hidden Harbours runs
        /// a deterministic 24-hour clock (ADR 0013) whose whole point is that the world's light is a
        /// force you read, so a face lit at a fixed key would be wrong by 07:00 and would sit visibly
        /// still while the sea and the sod around it moved. The pre-lit channel is therefore not baked at
        /// all — which also removes a quarter of the bake.</para>
        /// </summary>
        public static readonly string[] LiveChannels = { "_unlit", "_normal", "_mask" };

        /// <summary>Whether a channel imports as sRGB. Albedo is colour; the normal, the mask and the
        /// displacement profile are DATA and must import linear or every lighting term is gamma-bent.</summary>
        public static bool IsSrgb(string channelSuffix) =>
            channelSuffix != "_normal" && channelSuffix != "_mask";

        // =====================================================================================
        //  CANVAS SIZES (cliff.json; CliffRigBakeTests pins the rig's actual output against these)
        // =====================================================================================

        /// <summary>Face canvas, px.</summary>
        public const int FaceWidth = 384, FaceHeight = 288;

        /// <summary>
        /// Face coverage in metres — <b>12 m along the shore × 9 m DOWN THE SURFACE</b>.
        ///
        /// <para><b>⚠ SURFACE, NOT HEIGHT — the trap that makes a battered wall lie about its own
        /// shape.</b> <c>t</c> is 32 px per metre measured along the sloping face, not along the vertical
        /// drop. An 8 m-high bank at 48° needs 8/sin(48°) = 10.8 m of <c>t</c>, i.e. 1.2 tiles. Size the
        /// quad by surface length; size it by height and the bedding spacing comes out wrong in exactly
        /// the way that reads as "tilted vertical texture".</para>
        /// </summary>
        public const float FaceMetresS = 12f, FaceMetresT = 9f;

        /// <summary>Brow and toe decal strips: 384 × 128 px = 12 × 4 m, straight alpha.</summary>
        public const int StripWidth = 384, StripHeight = 128;
        public const float StripMetresS = 12f, StripMetresT = 4f;

        /// <summary>Where the sod line sits in the brow strip, as a fraction of its height — the seam a
        /// clifftop's ground meets the face at.</summary>
        public const float BrowLineAt = 0.42f;

        /// <summary>The toe feature each batter pairs with: a wall or a steep face is undercut into a
        /// <c>notch</c>; a ramp or a bank keeps its debris and reads as a <c>slump</c>. Index-aligned to
        /// <see cref="Batters"/>.</summary>
        public static readonly string[] ToeFeatureForBatter = { "notch", "notch", "slump", "slump" };

        /// <summary>Ledge sheet: 12 pieces × 3 bands of 32 px cells.</summary>
        public const int LedgeCell = 32;
        public const int LedgeSheetWidth = 384, LedgeSheetHeight = 96;

        public static readonly string[] LedgePieces =
        {
            "faceS", "cornSW", "cornSE", "innSW", "innSE", "sideW",
            "sideE", "diagSW", "diagSE", "roundSW", "roundSE", "notch",
        };

        /// <summary>The three heights a ledge piece is drawn at — a clifftop lip, a mid bench, and the
        /// foreshore ledge at the cliff's foot. The last is the one the tide works.</summary>
        public static readonly string[] LedgeBands = { "brow", "mid", "toe" };

        /// <summary>The contact sheet — where a ledge meets the ground — 5 pieces of 32 px.</summary>
        public static readonly string[] ContactPieces = { "faceS", "cornSW", "cornSE", "sideW", "sideE" };
        public const int ContactSheetWidth = 160, ContactSheetHeight = 32;

        // =====================================================================================
        //  THE PROFILE — the part that needs engine work
        // =====================================================================================

        /// <summary>
        /// Peak plan displacement (metres) the profile map encodes, either side of zero. The PNG is grey
        /// with <b>128 = zero</b>; decode is <c>(sample − 0.5) × 2 × <see cref="ProfileMetres"/></c>.
        ///
        /// <para><b>⚠ The rig hands back FLOATS, not bytes.</b> <c>profile().disp</c> is a float array in
        /// metres (measured range on this drop: −0.61 … +0.69 m); the 0..255 grey encoding is the
        /// BAKER's, not the rig's. Encode it wrong and the silhouette displacement is silently scaled.</para>
        /// </summary>
        public const float ProfileMetres = 1.15f;

        /// <summary>Subdivision (m) along the wall the README asks for when displacing by the profile —
        /// finer than this buys nothing at 32 px/m, coarser and the brow line straightens back out.</summary>
        public const float ProfileSubdivideMetres = 0.25f;

        /// <summary>The profile depends only on rock and batter — <b>not on aspect or wear</b> — so one
        /// map serves all fifteen bakes of a group. Asserted against the rig, because "it looked the
        /// same" is not a contract.</summary>
        public const bool ProfileIsAspectIndependent = true;

        // =====================================================================================
        //  IMPORT CONTRACT (README §6)
        // =====================================================================================

        /// <summary>Faces and profiles tile along the shore, so they wrap in S. Only the faces wrap in T
        /// as well (a face is periodic in both; a profile and a strip are clamped at top and bottom, and
        /// Unity's importer has one wrap mode per axis).</summary>
        public static TextureWrapMode WrapU(CliffAssetKind kind) =>
            kind == CliffAssetKind.Ledge ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;

        public static TextureWrapMode WrapV(CliffAssetKind kind) =>
            kind == CliffAssetKind.Face ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

        /// <summary>Everything is point-filtered pixel art <b>except the profile</b>, which is geometry:
        /// it is sampled to push vertices, so it wants bilinear or the wall comes out faceted at 0.25 m.</summary>
        public static FilterMode Filter(CliffAssetKind kind) =>
            kind == CliffAssetKind.Profile ? FilterMode.Bilinear : FilterMode.Point;

        /// <summary>Only the decals and the ledge tiles carry meaningful alpha; a face and a profile are
        /// fully covered.</summary>
        public static bool AlphaIsTransparency(CliffAssetKind kind) =>
            kind == CliffAssetKind.Strip || kind == CliffAssetKind.Ledge;

        /// <summary>The ledge pieces and the decal strips are sprites (they have silhouettes and sort);
        /// faces and profiles are plain textures sampled by the wall shader.</summary>
        public static bool IsSpriteAsset(CliffAssetKind kind) =>
            kind == CliffAssetKind.Strip || kind == CliffAssetKind.Ledge;

        /// <summary>
        /// <b>The profile alone imports CPU-READABLE</b>, because it alone is read by the CPU: the wall
        /// mesh is displaced by sampling it per vertex (<c>CliffWallSurface</c>), and a texture without
        /// <c>isReadable</c> throws the moment <c>GetPixelBilinear</c> touches it.
        ///
        /// <para>This is the same split the README already draws for filtering — <i>"Bilinear (it is
        /// geometry, not pixels)"</i>. A face, a strip and a ledge are only ever sampled by a shader and
        /// stay GPU-only, so the readable copy costs nothing on the assets that would actually be
        /// expensive: the three shipped profiles are 384 × 288 each.</para>
        /// </summary>
        public static bool IsCpuReadable(CliffAssetKind kind) => kind == CliffAssetKind.Profile;

        // =====================================================================================
        //  NAMES
        // =====================================================================================

        /// <summary>A face's file name: <c>&lt;Rock&gt;_&lt;Aspect&gt;{_S76|_S62|_S48}{_Lo|_Hi}&lt;channel&gt;</c>,
        /// with the wall and the base wear step carrying no suffix. Rock is Capitalised, matching the
        /// kit's own <c>tex/</c> listing.</summary>
        public static string FaceName(string rock, string aspect, int batterIndex, int stepIndex,
                                      string channelSuffix) =>
            $"{Capitalise(rock)}_{aspect}{BatterSuffixes[batterIndex]}{StepSuffixes[stepIndex]}{channelSuffix}";

        /// <summary>A brow decal's file name: <c>&lt;Aspect&gt;{_Lo|_Hi}</c>.</summary>
        public static string BrowName(string aspect, int stepIndex) =>
            $"{aspect}{StepSuffixes[stepIndex]}";

        /// <summary>A toe decal's file name: <c>&lt;Aspect&gt;{_Lo|_Hi}{_cave|_slump}</c>. The default
        /// (notch) feature carries no suffix — it is what a vertical wall's foot does.</summary>
        public static string ToeName(string aspect, int stepIndex, string feature) =>
            $"{aspect}{StepSuffixes[stepIndex]}" +
            (string.IsNullOrEmpty(feature) || feature == "notch" ? "" : "_" + feature);

        /// <summary>A profile map's file name: <c>&lt;Rock&gt;{_S76|_S62|_S48}</c> — no aspect, no wear.</summary>
        public static string ProfileName(string rock, int batterIndex) =>
            $"{Capitalise(rock)}{BatterSuffixes[batterIndex]}";

        /// <summary>A ledge sheet's file name: <c>&lt;Rock&gt;{_Lo|_Hi}</c>.</summary>
        public static string LedgeSheetName(string rock, int stepIndex) =>
            $"{Capitalise(rock)}{StepSuffixes[stepIndex]}";

        static string Capitalise(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    /// <summary>
    /// Which of the kit's four asset shapes a baked PNG is — the one discriminator the import contract,
    /// the wrap rules and the sprite/texture split all key on.
    ///
    /// <para>A NAMESPACE-level enum, deliberately not nested inside <see cref="CliffCatalog"/>: the shore
    /// map's <c>ShoreMaterial</c> was nested once and cost a CS0426 across every consumer. Additive
    /// members only.</para>
    /// </summary>
    public enum CliffAssetKind
    {
        /// <summary>A periodic 12 × 9 m wall texture — <see cref="CliffCatalog.LiveChannels"/> of it.</summary>
        Face = 0,
        /// <summary>A brow or toe decal strip, 12 × 4 m with straight alpha.</summary>
        Strip = 1,
        /// <summary>A 32 px iso ledge sheet or its contact sheet.</summary>
        Ledge = 2,
        /// <summary>The plan-displacement map, in metres, that gives the wall its real silhouette.</summary>
        Profile = 3,
    }
}
