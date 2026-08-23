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

        /// <summary>The finfish kinds, each drawn from its own <c>Fish_&lt;kind&gt;_deck</c> sheet.</summary>
        static readonly string[] FinfishKinds = { "cod", "haddock", "pollock", "mackerel" };

        /// <summary>The shellfish/crustacean kinds, each drawn from its own <c>CatchItem_&lt;kind&gt;</c>
        /// strip.</summary>
        static readonly string[] StripKinds = { "lobster", "crab", "mussel", "clam" };

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

            foreach (string kind in FinfishKinds)
            {
                Sprite[] variants = DeckLayVariants($"{FishIso}/Fish_{kind}_deck.png", kind, ref missing);
                kinds.Add(new CatchItemLibrary.KindEntry { Kind = kind, Variants = variants });
                if (variants.Length > 0) wired++;
            }

            foreach (string kind in StripKinds)
            {
                Sprite[] variants = StripVariants($"{Storage}/CatchItem_{kind}.png", kind, ref missing);
                kinds.Add(new CatchItemLibrary.KindEntry { Kind = kind, Variants = variants });
                if (variants.Length > 0) wired++;
            }

            var species = SpeciesRows(out int unmapped);

            library.Configure(kinds.ToArray(), species.ToArray(), fallbackKind: "cod");

            if (created) AssetDatabase.CreateAsset(library, LibraryPath);
            else EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            summary = $"{(created ? "Created" : "Refreshed")} '{LibraryPath}': " +
                      $"{wired}/{kinds.Count} kinds with art, {species.Count} species mapped" +
                      (missing > 0 ? $", {missing} sheet(s) MISSING (see the warnings above)" : "") +
                      (unmapped > 0 ? $", {unmapped} species left on the fallback kind" : "") +
                      ". Commit it; re-run after a fishing/storage re-bake.";
            return true;
        }

        // ---- the art ------------------------------------------------------------------------------------

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
        static List<CatchItemLibrary.SpeciesEntry> SpeciesRows(out int unmapped)
        {
            unmapped = 0;
            var rows = new List<CatchItemLibrary.SpeciesEntry>();
            var known = new HashSet<string>(FinfishKinds.Concat(StripKinds), System.StringComparer.Ordinal);

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

            // No name match: the CATEGORY still puts it in the right group for a pail (soft-shell clam →
            // clam → 'shell'; American lobster → lobster; rock crab → crab).
            if (leaf.Contains("lobster")) return "lobster";
            if (leaf.Contains("crab")) return "crab";
            if (leaf.Contains("mussel")) return "mussel";
            if (leaf.Contains("clam")) return "clam";
            return def.Category == FishCategory.Shellfish ? "clam" : null;
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
