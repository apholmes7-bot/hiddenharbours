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
        //  THE PX SKIN (docs/art/rigs/px-cliff-face-kit/) — a second SKIN on the same field
        // =====================================================================================

        /// <summary>
        /// The px cliff face rig — the pixel-language re-skin of the kit above.
        ///
        /// <para><b>⭐ A SKIN, NOT A FIELD.</b> Its <c>profile()</c> is bit-identical to the v10 rig's
        /// (<c>PxCliffFaceKitIntakeTests.TheProfileIsBitIdenticalToTheV10Rig</c>), so baking px moves no
        /// collider and no traverse rule. What it changes is the pixels of the three wall channels and
        /// the brow/toe decals, and the wall's <c>_unlit</c> slot carries the palette INDEX instead of a
        /// colour (see <see cref="IndexChannel"/>).</para>
        ///
        /// <para><b>⚠ It does not run alone.</b> It reads <c>globalThis.PxLang</c> at load, so
        /// <see cref="PxLanguagePath"/> runs FIRST in the same host — the two files concatenated, the
        /// kit's own order.</para>
        /// </summary>
        public const string PxRigScriptPath = "docs/art/rigs/px-cliff-face-kit/bake/pxCliffFaceRig.js";

        /// <summary>The px kit's copy of the pixel language — bands, hex maths and the key light.</summary>
        public const string PxLanguagePath = "docs/art/rigs/px-cliff-face-kit/bake/pixelLanguage.js";

        /// <summary>The px kit's gameplay sidecar: the relight thresholds the shader's px branch
        /// ports, read by <c>PxCliffRigBakeTests</c> so a re-drop that moves one fails CI.</summary>
        public const string PxSidecarPath = "docs/art/rigs/px-cliff-face-kit/pxCliffFaceRig.gameplay.json";

        /// <summary>The JS globals the px pair installs.</summary>
        public const string PxRigGlobalName = "PxCliffFace", PxLanguageGlobalName = "PxLang";

        /// <summary>The palette LUT as the kit shipped it — the reference the baked
        /// <see cref="PxPalettePath"/> is held equal to, pixel for pixel.</summary>
        public const string PxKitPalettePath = "docs/art/rigs/px-cliff-face-kit/CliffPx_palette.png";

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
        /// The shader's PROPERTY DEFAULT is the S row below.</para>
        ///
        /// <para><b>⚠ A frame mismatch, noted and NOT fixed here (px lane, 09-18).</b> Both kits pack the
        /// normal's G channel y-UP the face (<c>cliffRig.js</c> :867, <c>pxCliffFaceRig.js</c> :579),
        /// while these keys are in the rig's y-DOWN frame, and the v10 shader dots them raw. So the v10
        /// cast-shadow recovery divides by a slightly wrong <c>N·Lbake</c>. It is pre-existing and it is
        /// not this lane's to change. The px branch does not inherit it: it flips the key into the
        /// packed frame (<see cref="CliffPxRelightMath.PackedFrame"/>), and on the kit's own sample the
        /// recovered shadow tier then agrees with the rig's for 98.9% of cells, against 80.9% unflipped
        /// (<c>PxCliffRigBakeTests</c> holds the property).</para>
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

        /// <summary>The v10 colour channel — the wall's first texture slot.</summary>
        public const string UnlitChannel = "_unlit";

        /// <summary>
        /// The px kit's palette INDEX channel: R the LUT row, G the band 0..4, B "a rock texel, may take
        /// a tier step", A coverage.
        ///
        /// <para><b>⭐ ROUTE (b): IT RIDES THE <c>_unlit</c> SLOT.</b> The scenes on both coasts reference
        /// each band's <c>_unlit</c> texture by GUID, and there is no fourth slot. So a px bake MOVES each
        /// <c>&lt;face&gt;_unlit.png</c> to <c>&lt;face&gt;_index.png</c> with
        /// <c>AssetDatabase.MoveAsset</c> — which keeps the GUID — before writing the index bytes into it.
        /// Every serialised reference then resolves to the index with no scene change; a v10 bake moves
        /// it back. Renaming the band field itself is PR 3.</para>
        /// </summary>
        public const string IndexChannel = "_index";

        /// <summary>The px bake's three wall channels — <see cref="LiveChannels"/> with the colour slot
        /// carrying <see cref="IndexChannel"/>.</summary>
        public static readonly string[] PxLiveChannels = { IndexChannel, "_normal", "_mask" };

        /// <summary>Whether a channel imports as sRGB. Albedo is colour; the normal, the mask, the
        /// displacement profile and the px index are DATA and must import linear — the first three or
        /// every lighting term is gamma-bent, the index or every row and band it holds is a wrong
        /// palette entry.</summary>
        public static bool IsSrgb(string channelSuffix) =>
            channelSuffix != "_normal" && channelSuffix != "_mask" && channelSuffix != IndexChannel;

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
        //  THE MATERIAL AND THE PX PALETTE
        // =====================================================================================

        /// <summary>The one shipped wall material every band and chunk shares through a property
        /// block. The px bake flips <see cref="PxKeyword"/> on it and hands it the LUT.</summary>
        public const string MaterialPath = "Assets/_Project/Art/Materials/CliffFace.mat";

        /// <summary>
        /// The <c>shader_feature_local</c> keyword that selects the shader's px branch.
        ///
        /// <para><b>A BAKE-TIME switch, never a runtime one</b> (owner, 09-18: "bake switch"). The bake
        /// menu sets it and clears it with the pixels it writes, because the <c>_unlit</c> slot holds
        /// either colour or an index and the branch must match what is on disk. The material ships
        /// with it OFF.</para>
        /// </summary>
        public const string PxKeyword = "_HH_CLIFF_PX";

        /// <summary>
        /// Where the px palette LUT lives — <b>TRACKED</b>, outside the gitignored
        /// <see cref="BakeRoot"/>. The material is committed and references it by GUID, so the LUT and
        /// its meta are committed too: a clone must never hold a material whose palette resolves only
        /// on the machine that baked it.
        /// </summary>
        public const string PxPaletteFolder = "Assets/_Project/Art/Terrain/CliffPx";
        public const string PxPaletteName = "CliffPx_palette";
        public const string PxPalettePath = PxPaletteFolder + "/" + PxPaletteName + ".png";

        /// <summary>The LUT: 8 columns (bands 0..4, then 5..7 repeating band 4 to a power of two) ×
        /// 32 rows (25 used: three rocks × six tiers, then seven accessory palettes; 25..31 alpha 0).</summary>
        public const int PxPaletteWidth = 8, PxPaletteHeight = 32, PxPaletteBands = 5, PxPaletteRows = 25;

        /// <summary>
        /// The px rig's ONE key light, in the rig frame, before the batter tips it:
        /// <c>PxLang.LIGHT.key</c>. The px kit bakes all five aspects at this one key (the v10 kit
        /// baked each at its own, <see cref="AspectBakeLights"/>), so the shader's px branch divides the
        /// mask by this key, tipped per batter and flipped into the packed frame.
        /// <c>PxCliffRigBakeTests</c> holds it equal to the rig's.
        /// </summary>
        public static readonly Vector3 PxBakeKey = new Vector3(-0.55f, -0.66f, 0.52f).normalized;

        // =====================================================================================
        //  IMPORT CONTRACT (README §6)
        // =====================================================================================

        /// <summary>Faces and profiles tile along the shore, so they wrap in S. Only the faces wrap in T
        /// as well (a face is periodic in both; a profile and a strip are clamped at top and bottom, and
        /// Unity's importer has one wrap mode per axis). The ledges and the px palette clamp both ways
        /// — a wrapping LUT would bleed band 4 into band 0 at the column edge.</summary>
        public static TextureWrapMode WrapU(CliffAssetKind kind) =>
            kind == CliffAssetKind.Ledge || kind == CliffAssetKind.Palette
                ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;

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
        /// <summary>The px kit's 8 × 32 palette LUT (<see cref="CliffCatalog.PxPalettePath"/>) — colour,
        /// point-sampled, clamped both ways.</summary>
        Palette = 4,
    }
}
