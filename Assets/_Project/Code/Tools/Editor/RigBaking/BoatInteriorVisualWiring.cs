using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Points each measured hull at her own cabin</b> — the link and the cells, written onto the
    /// <see cref="BoatVisualDef"/> the runtime already reads.
    ///
    /// <para><b>Why the visual def and not a lookup service.</b> Exactly the reason
    /// <c>BoatVisualDef.HullMesh</c> lives there: the Boats module poses the hull and Art draws her, so
    /// the asset both already share is the one honest home for the link. A registry keyed on an id would
    /// be a second thing to keep in step, and the first hull renamed would break it silently.</para>
    ///
    /// <para><b>Builder-written, never hand-authored</b> — same rule and same reason as the interior defs
    /// themselves: 24 hulls x up to 24 sprite references is exactly the transcription a person gets
    /// wrong once and nobody notices, because a cabin one cell out of step still draws a cabin. Re-run it
    /// after any interior re-bake or re-slice.</para>
    ///
    /// <para><b>⚠ THE LEDGER IS THE TRUTH, AND IT IS CHECKED TWICE.</b> A hull the S0 intake REFUSED must
    /// never gain a def and must never be wired — the two sport fishers on an unstamped renderer pin and
    /// the cape on a forked rig. This walks the committed defs (which cannot exist for a refused hull) and
    /// then re-checks every stem against the contract's own <c>refused</c> list before writing anything.
    /// Belt and braces on purpose: the first check is an invariant somebody else maintains, and this is
    /// the last gate before a refused hull becomes enterable content.</para>
    ///
    /// <para>Run: <b>Hidden Harbours ▸ Dev ▸ 3D Hulls ▸ Wire Boat Interiors To Visuals</b>, or headless
    /// with <c>-executeMethod HiddenHarbours.Tools.RigBaking.BoatInteriorVisualWiring.WireAllCli</c>.</para>
    /// </summary>
    public static class BoatInteriorVisualWiring
    {
        private const string InteriorDefFolder = "Assets/_Project/Data/Boats/Interiors";
        private const string VisualDefFolder = "Assets/_Project/Data/Boats/Visuals";

        /// <summary>Where the per-hull cells assets live. Under <c>Resources</c> on purpose: it is the
        /// one place Unity will load from WITHOUT anything holding a reference, which is exactly the
        /// property that keeps a hull from dragging her cabin's pixels in on spawn.</summary>
        private const string CellsFolder =
            "Assets/_Project/Resources/" + BoatInteriorCellsDef.ResourcesFolder;

        /// <summary>What one hull's wiring did, for the report and for the tests.</summary>
        public struct Result
        {
            public string Stem;
            public bool Wired;
            public int Cells;
            public string Problem;
        }

        [MenuItem("Hidden Harbours/Dev/3D Hulls/Wire Boat Interiors To Visuals")]
        public static void WireAll()
        {
            var log = new StringBuilder();
            List<Result> results = Wire(log);
            int wired = results.Count(r => r.Wired);
            int failed = results.Count(r => !r.Wired);

            log.AppendLine($"{wired} wired, {failed} refused.");
            if (failed > 0) Debug.LogError(log.ToString());
            else Debug.Log(log.ToString());
        }

        /// <summary>Headless entry point. Exit code is meaningful here: any refusal is a real fault,
        /// because the ledger's own three hulls have no def and are therefore never even visited.</summary>
        public static void WireAllCli()
        {
            var log = new StringBuilder();
            int failed;
            try
            {
                List<Result> results = Wire(log);
                failed = results.Count(r => !r.Wired);
                log.AppendLine($"{results.Count(r => r.Wired)} wired, {failed} refused.");
                Debug.Log(log.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError($"{log}\n[BoatInteriorVisualWiring] {e}");
                failed = 1;
            }
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        /// <summary>
        /// Wire every hull that has a committed interior def. Returns one <see cref="Result"/> per hull
        /// visited; a hull with no def is never visited at all, which is what "absence is data" means
        /// here.
        /// </summary>
        public static List<Result> Wire(StringBuilder log)
        {
            var results = new List<Result>();
            BoatInteriorKit.Contract contract = BoatInteriorSheetSlicer.LoadContract();
            if (contract == null)
            {
                log.AppendLine("[BoatInteriorVisualWiring] no sheet contract at " +
                               BoatInteriorKit.ContractPath + " — bake and slice the sheets first.");
                return results;
            }

            var refused = new HashSet<string>(
                (contract.refused ?? Array.Empty<BoatInteriorKit.RefusedEntry>())
                    .Select(r => r.hullStem ?? "")
                    .Where(s => s.Length > 0), StringComparer.Ordinal);

            var byStem = new Dictionary<string, BoatInteriorKit.SheetEntry>(StringComparer.Ordinal);
            foreach (BoatInteriorKit.SheetEntry s in contract.sheets ?? Array.Empty<BoatInteriorKit.SheetEntry>())
                byStem[s.defId ?? ""] = s;

            string[] guids = AssetDatabase.FindAssets("t:BoatInteriorDef", new[] { InteriorDefFolder });
            Array.Sort(guids);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<BoatInteriorDef>(path);
                if (def == null) continue;

                string stem = Path.GetFileNameWithoutExtension(path);
                var r = new Result { Stem = stem };

                if (!byStem.TryGetValue(def.Id ?? "", out BoatInteriorKit.SheetEntry sheet))
                {
                    r.Problem = $"no sheet in the contract for def id '{def.Id}'";
                    results.Add(r);
                    log.AppendLine($"  REFUSED {stem}: {r.Problem}");
                    continue;
                }

                // ⚠ The ledger's own word, re-checked at the last gate. A def should not exist for a
                // refused hull at all; if one does, this is where it stops rather than where it ships.
                if (refused.Contains(sheet.hullStem))
                {
                    r.Problem = $"the S0 ledger REFUSES hull stem '{sheet.hullStem}' — it must carry no " +
                                "def and no wiring. Fix the intake, not this.";
                    results.Add(r);
                    log.AppendLine($"  REFUSED {stem}: {r.Problem}");
                    continue;
                }

                var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>($"{VisualDefFolder}/{stem}.asset");
                if (visual == null)
                {
                    r.Problem = $"no BoatVisualDef at {VisualDefFolder}/{stem}.asset";
                    results.Add(r);
                    log.AppendLine($"  REFUSED {stem}: {r.Problem}");
                    continue;
                }

                Sprite[] cells = LoadCells(sheet, out string cellProblem);
                if (cells == null)
                {
                    r.Problem = cellProblem;
                    results.Add(r);
                    log.AppendLine($"  REFUSED {stem}: {r.Problem}");
                    continue;
                }

                int[] rowForLevel = MapLevels(def, sheet, out string mapProblem);
                if (rowForLevel == null)
                {
                    r.Problem = mapProblem;
                    results.Add(r);
                    log.AppendLine($"  REFUSED {stem}: {r.Problem}");
                    continue;
                }

                // THE PIXELS go to their own asset under Resources; the visual def keeps only the
                // LINK. See BoatInteriorCellsDef on why the reference is broken on purpose.
                if (!WriteCells(def, sheet, cells, rowForLevel, out string cellsProblem))
                {
                    r.Problem = cellsProblem;
                    results.Add(r);
                    log.AppendLine($"  REFUSED {stem}: {r.Problem}");
                    continue;
                }

                Undo.RecordObject(visual, "Wire boat interior");
                visual.Interior = def;
                EditorUtility.SetDirty(visual);

                r.Wired = true;
                r.Cells = cells.Length;
                results.Add(r);
                log.AppendLine($"  {stem}: {cells.Length} cells " +
                               $"({sheet.levels.Length} levels x {sheet.facings} facings), " +
                               $"{sheet.pixelsPerMetre} px/m, rig {sheet.convention} (cells clockwise)");
            }

            AssetDatabase.SaveAssets();
            return results;
        }

        /// <summary>
        /// Write (or refresh) one hull's cells asset under <c>Resources</c>.
        ///
        /// <para>The asset is reused in place when it already exists, so its GUID survives and nothing
        /// that points at it breaks on a re-bake — the same courtesy the sheet slicer pays sprite ids.</para>
        /// </summary>
        private static bool WriteCells(BoatInteriorDef def, BoatInteriorKit.SheetEntry sheet,
                                       Sprite[] cells, int[] rowForLevel, out string problem)
        {
            problem = null;
            string file = BoatInteriorCellsDef.FileNameFor(def.Id);
            if (string.IsNullOrEmpty(file)) { problem = $"def '{def.name}' has no id"; return false; }

            if (!AssetDatabase.IsValidFolder(CellsFolder))
            {
                string parent = System.IO.Path.GetDirectoryName(CellsFolder).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(CellsFolder));
            }

            string path = $"{CellsFolder}/{file}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<BoatInteriorCellsDef>(path);
            bool fresh = asset == null;
            if (fresh) asset = ScriptableObject.CreateInstance<BoatInteriorCellsDef>();

            asset.InteriorDefId = def.Id;
            asset.Cells = cells;
            asset.Facings = sheet.facings;
            asset.CellLevels = (string[])sheet.levels.Clone();
            asset.CellRowForLevel = rowForLevel;

            // ⚠️⚠️ ALWAYS FALSE, AND THAT IS THE FIX — READ BEFORE "RESTORING" sheet.convention HERE.
            //
            // This line used to read `string.Equals(sheet.convention, "CounterClockwise")`, which
            // conflated two different facts that happen to share a word:
            //
            //   • `sheet.convention` describes the RIG — "the measured azimuth convention of the
            //     EXTERIOR rig this room was cut from, THE CONVENTION THE CELLS WERE CORRECTED BY"
            //     (BoatInteriorKit.SheetEntry.convention). It is an INPUT, and
            //     SheetConvention_IsTheExteriorsMeasuredHandedness asserts it against the shipped
            //     exterior rig's own ground-plane bearing. It is correct and stays exactly as it is.
            //
            //   • `CellsAreCounterClockwise` describes the PIXELS — it is the flag
            //     IsoFacing.HeadingToFacingIndex uses to UN-MIRROR counter-clockwise art
            //     (`if (facingsAreCounterClockwise) idx = count - idx`). It is an OUTPUT.
            //
            // The baker already applied the correction at bake time: every cell is rendered through
            // RigBaker.DirForCell(facing, facings, probe.Convention), which maps cell k to dir
            // (facings-k)%facings for a counter-clockwise rig and to k for a clockwise one. Whatever
            // the rig's handedness, the CELLS come off the press canonically CLOCKWISE — cell i
            // depicts +45°·i, exactly as labelled. There is nothing left to un-mirror.
            //
            // Feeding the rig's convention in here therefore MIRRORED AN ALREADY-CORRECT SHEET and
            // drew the wrong facing at every heading except the two that are their own mirror (0 and
            // facings/2) — measured on the shipped pixels at 45.0–45.3° per column across the fleet.
            // It is the same bug BoatVisualLibraryBuilder's LobsterBoatIso block calls "the precise
            // bug that shipped five times in this project before anyone measured", and it is why the
            // owner's intro cabin drew on the cuddy roof at a W heading.
            //
            // BoatInteriorCellHandednessTests reads the shipped PNGs and goes red the instant this
            // flips back. Do not change this line to make a different test pass.
            asset.CellsAreCounterClockwise = false;
            asset.ResidentMegabytes = ResidentMegabytes(sheet);

            if (fresh) AssetDatabase.CreateAsset(asset, path);
            else EditorUtility.SetDirty(asset);

            if (!asset.IsUsableFor(def))
            {
                problem = "the cells asset just written does not fit its own def — rows, map or array " +
                          "disagree. This is a builder bug, not a data one.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// What this hull's pages cost once resident, MB of RGBA32 — written into the asset so the
        /// budget is legible in an inspector and assertable in a test, rather than something a profiler
        /// has to be opened to discover.
        /// </summary>
        private static float ResidentMegabytes(BoatInteriorKit.SheetEntry sheet)
        {
            long px = 0;
            foreach (BoatInteriorKit.SheetPage p in sheet.pages ?? Array.Empty<BoatInteriorKit.SheetPage>())
                px += (long)p.sheetW * p.sheetH;
            return (float)(px * 4.0 / (1024 * 1024));
        }

        /// <summary>
        /// <b>Which sheet ROW draws each of the def's LEVELS</b> — the map the whole wiring turns on,
        /// computed here once so nothing re-derives it from a name at runtime.
        ///
        /// <para><b>⚠ The two lists are not the same list, in length OR in order.</b> A
        /// <see cref="BoatInteriorDef"/> declares every level a route can reach, and on the five ships
        /// that includes <c>main_deck</c> (plus the tanker's <c>poop_deck</c>) — EXTERIOR working decks
        /// the interior sheets rightly never bake. Meanwhile the sheets run
        /// <c>bridge/house/below</c> where the defs run
        /// <c>main_deck/house_sole/bridge_sole/below_sole</c>. Indexing the cells by the def's level
        /// index therefore draws the BRIDGE while the player stands on the HOUSE sole: a perfectly
        /// plausible picture of the wrong room, on every ship in the fleet.</para>
        ///
        /// <para><b>−1 is a real answer</b>, not a failure: an outdoor deck has no interior to draw, and
        /// the runtime uses that to keep a cabin door from walking somebody onto a working deck (the
        /// trawler declares main_deck at 3.5 m, the exact height of her house sole).</para>
        ///
        /// <para><b>Matched by id, and a sheet row that matches NOTHING is refused.</b> The kit's level
        /// keys are the def's ids less a <c>_sole</c> suffix, which is a convention — so it is used
        /// HERE, once, where a mismatch can be reported by name, and never at runtime where it would
        /// fail silently. If the two ever stop corresponding, this refuses the hull rather than wiring a
        /// guess.</para>
        /// </summary>
        private static int[] MapLevels(BoatInteriorDef def, BoatInteriorKit.SheetEntry sheet,
                                       out string problem)
        {
            problem = null;
            int levels = def.Levels != null ? def.Levels.Length : 0;
            var map = new int[levels];
            for (int i = 0; i < levels; i++) map[i] = -1;

            for (int row = 0; row < sheet.levels.Length; row++)
            {
                string key = sheet.levels[row];
                int at = -1;
                for (int i = 0; i < levels; i++)
                {
                    string id = def.Levels[i] != null ? def.Levels[i].Id : null;
                    if (string.IsNullOrEmpty(id)) continue;
                    if (string.Equals(id, key, StringComparison.Ordinal) ||
                        string.Equals(id, key + "_sole", StringComparison.Ordinal))
                    { at = i; break; }
                }

                if (at < 0)
                {
                    problem = $"the sheet bakes a level '{key}' that the def declares nowhere " +
                              "(def levels: " +
                              string.Join(", ", (def.Levels ?? Array.Empty<BoatInteriorLevel>())
                                                .Where(l => l != null).Select(l => l.Id)) +
                              "). The sheets and the def disagree — re-run the def builder and the " +
                              "sheet bake from the same kit.";
                    return null;
                }
                map[at] = row;
            }

            bool any = map.Any(r => r >= 0);
            if (!any)
            {
                problem = "no level of this def is drawn by any sheet row";
                return null;
            }
            return map;
        }

        /// <summary>
        /// The sliced cells for one hull, in <see cref="BoatInteriorKit.SheetEntry.CellIndex"/>'s own
        /// LEVEL-MAJOR order — carried across rather than re-derived, so the runtime and the baker cannot
        /// drift about a layout.
        ///
        /// <para>Returns null on the first hole, with the reason. All-or-nothing for the same reason the
        /// def's own gate is: a partly wired sheet draws a plausible picture of the wrong room.</para>
        /// </summary>
        private static Sprite[] LoadCells(BoatInteriorKit.SheetEntry sheet, out string problem)
        {
            problem = null;
            var byName = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (BoatInteriorKit.SheetPage page in sheet.pages ?? Array.Empty<BoatInteriorKit.SheetPage>())
            {
                foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(page.AssetPath))
                    if (o is Sprite s) byName[s.name] = s;
            }

            var cells = new Sprite[sheet.CellCount];
            for (int level = 0; level < sheet.levels.Length; level++)
            {
                for (int facing = 0; facing < sheet.facings; facing++)
                {
                    string wanted = sheet.SpriteName(level, facing);
                    if (!byName.TryGetValue(wanted, out Sprite sprite))
                    {
                        problem = $"sprite '{wanted}' is missing — re-slice the sheets " +
                                  "(Hidden Harbours ▸ Art ▸ Slice Boat Interior Sheets)";
                        return null;
                    }
                    cells[sheet.CellIndex(level, facing)] = sprite;
                }
            }
            return cells;
        }
    }
}
