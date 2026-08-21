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

                Undo.RecordObject(visual, "Wire boat interior");
                visual.Interior = def;
                visual.InteriorCells = cells;
                visual.InteriorFacings = sheet.facings;
                visual.InteriorCellLevels = (string[])sheet.levels.Clone();
                visual.InteriorCellRowForLevel = rowForLevel;
                visual.InteriorCellsAreCounterClockwise =
                    string.Equals(sheet.convention, "CounterClockwise", StringComparison.Ordinal);
                EditorUtility.SetDirty(visual);

                r.Wired = true;
                r.Cells = cells.Length;
                results.Add(r);
                log.AppendLine($"  {stem}: {cells.Length} cells " +
                               $"({sheet.levels.Length} levels x {sheet.facings} facings), " +
                               $"{sheet.pixelsPerMetre} px/m, {sheet.convention}");
            }

            AssetDatabase.SaveAssets();
            return results;
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
