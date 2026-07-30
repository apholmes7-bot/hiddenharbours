#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;          // ShrubCatalog, ShrubSheetSlicer
using HiddenHarbours.Tools.RigBaking;     // ShrubBaker

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// Bakes <b>only the slice of the shrub kit St Peters needs</b>, and slices it.
    ///
    /// <para><b>Why a targeted entry point rather than the kit's own bake-everything menu.</b> The shrub kit
    /// ships to order — no pixels — and its full matrix is 20 species × 8 phases × 5 snow steps × 4 variants
    /// × 3 stages. <c>Report Shrub Sheet Budget</c> puts a number on it: at stage <c>full</c>, both axes and
    /// all three channels is <b>72.42 MiB</b> (24.14 MiB albedo-only). The owner's atlas-budget call on that
    /// matrix is still OPEN, so committing it would be spending an undecided budget. This bakes one corner
    /// of it and says exactly which:</para>
    /// <list type="bullet">
    /// <item><b>One axis</b> — the <b>VARIANT</b> axis (4 individuals × 4 sway frames) at one calendar
    /// station. Four different individuals of a species is exactly what a scattered thicket wants, and it
    /// is <i>half</i> the phase axis's eight columns, seven of which M1 would never place.
    /// <para>⚠️ This PR's first draft baked the PHASE axis instead, as a workaround: at the time
    /// <c>ShrubBaker.BakeVariantAxis</c> wrote a sheet ONE sway row tall where the contract declares four
    /// and the kit's own slicer refused every one. <b>#347 fixed the baker</b> (both axes now lay out all four
    /// rows), so the intended axis is available and this bakes it. The workaround was also quietly wrong
    /// for the barren: it took its variety from the four SWAY FRAMES, and on the stiff mats — lowbush
    /// blueberry is one, and is in this slice — all four sway frames are <b>bit-identical</b>, so every
    /// blueberry on the island would have been the same sprite. Variants are individuals; sway frames are
    /// the same individual leaning.</para></item>
    /// <item><b>One phase</b> — <c>green</c> (July). The island the player walks in M1. On this axis the
    /// phase is the sheet's FIXED dimension rather than a column to index into.</item>
    /// <item><b>One stage</b> — <c>full</c>. Grown shrubs on ground that has been reverting for years.</item>
    /// <item><b>Six species</b>, covering all five of the rig's habitats. See <see cref="Species"/>.</item>
    /// </list>
    ///
    /// <para><b>⚠ Snow is out of scope entirely.</b> M1 is not winter, and the snow axis is what makes the
    /// matrix a matrix.</para>
    ///
    /// <para>Menu: <c>Hidden Harbours ▸ Art ▸ Bake St Peters Shrub Slice</c>. The sheets it writes are
    /// COMMITTED (unlike the kit's default bake, which ships nothing) because the region cannot be built
    /// without them — the PR that adds them states the MiB.</para>
    /// </summary>
    public static class StPetersShrubBake
    {
        /// <summary>The one calendar station baked: July. M1's island is a summer island. This is the
        /// variant-axis sheet's FIXED dimension — every column on the sheet is at this phase.</summary>
        public const string Phase = "green";

        /// <summary>Grown shrubs — the ground has been reverting since the families left.</summary>
        public const string Stage = "full";

        /// <summary>
        /// The six species, chosen to cover ALL FIVE of the rig's habitats with the smallest bake that can
        /// dress the island — and every one of them is a species the contract itself places in that habitat,
        /// never a label I picked:
        /// <list type="bullet">
        /// <item><c>LowbushBlueberry</c> — <b>barren</b>. The wind-scoured heath of an exposed headland.</item>
        /// <item><c>SweetGale</c> — <b>bog</b>. The classic Acadian bog shrub, for the wet hollows.</item>
        /// <item><c>Meadowsweet</c> — <b>swale</b>. Damp meadow.</item>
        /// <item><c>BeakedHazelnut</c> — <b>woods</b>. The understorey inside a stand.</item>
        /// <item><c>WildRose</c> and <c>Raspberry</c> — <b>edge</b>. Both are named in
        /// <c>scene-sizing</c> §5.1's own brief for St Peters' reverting interior ("wild
        /// roses/raspberries"), which is why the edge habitat gets two and the others one.</item>
        /// </list>
        /// </summary>
        public static readonly string[] Species =
        {
            "LowbushBlueberry", "SweetGale", "Meadowsweet", "BeakedHazelnut", "WildRose", "Raspberry",
        };

        [MenuItem("Hidden Harbours/Art/Bake St Peters Shrub Slice", priority = 56)]
        public static void BakeSlice()
        {
            // Fail loudly on a species the rig has never heard of. The kit's own warning is that the README's
            // display names are NOT its keys — "Wild Raspberry" is `Raspberry` and "Winterberry" is
            // `WinterberryHolly` — and a camel-cased label fails silently right up until a sheet stem cannot
            // be matched. Checking against the catalog's cross-check list turns that into a build error here.
            foreach (string s in Species)
                if (Array.IndexOf(ShrubCatalog.SpeciesKeys, s) < 0)
                    throw new ArgumentException(
                        $"'{s}' is not a shrub species key. The README's display names are not the rig's " +
                        $"keys. Known: {string.Join(", ", ShrubCatalog.SpeciesKeys)}");

            ShrubBakeResult result;
            try
            {
                // The variant sheet is baked at ONE calendar station and its four columns are four
                // individuals. St Peters places one column per shrub, hashed by position.
                result = ShrubBaker.BakeVariantAxis(Species, Stage, Phase,
                    progress: (label, t) =>
                        EditorUtility.DisplayProgressBar("Baking St Peters' shrub slice", label, t));
            }
            finally { EditorUtility.ClearProgressBar(); }

            AssetDatabase.Refresh();

            // ⚠️ Re-import every written file BEFORE anything reads a texture: the sheets were written with
            // File.WriteAllBytes behind the AssetDatabase's back, and a mid-build import invalidates any
            // in-memory reference taken before it. (Straight from the kit's own bake menu.)
            foreach (var sheet in result.Sheets)
            foreach (string path in sheet.AssetPaths)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            ShrubSheetSlicer.SliceAllMenu();
            bool verified = ShrubSheetSlicer.VerifyAll(logEachPass: false);

            long bytes = result.Sheets.SelectMany(s => s.AssetPaths)
                                      .Select(p => new System.IO.FileInfo(p))
                                      .Where(f => f.Exists).Sum(f => f.Length);

            Debug.Log($"[stpeters-shrubs] Baked {Species.Length} species at {Stage}/{Phase} on the VARIANT " +
                      $"axis ({ShrubCatalog.Variants} individuals × {ShrubCatalog.SwayFrames} sway rows): " +
                      $"{result.Sheets.Count} sheets, {bytes / 1048576f:0.00} MiB on disk. " +
                      (verified
                          ? "Sliced and verified against the contract."
                          : "⚠️ VERIFY reported problems — do not commit until they are clear.") +
                      " Commit every *.png with its .meta (LFS covers *.png).");
        }

        /// <summary>Headless entry for <c>-executeMethod</c>. Never pair with <c>-runTests</c>.</summary>
        public static void BakeSliceFromCommandLine()
        {
            try { BakeSlice(); EditorApplication.Exit(0); }
            catch (Exception ex)
            {
                Debug.LogError($"[stpeters-shrubs] headless bake failed: {ex}");
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
