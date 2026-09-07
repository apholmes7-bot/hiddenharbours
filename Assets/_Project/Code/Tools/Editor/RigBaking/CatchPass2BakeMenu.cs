using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HiddenHarbours.Fishing;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry point for catch pass 2's fish sheets, and the resolver that decides which
    /// weight band each species' size ladder spans.
    ///
    /// <para>Same philosophy as <see cref="FishingKitBakeMenu"/>: one click bakes, and the decisions
    /// that are already made (which species, which states, how many rungs) are not offered as dials.
    /// The one thing this menu DOES decide is the ladder's band, and it decides it by reading the
    /// shipped content rather than by carrying a table — see <see cref="ResolveWeightBands"/>.</para>
    /// </summary>
    public static class CatchPass2BakeMenu
    {
        public const string OutputFolder = CatchPass2Baker.DefaultOutputFolder;

        /// <summary>
        /// One click for the whole of catch pass 2 — 210 fish sheets plus the 34 storage-side ones
        /// (30 as pass 2 shipped, plus the hod’s four heap bands).
        /// The fish go first because they are the long pole and a failure there should stop the run
        /// before anything else is written.
        /// </summary>
        [MenuItem("Hidden Harbours/Art/Bake Catch Pass 2 (fish + crustaceans + shellfish + hod)",
                  priority = 47)]
        public static void BakeCatchPass2()
        {
            BakeCatchPass2Fish();
            BakeCatchPass2Storage();
        }

        [MenuItem("Hidden Harbours/Art/Bake Catch Pass 2 Storage (crustaceans + shellfish + hod + items)",
                  priority = 49)]
        public static void BakeCatchPass2Storage()
        {
            var bakes = new (string Label, Func<FishingBakeResult> Run)[]
            {
                ("crustaceans", () => CatchPass2StorageBaker.BakeCrustaceans(
                    progress: (l, t) => EditorUtility.DisplayProgressBar("Baking crustaceans", l, t))),
                ("shellfish", () => CatchPass2StorageBaker.BakeShellfish(
                    progress: (l, t) => EditorUtility.DisplayProgressBar("Baking shellfish", l, t))),
                ("clam hod", () => CatchPass2StorageBaker.BakeClamHod(
                    progress: (l, t) => EditorUtility.DisplayProgressBar("Baking the clam hod", l, t))),
                ("catch items", () => CatchPass2StorageBaker.BakeCatchItems(
                    progress: (l, t) => EditorUtility.DisplayProgressBar("Baking catch items", l, t))),
            };

            try
            {
                foreach (var (label, run) in bakes)
                {
                    FishingBakeResult r = run();
                    Debug.Log($"[CatchPass2BakeMenu] {label}: {r.Sheets.Count} sheet(s), " +
                              $"{r.CellsRendered} cells, {r.TotalPngBytes / 1024} KB, " +
                              $"{r.TotalMilliseconds / 1000.0:F1}s\n  " +
                              string.Join("\n  ", r.Sheets.Select(s => s.ToString())));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CatchPass2BakeMenu] Storage bake FAILED, nothing further ran.\n{ex}");
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// The clam hod ALONE — its two wire layers, the four heap bands and the anchors JSON.
        ///
        /// <para><b>Why the hod has its own entry when the others do not.</b> Every stem this bake
        /// writes is rewritten whether or not it changed, and a re-bake is not byte-deterministic. So a
        /// lane that only needs the hod’s sheets and reaches for the whole storage bake ships churn on
        /// ~30 shipped sheets nobody asked to change — and then has to prove, one PNG at a time, that
        /// the churn was only the encoder. Baking exactly what you changed is cheaper than auditing
        /// what you did not.</para>
        /// </summary>
        [MenuItem("Hidden Harbours/Art/Bake the Clam Hod (2 wire layers + 4 heap bands)",
                  priority = 51)]
        public static void BakeClamHodOnly()
        {
            try
            {
                FishingBakeResult r = CatchPass2StorageBaker.BakeClamHod(
                    progress: (l, t) => EditorUtility.DisplayProgressBar("Baking the clam hod", l, t));
                Debug.Log($"[rig-baker] clam hod: {r.Sheets.Count} sheet(s), {r.CellsRendered} cells, "
                          + $"{r.TotalPngBytes / 1024} KB, {r.TotalMilliseconds / 1000.0:F1}s\n  "
                          + string.Join("\n  ", r.Sheets.Select(s => s.ToString()))
                          + $"\n  anchors: {r.AnchorJsonPath}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Headless entry point for <c>-executeMethod</c>.
        ///
        /// <para>⚠️ <c>-executeMethod</c> REQUIRES <c>-quit</c> (without it the editor bakes, logs and
        /// then sits on the project lock forever, and the next run dies with return code 1 while the
        /// shell still reports exit 0). <c>-runTests</c> is the opposite and must never be given
        /// <c>-quit</c>. Confirm this ran by grepping a FRESH log for <c>[rig-baker] clam hod:</c> —
        /// never by the exit code, and never by the sheets being present.</para>
        /// </summary>
        public static void BakeClamHodFromCommandLine()
        {
            try
            {
                BakeClamHodOnly();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] headless clam hod bake failed: {ex}");
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Hidden Harbours/Art/Bake Catch Pass 2 Fish (7 species × 10 states × 3 sizes)",
                  priority = 48)]
        public static void BakeCatchPass2Fish()
        {
            CatchPass2BakeResult result = null;
            try
            {
                var bands = ResolveWeightBands();
                result = CatchPass2Baker.BakeFish(
                    bands,
                    progress: (label, t) =>
                        EditorUtility.DisplayProgressBar("Baking catch pass-2 fish sheets", label, t));
                Debug.Log(Summarise(result, bands));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CatchPass2BakeMenu] Bake FAILED, nothing further ran.\n{ex}");
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Re-emit <c>FishIsoAnchors.json</c> alone — the ladder (per-rung kg and hand count) and
        /// the per-rung mouth tables — <b>without re-rendering the 210 sheets</b>.
        ///
        /// <para>Use this when the sidecar's SHAPE changes but no pixel does: the sheets on disk
        /// stay exactly as they were baked, and one JSON is rewritten. Re-running the full fish
        /// bake would give the same sidecar at the cost of a long render and 210 files of churn,
        /// and a re-bake is not byte-deterministic, so it would also invalidate the slice guards
        /// for no reason.</para>
        ///
        /// <para>It reads the SAME weight bands as the bake, so the ladder it publishes is the one
        /// the sheets were actually rendered at. If the shipped defs' weight ranges have moved
        /// since the bake, this will say so by publishing a different ladder — and then the sheets
        /// genuinely are stale and want the full bake.</para>
        /// </summary>
        [MenuItem("Hidden Harbours/Art/Rewrite Catch Pass 2 Fish Anchors (sidecar only, no re-bake)",
                  priority = 50)]
        public static void RewriteCatchPass2FishAnchors()
        {
            try
            {
                var bands = ResolveWeightBands();
                CatchPass2BakeResult result = CatchPass2Baker.RewriteFishAnchors(bands);
                var sb = new StringBuilder();
                sb.AppendLine($"[CatchPass2BakeMenu] Rewrote {result.AnchorJsonPath} in " +
                              $"{result.TotalMilliseconds:F0} ms — no sheet was re-rendered.");
                foreach (var kv in result.Ladders)
                    sb.AppendLine($"  {kv.Key,-9} {string.Join(" | ", kv.Value)}");
                Debug.Log(sb.ToString().TrimEnd());
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CatchPass2BakeMenu] Anchor rewrite FAILED — the sidecar on " +
                               "disk is untouched.\n" + ex);
                throw;
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// The weight band each rig species' ladder spans, read from the shipped
        /// <see cref="FishSpeciesDef"/> assets.
        ///
        /// <para>A species is matched to a def the SAME way <c>RodKitImporter.BuildFishSpecies</c>
        /// matches one — the rig's sheet key must appear in the def's id, so <c>cod</c> finds
        /// <c>fish.atlantic_cod</c>. Using the importer's own rule is the point: if the two ever
        /// disagreed, a species would be baked against one def's weights and wired to another's.</para>
        ///
        /// <para>⚠️ Unlike the importer this REFUSES an ambiguous match rather than taking the first.
        /// A substring rule is only safe while the ids happen not to collide; the day content adds
        /// <c>fish.pollock_juvenile</c>, "first match wins" would silently re-band the pollock. A
        /// species with NO match is not an error — bass, flounder and herring have no def yet, and
        /// their ladders fall back to the rig's own <c>SPECIES.range</c>.</para>
        /// </summary>
        public static IReadOnlyDictionary<string, CatchPass2Baker.FishWeightBand> ResolveWeightBands()
        {
            FishSpeciesDef[] defs = AssetDatabase.FindAssets("t:FishSpeciesDef")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<FishSpeciesDef>)
                .Where(d => d != null && !string.IsNullOrEmpty(d.Id))
                .ToArray();

            var bands = new Dictionary<string, CatchPass2Baker.FishWeightBand>(StringComparer.Ordinal);

            using IRigScriptHost host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get(CatchPass2Baker.RigKey);
            RigCatalog.Install(host, entry);
            var species = FishingKitBaker.ReadStringArray(host, $"{entry.GlobalName}.ORDER");

            foreach (string key in species)
            {
                FishSpeciesDef[] hits = defs.Where(d => d.Id.Contains(key)).ToArray();
                if (hits.Length == 0) continue;
                if (hits.Length > 1)
                    throw new InvalidOperationException(
                        $"Rig species '{key}' matches {hits.Length} FishSpeciesDef ids " +
                        $"({string.Join(", ", hits.Select(h => h.Id))}). The ladder cannot pick a " +
                        "weight band, and RodKitImporter would silently take the first. Give the " +
                        "rig key a unique substring, or teach both sides an explicit mapping — do " +
                        "not let this resolve by luck.");

                FishSpeciesDef def = hits[0];
                if (def.MaxWeightKg <= 0f || def.MinWeightKg < 0f || def.MinWeightKg > def.MaxWeightKg)
                    throw new InvalidOperationException(
                        $"{def.Id} has an unusable weight band ({def.MinWeightKg}..{def.MaxWeightKg} kg). " +
                        "ContentValidationTests should have caught this before a bake did.");

                bands[key] = new CatchPass2Baker.FishWeightBand(def.Id, def.MinWeightKg, def.MaxWeightKg);
            }

            return bands;
        }

        static string Summarise(CatchPass2BakeResult r,
                                IReadOnlyDictionary<string, CatchPass2Baker.FishWeightBand> bands)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[CatchPass2BakeMenu] {r.Sheets.Count} sheet(s), {r.CellsRendered} cells, " +
                          $"{r.TotalPngBytes / 1024} KB, {r.TotalMilliseconds / 1000.0:F1}s " +
                          $"({r.EngineName}, convention {r.MeasuredConvention}).");

            sb.AppendLine("  ladder:");
            foreach (var kv in r.Ladders.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                string band = bands.ContainsKey(kv.Key)
                    ? bands[kv.Key].DefId
                    : "no def — SPECIES.range";
                sb.AppendLine($"    {kv.Key,-9} {string.Join("  ", kv.Value.Select(x => x.ToString()))}" +
                              $"   [{band}]");
            }

            if (r.Clipped.Count == 0)
            {
                sb.AppendLine("  clip ledger: EMPTY — nothing drew past its cell.");
            }
            else
            {
                sb.AppendLine($"  ⚠️ clip ledger: {r.Clipped.Count} cell(s) drew past the sheet cell. " +
                              "This is recorded, not fatal — cod at its own declared 1.5 and bass at " +
                              "its own 1.6 already clip, so refusing would make pass 2 unbakeable. " +
                              "CatchPass2BakeTests pins the ledger in both directions.");
                foreach (var group in r.Clipped.GroupBy(c => $"{c.Species}/{c.State} r{c.Rung} ({c.Edges})")
                                               .OrderBy(g => g.Key, StringComparer.Ordinal))
                    sb.AppendLine($"    {group.Key}: {group.Count()} cell(s)");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
