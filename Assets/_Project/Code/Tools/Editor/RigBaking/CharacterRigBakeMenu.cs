using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry point for the character FIGHT/BALANCE sheets — Rod Fishing v2 wave 1
    /// (spec: PR #251). One click bakes all eight states, force-reimports, and slices; nothing
    /// further to run. Fixed recipe, same philosophy as <see cref="RigBakeMenu"/>: the dials that
    /// matter (which states) are a decision already made, and the geometry (cell, frame counts,
    /// pivot) comes from the rig at bake time.
    /// </summary>
    public static class CharacterRigBakeMenu
    {
        public const string CharacterArtFolder = "Assets/_Project/Art/Characters/Iso";

        /// <summary>Sheets are named for the default build the rig renders with no build override.</summary>
        public const string SheetPrefix = "Fisher_";

        public const string AnchorFileName = "FisherFightAnchors.json";

        /// <summary>
        /// The eight fight/balance states of the v2 wave-1 spec, named exactly as the rig's ANIMS
        /// table declares them (frame counts are READ from that table, never restated here).
        /// hold/cast_short/cast_long are NOT re-baked — they shipped in #218 and stay until the
        /// flick-cast wave retires them.
        /// </summary>
        public static readonly string[] FightStates =
        {
            "bite", "strike", "reel", "land", "castBack", "castRelease", "balance", "stagger",
        };

        [MenuItem("Hidden Harbours/Art/Bake Character Fight Sheets (8 states × 8 dir)", priority = 42)]
        public static void BakeCharacterFightSheets()
        {
            CharacterBakeResult r;
            try
            {
                r = CharacterRigBaker.Bake(
                    "character", FightStates, CharacterArtFolder, SheetPrefix, AnchorFileName,
                    progress: (label, t) =>
                        EditorUtility.DisplayProgressBar("Baking character fight sheets", label, t));
                Debug.Log(Summarise(r));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] character fight sheets FAILED: {ex.Message}\n{ex}");
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();

            // Reload from disk before anything wires a serialized reference to these sprites: a
            // mid-build import can invalidate in-memory refs, and the sheets were written by
            // File.WriteAllBytes behind the AssetDatabase's back.
            foreach (var sheet in r.Sheets)
                AssetDatabase.ImportAsset(sheet.AssetPath, ImportAssetOptions.ForceUpdate);

            // Slice in the same operation rather than leaving it as a step the owner has to
            // remember. ArtImportPipeline stamps the pixel-art import lock (PPU 32, Point,
            // Uncompressed) on first import; CharacterSheetSlicer adds the Multiple-mode grid and
            // the ground-contact pivot. These sheets are all ≤ 768 px wide — no cap lift needed
            // (and CharacterRigBaker refuses anything over the 2048 default cap at bake time).
            Art.Editor.CharacterSheetSlicer.SliceAllMenu();

            Debug.Log("[rig-baker] Baked and sliced. Nothing further to run — but the results must " +
                      "be COMMITTED: the 8 Fisher_*.png sheets + their .meta files (LFS covers " +
                      $"*.png) and {AnchorFileName} + its .meta, all under {CharacterArtFolder}/.");
        }

        public static string Summarise(CharacterBakeResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[rig-baker] character fight sheets");
            sb.AppendLine($"  engine      : {r.EngineName}");
            sb.AppendLine($"  rig         : {r.RigKey}  {r.Geometry}");
            sb.AppendLine($"  convention  : MEASURED {r.MeasuredConvention} -> emitted unchanged " +
                          "(FacingsAreCounterClockwise = false)");
            sb.AppendLine($"  cells       : {r.CellsRendered} across {r.Sheets.Count} state sheets");
            foreach (var s in r.Sheets) sb.AppendLine($"  sheet       : {s}");
            sb.AppendLine($"  anchors     : {r.AnchorJsonPath}");
            sb.AppendLine($"  render time : {r.RenderMilliseconds:F0} ms " +
                          $"({r.RenderMilliseconds / Mathf.Max(1, r.CellsRendered):F2} ms/cell)");
            sb.AppendLine($"  total time  : {r.TotalMilliseconds / 1000.0:F2} s");
            sb.Append    ($"  png on disk : {r.TotalPngBytes / 1024.0:F0} KB");
            return sb.ToString();
        }

        /// <summary>
        /// Headless entry point for CI / -executeMethod.
        ///
        /// ⚠️ Never invoke this with -quit alongside -runTests. The two race: Unity exits 0 having
        /// written total=0, which reads as a pass. That trap is recorded in ADR 0021 and it would
        /// quietly poison any job written that way.
        /// </summary>
        public static void BakeCharacterFightSheetsFromCommandLine()
        {
            try
            {
                BakeCharacterFightSheets();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] headless character bake failed: {ex}");
                EditorApplication.Exit(1);
            }
        }
    }
}
