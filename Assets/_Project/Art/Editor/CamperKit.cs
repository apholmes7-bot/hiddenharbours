using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The camper kit's identity: which builds get baked, at what facings and door-swing frames, and
    /// the contract schema the slicer and the runtime presenter read back.
    ///
    /// <para>Everything here is DATA. The scope of a bake is <see cref="Builds"/> — one row per sheet —
    /// so re-scoping the kit is a table edit, never a code change. Cell geometry, pivots and the crop
    /// rect are read FROM THE RIG at bake time (ADR 0021 §4); this file never restates them.</para>
    ///
    /// <para><b>Two sheets per variant, and the difference between them is the awning.</b> A
    /// <c>rest</c> sheet is the camper as the kit ships it — the Clipper's awning fitted, because that
    /// is its default — and it is the sheet whose per-facing boxes are the README's published table. An
    /// <c>enter</c> sheet is the door cue, and it bakes with the awning OFF because the Clipper's
    /// default awning stands directly over its own door: measured, the swing 0→1 repaint at W is 253 px
    /// with the awning and 1535 px without. See <see cref="AwningPop"/> for what that split costs.</para>
    /// </summary>
    public static class CamperKit
    {
        /// <summary><see cref="HiddenHarbours.Tools.RigBaking.RigCatalog"/> key.</summary>
        public const string RigKey = "camper";

        /// <summary>The global the rig installs.</summary>
        public const string GlobalName = "CamperIso";

        public const string OutputFolder = "Assets/_Project/Art/Sprites/Camper/Iso";
        public const string ContractPath = OutputFolder + "/Camper.json";

        /// <summary>
        /// Unity's default import ceiling, and the number this kit solves its packing for.
        ///
        /// <para>⚠️ The bake cap and the IMPORT cap must be the SAME number. They fail in opposite
        /// directions: above Unity's hard 4096 a bake refuses, but BETWEEN this value and 4096 the bake
        /// succeeds and the import silently DOWNSCALES with the sprite count still correct — which
        /// poisons every slice rect without a single error.</para>
        ///
        /// <para>This is what fixes the sheet layout at <b>columns = swing frames, rows = facings</b>.
        /// The obvious layout — a row of eight facings — puts the Clipper at 8 × 322 = 2576 px wide,
        /// over this cap, before a single cue frame is added. Turned the other way the widest sheet is
        /// 6 × 322 = 1932.</para>
        /// </summary>
        public const int ImportSizeCap = 2048;

        /// <summary>Pixels per metre. The rig's own <c>PX</c>, cross-checked at bake time.</summary>
        public const int PixelsPerUnit = 32;

        /// <summary>Facings. The rig's <c>DIRS</c>, cross-checked at bake time.</summary>
        public const int Facings = 8;

        /// <summary>
        /// Frames in the door cue, <c>swing</c> 0 → 1 inclusive — the rig's <c>frames(dir,n)</c> ladder.
        ///
        /// <para>A chosen granularity, not a rig-declared one: <c>swing</c> is continuous and all ten
        /// frames of an <c>n=10</c> ladder render distinctly. Six is where the consecutive-frame repaint
        /// at W sits around 850–1050 px on the Clipper — a step you read as motion rather than as a
        /// jump — and it is what keeps the Clipper's enter sheet at 1932 × 1848, inside
        /// <see cref="ImportSizeCap"/>. Seven columns would be 2254 px and over it.</para>
        /// </summary>
        public const int CueFrames = 6;

        /// <summary>Frames on a <c>rest</c> sheet: the parked camper, door shut.</summary>
        public const int RestFrames = 1;

        /// <summary>
        /// The weather scalar every sheet bakes at — the rig's own default, passed EXPLICITLY on every
        /// render rather than left to <c>resolve</c>.
        ///
        /// <para>Spelling out every option is this repo's rule for these rigs and it is not
        /// superstition: the fuel kit's <c>wear</c> has a fall-through that shares a geometry-cache key
        /// with a real state, so an omitted option there makes the output depend on what was rendered
        /// earlier in the session. Nothing in <c>camperIsoRig.js</c> does that today. The habit stays
        /// because the next drop is the one that would.</para>
        /// </summary>
        public const float BakedWeather = 0.32f;

        /// <summary>
        /// The bare polished-aluminium skin — <c>paint: null</c>, the rig's default and the whole point
        /// of a riveted monocoque (a 9-step alum ramp plus a polish term; any BODY ramp drops the polish
        /// to a matte 0.22).
        ///
        /// <para>The kit's other ten ramps are a RE-BAKE, not a re-author: adding a paint axis is a row
        /// per skin in <see cref="Builds"/> and nothing else. Not done here because nothing asks for a
        /// painted camper yet and eleven skins would be eleven times the texture memory.</para>
        /// </summary>
        public const string BakedPaint = "null";

        // ---- the build table -------------------------------------------------------------------

        /// <summary>What a sheet is FOR, which is also what decides its awning and its frame count.</summary>
        public enum Role
        {
            /// <summary>The parked camper at the variant's DEFAULT fit-out, door shut. One frame.</summary>
            Rest,

            /// <summary>The door cue, awning OFF so it reads. <see cref="CueFrames"/> frames.</summary>
            Enter,
        }

        /// <summary>One baked sheet: one variant in one role, all facings × that role's frames.</summary>
        public readonly struct Build
        {
            public readonly string Variant;
            public readonly Role Role;

            public Build(string variant, Role role) { Variant = variant; Role = role; }

            /// <summary>Sheet stem, e.g. <c>camper_clipper_enter</c>. Also the contract key.</summary>
            public string Key => $"camper_{Variant}_{Role.ToString().ToLowerInvariant()}";

            /// <summary>Frames on this sheet's swing axis.</summary>
            public int Frames => Role == Role.Enter ? CueFrames : RestFrames;

            /// <summary>
            /// Whether the awning is fitted.
            ///
            /// <para><b>Only <see cref="Role.Enter"/> decides this; <see cref="Role.Rest"/> defers to
            /// the rig.</b> A rest sheet bakes the variant's own default, which the baker READS from
            /// <c>resolve({variant}).awning</c> rather than restating — so a drop that changes which
            /// variant fits an awning changes the sheets without anyone editing this file. An enter
            /// sheet is always unawninged, because the awning is what hides the cue.</para>
            /// </summary>
            public bool? AwningOverride => Role == Role.Enter ? false : (bool?)null;

            public override string ToString() => Key;
        }

        /// <summary>
        /// The rig's two lengths, in its own <c>list()</c> order. Kept here so the build table can be
        /// built without a host; the baker checks this list against the live rig, because a variant
        /// added upstream and missed here bakes a short kit.
        /// </summary>
        public static readonly IReadOnlyList<string> Variants = new[] { "bantam", "clipper" };

        /// <summary>Every sheet this kit bakes: both lengths, both roles.</summary>
        public static IReadOnlyList<Build> Builds =>
            Variants.SelectMany(v => new[] { new Build(v, Role.Rest), new Build(v, Role.Enter) })
                    .ToArray();

        // ---- the one thing this split costs, on the record --------------------------------------

        /// <summary>
        /// ⚠️ <b>The Clipper's awning is on its rest sheet and off its enter sheet, so it VANISHES the
        /// moment the door starts to open.</b> Measured at the crop the sheets share: rest and enter
        /// frame 0 differ by 2858–5083 px per facing on the Clipper, and by exactly <b>0</b> on the
        /// Bantam, which fits no awning either way (<c>CamperBakeTests</c> pins both halves).
        ///
        /// <para>This is a consequence of the handoff's instruction to bake the cue unawninged, not an
        /// accident, and it is a RUNTIME choice rather than a bake defect — the two sheets share a cell
        /// size and a pivot exactly, so a presenter that wants no pop can simply use
        /// <c>camper_clipper_enter</c> frame 0 as its parked frame and never draw the awning. What it
        /// cannot do is cross-fade: there is no sheet where the awning furls.</para>
        /// </summary>
        public const string AwningPop =
            "clipper rest carries the awning, clipper enter does not — 2858..5083 px per facing";

        // ---- the contract ----------------------------------------------------------------------

        /// <summary>One anchor the rig publishes, in CROPPED cell px, top-left origin.</summary>
        [Serializable]
        public class AnchorEntry
        {
            public int facing;                 // sheet ROW (clockwise, post-correction)
            public int rigDir;                 // the rig dir that row was rendered at
            public float doorX, doorY;         // the leaf's own centre — where the cue happens
            public float stepX, stepY;         // the unfolded tread, i.e. where a body stands to enter
            public float hitchX, hitchY;       // the coupler, for parking a camper against a truck
        }

        /// <summary>
        /// One sheet, as written to <see cref="ContractPath"/>. Plain fields and
        /// <see cref="JsonUtility"/> both ways, so the serialiser and the parser are one code path.
        /// </summary>
        [Serializable]
        public class SheetEntry
        {
            public string key;                 // camper_clipper_enter
            public string variant;             // bantam | clipper
            public string role;                // rest | enter
            public bool awning;                // as RESOLVED, never as assumed
            public float weather;
            public int facings;
            public int frames;                 // the swing axis
            public int cellW, cellH;           // the CROPPED cell
            public int cropX, cropY;           // where that cell sits in the rig's native 384×320
            public int pivotX, pivotY;         // top-left origin, inside the cropped cell
            public int columns, rows;          // columns = frames, rows = facings
            public int sheetW, sheetH;
            public int maxCueDeltaPx;          // the widest swing 0→1 repaint over all facings
            public bool pivotInsideInk;
            public AnchorEntry[] anchors;

            /// <summary>Unity's normalised, BOTTOM-origin sprite pivot. The y term is
            /// <c>(H − pivotY)/H</c> per ADR 0026 — the hull convention, because a camper's pivot is a
            /// projected POINT (the ground-centre of its body footprint) and not a chosen row.</summary>
            public Vector2 NormalisedPivot =>
                new Vector2(pivotX / (float)cellW, (cellH - pivotY) / (float)cellH);

            /// <summary>
            /// Sprite name for one cell. Rows are facings, columns are swing frames.
            ///
            /// <para><b>The facing suffix comes LAST on a single-frame sheet, and that is the point.</b>
            /// This repo's kits all bake to <c>stem…_d0 … _d7</c> (the fuel kit's
            /// <c>jerry_s20_gas_f2_d3</c> is the pattern), so a consumer that wants "this thing at
            /// facing 2" matches on the <c>_d2</c> suffix. A <c>rest</c> sheet has one frame, so a
            /// swing index in its name would be noise — and leaving it off makes the parked frame the
            /// UNIQUE answer to that suffix, instead of one of seven candidates competing with five
            /// mid-swing cue frames. An <c>enter</c> sheet is an animation strip and is addressed
            /// (facing, then frame), so its names end in the frame.</para>
            /// </summary>
            public string SpriteName(int facing, int frame) =>
                frames > 1 ? $"{key}_d{facing}_s{frame}" : $"{key}_d{facing}";
        }

        [Serializable]
        public class Contract
        {
            public string generated;
            public string rigKey, globalName;

            /// <summary>
            /// The convention the RIG turns in, measured by <c>CamperRigAzimuthProbe</c>.
            ///
            /// <para>⚠️ The SHEETS are the other one. A counter-clockwise rig is baked cell <c>k</c> from
            /// <c>render((8−k)%8)</c> (<c>RigBaker.DirForCell</c>), so what lands on disk is genuinely
            /// clockwise — which is why every entry's <see cref="AnchorEntry.rigDir"/> records the dir
            /// its row actually came from.</para>
            /// </summary>
            public string rigConvention;

            public int pixelsPerUnit;
            public int importSizeCap;
            public float weather;
            public string paint;
            public SheetEntry[] sheets;

            public SheetEntry Find(string key) =>
                sheets?.FirstOrDefault(s => string.Equals(s.key, key, StringComparison.Ordinal));
        }
    }
}
