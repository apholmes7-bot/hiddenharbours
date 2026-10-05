using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE TERRAIN PLAN WINDOW</b> (Hidden Harbours ▸ World ▸ Terrain Plan…; terrain PR 5). St Peters' plan, step by
    /// step, each step logging what it found:
    /// <list type="bullet">
    /// <item><b>Intake a ground file</b>: a package's ground file into the plan's ground folder (<see cref="GroundFileIntake"/>);
    /// <b>Import ground</b> lays it, twice, and logs each ask's record.</item>
    /// <item><b>Validate</b> the Defs (ids, one per file, every reference resolving).</item>
    /// <item><b>Compare sources</b>: the game's sources now against the frozen file (they must agree while the plan
    /// stands on them). <b>Freeze sources</b> gathers and writes them, once, before the maps are written.</item>
    /// <item><b>Derive</b> from the frozen sources; <b>Derive twice</b>, from scratch each time: the two must hash the same.</item>
    /// <item><b>Run guards</b>: <see cref="TerrainPlanGuards"/> over a derivation and the scene as it stands.</item>
    /// <item><b>Write maps</b>: the R16 height, the still water, the six splat maps and the manifest, read back.</item>
    /// </list>
    /// The same steps run in a batch editor through <c>-executeMethod</c> (the <c>Batch…</c> methods): each logs its
    /// lines and exits 0 when it holds, 1 when it does not.
    /// </summary>
    public sealed class TerrainPlanWindow : EditorWindow
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        readonly List<string> _log = new List<string>();
        Vector2 _scroll;

        [MenuItem("Hidden Harbours/World/Terrain Plan…")]
        public static void Open() => GetWindow<TerrainPlanWindow>("Terrain Plan");

        void OnGUI()
        {
            EditorGUILayout.LabelField("St Peters' terrain plan", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Plan", StPetersTerrainPlan.PlanPath);
            EditorGUILayout.LabelField("Sources", File.Exists(StPetersTerrainPlan.SourcesPath) ? StPetersTerrainPlan.SourcesPath : "not frozen yet");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Intake ground file…"))
                {
                    string json = EditorUtility.OpenFilePanel("A package's ground file", "", "json");
                    if (!string.IsNullOrEmpty(json))
                        Run(log => IntakeGround(log, json, Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(json)))));
                }
                if (GUILayout.Button("Import ground")) Run(ImportGround);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate")) Run(Validate);
                if (GUILayout.Button("Compare sources")) Run(CompareSources);
                if (GUILayout.Button("Freeze sources…"))
                {
                    bool frozen = File.Exists(StPetersTerrainPlan.SourcesPath);
                    if (EditorUtility.DisplayDialog("Freeze the sources",
                            frozen ? "The sources are frozen already. Overwrite them with the game's sources now? Do this only before the maps are written."
                                   : "Gather the sources from the game and write " + StPetersTerrainPlan.SourcesPath + "?",
                            frozen ? "Overwrite" : "Freeze", "Cancel"))
                        Run(log => Freeze(log, frozen));
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Derive")) Run(DeriveOnce);
                if (GUILayout.Button("Derive twice")) Run(DeriveTwice);
                if (GUILayout.Button("Run guards")) Run(Guards);
            }
            if (GUILayout.Button("Write maps…") &&
                EditorUtility.DisplayDialog("Write the maps", "Derive, then write the R16 height, the still water, the six splat maps, the seabed asset's range and the manifest?",
                                            "Write", "Cancel"))
                Run(WriteMaps);
            if (GUILayout.Button("Clear")) _log.Clear();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (string line in _log) EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }

        void Run(Func<List<string>, bool> step)
        {
            var lines = new List<string>();
            try { step(lines); }
            catch (Exception e) { lines.Add("FAILED: " + e.Message); Debug.LogException(e); }
            _log.AddRange(lines);
            foreach (string line in lines) Debug.Log("[TerrainPlan] " + line);
            Repaint();
        }

        // ---- the steps: each adds its lines and says whether it holds ------------------------------------------------------

        public static bool Validate(List<string> log)
        {
            var defs = StPetersTerrainPlan.LoadDefs(out var files);
            var problems = TerrainPlanValidation.Validate(StPetersTerrainPlan.LoadPlan(), defs, files);
            log.Add("VALIDATE " + (problems.Count == 0 ? "PASS" : "FAIL") + ": " + defs.Count + " Defs in " + files.Distinct().Count() + " files, " + problems.Count + " problems");
            log.AddRange(problems.Select(p => "  " + p));
            return problems.Count == 0;
        }

        /// <summary>
        /// A package's ground file into St Peters' ground folder (the file's JSON at <paramref name="jsonPath"/>, its base
        /// beside it), then the plan pointed at it if it reads none. A plan that reads another file is left as it is.
        /// </summary>
        public static bool IntakeGround(List<string> log, string jsonPath, string package)
        {
            var sw = Stopwatch.StartNew();
            if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath)) { log.Add("INTAKE FAIL: no ground file at '" + jsonPath + "'"); return false; }
            var file = GroundFileIntake.Intake(jsonPath, StPetersTerrainPlan.GroundFolder, package, log);
            var plan = StPetersTerrainPlan.LoadPlan();
            if (plan.Ground == null)
            {
                plan.Ground = file;
                EditorUtility.SetDirty(plan);
                AssetDatabase.SaveAssets();
                log.Add("  the plan's Ground now reads " + file.Id);
            }
            else if (plan.Ground != file)
            {
                log.Add("INTAKE FAIL: the plan reads another ground file, " + plan.Ground.Id + "; it is left as it is");
                return false;
            }
            log.Add("INTAKE PASS: " + file.Id + " from " + package + " (" + Secs(sw) + ")");
            return true;
        }

        /// <summary>The plan's ground file imported twice, from a fresh read each time: the two must hash the same.</summary>
        public static bool ImportGround(List<string> log)
        {
            var sw = Stopwatch.StartNew();
            var plan = StPetersTerrainPlan.LoadPlan();
            var a = StPetersTerrainPlan.Import(plan);
            if (a == null) { log.Add("IMPORT FAIL: the plan has no ground file"); return false; }
            var b = StPetersTerrainPlan.Import(plan);
            bool same = a.CodesSha256 == b.CodesSha256 && TerrainPlanMaps.Sha256(a.E) == TerrainPlanMaps.Sha256(b.E);
            log.Add("IMPORT " + (same ? "PASS" : "FAIL") + ": " + plan.Ground.Id + " codes " + a.CodesSha256 + " / " + b.CodesSha256 + "; E " + TerrainPlanMaps.Sha256(a.E) +
                    "; base pixels " + a.BasePixelsSha256 + " (" + Secs(sw) + ")");
            foreach (var k in a.Asks)
                log.Add("  " + k.Id + " (" + k.Kind + "): " + k.Cells + " cells; raise up to " + k.MaxRaise.ToString("0.000", Inv) + ", cut up to " + k.MaxCut.ToString("0.000", Inv) +
                        (k.Kind == GroundAskKind.HollowFill ? "; level " + k.Level.ToString("R", Inv) + ", held " + k.Held : ""));
            return same;
        }

        public static bool CompareSources(List<string> log)
        {
            var diffs = StPetersTerrainPlan.CompareWithFrozen();
            log.Add("COMPARE " + (diffs.Count == 0 ? "PASS" : "FAIL") + ": the game's sources and " + StPetersTerrainPlan.SourcesPath + " differ in " + diffs.Count +
                    " places (tolerance " + StPetersTerrainPlan.CompareTolerance.ToString("R", Inv) + ")");
            log.AddRange(diffs.Select(d => "  " + d));
            return diffs.Count == 0;
        }

        public static bool Freeze(List<string> log, bool overwrite)
        {
            var sw = Stopwatch.StartNew();
            var s = StPetersTerrainPlan.Gather();
            string sha = StPetersTerrainPlan.Freeze(s, overwrite);
            log.Add("FROZE " + StPetersTerrainPlan.SourcesPath + ": sha256 " + sha + "; " + s.Items.Count + " roots, " + s.Items.Values.Sum(v => v.Count) + " items, " +
                    s.Buildings.Count + " buildings (" + Secs(sw) + ")");
            return true;
        }

        public static bool DeriveOnce(List<string> log)
        {
            var sw = Stopwatch.StartNew();
            var r = StPetersTerrainPlan.Derive();
            log.Add("DERIVE " + r.Sha256() + " (" + Secs(sw) + ")");
            log.AddRange(r.Order.Select(o => "  " + o));
            return true;
        }

        /// <summary>Two derivations, each from a fresh read of the frozen sources and a fresh sample of today's ground.</summary>
        public static bool DeriveTwice(List<string> log)
        {
            var sw = Stopwatch.StartNew();
            var a = StPetersTerrainPlan.Derive();
            var b = StPetersTerrainPlan.Derive();
            bool same = a.Sha256() == b.Sha256() && a.Order.SequenceEqual(b.Order) &&
                        a.Numbers.Count == b.Numbers.Count && a.Numbers.All(kv => b.Numbers.TryGetValue(kv.Key, out var v) && v == kv.Value);
            log.Add("TWICE " + (same ? "PASS" : "FAIL") + ": " + a.Sha256() + " / " + b.Sha256() + " (" + Secs(sw) + ")");
            return same;
        }

        public static bool Guards(List<string> log)
        {
            var sw = Stopwatch.StartNew();
            var plan = StPetersTerrainPlan.LoadPlan();
            var src = StPetersTerrainPlan.LoadFrozen();
            var r = TerrainPlanDerivation.Derive(plan, src);
            var cases = TerrainPlanGuards.All(StPetersTerrainPlan.GuardInput(plan, src, r));
            int pass = cases.Count(k => k.Pass);
            log.Add("GUARDS " + pass + "/" + cases.Count + " pass; hash " + r.Sha256() + " (" + Secs(sw) + ")");
            log.AddRange(cases.Select(k => "  " + k));
            return pass == cases.Count;
        }

        public static bool WriteMaps(List<string> log)
        {
            var sw = Stopwatch.StartNew();
            var plan = StPetersTerrainPlan.LoadPlan();
            var src = StPetersTerrainPlan.LoadFrozen();
            var r = TerrainPlanDerivation.Derive(plan, src);
            var man = TerrainPlanMapWriter.Write(plan, src, r);
            var again = TerrainPlanMapWriter.Differences(TerrainPlanMapWriter.ExpectedOf(plan, StPetersTerrainPlan.Derive()));
            log.Add("WROTE " + man.Maps.Count + " maps and " + StPetersTerrainPlan.ManifestPath + " at " + plan.HeightRange.x.ToString("R", Inv) + " to " +
                    plan.HeightRange.y.ToString("R", Inv) + "; derivation " + r.Sha256() + "; a fresh derivation " +
                    (again.Count == 0 ? "reads back the same" : "differs in " + again.Count + " places") + " (" + Secs(sw) + ")");
            log.AddRange(man.Maps.Select(m => "  " + m.Role + " " + m.Path + " values " + m.ValuesSha256 + " file " + m.FileSha256));
            log.AddRange(again.Select(d => "  " + d));
            return again.Count == 0;
        }

        // ---- the batch entries (-executeMethod HiddenHarbours.App.Editor.TerrainPlanWindow.Batch…) ----------------------

        /// <summary>-groundFile &lt;the file's JSON&gt; -groundPackage &lt;the package's name&gt;.</summary>
        public static void BatchIntakeGround() => Batch(log => IntakeGround(log, Arg("-groundFile"), Arg("-groundPackage")));
        public static void BatchImportGround() => Batch(ImportGround);
        public static void BatchValidate() => Batch(Validate);
        public static void BatchCompareSources() => Batch(CompareSources);
        public static void BatchFreezeSources() => Batch(log => Freeze(log, false));
        public static void BatchDeriveTwice() => Batch(DeriveTwice);
        public static void BatchGuards() => Batch(Guards);
        public static void BatchWriteMaps() => Batch(WriteMaps);

        static void Batch(Func<List<string>, bool> step)
        {
            var lines = new List<string>();
            bool ok;
            try { ok = step(lines); }
            catch (Exception e) { lines.Add("FAILED: " + e); ok = false; }
            foreach (string line in lines) Debug.Log("[TerrainPlan] " + line);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>The command line's value after <paramref name="name"/>, or null.</summary>
        static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (string.Equals(a[i], name, StringComparison.Ordinal)) return a[i + 1];
            return null;
        }

        static string Secs(Stopwatch sw) => sw.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s";
    }
}
