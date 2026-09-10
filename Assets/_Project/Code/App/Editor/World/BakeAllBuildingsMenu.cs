#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Text;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;          // the slicers, the prefab builders, VillageBuildingCatalog
using HiddenHarbours.Tools.RigBaking;     // the bake menus
using Debug = UnityEngine.Debug;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>ONE COMMAND THAT MAKES EVERY BUILDING THE KITS KNOW ABOUT</b> — bake, slice, and build the
    /// prefabs, in the order the pipeline actually requires, so a world creator can go from a fresh
    /// checkout to a palette of buildings without knowing which of six menu items comes first.
    ///
    /// <para><b>It contains no bake logic of its own.</b> Every step below calls the EXISTING menu's own
    /// entry point by name. That is the whole point: a second copy of the bake order is a second thing to
    /// forget to update the day a kit gains a build, and the failure mode of a stale copy is a sheet the
    /// slicer slices against last week's cell — which draws, and is wrong.</para>
    ///
    /// <para><b>⚠️ The order is a dependency chain, not a preference.</b> A bake writes PNG sheets and a
    /// JSON contract; the slicer reads the contract to set the sprite sub-assets in the sheet's
    /// <c>.meta</c>; the prefab builders read the SLICED sprites. Run the prefab build before the slice
    /// and it builds prefabs out of whole-sheet sprites — one enormous building per key, and no error
    /// anywhere. Each bake is therefore followed by an <see cref="AssetDatabase.Refresh"/> before its
    /// slicer runs, because the slicer works through the importer and the importer has not seen a file
    /// that Unity does not yet know exists.</para>
    ///
    /// <para><b>It stops at the first failure, and names the kit.</b> Carrying on past a failed bake
    /// produces a contract that claims sheets which are not there, and the next kit's summary line then
    /// reads green over the top of it. One red line naming the kit is worth more than five more lines.</para>
    /// </summary>
    public static class BakeAllBuildingsMenu
    {
        /// <summary>The menu path. A const so a test or a script can name the command it means.</summary>
        public const string MenuPath = "Hidden Harbours/World/Bake All Buildings";

        /// <summary>What the console lines are tagged with.</summary>
        public const string LogPrefix = "[bake-all-buildings]";

        // Priority 200 opens the World submenu below the Art (38–72) and Dev (121+) bands. Unity draws a
        // separator at any priority gap over 10, so this renders as its own section rather than as a
        // stray item at the bottom of Dev — the same trap that made "Bake Buildings" look absent at 23.
        [MenuItem(MenuPath, priority = 200)]
        public static void BakeAllMenu()
        {
            bool ok = RunAll(out string report);

            if (ok)
                Debug.Log($"{LogPrefix} every kit is baked, sliced and built.\n{report}");
            else
                Debug.LogError($"{LogPrefix} STOPPED — see the kit named in the last line.\n{report}");
        }

        /// <summary>
        /// Headless entry point for <c>-executeMethod</c>:
        /// <c>HiddenHarbours.App.Editor.BakeAllBuildingsMenu.BakeAllFromCommandLine</c>.
        ///
        /// <para>Exits non-zero on the first failure, so a batch bake fails loudly rather than leaving a
        /// folder and a contract that disagree. Draws no progress bar in batch mode — a modal in a
        /// headless editor is a run that never returns.</para>
        /// </summary>
        public static void BakeAllFromCommandLine()
        {
            try
            {
                bool ok = RunAll(out string report);
                Debug.Log($"{LogPrefix} (batch) report:\n{report}");
                if (!ok) EditorApplication.Exit(1);
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogPrefix} (batch) threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        // =====================================================================================
        //  THE RUN
        // =====================================================================================

        /// <summary>What one kit's step did.</summary>
        readonly struct StepResult
        {
            /// <summary>How many sheets the slicer sliced. -1 when this step has no slicer.</summary>
            public readonly int Sheets;

            /// <summary>How many prefabs were written. -1 when this step writes none.</summary>
            public readonly int Prefabs;

            /// <summary>How many builds failed. Anything above zero stops the run.</summary>
            public readonly int Failed;

            /// <summary>Anything worth saying on the summary line beyond the counts.</summary>
            public readonly string Note;

            public StepResult(int failed, int sheets = -1, int prefabs = -1, string note = null)
            {
                Failed = failed;
                Sheets = sheets;
                Prefabs = prefabs;
                Note = note;
            }
        }

        /// <summary>
        /// Run every step in order. Returns true when all of them passed; <paramref name="report"/> holds
        /// one line per kit either way, so a failed run still says what got as far as being made.
        /// </summary>
        public static bool RunAll(out string report)
        {
            var log = new StringBuilder();
            bool batch = Application.isBatchMode;
            var whole = Stopwatch.StartNew();
            bool ok = true;

            // ⚠️ The order below IS the dependency chain. Village buildings first because they are what
            // the world creator drags in; the shells have to exist before an interior means anything.
            (string kit, Func<StepResult> run)[] steps =
            {
                ("village buildings (M1 set + derelicts)", VillageBuildings),
                ("houses + wharf (packer proof)",          HousesAndWharf),
                ("interiors (rooms + props)",              Interiors),
                ("shops (shells + interiors)",             Shops),
                ("shop fixtures (standalone props)",       ShopFixtures),
                ("prefabs (village buildings + decor)",    Prefabs),
            };

            try
            {
                for (int i = 0; i < steps.Length; i++)
                {
                    (string kit, Func<StepResult> run) = steps[i];

                    if (!batch)
                        EditorUtility.DisplayProgressBar(
                            "Bake All Buildings", $"{kit}…", i / (float)steps.Length);

                    var timer = Stopwatch.StartNew();
                    StepResult r;
                    try
                    {
                        r = run();
                    }
                    catch (Exception e)
                    {
                        timer.Stop();
                        log.AppendLine($"  ✗ {kit,-38} threw after {Seconds(timer)}: {e.Message}");
                        Debug.LogError($"{LogPrefix} '{kit}' threw:\n{e}");
                        ok = false;
                        break;
                    }
                    timer.Stop();

                    log.AppendLine(Describe(kit, r, timer));

                    if (r.Failed > 0)
                    {
                        Debug.LogError(
                            $"{LogPrefix} '{kit}' reported {r.Failed} failure(s) — stopping here rather " +
                            "than baking the next kit on top of a contract that claims sheets which are " +
                            "not on disk. The errors above name the builds.");
                        ok = false;
                        break;
                    }
                }
            }
            finally
            {
                if (!batch) EditorUtility.ClearProgressBar();
            }

            whole.Stop();
            log.AppendLine();
            log.AppendLine($"  {(ok ? "all kits done" : "STOPPED")} in {Seconds(whole)}.");
            report = log.ToString();
            return ok;
        }

        static string Describe(string kit, in StepResult r, Stopwatch timer)
        {
            var line = new StringBuilder();
            line.Append(r.Failed > 0 ? "  ✗ " : "  ✓ ");
            line.Append(kit.PadRight(38));
            line.Append(r.Sheets >= 0 ? $"{r.Sheets,3} sheet(s)" : "     no slice");
            line.Append(r.Prefabs >= 0 ? $", {r.Prefabs,3} prefab(s)" : ",              ");
            line.Append($", {Seconds(timer)}");
            if (r.Failed > 0) line.Append($", {r.Failed} FAILED");
            if (!string.IsNullOrEmpty(r.Note)) line.Append($" — {r.Note}");
            return line.ToString();
        }

        static string Seconds(Stopwatch t) => $"{t.Elapsed.TotalSeconds:0.0}s";

        // =====================================================================================
        //  THE STEPS — each one calls the existing menu's own entry point, by name
        // =====================================================================================

        /// <summary>The M1 set: the buildings world-content actually ships and the derelicts.</summary>
        static StepResult VillageBuildings()
        {
            string bake = VillageBuildingBakeMenu.BakeAll(out int bakeFailed);
            Debug.Log($"{LogPrefix} village buildings:\n{bake}");
            AssetDatabase.Refresh();
            if (bakeFailed > 0) return new StepResult(bakeFailed, note: "bake");

            int sliced = VillageBuildingSheetSlicer.SliceAll(out int sliceFailed);
            return new StepResult(sliceFailed, sheets: sliced,
                                  note: sliceFailed > 0 ? "slice" : null);
        }

        /// <summary>
        /// The packer-proof bake — the five house presets and the wharf buildings, baked worst-case for
        /// the crop and texture-cap proof.
        ///
        /// <para>⚠️ <see cref="BuildingBakeMenu.BakeAllMenu"/> returns void and reports its failures to
        /// the console rather than to its caller, so this step counts the errors logged while it runs.
        /// That is a wider net than a failure count — an unrelated system logging an error during the
        /// bake stops the run too — and it is deliberately the cautious direction: a proof bake that
        /// logged an error is not a proof.</para>
        /// </summary>
        static StepResult HousesAndWharf()
        {
            int errors = CountErrorsDuring(BuildingBakeMenu.BakeAllMenu);
            AssetDatabase.Refresh();
            return new StepResult(errors, note: errors > 0 ? "errors logged during the bake" : null);
        }

        /// <summary>The rooms that go inside the buildings, and the furniture that goes in them.</summary>
        static StepResult Interiors()
        {
            string bake = InteriorBakeMenu.BakeAll(out int bakeFailed);
            Debug.Log($"{LogPrefix} interiors:\n{bake}");
            AssetDatabase.Refresh();
            if (bakeFailed > 0) return new StepResult(bakeFailed, note: "bake");

            int sliced = InteriorSheetSlicer.SliceAll(out int sliceFailed);
            return new StepResult(sliceFailed, sheets: sliced,
                                  note: sliceFailed > 0 ? "slice" : null);
        }

        /// <summary>The shop shells and their ground plans.</summary>
        static StepResult Shops()
        {
            string bake = ShopBakeMenu.BakeAll(out int bakeFailed);
            Debug.Log($"{LogPrefix} shops:\n{bake}");
            AssetDatabase.Refresh();
            if (bakeFailed > 0) return new StepResult(bakeFailed, note: "bake");

            int sliced = ShopSheetSlicer.SliceAll(out int sliceFailed);
            return new StepResult(sliceFailed, sheets: sliced,
                                  note: sliceFailed > 0 ? "slice" : null);
        }

        /// <summary>The counters, shelves and cases that stand inside a shop.
        /// <see cref="ShopFixtureBakeMenu.BakeAndSlice"/> bakes, slices and verifies in one call and
        /// throws on failure — which the run loop catches and reports against this kit.</summary>
        static StepResult ShopFixtures()
        {
            ShopFixtureBakeMenu.BakeAndSlice();
            AssetDatabase.Refresh();
            return new StepResult(0);
        }

        /// <summary>The prefabs a world creator actually drags into a region scene. Last, because they
        /// read the SLICED sprites — build them before the slice and every prefab is one whole sheet.
        /// </summary>
        static StepResult Prefabs()
        {
            int buildings = VillageBuildingPrefabBuilder.BuildAll(VillageBuildingCatalog.PrefabRoot);
            DecorPrefabBuilder.Build();
            AssetDatabase.Refresh();

            return new StepResult(
                buildings > 0 ? 0 : 1,
                prefabs: buildings,
                note: buildings > 0 ? "+ decor" : "no village building prefab was written — are the " +
                                                  "sheets sliced?");
        }

        // =====================================================================================

        /// <summary>Run <paramref name="body"/> and report how many errors it logged. The only failure
        /// signal a void menu command offers.</summary>
        static int CountErrorsDuring(Action body)
        {
            int errors = 0;
            Application.LogCallback handler = (condition, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    errors++;
            };

            Application.logMessageReceived += handler;
            try { body(); }
            finally { Application.logMessageReceived -= handler; }
            return errors;
        }
    }
}
#endif
