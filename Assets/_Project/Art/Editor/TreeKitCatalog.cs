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
        /// 3648 px hull sheets: every tree sheet is comfortably inside it (the widest is Red Oak at
        /// 660 px), so a sheet that needed a lift would mean the bake recipe grew — which is a
        /// decision, not an import setting.
        /// </summary>
        public const int ImportSizeCap = 2048;

        /// <summary>Channel suffix on a sheet stem. The albedo carries no suffix (it is what
        /// ships); the two data channels do.</summary>
        public const string MaskSuffix = "_mask";
        public const string NormalSuffix = "_normal";

        /// <summary>The three channels, in bake order.</summary>
        public enum Channel { Albedo, Mask, Normal }

        public static readonly Channel[] Channels =
        {
            Channel.Albedo, Channel.Mask, Channel.Normal,
        };

        public static string SuffixFor(Channel c) => c switch
        {
            Channel.Albedo => "",
            Channel.Mask => MaskSuffix,
            Channel.Normal => NormalSuffix,
            _ => throw new ArgumentOutOfRangeException(nameof(c)),
        };

        /// <summary>
        /// ⚠️ <b>THE TWO DATA CHANNELS MUST IMPORT WITH sRGB OFF.</b> The mask packs
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
            int n = 0;
            foreach (var e in contract.trees) n += e.seasons.Length * Channels.Length;
            var paths = new string[n];
            int i = 0;
            foreach (var e in contract.trees)
            foreach (string season in e.seasons)
            foreach (var c in Channels)
                paths[i++] = SheetPath(e.species, e.stage, season, c);
            return paths;
        }

        /// <summary>The <see cref="Channel"/> a stem belongs to, by its suffix.</summary>
        public static Channel ChannelOf(string stem) =>
            stem.EndsWith(MaskSuffix, StringComparison.Ordinal) ? Channel.Mask
            : stem.EndsWith(NormalSuffix, StringComparison.Ordinal) ? Channel.Normal
            : Channel.Albedo;

        /// <summary>The entry a sheet stem belongs to, or null for a stranger (which must fail,
        /// not guess).</summary>
        public static Entry EntryForStem(Contract contract, string stem)
        {
            foreach (var e in contract.trees)
            foreach (string season in e.seasons)
            foreach (var c in Channels)
                if (string.Equals(stem, StemFor(e.species, e.stage, season, c), StringComparison.Ordinal))
                    return e;
            return null;
        }

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
