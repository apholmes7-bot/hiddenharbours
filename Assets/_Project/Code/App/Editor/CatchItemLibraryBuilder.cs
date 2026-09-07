#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;      // FishCategory — the fallback when a species name matches no baked kind
using HiddenHarbours.Fishing;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The catch-item ART TABLE, built once into a committed asset</b> — which baked sprite draws each
    /// catch kind, and which visual kind each hold <c>speciesId</c> maps to (rule 2 / ADR 0003).
    ///
    /// <para><b>Why this had to exist before a container could visibly fill.</b> Every container picture
    /// in the game reads its contents through <see cref="CatchItemLibrary"/> — the tote to choose an item
    /// sprite, the pail to choose which of the bucket rig's three catch groups it is drawing. The class
    /// shipped; the ASSET never did. So every fill path resolved a null table, gave up, and drew an empty
    /// container over a hold with fish in it. There was nothing to see and nothing logged, which is the
    /// shape of bug this repo has paid for more than once.</para>
    ///
    /// <para><b>Everything here is READ, never typed.</b> The species→kind rows come from the shipped
    /// <see cref="FishSpeciesDef"/> assets (their own ids, and their <see cref="FishCategory"/> for the
    /// fallback); the sprites come from the baked sheets by name. The one recipe restated is the fish
    /// DECK LAY — variant <c>v</c> is direction row <c>[2,6,3,5][v]</c>, frame <c>v</c> — which is
    /// <c>CatchKit.item</c>'s own composition and is pinned by <c>CatchStorageBakeTests</c>.</para>
    ///
    /// <para><b>Degrades per element</b>, the house rule for importers: a species with no art still gets
    /// its kind row (the pail only needs the kind), a kind with no sheet gets an empty variant list and
    /// the tote renderer skips it rather than drawing a blank, and a missing sheet is a WARNING naming
    /// the file rather than a silent shorter table.</para>
    /// </summary>
    public static class CatchItemLibraryBuilder
    {
        const string MenuPath = "Hidden Harbours/Art/Import (after a new drop)/Build Catch Item Library";
        const string OutputFolder = "Assets/_Project/Data/Art";
        const string AssetName = "CatchItemLibrary";
        const string FishIso = "Assets/_Project/Art/Fishing/Iso";
        const string Storage = "Assets/_Project/Art/Fishing/Storage";
        const string DataFish = "Assets/_Project/Data/Fish";

        /// <summary>Where the built asset lands — what the region builders load.</summary>
        public const string LibraryPath = OutputFolder + "/" + AssetName + ".asset";

        /// <summary>
        /// The finfish kinds, discovered from the baked sheets themselves — every
        /// <c>Fish_&lt;kind&gt;_deck.png</c> whose stem carries no size suffix.
        ///
        /// <para><b>Read, not typed</b>, like everything else in this builder. Catch pass 2 took the
        /// species from four to seven, and a hand-kept array is exactly the thing that would still say
        /// four: the bass, flounder and herring sheets would sit on disk with nothing drawing them and
        /// no error anywhere. Discovery also means the ladder's <c>_sm</c>/<c>_lg</c> siblings are
        /// skipped without a rule about them — a container fill draws ONE size of item, and the middle
        /// rung is the unsuffixed stem.</para>
        /// </summary>
        static string[] FinfishKinds() => StemsMatching(FishIso, "Fish_", "_deck");

        /// <summary>
        /// The shellfish/crustacean kinds, discovered from the pass-2 item strips
        /// (<c>CatchItem2_&lt;kind&gt;.png</c>) — seven where pass 1 baked four, the new ones being
        /// oyster, periwinkle and scallop.
        /// </summary>
        static string[] StripKinds() => StemsMatching(Storage, "CatchItem2_", "");

        /// <summary>
        /// The kind names of every sheet in <paramref name="folder"/> shaped
        /// <c>&lt;prefix&gt;&lt;kind&gt;&lt;suffix&gt;.png</c>, ordinal-sorted so the built asset is
        /// stable across machines (a file enumeration's order is not).
        ///
        /// <para>A kind containing <c>_sm</c>/<c>_lg</c> is a size rung of another kind, not a kind.</para>
        /// </summary>
        static string[] StemsMatching(string folder, string prefix, string suffix)
        {
            var kinds = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (!name.StartsWith(prefix, System.StringComparison.Ordinal)) continue;
                if (suffix.Length > 0 && !name.EndsWith(suffix, System.StringComparison.Ordinal)) continue;

                string kind = name.Substring(prefix.Length, name.Length - prefix.Length - suffix.Length);
                if (kind.Length == 0 || kind.Contains("_sm") || kind.Contains("_lg")) continue;
                if (kind.Contains("_")) continue;   // Fish_cod_swim etc — a state, not a deck lay
                kinds.Add(kind);
            }
            kinds.Sort(System.StringComparer.Ordinal);
            return kinds.ToArray();
        }

        /// <summary>
        /// The rig's deck-lay recipe: variant <c>v</c> is the fish sheet's direction row
        /// <c>DeckLayRows[v]</c> at frame <c>v</c>. Four variants, because every item sheet bakes exactly
        /// <see cref="CatchFillMath.Variants"/> columns.
        /// </summary>
        static readonly int[] DeckLayRows = { 2, 6, 3, 5 };

        [MenuItem(MenuPath, priority = 233)]
        public static void Build()
        {
            if (BuildLibrary(out string summary)) Debug.Log($"[CatchItemLibraryBuilder] {summary}");
            else Debug.LogError($"[CatchItemLibraryBuilder] {summary}");
        }

        /// <summary>
        /// Create or refresh <see cref="LibraryPath"/>. Non-destructive: it refreshes the asset in place,
        /// keeping its guid, so nothing pointing at it breaks. False (with a reason) only when the output
        /// folder cannot be made — a missing SHEET is survivable and reported, never fatal.
        /// </summary>
        public static bool BuildLibrary(out string summary)
        {
            summary = null;
            if (!EnsureFolder(OutputFolder))
            {
                summary = $"Could not create '{OutputFolder}' — nothing written.";
                return false;
            }

            var library = AssetDatabase.LoadAssetAtPath<CatchItemLibrary>(LibraryPath);
            bool created = library == null;
            if (created) library = ScriptableObject.CreateInstance<CatchItemLibrary>();

            var kinds = new List<CatchItemLibrary.KindEntry>();
            int wired = 0, missing = 0;

            string[] finfish = FinfishKinds(), strips = StripKinds();
            if (finfish.Length == 0 && strips.Length == 0)
            {
                summary = "No baked catch sheets found at all — refusing to overwrite the library " +
                          "with an empty table. Run the catch pass 2 bake, then BOTH sheet slicers " +
                          "(the bake does not slice), then re-run this.";
                return false;
            }

            int held = 0;
            foreach (string kind in finfish)
            {
                Sprite[] variants = DeckLayVariants($"{FishIso}/Fish_{kind}_deck.png", kind, ref missing);
                var entry = new CatchItemLibrary.KindEntry { Kind = kind, Variants = variants };
                // A carried fish hangs from one hand by the TAIL. The two-arm cradle is the fight's
                // to choose, because only the fight knows the fish's weight (RodFightPresenter picks
                // a size rung, and the rung's own hand count decides gill vs tail). A container's
                // catch has no weight to ask about.
                if (WireHeld(entry, $"{FishIso}/Fish_{kind}_tail.png", Directions)) held++;
                kinds.Add(entry);
                if (variants.Length > 0) wired++;
            }

            foreach (string kind in strips)
            {
                Sprite[] variants = StripVariants($"{Storage}/CatchItem2_{kind}.png", kind, ref missing);
                var entry = new CatchItemLibrary.KindEntry { Kind = kind, Variants = variants };
                // Two sheet families, because the rig lofts the two animals differently: a crustacean
                // is a solid that TURNS (eight facings, gripped on the back), a handful of shellfish
                // is a clutch the rig draws with no camera at all (one facing, two variants).
                if (WireHeld(entry, $"{Storage}/Crust2Held_{kind}.png", Directions)
                    || WireHeld(entry, $"{Storage}/Shell2Hand_{kind}.png", 1)) held++;
                kinds.Add(entry);
                if (variants.Length > 0) wired++;
            }

            RefusePass1(strips);

            var species = SpeciesRows(finfish, strips, out int unmapped);

            library.Configure(kinds.ToArray(), species.ToArray(), fallbackKind: "cod");

            if (created) AssetDatabase.CreateAsset(library, LibraryPath);
            else EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            summary = $"{(created ? "Created" : "Refreshed")} '{LibraryPath}': " +
                      $"{wired}/{kinds.Count} kinds with art, {held} with HELD art, " +
                      $"{species.Count} species mapped" +
                      (missing > 0 ? $", {missing} sheet(s) MISSING (see the warnings above)" : "") +
                      (unmapped > 0 ? $", {unmapped} species left on the fallback kind" : "") +
                      ". Commit it; re-run after a fishing/storage re-bake.";
            return true;
        }

        // ---- the art ------------------------------------------------------------------------------------

        /// <summary>The ADR-0006 facing count — what a directional held sheet is baked at.</summary>
        const int Directions = 8;

        /// <summary>
        /// Wire one kind's held art from <paramref name="sheetPath"/>, returning false (and leaving the
        /// entry untouched) when that sheet is not on disk.
        ///
        /// <para>The frames-per-facing is DIVIDED OUT of the real sprite count rather than declared,
        /// for the same reason the rest of this file reads instead of typing: the crustacean's held
        /// pose is 2 frames today and the day the art director gives it 3, a declared 2 would silently
        /// drop a third of the animation and show the wrong frame at every other facing.</para>
        ///
        /// <para>A sheet whose cell count is not a whole multiple of the facing count is REFUSED with
        /// its own error rather than truncated — that means the slicer and the bake disagree, and a
        /// facing-major index built on a wrong stride draws the right animal facing the wrong way,
        /// which is the hardest kind of wrong to see.</para>
        /// </summary>
        static bool WireHeld(CatchItemLibrary.KindEntry entry, string sheetPath, int facings)
        {
            Sprite[] cells = facings > 1
                ? PersistentCoreBuilder.LoadIsoDirFrames(sheetPath)
                : AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>()
                               .OrderBy(s => s.name, System.StringComparer.Ordinal).ToArray();
            if (cells.Length == 0) return false;

            if (cells.Length % facings != 0)
            {
                Debug.LogError($"[CatchItemLibraryBuilder] '{sheetPath}' sliced to {cells.Length} " +
                               $"cells, which is not a whole multiple of {facings} facings — the " +
                               $"'{entry.Kind}' held art is SKIPPED rather than indexed on a stride " +
                               "that would turn the animal the wrong way. Re-run the storage slicer.");
                return false;
            }

            entry.Held = cells;
            entry.HeldFacings = facings;
            entry.HeldFramesPerFacing = cells.Length / facings;
            return true;
        }

        /// <summary>The four deck lays of one finfish kind: direction row <c>DeckLayRows[v]</c>, frame
        /// <c>v</c> — the rig's own recipe, not a guess.</summary>
        static Sprite[] DeckLayVariants(string sheetPath, string kind, ref int missing)
        {
            Sprite[] cells = PersistentCoreBuilder.LoadIsoDirFrames(sheetPath);
            if (cells.Length == 0)
            {
                Warn(sheetPath, kind, ref missing);
                return System.Array.Empty<Sprite>();
            }

            int perDir = cells.Length / 8;
            var variants = new Sprite[CatchFillMath.Variants];
            for (int v = 0; v < variants.Length; v++)
            {
                int row = DeckLayRows[v % DeckLayRows.Length];
                int frame = v % perDir;
                variants[v] = cells[row * perDir + frame];
            }
            return variants;
        }

        /// <summary>The four baked lay variants of one shellfish/crustacean strip — a single direction
        /// row whose four frames ARE the variants.</summary>
        static Sprite[] StripVariants(string sheetPath, string kind, ref int missing)
        {
            Sprite[] all = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>()
                                        .OrderBy(s => s.name, System.StringComparer.Ordinal).ToArray();
            if (all.Length == 0)
            {
                Warn(sheetPath, kind, ref missing);
                return System.Array.Empty<Sprite>();
            }

            var variants = new Sprite[CatchFillMath.Variants];
            for (int v = 0; v < variants.Length; v++) variants[v] = all[v % all.Length];
            return variants;
        }

        /// <summary>
        /// Say so, once, when a superseded pass-1 strip is still on disk.
        ///
        /// <para><b>Why a log and not a silent fallback.</b> The obvious kindness — "use
        /// <c>CatchItem_lobster</c> when <c>CatchItem2_lobster</c> is missing" — is the failure mode
        /// this repo keeps paying for: the game would draw pass-1 art, look approximately right, and
        /// never tell anyone the pass-2 bake had not been run. A kind with no pass-2 sheet already
        /// reports itself through <see cref="Warn"/>; this adds the other half of the sentence, which
        /// is that a file exists that LOOKS like the answer and is not.</para>
        /// </summary>
        static void RefusePass1(string[] pass2Kinds)
        {
            var stale = new List<string>();
            foreach (string kind in StemsMatching(Storage, "CatchItem_", ""))
                stale.Add($"CatchItem_{kind}.png");
            if (stale.Count == 0) return;

            Debug.LogWarning(
                $"[CatchItemLibraryBuilder] {stale.Count} superseded pass-1 item strip(s) are still on " +
                $"disk and were NOT used: {string.Join(", ", stale)}. Catch pass 2 supersedes them " +
                $"({pass2Kinds.Length} CatchItem2_* kinds were sourced instead). They can be deleted; " +
                "nothing reads them. This is a notice, not a failure.");
        }

        static void Warn(string sheetPath, string kind, ref int missing)
        {
            missing++;
            Debug.LogWarning($"[CatchItemLibraryBuilder] No sliced sprites at '{sheetPath}' — the '{kind}' " +
                             "kind gets no art. A container holding it still fills (the PAIL only needs " +
                             "the kind, for its catch group); the tote simply skips that item rather than " +
                             "drawing a blank. Run the fishing/storage bake, then re-run this.");
        }

        // ---- the species map ------------------------------------------------------------------------------

        /// <summary>
        /// Every shipped species, mapped to the visual kind it draws as. The kind is the species id's own
        /// leaf where that names a baked kind (<c>fish.atlantic_cod</c> → <c>cod</c>); otherwise the
        /// CATEGORY decides, which is how a species with no art of its own still lands in the right
        /// bucket group.
        /// </summary>
        static List<CatchItemLibrary.SpeciesEntry> SpeciesRows(string[] finfish, string[] strips,
                                                                 out int unmapped)
        {
            unmapped = 0;
            var rows = new List<CatchItemLibrary.SpeciesEntry>();
            var known = new HashSet<string>(finfish.Concat(strips), System.StringComparer.Ordinal);

            foreach (string guid in AssetDatabase.FindAssets("t:FishSpeciesDef", new[] { DataFish }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(path);
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;

                string kind = KindFor(def, known);
                if (kind == null) { unmapped++; continue; }
                rows.Add(new CatchItemLibrary.SpeciesEntry { SpeciesId = def.Id, Kind = kind });
            }

            rows.Sort((a, b) => System.StringComparer.Ordinal.Compare(a.SpeciesId, b.SpeciesId));
            return rows;
        }

        /// <summary>Which baked kind a species draws as, or null when nothing fits and the library's own
        /// fallback should answer.</summary>
        static string KindFor(FishSpeciesDef def, HashSet<string> known)
        {
            // fish.atlantic_cod → "atlantic_cod" → the last word, "cod".
            string leaf = def.Id.Substring(def.Id.LastIndexOf('.') + 1);
            if (known.Contains(leaf)) return leaf;

            int under = leaf.LastIndexOf('_');
            if (under >= 0 && known.Contains(leaf.Substring(under + 1))) return leaf.Substring(under + 1);

            // No exact match: a kind name appearing anywhere in the leaf still places it (american
            // lobster → lobster, rock crab → crab, soft_shell_clam → clam). Generalised over the
            // DISCOVERED kinds rather than a list of four, so pass 2's oyster, periwinkle and scallop
            // land without anyone remembering to add three lines here.
            //
            // Longest first, so a kind that contains another ('periwinkle' does not, but the day a
            // 'rock crab' kind sits beside 'crab' it would) resolves to the more specific one.
            foreach (string kind in known.OrderByDescending(k => k.Length))
                if (leaf.Contains(kind)) return kind;

            // Still nothing: the CATEGORY at least puts it in the right group for a pail, which only
            // needs to know it is shellfish.
            return def.Category == FishCategory.Shellfish && known.Contains("clam") ? "clam" : null;
        }

        static bool EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return true;
            string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || !AssetDatabase.IsValidFolder(parent)) return false;
            AssetDatabase.CreateFolder(parent, leaf);
            return AssetDatabase.IsValidFolder(folder);
        }
    }
}
#endif
