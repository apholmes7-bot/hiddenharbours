#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The PLACEMENT CONTRACT for the Acadian tree kit — the schema of
    /// <c>Assets/_Project/Art/Foliage/Trees/Trees.json</c> and the one place anything downstream
    /// reads a tree's cell, pivot, flare pad, trunk anchor or true height.
    ///
    /// <para><b>Serializer == parser.</b> <c>TreeRigBaker</c> writes this exact type with
    /// <see cref="JsonUtility.ToJson(object,bool)"/> and <see cref="Load"/> reads it back with
    /// <see cref="JsonUtility.FromJson{T}"/>, so the bake and its consumers cannot drift apart —
    /// the same rule <c>CatchStorageAnchors</c> established for the storage kit.</para>
    ///
    /// <para>⚠️ <b>NOTHING HERE IS A HAND-MAINTAINED TABLE.</b> Every number is read from
    /// <c>TreeRig.sheetSpec()</c> at bake time (ADR 0021 §4: cell geometry, pivot and the crop rect
    /// come from the rig, not from a README). If a species' cell changes, re-bake — do not edit the
    /// JSON.</para>
    /// </summary>
    public static class TreeKitCatalog
    {
        /// <summary>The kit's folder. A SIBLING of the loose <c>Foliage/*.png</c> single-tree
        /// sprites from the 2026-07-15 drop (which share these species names and are NOT rig art),
        /// and outside <see cref="FoliageSheetSlicer.FlowersRoot"/> so neither tool sees the
        /// other's sheets.</summary>
        public const string TreesRoot = "Assets/_Project/Art/Foliage/Trees/";

        public const string ContractFileName = "Trees.json";

        public static string ContractPath => TreesRoot + ContractFileName;

        /// <summary>
        /// The rig this kit is baked from. Read-only reference for us — it is the art-director
        /// role's file (<c>docs/art/rigs/**</c>).
        ///
        /// <para><b>PASS 3 since 2026-09-02.</b> <c>treeIsoRig3.js</c> supersedes
        /// <c>treeIsoRig2.js</c>, which stays committed as the previous generation (the same way
        /// <c>shoreIsoKitRig2.js</c> sits beside <c>shoreIsoKitRig.js</c>). Pass 1 built real volume
        /// and lit it correctly, but every crown came out of one soft-ellipsoid cloud, so the family
        /// read as artichokes; pass 2 rebuilt crowns as identified leaf MASSES with a Worley cell
        /// partition and visible branches. <b>Pass 3 builds the SKELETON first</b> — fork height,
        /// primary count, the curve a limb takes to its target, per-species conifer tiering — and
        /// hangs authored 4–9 px leaf STAMPS off the limb tips, so the crown silhouette is a
        /// consequence of the wood and winter is the same skeleton with twig fans.
        ///
        /// <para>⚠️ <b>Nothing about the CONTRACT changed</b>, which is why the swap is two constants
        /// and a re-bake rather than a pipeline rewrite: PPU, camera (ELEV/CE/SE), LIGHT, the three
        /// rules (RIM_PX/MIN_BODY/MIN_R), SEASONS, STAGES, VARIANTS, SWAY, KEYLINE_DEFAULT and all
        /// ten species keys are IDENTICAL across all three passes — verified constant by constant at
        /// import, in the repo's own V8. Pass 3 drops <c>LEAF_W</c>/<c>LEAF_H</c> (nothing here read
        /// them) and adds <c>SCALE</c>, <c>M2PX</c>, <c>woodView</c> and <c>STENCILS</c>.</para>
        ///
        /// <para>⚠️⚠️ <b>WHAT PASS 3 DOES CHANGE IS THE WORLD SIZE OF EVERY TREE, and that is a
        /// ruling, not a refresh.</b> Pass 3 carries each species' TRUE mature height and maps it
        /// through <c>SCALE = 0.6</c>, so relative scale becomes real rather than compressed toward a
        /// common height. Measured mature/summer, pass 2 → pass 3: black spruce 5.6 → 6.6 m (×1.18),
        /// red oak 5.3 → 13.2 (×2.49), white pine 6.9 → 16.2 (×2.35). Cells go from 79×141…165×191 to
        /// 73×179…331×347. Everything downstream that was tuned against a 5–7 m tree — the woodland
        /// planter's spacing, the Y-sort band, texture memory — meets a 7–16 m one. SCALE is a rig
        /// constant: raising or lowering it and re-baking re-measures every cell and pivot.</para>
        /// </summary>
        public const string RigScriptPath = "docs/art/rigs/treeIsoRig3.js";

        /// <summary>⚠️ <c>TreeRig3</c>, not <c>TreeRig2</c> and not <c>TreeRig</c>. Each pass installs
        /// its OWN global and exposes the same surface, so a consumer swaps ONE identifier — but a
        /// stale name here would silently bake a previous pass's pixels against this pass's contract
        /// if two files were ever loaded into one host, and at pass 3 that would be a tree at HALF
        /// its world height. Everything reads this constant; nothing hardcodes the name.</summary>
        public const string RigGlobalName = "TreeRig3";

        /// <summary>The superseded PREVIOUS-pass rig, kept committed for provenance and for the
        /// constants-are-identical proof in <c>TreeRigBakeTests</c>. Nothing bakes from it.
        /// <c>treeIsoRig.js</c> (pass 1) also stays committed, one generation further back.</summary>
        public const string PreviousRigScriptPath = "docs/art/rigs/treeIsoRig2.js";

        public const string PreviousRigGlobalName = "TreeRig2";

        /// <summary>
        /// <b>PASS 4.1 (tree-rig-kit v4.1, 2026-09-23) — committed BESIDE pass 3, not yet live.</b>
        /// The rig bakes ONE rest pose per season and hands the wind to the shader: two weight maps
        /// (<c>_wind</c>, <c>_phase</c>) that <c>treeMaps4.js</c> reads off the rig without changing
        /// it, plus a per-pixel snow threshold (<c>_snow</c>) so any cover is one texture instead of a
        /// sheet per cover. <c>HHTreePass4</c> (<c>TreePass4Glue</c>) is our glue over the two.
        ///
        /// <para>⚠️ <b>The switch is <see cref="RigScriptPath"/>, and it is a Phase B act.</b> Until
        /// it moves, every consumer keeps reading pass 3: <see cref="IsPass4Live"/> is false, the
        /// pass-4 baker refuses to overwrite the live kit, and the shader's <c>_TreeMaps</c> row
        /// defaults to 0, which draws today's trees bit-identically.</para>
        /// </summary>
        public const string Pass4RigScriptPath = "docs/art/rigs/treeIsoRig4.js";

        public const string Pass4RigGlobalName = "TreeRig4";

        /// <summary>The maps half of the drop: <c>rest</c>, <c>snowMap</c>, <c>windMaps</c>,
        /// <c>constants</c> and the reference <c>shade</c> the tree shader is ported from. Load it
        /// AFTER the rig — it reads <c>globalThis.TreeRig4</c> at install.</summary>
        public const string Pass4MapsScriptPath = "docs/art/rigs/treeMaps4.js";

        public const string Pass4MapsGlobalName = "TreeMaps4";

        /// <summary>Our glue's global (<c>TreePass4Glue.Js</c>): it survives, packs and refuses —
        /// the rig and the maps stay the art-director's files, byte for byte.</summary>
        public const string Pass4GlueGlobalName = "HHTreePass4";

        /// <summary>Whether the kit the game draws is pass 4. A PROPERTY, not a constant expression,
        /// so a branch on it never reads as unreachable code while the switch is off.</summary>
        public static bool IsPass4Live =>
            string.Equals(RigScriptPath, Pass4RigScriptPath, StringComparison.Ordinal);

        /// <summary>
        /// ✅ <b>EMPTY SINCE PASS 3 (2026-09-02) — the tamarack came back.</b> The list stays, and so
        /// does every consumer of it, because the next drop can hold a species back for the same
        /// reason this one did.
        ///
        /// <para><b>Measured before emptying it</b>, in the repo's own V8 and against the gate's own
        /// terms (<c>audit.pass &amp;&amp; thinPct &lt;= 4%</c>), every species × 4 variants ×
        /// summer/winter at mature: the worst thinPct in the whole set is <b>0.9%</b>, and the
        /// tamarack's own worst is <b>0.8%</b> against the 5.4% that held her back. Ten of ten
        /// species clear it, so the entry's own instruction below applies.</para>
        ///
        /// <para>What follows is the 2026-07-29 record, kept because it is the reason the mechanism
        /// exists:</para>
        ///
        /// <para><b>Tamarack</b> failed the pass-2 rig's OWN rule-1 gate
        /// (<c>audit.pass &amp;&amp; thinPct &lt;= 4%</c>): it measures <b>5.4%</b>, a 35% overshoot,
        /// against 1.1% under pass 1, and its <c>bodyRatio</c> fell 80 → 66. The other nine species
        /// improved. It is the larch — the thinnest needle grain in the rig's <c>GRAINS</c> — so pass
        /// 2's Worley leaf-cell partition most likely subdivides an already-wispy tuft below the 5 px
        /// clump floor.</para>
        ///
        /// <para><b>Coordinator ruling 2026-07-29:</b> ship the nine improved species, hold Tamarack at
        /// its pass-1 bake, do NOT touch the rig file and do NOT loosen the gate. The rig fix is a
        /// separate art-director-lane PR (the choice between thickening at the emitter and declaring a
        /// floor-exempt rimless material, on the strap-material precedent, is with the owner).</para>
        ///
        /// <para>⚠️ <b>What being held back MEANS, concretely:</b> a held-back species is absent from
        /// <c>Trees.json</c>, so it is absent from <see cref="AcadianTreeCatalog"/>'s placeable set and
        /// no tool will place it. Its three pass-1 sheets and their <c>.meta</c> files stay committed
        /// and <b>untouched</b> — already sliced, already pivoted, by the pass-1 bake that wrote them.
        /// <see cref="TreeSheetSlicer"/> therefore SKIPS them rather than erroring: it cannot re-slice
        /// a sheet with no contract entry (no cell, no pivot), and it does not need to.</para>
        ///
        /// <para>Delete the entry — do not edit around it — the day the rig clears its own gate and the
        /// species re-enters the bake.</para>
        /// </summary>
        public static readonly string[] HeldBackSpecies = System.Array.Empty<string>();

        /// <summary>Whether a species is held back at a previous pass — see
        /// <see cref="HeldBackSpecies"/>.</summary>
        public static bool IsHeldBack(string species) =>
            Array.IndexOf(HeldBackSpecies, species) >= 0;

        /// <summary>
        /// Whether a sheet stem belongs to a held-back species. Matches on the leading key with a
        /// separator required, so a hypothetical <c>TamarackHybrid</c> could never be claimed by
        /// <c>Tamarack</c>'s prefix and skipped by accident.
        /// </summary>
        public static bool IsHeldBackStem(string stem)
        {
            if (string.IsNullOrEmpty(stem)) return false;
            foreach (string key in HeldBackSpecies)
                if (stem.StartsWith(key, StringComparison.Ordinal) &&
                    stem.Length > key.Length && stem[key.Length] == '_')
                    return true;
            return false;
        }

        /// <summary>
        /// The DEFAULT importer texture cap. Over this Unity imports SILENTLY DOWNSCALED and the
        /// sprite COUNT still matches, so only a cell-size/pivot assert catches it. The tree
        /// slicer deliberately does NOT lift the cap the way <c>SpriteSheetSlicer</c> does for the
        /// 3648 px hull sheets: every tree sheet is inside it (the widest is Red Oak's row, 1324 px
        /// at pass 3 and 1628 px at pass 4, whose cells carry the wind's reach), so a sheet that
        /// needed a lift would mean the bake recipe grew — which is a decision, not an import
        /// setting.
        /// </summary>
        public const int ImportSizeCap = 2048;

        /// <summary>Channel suffix on a sheet stem. The albedo carries no suffix (it is what
        /// ships); the two data channels do.</summary>
        public const string MaskSuffix = "_mask";
        public const string NormalSuffix = "_normal";

        /// <summary>Pass 4's three data channels (see <see cref="Pass4Channels"/>).</summary>
        public const string WindSuffix = "_wind";
        public const string PhaseSuffix = "_phase";
        public const string SnowSuffix = "_snow";

        /// <summary>Every channel either pass bakes. Pass 3 bakes the first three
        /// (<see cref="Channels"/>); pass 4 bakes all six (<see cref="Pass4Channels"/>).</summary>
        public enum Channel { Albedo, Mask, Normal, Wind, Phase, Snow }

        /// <summary>The three channels pass 3 bakes, in bake order. ⚠️ Unchanged by pass 4: the
        /// sheet-count guards multiply by its length, and pass 3 is what the game draws until the
        /// switch.</summary>
        public static readonly Channel[] Channels =
        {
            Channel.Albedo, Channel.Mask, Channel.Normal,
        };

        /// <summary>
        /// The six channels pass 4 bakes per season, in bake order. The mask keeps OUR order
        /// (R key · G back rim · B depth · A coverage); the new data lives in new sheets:
        /// <c>_wind</c> (R lean · G sway · B|A packed class/palette/stamp bits), <c>_phase</c>
        /// (R wave · G play · B depth · A packed bits) and <c>_snow</c> (the cover byte at which a
        /// pixel turns to snow; A = 0 outside the tree).
        /// </summary>
        public static readonly Channel[] Pass4Channels =
        {
            Channel.Albedo, Channel.Mask, Channel.Normal, Channel.Wind, Channel.Phase, Channel.Snow,
        };

        /// <summary>The channels one pass-4 season row writes: all six, or the albedo alone when
        /// the season borrows another season's maps (a deciduous autumn draws on summer's
        /// wood and weights and only recolours them).</summary>
        public static readonly Channel[] AlbedoOnly = { Channel.Albedo };

        /// <summary>
        /// The one shared palette sheet pass 4 writes beside the species sheets: 8 px wide, row 0 the
        /// snow row, then every distinct gap row ("the dark between leaves" a moved leaf uncovers).
        /// Colour, so it imports sRGB, point-filtered and uncompressed; the slicer gives it that
        /// import and never slices it (it has no cell and no pivot).
        /// </summary>
        public const string PaletteFileName = "TreePalette.png";

        public static string PalettePath => TreesRoot + PaletteFileName;

        /// <summary>A palette row is this many colours: the width of <see cref="PaletteFileName"/>
        /// and the 3-bit index the packed bits carry.</summary>
        public const int PaletteWidth = 8;

        public static string SuffixFor(Channel c) => c switch
        {
            Channel.Albedo => "",
            Channel.Mask => MaskSuffix,
            Channel.Normal => NormalSuffix,
            Channel.Wind => WindSuffix,
            Channel.Phase => PhaseSuffix,
            Channel.Snow => SnowSuffix,
            _ => throw new ArgumentOutOfRangeException(nameof(c)),
        };

        /// <summary>
        /// ⚠️ <b>THE DATA CHANNELS MUST IMPORT WITH sRGB OFF.</b> The mask packs
        /// <c>R = key light · G = back rim · B = depth · A = coverage</c> and the normal packs a
        /// view-space vector: both are NUMBERS, not colour. Leave sRGB on and Unity applies a gamma
        /// curve to them — the sprite still LOOKS fine in the inspector, the lighting is simply
        /// wrong by a curve, and nothing but a numeric assert notices.
        /// </summary>
        public static bool IsColourChannel(Channel c) => c == Channel.Albedo;

        /// <summary>Sheet stem for one species × stage × season × channel, e.g.
        /// <c>RedSpruce_mature_summer_mask</c>.</summary>
        public static string StemFor(string species, string stage, string season, Channel channel) =>
            $"{species}_{stage}_{season}{SuffixFor(channel)}";

        public static string SheetPath(string species, string stage, string season, Channel channel) =>
            TreesRoot + StemFor(species, stage, season, channel) + ".png";

        // =================================================================================
        // the contract schema
        // =================================================================================

        [Serializable]
        public sealed class Contract
        {
            public string note;
            public string rig;
            public string global;
            public int ppu;
            public CameraBlock camera;
            public LightBlock light;
            public RulesBlock rules;
            public SheetBlock sheet;
            public ChannelBlock channels;
            public Entry[] trees;

            // ---- pass 4 only: empty on a pass-3 contract (see HasPass4) -----------------------

            /// <summary>Where the wind and snow maps come from and how the packed bits are laid
            /// out.</summary>
            public MapsBlock maps;

            /// <summary>The snow row every species shares: <see cref="PaletteWidth"/> colours as
            /// <c>rrggbb</c>, sorted by packed RGB and padded by repeating the last. Palette row 0.
            /// The glue refuses a snowed pixel whose colour is not in it.</summary>
            public string[] snowRow;

            public PaletteBlock palette;
        }

        [Serializable]
        public sealed class MapsBlock
        {
            public string script;
            public string global;
            public string glue;
            public int glueVersion;

            /// <summary>The rig's wind loop in frames (16). The shader steps through it.</summary>
            public int loop;

            public string windNote;
            public string snowNote;
            public string packNote;
        }

        [Serializable]
        public sealed class PaletteBlock
        {
            public string file;
            public int width;
            public int rows;
            public string note;
        }

        /// <summary>
        /// A species' wind response, as <c>TreeMaps4.constants()</c> reports it at the baked stage.
        /// The shader reads these per renderer, the way it reads <c>_TrunkAnchor</c>.
        /// </summary>
        [Serializable]
        public sealed class WindBlock
        {
            /// <summary>The rig's height term in px (<c>max(26, worldH · size)</c>).</summary>
            public float H;

            /// <summary>Trunk bend in px at a full gale.</summary>
            public float bendPx;

            /// <summary>Limb reach in px at a full gale.</summary>
            public float limbPx;

            /// <summary>How far a limb bobs vertically, as a share of its reach.</summary>
            public float bob;

            /// <summary>How often a leaf flutters: the rig's per-species flutter rate.</summary>
            public float flutter;

            /// <summary>The rig's <c>shimmer</c>: empty, or <c>[strength, calm share]</c> for the
            /// four broadleaves that tremble in still air. Only the product reaches the shader.</summary>
            public float[] shimmer;

            /// <summary>Conifer flutter pushes a needle tuft sideways; broadleaf flutter also
            /// lifts it.</summary>
            public bool conifer;

            /// <summary>The padding the cell carries on each side for the sway to move into.</summary>
            public int windReach;
        }

        /// <summary>
        /// What one season of one species draws from. A season can borrow another's sheets: an
        /// evergreen autumn is summer outright, and a deciduous autumn keeps summer's maps and
        /// bakes only its own albedo. The glue proves the borrowed maps match pixel for pixel.
        /// </summary>
        [Serializable]
        public sealed class SeasonRow
        {
            public string season;

            /// <summary>The season whose mask, normal, wind, phase and snow sheets this one
            /// draws.</summary>
            public string maps;

            /// <summary>The season whose albedo this one draws.</summary>
            public string albedo;

            /// <summary>The dark between leaves a moved leaf uncovers: <see cref="PaletteWidth"/>
            /// colours as <c>rrggbb</c>.</summary>
            public string[] gapRow;

            /// <summary>The texel row of <see cref="PaletteFileName"/> that holds
            /// <see cref="gapRow"/>, as Unity numbers it: <c>Texture2D.GetPixel</c>'s y and the
            /// shader's <c>Load</c> y, bottom up. Row 0 is the snow row.</summary>
            public int paletteRow;
        }

        [Serializable]
        public sealed class CameraBlock
        {
            public string name;
            public string view;
            public float elevDeg;
            public float heightScale;
            public float depthScale;
        }

        [Serializable]
        public sealed class LightBlock
        {
            public float[] key;
            public float[] rim;
            public string note;
        }

        [Serializable]
        public sealed class RulesBlock
        {
            public int rimPx;
            public int minBodyPx;
            public int minClumpRadiusPx;
            public string note;
        }

        [Serializable]
        public sealed class SheetBlock
        {
            public int cols;
            public int rows;
            public string colAxis;
            public string rowAxis;
            /// <summary>How many sway rows the RIG can produce (4). Kept so the difference from
            /// <see cref="rows"/> is visible in the data, not just in a commit message.</summary>
            public int rigSwayRows;
            public string swayNote;
        }

        [Serializable]
        public sealed class ChannelBlock
        {
            public string albedo;
            public string mask;
            public string normal;
            public string coverageNote;
        }

        [Serializable]
        public sealed class Entry
        {
            public string species;
            public string name;
            public string latin;
            public string form;
            public string stage;
            public string[] seasons;

            /// <summary>True world height in metres at this stage (PPU 32).</summary>
            public float metres;

            public int cellW;
            public int cellH;

            /// <summary>The trunk foot, in cell px from the TOP-LEFT — the rig's own
            /// <c>sheetSpec().pivot</c>.</summary>
            public int pivotX;
            public int pivotY;

            /// <summary>Rows of near-root flare BELOW the trunk foot: <c>cellH − 1 − pivotY</c>,
            /// the rig's own <c>pad</c>. ⚠️ This is why a tree does NOT pivot bottom-centre.</summary>
            public int nearFlarePad;

            /// <summary>
            /// <c>_TrunkAnchor</c> for THIS species: the trunk foot as a fraction of cell height,
            /// <c>nearFlarePad / cellH</c>. The wind shader holds everything below this still and
            /// sways the canopy above it, so the value belongs per species — measured 0.0519
            /// (Trembling Aspen) to 0.0922 (White Cedar) on the pass-2 rig, against the one shipped
            /// material constant of 0.14. (Pass 1 measured 0.0833–0.1447. The pass-2 buttressed root
            /// flare is a shallower pad, so the whole band moved DOWN — the shipped 0.14 now
            /// over-anchors all ten species rather than eight of the ten.)
            /// </summary>
            public float trunkAnchor;

            /// <summary>The Unity sprite pivot: normalised, BOTTOM-origin. Equals
            /// <c>(pivotX / cellW, nearFlarePad / cellH)</c> — see
            /// <see cref="NormalizedPivot"/> for why the y term is not <c>(cellH − pivotY)/cellH</c>.</summary>
            public float unityPivotX;
            public float unityPivotY;

            /// <summary>Baked sheet size: <c>cols × cellW</c> by <c>rows × cellH</c>.</summary>
            public int sheetW;
            public int sheetH;

            /// <summary>What the rig's own <c>sheetSpec()</c> reports for the FULL 4-sway-row
            /// sheet, plus its 2048 verdict. Recorded even though we bake one row, so the day
            /// somebody wants the sway rows the headroom is already on record.</summary>
            public int rigSheetW;
            public int rigSheetH;
            public bool rigFitsUnity2048;

            public Audit audit;

            // ---- pass 4 only ------------------------------------------------------------------

            public WindBlock wind;

            /// <summary>One row per season in <see cref="seasons"/>, in the same order.</summary>
            public SeasonRow[] seasonRows;
        }

        [Serializable]
        public sealed class Audit
        {
            /// <summary>Worst (highest) <c>report.thinPct</c> across the baked variants — foliage
            /// mass too thin to carry a rim, per rule 1.</summary>
            public float thinPct;
            public int bodyRatio;
            public int despeckled;
            public bool pass;
            public bool underFloor;
        }

        // =================================================================================
        // reading it back
        // =================================================================================

        /// <summary>
        /// The committed contract, or null with a logged error if it is missing/unparseable.
        /// </summary>
        public static Contract Load()
        {
            if (!File.Exists(ContractPath))
            {
                Debug.LogError(
                    $"[TreeKitCatalog] No contract at '{ContractPath}'. Run " +
                    "Hidden Harbours ▸ Art ▸ Bake Acadian Trees — the sheets and this file are " +
                    "written by the same bake and are only meaningful together.");
                return null;
            }

            var contract = JsonUtility.FromJson<Contract>(File.ReadAllText(ContractPath));
            if (contract?.trees == null || contract.trees.Length == 0)
            {
                Debug.LogError($"[TreeKitCatalog] '{ContractPath}' parsed but carries no trees.");
                return null;
            }
            return contract;
        }

        /// <summary>One species+stage entry, or null if the bake did not cover it.</summary>
        public static Entry Find(Contract contract, string species, string stage)
        {
            if (contract?.trees == null) return null;
            foreach (var e in contract.trees)
                if (string.Equals(e.species, species, StringComparison.Ordinal) &&
                    string.Equals(e.stage, stage, StringComparison.Ordinal))
                    return e;
            return null;
        }

        /// <summary>
        /// The Unity sprite pivot for an entry: normalised from the BOTTOM-LEFT, so
        /// <c>(pivotX / cellW, nearFlarePad / cellH)</c>.
        ///
        /// <para>⚠️ <b>THE Y TERM IS DELIBERATELY <c>pad/h</c>, NOT THE REPO'S USUAL
        /// <c>(h − pivotY)/h</c></b> (<c>RigGeometry.UnityNormalisedPivot</c>,
        /// <c>FishingSheetSlicer.KitSpec.NormalizedPivot</c>). Those two differ by exactly one
        /// pixel — the top edge of the trunk-foot row versus its bottom edge — and for a tree the
        /// bottom edge is the one that matters, for a reason no other kit has: the SAME fraction
        /// also has to serve as the wind shader's <c>_TrunkAnchor</c>. Take
        /// <c>(h − pivotY)/h</c> and the ground plane would sit one row ABOVE the anchor, so the
        /// lowest row of near-root flare would be outside the planted band and would sway. The
        /// design doc published this number as <c>uv.y = 20/166 = 0.120</c> for Red Spruce under the
        /// pass-1 rig, and it is <c>10/159 = 0.0629</c> under pass 2; matching whichever the live
        /// bake reports keeps one fraction doing both jobs.</para>
        ///
        /// <para>Either choice is edge-aligned, which is what keeps the sprite pixel-snapped at
        /// PPU 32 — a pixel-CENTRE pivot <c>(pad + 0.5)/h</c> would be half a pixel off the
        /// camera's grid and shimmer.</para>
        /// </summary>
        public static Vector2 NormalizedPivot(Entry e) =>
            new Vector2((float)e.pivotX / e.cellW, (float)e.nearFlarePad / e.cellH);

        /// <summary>The pivot in cell px from the rect's own bottom-left, which is what
        /// <c>Sprite.pivot</c> reports.</summary>
        public static Vector2 PivotPixels(Entry e) => new Vector2(e.pivotX, e.nearFlarePad);

        /// <summary>
        /// This species' <c>_TrunkAnchor</c> — the named entry point for whoever wires the wind
        /// shader when trees get placed, so the number comes from the bake and not from a material
        /// default. Throws rather than falling back to a plausible constant: silently anchoring
        /// every species at 0.14 is exactly the reading this replaces.
        ///
        /// <para>The measured spread across the ten species is <b>0.0519 (Trembling Aspen) to 0.0922
        /// (White Cedar)</b> on the pass-2 rig (pass 1: 0.0833–0.1447), against the single 0.14
        /// shipped on <c>Art/Materials/Tree.mat</c> — so one material-wide value now over-anchors
        /// <b>all ten</b>, freezing canopy that should move.
        /// The consumer wants a per-renderer <c>MaterialPropertyBlock</c> (or a material per
        /// species); this catalog supplies the value, not the wiring.</para>
        /// </summary>
        public static float TrunkAnchorFor(Contract contract, string species, string stage)
        {
            Entry e = Find(contract, species, stage);
            if (e == null)
                throw new ArgumentException(
                    $"No '{species}/{stage}' in {ContractFileName}. This wave bakes mature/summer " +
                    "only; the other stages and seasons are a deliberate follow-up, not a gap to " +
                    "paper over with a default anchor.");
            return e.trunkAnchor;
        }

        /// <summary>Every sheet path this contract claims, in bake order.</summary>
        public static string[] AllSheetPaths(Contract contract)
        {
            var paths = new System.Collections.Generic.List<string>();
            foreach (var e in contract.trees)
            foreach (string season in e.seasons)
            foreach (var c in ChannelsFor(e, season))
                paths.Add(SheetPath(e.species, e.stage, season, c));
            return paths.ToArray();
        }

        /// <summary>The <see cref="Channel"/> a stem belongs to, by its suffix.</summary>
        public static Channel ChannelOf(string stem) =>
            stem.EndsWith(MaskSuffix, StringComparison.Ordinal) ? Channel.Mask
            : stem.EndsWith(NormalSuffix, StringComparison.Ordinal) ? Channel.Normal
            : stem.EndsWith(WindSuffix, StringComparison.Ordinal) ? Channel.Wind
            : stem.EndsWith(PhaseSuffix, StringComparison.Ordinal) ? Channel.Phase
            : stem.EndsWith(SnowSuffix, StringComparison.Ordinal) ? Channel.Snow
            : Channel.Albedo;

        /// <summary>The entry a sheet stem belongs to, or null for a stranger (which must fail,
        /// not guess).</summary>
        public static Entry EntryForStem(Contract contract, string stem)
        {
            foreach (var e in contract.trees)
            foreach (string season in e.seasons)
            foreach (var c in ChannelsFor(e, season))
                if (string.Equals(stem, StemFor(e.species, e.stage, season, c), StringComparison.Ordinal))
                    return e;
            return null;
        }

        // =================================================================================
        // pass 4
        // =================================================================================

        /// <summary>Whether a contract carries pass 4's fields. A pass-3 contract parses with
        /// them empty or default, so this is the one test anything downstream makes.</summary>
        public static bool HasPass4(Contract contract) =>
            contract?.snowRow != null && contract.snowRow.Length == PaletteWidth &&
            contract.maps != null && !string.IsNullOrEmpty(contract.maps.script);

        /// <summary>Whether one entry carries pass 4's wind response and season rows.</summary>
        public static bool HasPass4(Entry e) =>
            e?.wind != null && e.wind.H > 0f &&
            e.seasonRows != null && e.seasons != null && e.seasonRows.Length == e.seasons.Length;

        /// <summary>The pass-4 row for one season of an entry, or null.</summary>
        public static SeasonRow SeasonRowFor(Entry e, string season)
        {
            if (e?.seasonRows == null) return null;
            foreach (var row in e.seasonRows)
                if (string.Equals(row.season, season, StringComparison.Ordinal))
                    return row;
            return null;
        }

        /// <summary>
        /// The channels one season of one entry has sheets for: pass 3's three; or, for pass 4,
        /// all six when the season draws its own maps, the albedo alone when it borrows the maps
        /// but not the colour, and none when it borrows both.
        /// </summary>
        public static Channel[] ChannelsFor(Entry e, string season)
        {
            if (!HasPass4(e)) return Channels;
            SeasonRow row = SeasonRowFor(e, season);
            if (row == null) return Array.Empty<Channel>();
            if (string.Equals(row.maps, season, StringComparison.Ordinal)) return Pass4Channels;
            if (string.Equals(row.albedo, season, StringComparison.Ordinal)) return AlbedoOnly;
            return Array.Empty<Channel>();
        }

        /// <summary>The one shimmer number the shader reads: strength × calm share, the still-air
        /// flutter a broadleaf keeps. Zero for a species without shimmer.</summary>
        public static float ShimmerCalm(WindBlock w) =>
            w?.shimmer != null && w.shimmer.Length == 2 ? w.shimmer[0] * w.shimmer[1] : 0f;

        /// <summary>
        /// The sprite mesh one entry's sheets must import with. Pass 3 is
        /// <see cref="SpriteMeshType.Tight"/>: its sway bends VERTICES, and the bend curve needs
        /// the intermediate vertices a Tight mesh has (see <c>TreeSheetSlicer</c>). Pass 4 is
        /// <see cref="SpriteMeshType.FullRect"/>: with <c>_TreeMaps</c> on, the vertex stage does not
        /// bend, and the FRAGMENT gathers leaves from up to <c>windReach</c> px beyond the rest
        /// outline. A Tight mesh traced round the rest pixels would clip every leaf the wind carries
        /// past it. The price is overdraw across the empty margin of the cell, which the plate
        /// measures.
        /// </summary>
        public static SpriteMeshType MeshTypeFor(Entry e) =>
            HasPass4(e) ? SpriteMeshType.FullRect : SpriteMeshType.Tight;

        /// <summary>
        /// Whether a channel's sheet imports as ONE channel, R8. Only pass 4's snow sheet: every texel
        /// is the cover byte at which that pixel turns to snow, and the shader reads its R alone. At
        /// R8 it costs a quarter of an RGBA32 sheet — 3.15 MB instead of 12.6 MB for one season of
        /// the mature set.
        /// </summary>
        public static bool IsSingleChannel(Channel c) => c == Channel.Snow;

        /// <summary>
        /// The platform override that carries the R8 format. The Default platform only picks an
        /// AUTOMATIC format (from the compression setting); a named format is a per-platform
        /// override. PC-first (ADR 0005), so Standalone. A later mobile port adds its own override;
        /// until then, other platforms get the lossless RGBA32 default, which is correct, only
        /// larger.
        /// </summary>
        public const string SingleChannelPlatform = "Standalone";

        /// <summary>Read the sprite mesh type an importer will use — it lives on
        /// <see cref="TextureImporterSettings"/>, not on the importer itself, which is an easy
        /// thing to look for in the wrong place.</summary>
        public static SpriteMeshType MeshTypeOf(TextureImporter importer)
        {
            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            return s.spriteMeshType;
        }
    }
}
#endif
