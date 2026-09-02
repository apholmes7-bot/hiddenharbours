using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry points. Phase 1 ships fixed recipes rather than a window — the window and
    /// the live scrubbing preview are Phase 2 (ADR 0021 "Phase 1 should ship LIVE SCRUBBING" is
    /// about what the ENGINE makes possible; this PR delivers the bake path underneath it).
    /// </summary>
    public static class RigBakeMenu
    {
        public const string BoatArtFolder = "Assets/_Project/Art/Boats";

        /// <summary>The owner's decision: 32 directions on every hull.</summary>
        public const int Facings = 32;

        /// <summary>Large hulls get 4 rock frames, small hulls 8. A 12 m lobster boat genuinely
        /// rocks less than a 5 m punt, so this is art direction, not only a memory budget.</summary>
        public const int LargeHullRockFrames = 4;
        public const int SmallHullRockFrames = 8;

        [MenuItem("Hidden Harbours/Art/Bake Lobster Boat (32 dir × 4 rock)", priority = 40)]
        public static void BakeLobsterBoat() => BakeLargeHull("lobsterBoat", "LobsterBoatIso", "lobster boat");

        /// <summary>
        /// The Cape Islander's exterior sheet — the LAST hand-exported hull sheet in the repo until
        /// this recipe replaced it (8 cells, baked in a browser at #224 before her rig existed, pivot
        /// recovered from pixels). Same recipe as the lobster: 32 facings, 4 rock frames across two
        /// pages, the rig's own pivot, and the baker's clockwise contract — so her
        /// <c>BoatVisualDef.FacingsAreCounterClockwise</c> is FALSE from here on
        /// (<c>CapeIslanderFacingTests</c> reads her pixels and pins it).
        /// </summary>
        [MenuItem("Hidden Harbours/Art/Bake Cape Islander (32 dir × 4 rock)", priority = 41)]
        public static void BakeCapeIslander() => BakeLargeHull("capeIslander", "CapeIslanderIso", "cape islander");

        /// <summary>
        /// The large-hull recipe: a 32-facing base sheet plus a 4-frame rock grid, which at a
        /// 456×420 cell lands as <c>{base}.png</c> + <c>{base}Rock0.png</c> + <c>{base}Rock1.png</c>
        /// (the baker splits 128 rock cells across two 8×8 pages to stay under the texture cap).
        /// Bake + slice in one operation — see the note on the slice below for why that is
        /// correctness, not convenience.
        /// </summary>
        static void BakeLargeHull(string rigKey, string baseName, string label)
        {
            RunBake(new BakeRequest(rigKey, Facings, rockFrames: 0, BoatArtFolder, baseName),
                    $"{label} — base facings");

            RunBake(new BakeRequest(rigKey, Facings, LargeHullRockFrames, BoatArtFolder, $"{baseName}Rock"),
                    $"{label} — rock grid");

            AssetDatabase.Refresh();

            // Reload from disk before anything wires a serialized reference to these sprites: a
            // mid-build import can invalidate in-memory refs, and the sheets were written by
            // File.WriteAllBytes behind the AssetDatabase's back.
            foreach (var p in new[]
                     {
                         $"{BoatArtFolder}/{baseName}.png",
                         $"{BoatArtFolder}/{baseName}Rock0.png",
                         $"{BoatArtFolder}/{baseName}Rock1.png",
                     })
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);

            // Slice in the same operation rather than leaving it as a step the owner has to
            // remember. This matters more than convenience: a freshly written sheet auto-imports at
            // the DEFAULT 2048 cap, so a 3648-wide page lands DOWNSCALED — and the downscale is
            // silent, because the sprite COUNT still comes out right. SpriteSheetSlicer lifts the
            // cap from its manifest, so running it here is what makes the bake correct, not merely
            // convenient. (LobsterBoatSheetSliceTests is the assert that catches it if this is ever
            // removed; it caught exactly this during development.)
            Art.Editor.SpriteSheetSlicer.SliceAllMenu();

            Debug.Log("[rig-baker] Baked and sliced. Nothing further to run.");
        }

        /// <summary>
        /// Re-bakes the punt to a scratch folder so it can be eyeballed against the shipped sheet.
        /// Deliberately NOT written over Assets/_Project/Art/Boats/PuntIso.png: that art is shipped
        /// and playtested, and ADR 0021 leaves "re-bake the existing kits" as a later, one-at-a-time
        /// job behind a visual diff. The automated equivalent is PuntGoldenMasterTests.
        /// </summary>
        [MenuItem("Hidden Harbours/Dev/Bake Punt to artifacts (golden-master eyeball)", priority = 120)]
        public static void BakePuntToScratch()
        {
            RunBake(new BakeRequest("punt", facings: 8, rockFrames: 0,
                                    "artifacts/rig-bake", "PuntIso"),
                    "punt — 8 facings into artifacts/rig-bake (shipped art untouched)");
        }

        static void RunBake(BakeRequest req, string label)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Baking rig", label, 0.1f);
                var r = RigBaker.Bake(req);
                Debug.Log(Summarise(r, label));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] {label} FAILED: {ex.Message}\n{ex}");
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static string Summarise(BakeResult r, string label)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[rig-baker] {label}");
            sb.AppendLine($"  engine      : {r.EngineName}");
            sb.AppendLine($"  rig         : {r.RigKey}  {r.Geometry}");
            sb.AppendLine($"  convention  : MEASURED {r.MeasuredConvention} -> baked clockwise " +
                          "(FacingsAreCounterClockwise = false)");
            sb.AppendLine($"  cells       : {r.CellsRendered}  ({r.Facings} facings × " +
                          $"{Mathf.Max(1, r.RockFrames)} rock)");
            foreach (var p in r.Pages) sb.AppendLine($"  page        : {p}");
            sb.AppendLine($"  anchors     : {r.AnchorJsonPath}");
            sb.AppendLine($"  render time : {r.RenderMilliseconds:F0} ms " +
                          $"({r.RenderMilliseconds / Mathf.Max(1, r.CellsRendered):F2} ms/cell)");
            sb.AppendLine($"  total time  : {r.TotalMilliseconds / 1000.0:F2} s");
            sb.AppendLine($"  png on disk : {r.TotalPngBytes / 1024.0 / 1024.0:F2} MB");
            sb.Append    ($"  runtime mem : {r.RuntimeBytesRgba32 / 1024.0 / 1024.0:F1} MB RGBA32 " +
                          "(uncropped — tight-crop is deferred, see the PR)");
            return sb.ToString();
        }

        /// <summary>
        /// Headless entry point for CI / -executeMethod.
        ///
        /// ⚠️ Never invoke this with -quit alongside -runTests. The two race: Unity exits 0 having
        /// written total=0, which reads as a pass. That trap is recorded in ADR 0021 and it would
        /// quietly poison any job written that way.
        /// </summary>
        public static void BakeLobsterBoatFromCommandLine() => RunHeadless(BakeLobsterBoat);

        /// <summary>Headless twin of <see cref="BakeCapeIslander"/> — same -quit warning as above.</summary>
        public static void BakeCapeIslanderFromCommandLine() => RunHeadless(BakeCapeIslander);

        static void RunHeadless(Action bake)
        {
            try
            {
                bake();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] headless bake failed: {ex}");
                EditorApplication.Exit(1);
            }
        }
    }
}
