#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// <b>THE DOORYARD KIT — which of the yard rig's 62 pieces bake, at what dressing, and the contract
    /// the slicer and the placer read back.</b>
    ///
    /// <para>Sibling of <see cref="ShopFixtureKit"/> and deliberately the same shape: a build TABLE, a
    /// contract written BY the bake, and no geometry restated in this file. Everything measurable — the
    /// cell, the pivot, the panel's own metres — is read from the rig at bake time (ADR 0021 §4).</para>
    ///
    /// <para><b>⭐ TEN PIECES, NOT SIXTY-TWO, AND THE LINE IS DRAWN AT WHAT ENCLOSES A YARD.</b> S2 places
    /// the BOUNDARY: the fence styles the region yard tables already author, their corner posts and
    /// their gates. The other 52 pieces are dooryard DRESSING — beds, planters, clotheslines, swing
    /// sets, buoy posts — and which property gets which is one of the owner decisions still batched on
    /// #604. Adding one is a row here; the baker, the slicer and the contract are already written
    /// against the table.</para>
    ///
    /// <para><b>⚠️ THIS KIT DOES NOT BAKE THROUGH <c>IsoPropSheetBaker</c>, AND THE REASON IS A GATE, NOT
    /// A SHAPE.</b> Its <c>render(key, dir, opts)</c> call and its fixed 470×540 buffer are exactly that
    /// baker's, and <c>IsoPropSheetBaker.MeasureCell</c> IS the cell rule used here. What it cannot pass
    /// is <c>IsoPackContract.AssertKeylineGated</c>, which refuses any rig exposing no
    /// <c>KEYLINE_DEFAULT</c> global — a gate the owner set on 2026-08-06 for the four ISO-PACK rigs
    /// specifically. ADR 0031's standing consequence for everything else is that a sprite family keeps
    /// its keyline until its own natural redo and a mixed period is expected; this kit is new art with
    /// an ungated ring, so it bakes ringed like the fuel, nav-buoy and shop-fixture kits before it, and
    /// its retirement rides the outline-language arc rather than blocking a dooryard.</para>
    /// </summary>
    public static class YardKit
    {
        /// <summary><c>RigCatalog</c> key — registered in <c>RigCatalog.YardKit.cs</c>.</summary>
        public const string RigKey = "yardIso";

        /// <summary>The global the rig installs.</summary>
        public const string GlobalName = "YardIso";

        public const string OutputFolder = "Assets/_Project/Art/Sprites/Yard";
        public const string ContractFileName = "yardIso.contract.json";
        public static string ContractPath => OutputFolder + "/" + ContractFileName;

        public const string StemPrefix = "Yard_";

        /// <summary>Pixels per metre — the rig's own <c>PX</c>, asserted against it at bake time.</summary>
        public const int PixelsPerUnit = 32;

        /// <summary>Facings every piece bakes at. The rig's <c>order</c> array is 8 long; this is checked
        /// against it, never assumed.</summary>
        public const int Facings = 8;

        /// <summary>
        /// Unity's DEFAULT import ceiling, which this family solves its packing for.
        ///
        /// <para><b>⚠️ The bake cap and the IMPORT cap must be the SAME number</b>, and they fail in
        /// opposite directions: over Unity's hard 4096 a bake refuses, but BETWEEN this value and 4096
        /// the bake succeeds and the import silently DOWNSCALES with the sprite count still correct.
        /// The rig's native buffer is 470 px wide and the longest panel here is about 3 m, so eight
        /// cropped cells in one row have room to spare — a sheet near this cap means the rig changed.
        /// </para>
        /// </summary>
        public const int ImportSizeCap = 2048;

        // ---- the dressing every sheet bakes at -------------------------------------------------

        /// <summary>Camera elevation in degrees. The rig defaults to this; passed EXPLICITLY on every
        /// render so a drop that re-seats the default changes the sheets through this constant rather
        /// than behind it.</summary>
        public const double Elevation = 40;

        /// <summary>
        /// The calendar station every sheet bakes at — high summer.
        ///
        /// <para>The rig carries the shrub kit's 8-station year and this world has no season system, so
        /// one station is baked and it is the one the island is dressed in everywhere else. ⚠️ It is
        /// passed explicitly because <c>resolve()</c> falls through to <c>green</c> for any value it does
        /// not recognise — a misspelling would bake summer under a winter name with no error.</para>
        /// </summary>
        public const string BakedPhase = "green";

        /// <summary>Snow cap, 0..1. Stated rather than omitted: <c>resolve()</c> defaults it off the
        /// PHASE, so a later bake at <c>dormant</c> would silently frost every rail.</summary>
        public const float BakedSnow = 0f;

        /// <summary>Grime and sun-silvered timber, 0..1. The rig's own default, stated.</summary>
        public const float BakedWeather = 0.4f;

        /// <summary>Lamp-lit dressing. Night is a runtime lighting concern here, so the sheets bake by
        /// day.</summary>
        public const bool BakedNight = false;

        /// <summary>
        /// Composition with the flower and shrub rigs, and it is <b>OFF on purpose</b>.
        ///
        /// <para>⚠️ The kit's bed pieces ask <c>globalThis.Flowers</c>/<c>globalThis.Shrubs</c> for real
        /// sprites and fall back to native mounds when those are absent — so a baker that loads the yard
        /// rig ALONE still draws a bed, a DIFFERENT one, silently (the kit's IMPORT.md, gate 2). None of
        /// the ten pieces baked here is a bed and a hedge is native by design, so composition cannot
        /// change these pixels — but stating it is what keeps that true the day a baker in this process
        /// happens to have loaded the flower rig first.</para>
        /// </summary>
        public const bool BakedCompose = false;

        // ---- the build table --------------------------------------------------------------------

        /// <summary>What a baked piece is FOR on a boundary. The placer dispatches on this rather than on
        /// a piece name, so a style that swaps its panel swaps one row.</summary>
        public enum Role
        {
            /// <summary>A repeating length of boundary. Tiled along a straight segment.</summary>
            Panel,

            /// <summary>A post. Stands at a corner and at each lip of a gate.</summary>
            Post,

            /// <summary>A gate leaf, standing in the opening the boundary opens at.</summary>
            Gate,
        }

        /// <summary>
        /// One baked sheet: a piece from the rig's catalog at one dressing.
        ///
        /// <para><b>Every option that moves a pixel is on this row.</b> <c>paint</c>, <c>variant</c>,
        /// <c>len</c>, <c>kept</c>, <c>phase</c>, <c>snow</c>, <c>weather</c>, <c>night</c> and
        /// <c>seed</c> are the rig's whole option surface for a fence piece (<c>planted</c> is read only
        /// by beds and planters, which this table does not bake). ⚠️ Omitting one does NOT mean "no
        /// value" — <c>resolve()</c> substitutes its own, and for <c>len</c> that default is <b>0.4</b>
        /// on a run piece rather than 0. Every key is written out.</para>
        /// </summary>
        public readonly struct Build
        {
            /// <summary>The rig's piece id — checked against <c>YardIso.list()</c> before rendering.
            /// </summary>
            public readonly string Piece;

            /// <summary>What it does on a boundary.</summary>
            public readonly Role Part;

            /// <summary>Body paint ramp key, or null for the pieces the kit leaves unpainted (stone,
            /// wire, hedge, split rail). ⚠️ Passed EXPLICITLY as <c>null</c> rather than omitted —
            /// omitting it takes the piece's own default, and <c>picketPanel</c>'s default is white.
            /// </summary>
            public readonly string Paint;

            /// <summary>Index into the piece's <c>variants[]</c>.</summary>
            public readonly int Variant;

            /// <summary>Panel-length dial, 0..1. Footprint grows by the piece's own <c>runGain</c>; the
            /// metres it comes out at are MEASURED from the rig at bake time and recorded in the
            /// contract, never computed here.</summary>
            public readonly float Len;

            /// <summary>Upkeep, 0..1. The kit's headline axis: it moves paint chalk, lost pickets, post
            /// lean, line sag, weed skirt and hedge-clip roughness together. This is where a property's
            /// character lives.</summary>
            public readonly float Kept;

            /// <summary>Dressing seed. Fixed so a re-bake is reproducible.</summary>
            public readonly int Seed;

            /// <summary>Why this dressing. Prose in the data, for the same reason a yard row carries a
            /// reason.</summary>
            public readonly string Reason;

            public Build(string piece, Role part, string paint, int variant, float len, float kept,
                         int seed, string reason)
            {
                Piece = piece; Part = part; Paint = paint; Variant = variant;
                Len = len; Kept = kept; Seed = seed; Reason = reason;
            }

            /// <summary>Stable sheet key. One dressing per piece today, so the piece id IS the key — a
            /// second dressing of the same piece would need this to grow a suffix, and the contract
            /// would say so.</summary>
            public string Key => Piece;
        }

        /// <summary>
        /// The rig's own default panel dial for a run piece, stated because it is the one option whose
        /// omission changes GEOMETRY rather than colour: <c>resolve()</c> hands a run piece
        /// <c>len = 0.4</c> and a fixed piece <c>len = 0</c>. Taking the rig's default rather than
        /// inventing a number is the ADR 0021 §4 habit; what it buys is panels of roughly 2.3–3.0 m
        /// instead of 1.6–2.2 m, which is about a third fewer sprites over the fence the yard tables
        /// ask for.
        /// </summary>
        public const float RunLen = 0.4f;

        /// <summary>Panel dial for a piece that does not run. The rig's own default for those.</summary>
        public const float FixedLen = 0f;

        /// <summary>
        /// The table — ten pieces, one dressing each.
        ///
        /// <para>⚠️ <b>Post and rail, split rail and wire get NO corner post, and that is the kit's
        /// answer rather than a gap.</b> <c>fenceCorner</c> is a picket corner (its only variant is
        /// <c>picket</c>) and <c>stonePillar</c> is masonry; a rail fence's own panel carries its end
        /// posts, which is what a rail fence actually looks like where it turns. The placer's style
        /// table states the absence rather than reaching for the nearest piece.</para>
        /// </summary>
        public static readonly Build[] Builds =
        {
            // ---- picket: the kept front ---------------------------------------------------------
            new Build("picketPanel", Role.Panel, "white", variant: 0, len: RunLen, kept: 0.88f, seed: 11,
                      "the painted front of a kept house — high upkeep, because this is the yard the " +
                      "others on the green are read against"),
            new Build("picketGate", Role.Gate, "white", variant: 0, len: FixedLen, kept: 0.88f, seed: 11,
                      "shut (variant 0). A gate baked open would read as an invitation the boundary " +
                      "behind it does not honour — the walk-through is the GAP in the runs, not the " +
                      "swing of the leaf"),
            new Build("fenceCorner", Role.Post, "white", variant: 0, len: FixedLen, kept: 0.88f, seed: 11,
                      "the picket corner post, kept to match its panels"),

            // ---- post and rail: a working household ---------------------------------------------
            new Build("postRail", Role.Panel, "white", variant: 0, len: RunLen, kept: 0.72f, seed: 23,
                      "whitewashed three-rail, a notch less kept than the picket: paint you renew when " +
                      "you get to it"),

            // ---- split rail: what is still standing because nobody took it away ------------------
            new Build("splitRail", Role.Panel, null, variant: 0, len: RunLen, kept: 0.24f, seed: 37,
                      "⭐ the LOW-kept row, and the reason kept is a data axis rather than a colour " +
                      "grade: down here the zigzag leans off plumb and goes to weed at the base, which " +
                      "is what Aunt Ginny's yard row asks for in words"),

            // ---- page wire: keeps a dog in ------------------------------------------------------
            new Build("wireFence", Role.Panel, null, variant: 0, len: RunLen, kept: 0.62f, seed: 41,
                      "farm wire on posts, at the rig's own middling upkeep — a working yard, neither " +
                      "kept nor let go"),

            // ---- fieldstone: what the ground gave up ---------------------------------------------
            new Build("stoneWall", Role.Panel, null, variant: 0, len: RunLen, kept: 0.70f, seed: 53,
                      "low fieldstone (variant 0). The high course reads as an estate wall; this coast " +
                      "stacked what came out of the field"),
            new Build("stonePillar", Role.Post, null, variant: 0, len: FixedLen, kept: 0.70f, seed: 53,
                      "capped (variant 0), not finial — the gatepost of a farm, not of a manor"),

            // ---- clipped hedge: blocks the same ground, reads as care ----------------------------
            new Build("hedgeRun", Role.Panel, null, variant: 0, len: RunLen, kept: 0.85f, seed: 67,
                      "low clipped hedge (variant 0). Kept high on purpose: an unclipped hedge is a " +
                      "shrub, and the kit draws those from another family"),
            new Build("hedgeCorner", Role.Post, null, variant: 0, len: FixedLen, kept: 0.85f, seed: 67,
                      "the hedge's own return, clipped to match"),
        };

        public static Build? FindBuild(string key)
        {
            foreach (var b in Builds)
                if (string.Equals(b.Key, key, StringComparison.Ordinal)) return b;
            return null;
        }

        // ---- names ------------------------------------------------------------------------------

        public static string StemFor(string key) => StemPrefix + key;
        public static string SheetPath(string key) => OutputFolder + "/" + StemFor(key) + ".png";

        /// <summary>Sprite name for one baked facing. Columns are facings; there is one row.</summary>
        public static string SpriteNameFor(string key, int facing) => StemFor(key) + "_d" + facing;

        // ---- the contract -------------------------------------------------------------------------

        [Serializable]
        public sealed class Contract
        {
            public string note;
            public string generated;
            public string rigKey, globalName;

            /// <summary>The handedness the bake MEASURED, and the thing that entitles a placer to read
            /// cell <c>k</c> as compass bearing 45k.</summary>
            public string convention;
            public string conventionNote;
            public string pivotNote;

            public int ppu;
            public int facings;
            public int importSizeCap;

            /// <summary>The rig's native buffer before the union crop — a rig constant, recorded so a
            /// drop that resizes it shows in the diff.</summary>
            public int nativeCellW, nativeCellH;
            public int nativePivotX, nativePivotY;

            /// <summary>Baked dressing, so the sheets can be read back without this file's C#.</summary>
            public string phase;
            public float snow, weather, elev;
            public bool night, compose;

            /// <summary>
            /// The MEASURED cell → face-bearing table, degrees clockwise from north, in cell order.
            ///
            /// <para><b>⚠️ THIS IS THE FINDING THE WHOLE KIT TURNS ON.</b> The rig's sidecar declares
            /// <c>contract.facings = [N, NE, E, SE, S, SW, W, NW]</c> against its own <c>dir</c> index,
            /// and that list is CLOCKWISE while the rig steps counter-clockwise: the two agree only at
            /// dir 0 and dir 4. Look up "E" in the sidecar, take dir 2, and draw a panel facing WEST.
            /// This array is measured from the rig's own <c>project()</c> at bake time in the BAKED CELL
            /// frame — after <c>RigBaker.DirForCell</c> has applied the counter-clockwise correction —
            /// so it comes out as the plain clockwise compass every other baked kit in this repo
            /// carries, and it is MEASURED rather than asserted.</para>
            /// </summary>
            public float[] cellFaceBearings;

            /// <summary>The measured run axis per cell — the direction the panel's own length lies
            /// along, degrees clockwise from north. Exactly 90° from <see cref="cellFaceBearings"/> at
            /// every cell by the kit's axis contract (+X along the run, +Y the face); recorded because
            /// that is a claim a test can fail on rather than a sentence.</summary>
            public float[] cellRunBearings;

            public Entry[] pieces;

            public Entry Find(string key) =>
                pieces?.FirstOrDefault(e => string.Equals(e.key, key, StringComparison.Ordinal));
        }

        [Serializable]
        public sealed class Entry
        {
            public string key, piece, label, cat, role, reason;

            /// <summary>The EXACT options expression <c>render()</c> was handed — the audit trail, and
            /// the only complete statement of what these pixels are.</summary>
            public string optionsJs;

            public string paint;
            public int variant, seed;
            public float len, kept;

            /// <summary>Sheet file name (no folder).</summary>
            public string sheet;

            public int facings, columns, rows;

            /// <summary>The CROPPED cell every facing is packed at.</summary>
            public int cellW, cellH;

            /// <summary>Where the crop was taken from in the rig's native buffer.</summary>
            public int cropX, cropY;

            /// <summary>The piece's GROUND CENTRE in cropped-cell px from the cell's TOP-LEFT.</summary>
            public int pivotX, pivotY;

            public int sheetW, sheetH;
            public bool pivotInsideInk;

            /// <summary>
            /// The piece's footprint in METRES at the baked <c>len</c>, read from the rig's own
            /// <c>footprint()</c>: <c>x</c> along the run, <c>y</c> through the fence.
            ///
            /// <para><b>⭐ This is the number the placer tiles on</b>, and it must come from the rig at
            /// the baked <c>len</c> rather than from the sidecar's <c>size[]</c> samples — those are
            /// tabulated at len 0, 0.4 and 1 only, so any other dial would be interpolated and the
            /// panels would drift out of step with their own pixels.</para>
            /// </summary>
            public float footprintWidthMetres, footprintDepthMetres, heightMetres;

            public long pngBytes;
            public long runtimeBytesRgba32;

            public string Stem => StemFor(key);
            public string SpriteName(int facing) => SpriteNameFor(key, facing);

            /// <summary>Unity's normalised, BOTTOM-origin sprite pivot. The y term is
            /// <c>(H − pivotY)/H</c> per ADR 0026 — the piece's pivot is a projected POINT (its ground
            /// centre), not a chosen row.</summary>
            public Vector2 NormalisedPivot =>
                cellW <= 0 || cellH <= 0
                    ? new Vector2(0.5f, 0.5f)
                    : new Vector2(pivotX / (float)cellW, (cellH - pivotY) / (float)cellH);
        }

        /// <summary>The committed contract, or null with a logged warning if it is missing/unparseable.
        /// Null rather than throwing: a region build should say what is not baked yet and carry on,
        /// which is how every other kit in this folder behaves.</summary>
        public static Contract Load()
        {
            string abs = AbsoluteContractPath;
            if (!File.Exists(abs))
            {
                Debug.LogWarning(
                    "[YardKit] No contract at '" + ContractPath + "'. Run Hidden Harbours ▸ Art ▸ Bake " +
                    "Yard Kit — the sheets and this file are written together, and a consumer reading " +
                    "one without the other places a stale sheet on a fresh pivot.");
                return null;
            }

            var contract = JsonUtility.FromJson<Contract>(File.ReadAllText(abs));
            if (contract?.pieces == null || contract.pieces.Length == 0)
            {
                Debug.LogError("[YardKit] '" + ContractPath + "' parsed but carries no pieces.");
                return null;
            }
            return contract;
        }

        public static string AbsoluteContractPath =>
            Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, ContractPath);

        /// <summary>
        /// The rig options expression for one build — written once, so the bake, the contract's audit
        /// trail and any probe all hand the rig the SAME string.
        /// </summary>
        public static string OptionsJs(in Build b) =>
            "{paint:" + (b.Paint == null ? "null" : Quote(b.Paint)) +
            ",variant:" + b.Variant.ToString(CultureInfo.InvariantCulture) +
            ",len:" + Num(b.Len) +
            ",kept:" + Num(b.Kept) +
            ",phase:" + Quote(BakedPhase) +
            ",snow:" + Num(BakedSnow) +
            ",weather:" + Num(BakedWeather) +
            ",night:" + (BakedNight ? "true" : "false") +
            ",seed:" + b.Seed.ToString(CultureInfo.InvariantCulture) +
            ",compose:" + (BakedCompose ? "true" : "false") +
            ",elev:" + Num((float)Elevation) + "}";

        static string Num(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        static string Quote(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

        /// <summary>sRGB lives on <see cref="TextureImporterSettings"/> rather than on the importer —
        /// restated because Art.Editor kits do not reference each other's helpers.</summary>
        public static bool SRgbOf(TextureImporter importer)
        {
            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            return s.sRGBTexture;
        }
    }
}
#endif
