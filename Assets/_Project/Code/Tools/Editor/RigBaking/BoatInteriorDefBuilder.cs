using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Builds <see cref="BoatInteriorDef"/> assets from the boat-interiors kit — for the hulls the
    /// intake cleared, and for no others.</b>
    ///
    /// <para>Four gates stand between a sidecar and an asset, and each one is a REFUSAL rather than a
    /// warning, because the failure they guard against is silent: a cabin drawn from measurements of a
    /// boat that has since changed shape looks completely plausible and is wrong.</para>
    /// <list type="number">
    /// <item><b>S0.</b> The hull's row in <c>docs/art/rigs/boat-interiors-intake/s0-verdicts.json</c>
    /// must say CLEAN. Absent means UNKNOWN means refused.</item>
    /// <item><b>The interior renderer's SHA</b> must match the <c>boatInteriorRig.js</c> that shipped.</item>
    /// <item><b>The exterior loft's SHA</b> must match the hull rig the sidecar names — which is the
    /// REVISED rig bundled in the kit's <c>hull-rigs/</c>, never the repository's copy. See
    /// <see cref="ReadHullRig"/>: confusing the two refuses every sidecar in the drop.</item>
    /// <item><b>Every section must be understood.</b> A key this builder would drop stops the hull.</item>
    /// </list>
    /// Gates 2–4 live in <see cref="BoatInteriorSidecarReader"/>, which is pure and tested without an
    /// editor; this class is the filesystem and <c>AssetDatabase</c> around it.
    ///
    /// <para><b>It never touches <c>docs/art/rigs/gameplay/</c>.</b> The merge is computed and REPORTED
    /// (so the owner can see what adopting the kit's sections would do) and then thrown away. Those
    /// eleven files belong to art-director and are pinned by <c>DeckSidecarImportParityTests</c>.</para>
    /// </summary>
    public static class BoatInteriorDefBuilder
    {
        const string KitFolder = "docs/art/rigs/boat-interiors-kit";
        const string GameplayFolder = "docs/art/rigs/gameplay";
        const string RigFolder = "docs/art/rigs";
        const string InteriorFolder = "Assets/_Project/Data/Boats/Interiors";
        const string InteriorRigFileName = "boatInteriorRig.js";

        [MenuItem("Hidden Harbours/Dev/Boats/Build Boat Interior Defs (cleared hulls only)", priority = 123)]
        public static void BuildAll()
        {
            string report = Run(out int built, out int refused);
            Debug.Log(report);
            if (refused > 0)
                Debug.LogError($"[BoatInteriorDefBuilder] {refused} interior sidecar(s) REFUSED — see the " +
                               "report above. Those hulls have no interior def and cannot be entered; " +
                               "each refusal names what upstream must fix.");
            EditorUtility.DisplayDialog("Build boat interior defs",
                $"{built} built, {refused} refused.\n\nFull report in the Console.", "OK");
        }

        /// <summary>
        /// Headless entry (-executeMethod). Exits non-zero on any refusal: a refused sidecar is not a
        /// partial success, it is a hull the fleet quietly lost.
        /// </summary>
        public static void BuildAllCli()
        {
            try
            {
                Debug.Log(Run(out int built, out int refused));
                if (refused > 0)
                {
                    Debug.LogError($"[BoatInteriorDefBuilder] {refused} refused.");
                    EditorApplication.Exit(1);
                    return;
                }
                if (built == 0)
                {
                    Debug.LogError("[BoatInteriorDefBuilder] built nothing and refused nothing — the kit " +
                                   "was not found where it was expected.");
                    EditorApplication.Exit(1);
                    return;
                }
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoatInteriorDefBuilder] {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>The whole run, as a report. Separated from the menu so both entry points and any
        /// future test share one path.</summary>
        public static string Run(out int built, out int refused)
        {
            built = 0;
            refused = 0;
            var log = new StringBuilder("[BoatInteriorDefBuilder]\n");

            string repo = RepoRoot();
            string kit = Path.Combine(repo, KitFolder);
            if (!Directory.Exists(kit))
            {
                log.Append($"  kit not found at {KitFolder} — nothing to do.\n");
                return log.ToString();
            }

            // The one renderer every sidecar must pin. Read once: 27 sidecars hashing the same 100 kB
            // file 27 times is the kind of thing that makes a builder feel slow for no reason.
            string interiorRigPath = Path.Combine(kit, InteriorRigFileName);
            if (!File.Exists(interiorRigPath))
            {
                log.Append($"  {InteriorRigFileName} missing from the kit — every sidecar would refuse. " +
                           "Stopping.\n");
                return log.ToString();
            }
            byte[] interiorRigBytes = File.ReadAllBytes(interiorRigPath);
            log.Append($"  interior renderer: {InteriorRigFileName} " +
                       $"({DeckSidecarReader.Sha256Hex(interiorRigBytes).Substring(0, 12)}…)\n");

            Dictionary<string, BoatInteriorS0Entry> ledger;
            try { ledger = BoatInteriorS0Ledger.Parse(File.ReadAllText(Path.Combine(repo, BoatInteriorS0Ledger.LedgerPath))); }
            catch (Exception e)
            {
                log.Append($"  S0 ledger unreadable ({e.Message}) — refusing everything. A builder that " +
                           "cannot read the adjudication must not fall back to permissive.\n");
                return log.ToString();
            }
            log.Append($"  S0 ledger: {ledger.Count} hull(s) adjudicated\n");

            IReadOnlyList<HullSidecarIdentity> catalogue = ReadHullCatalogue(repo, log);
            EnsureFolder(InteriorFolder);

            string[] sidecars = Directory.GetFiles(kit, "*.interior.json", SearchOption.AllDirectories);
            Array.Sort(sidecars, StringComparer.Ordinal);
            log.Append($"  {sidecars.Length} interior sidecar(s) found\n\n");

            foreach (string path in sidecars)
            {
                string rel = RepoRelative(repo, path);
                if (BuildOne(repo, path, rel, interiorRigBytes, ledger, catalogue, log)) built++;
                else refused++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            log.Append($"\n  {built} built, {refused} refused.\n");
            return log.ToString();
        }

        // ---- one sidecar -----------------------------------------------------------------------------

        static bool BuildOne(string repo, string path, string rel, byte[] interiorRigBytes,
                             Dictionary<string, BoatInteriorS0Entry> ledger,
                             IReadOnlyList<HullSidecarIdentity> catalogue, StringBuilder log)
        {
            string json;
            try { json = File.ReadAllText(path); }
            catch (Exception e) { log.Append($"  ✗ {rel}: unreadable ({e.Message})\n"); return false; }

            // GATE 1 — S0, before anything else. Reading a refused hull's geometry at all invites
            // somebody to "just use it"; the adjudication is the first question, not the last.
            string hullStem = PeekHullStem(json);
            BoatInteriorS0Entry verdict = BoatInteriorS0Ledger.For(ledger, hullStem);
            if (!verdict.IsClean)
            {
                log.Append($"  ✗ {rel}\n      {BoatInteriorS0Ledger.RefusalMessage(verdict)}\n");
                return false;
            }

            // GATES 2–4 live in the reader. The exterior loft it names has to be found first.
            byte[] hullRigBytes = ReadHullRig(repo, json, out string hullRigRel);
            BoatInteriorRead read = BoatInteriorSidecarReader.Read(json, rel, interiorRigBytes, hullRigBytes);
            if (!read.Ok)
            {
                log.Append($"  ✗ {rel}\n");
                foreach (string e in read.Errors) log.Append($"      {e}\n");
                return false;
            }

            BoatInteriorHullResolver.Resolution resolved =
                BoatInteriorHullResolver.Resolve(read.HullStem, catalogue);
            if (!resolved.Ok)
            {
                log.Append($"  ✗ {rel}\n      {resolved.Error}\n");
                return false;
            }

            string assetPath = $"{InteriorFolder}/{BoatInteriorHullResolver.AssetName(resolved.HullFileStem)}.asset";
            BoatInteriorDef def = Upsert(assetPath, read, resolved.HullFileStem, hullRigRel);

            log.Append($"  ✓ {rel}\n      → {def.Id}  ({def.Levels.Length} level(s), " +
                       $"{def.Anchors.Length} anchor(s), {def.Routes.Length} route(s), " +
                       $"{def.PixelsPerMetre} px/m)\n");
            foreach (string n in read.Notes) log.Append($"      note: {n}\n");
            ReportRigDivergence(repo, read.HullRigStem, hullRigBytes, log);

            ReportMerge(repo, resolved.HullFileStem, json, log);
            return true;
        }

        static BoatInteriorDef Upsert(string assetPath, BoatInteriorRead read, string hullFileStem,
                                      string hullRigRel)
        {
            var def = AssetDatabase.LoadAssetAtPath<BoatInteriorDef>(assetPath);
            bool fresh = def == null;
            if (fresh) def = ScriptableObject.CreateInstance<BoatInteriorDef>();

            def.Id = BoatInteriorHullResolver.DefId(hullFileStem);
            def.FitsHulls = read.FitsHulls;
            def.SourceSidecar = read.SidecarPath;
            def.SourceInteriorRig = $"{KitFolder}/{read.InteriorRigFileName}";
            def.SourceHullRig = hullRigRel;
            def.InteriorRigSha256 = read.ExpectedInteriorRigSha;
            def.HullRigSha256 = read.ExpectedHullRigSha;
            def.PixelsPerMetre = read.PixelsPerMetre;
            def.CellPixels = read.CellPixels;
            def.PivotPixels = read.PivotPixels;
            def.FootprintOutline = read.Footprint;
            def.Levels = read.Levels.ToArray();
            def.Door = read.Door;
            def.AdditionalDoors = read.AdditionalDoors.ToArray();
            def.Anchors = read.Anchors.ToArray();
            def.Routes = read.Routes.ToArray();
            def.RidesHullRock = read.RidesHullRock;

            if (fresh) AssetDatabase.CreateAsset(def, assetPath);
            else EditorUtility.SetDirty(def);
            return def;
        }

        /// <summary>
        /// Compute the merge this interior WOULD make against the committed hull sidecar, and print it.
        /// Nothing is written — see the class doc. The value is that the owner can read, from the build
        /// log, exactly which sections adopting the kit would replace.
        /// </summary>
        static void ReportMerge(string repo, string hullFileStem, string interiorJson, StringBuilder log)
        {
            string basePath = Path.Combine(repo, GameplayFolder, hullFileStem + ".gameplay.json");
            if (!File.Exists(basePath))
            {
                log.Append($"      merge: no committed sidecar at {GameplayFolder}/{hullFileStem}" +
                           ".gameplay.json — nothing to merge against.\n");
                return;
            }

            InteriorMergeResult merge;
            try { merge = BoatInteriorGameplayMerge.Merge(File.ReadAllText(basePath), interiorJson); }
            catch (Exception e) { log.Append($"      merge: failed ({e.Message})\n"); return; }

            log.Append("      merge (DRY RUN — nothing written):\n");
            foreach (string c in merge.Changes) log.Append($"        · {c}\n");
            foreach (string e in merge.Errors) log.Append($"        ! {e}\n");
        }

        // ---- filesystem --------------------------------------------------------------------------------

        /// <summary>
        /// Every committed hull sidecar's identity. Enumerated NON-RECURSIVELY and read for its
        /// <c>rig</c>/<c>variant</c> fields — the same folder and the same shallow sweep
        /// <c>DeckSidecarImportParityTests</c> uses, so the two always see the same set of hulls.
        /// </summary>
        static IReadOnlyList<HullSidecarIdentity> ReadHullCatalogue(string repo, StringBuilder log)
        {
            var catalogue = new List<HullSidecarIdentity>();
            string folder = Path.Combine(repo, GameplayFolder);
            if (!Directory.Exists(folder))
            {
                log.Append($"  {GameplayFolder} not found — no hull can be resolved.\n");
                return catalogue;
            }

            string[] files = Directory.GetFiles(folder, "*.gameplay.json");
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string f in files)
            {
                try
                {
                    object root = DeckSidecarJson.Parse(File.ReadAllText(f));
                    catalogue.Add(new HullSidecarIdentity(
                        Path.GetFileName(f).Replace(".gameplay.json", ""),
                        DeckSidecarJson.String(DeckSidecarJson.Member(root, "rig")),
                        BoatInteriorHullResolver.VariantKeyOf(DeckSidecarJson.Member(root, "variant"))));
                }
                catch (Exception e)
                {
                    log.Append($"  hull catalogue: {Path.GetFileName(f)} unreadable ({e.Message})\n");
                }
            }
            log.Append($"  hull catalogue: {catalogue.Count} committed sidecar(s)\n");
            return catalogue;
        }

        /// <summary>
        /// The exterior rig an interior sidecar names, as bytes; null when it is not on disk (which the
        /// reader turns into a hull-rig refusal).
        ///
        /// <para><b>This reads the rig from the KIT, not from <c>docs/art/rigs/</c>, and the distinction
        /// is the whole point.</b> Every sidecar's <c>hullRigSha256</c> pins the REVISED rig shipped
        /// beside it in <c>hull-rigs/</c> — the one that publishes the loft the rooms were measured
        /// from. None of them pins the repository's copy, and none ever could: the repository's copy is
        /// by definition the version *before* the drop added the door and the loft. Hashing
        /// <c>docs/art/rigs/</c> here would therefore refuse all twenty-seven sidecars, including the
        /// ones that are perfectly consistent — which is exactly what it did until CI caught it.</para>
        ///
        /// <para>So this arm asks the question it can actually answer: <b>does this sidecar describe the
        /// rig that shipped with it?</b> Whether the REPOSITORY has since diverged from that rig is a
        /// different question, it is Axis B of the S0 adjudication, and it is already answered — per
        /// hull, with evidence — in the committed ledger. Conflating the two is what produced a wrong
        /// verdict for the cape.</para>
        /// </summary>
        static byte[] ReadHullRig(string repo, string interiorJson, out string repoRelative)
        {
            repoRelative = "";
            try
            {
                var pin = DeckSidecarJson.AsObject(
                    DeckSidecarJson.Member(DeckSidecarJson.Parse(interiorJson), "hullRigSha256"));
                if (pin == null || pin.Count != 1) return null;
                foreach (var kv in pin)
                {
                    repoRelative = $"{KitFolder}/hull-rigs/{kv.Key}.js";
                    string p = Path.Combine(repo, KitFolder, "hull-rigs", kv.Key + ".js");
                    return File.Exists(p) ? File.ReadAllBytes(p) : null;
                }
            }
            catch (Exception) { /* the reader reports an unparseable sidecar properly */ }
            return null;
        }

        /// <summary>
        /// Say whether the repository's copy of this hull's rig differs from the bundled one, and by how
        /// much. Reported on every run, never a refusal: for most hulls it differs precisely because the
        /// bundled revision ADDS the door and the published loft, which is the drop's entire purpose.
        ///
        /// <para>It is reported because the difference is the thing that decides whether a bundled rig
        /// may ever be copied over <c>docs/art/rigs/</c> — and because a hull where the two have
        /// diverged for some OTHER reason is the cape's situation, which cost a wrong verdict to find by
        /// hand. Printing it every run means the next one is noticed by reading the log.</para>
        /// </summary>
        static void ReportRigDivergence(string repo, string hullRigStem, byte[] bundled, StringBuilder log)
        {
            if (bundled == null || string.IsNullOrEmpty(hullRigStem)) return;
            string mainPath = Path.Combine(repo, RigFolder, hullRigStem + ".js");
            if (!File.Exists(mainPath))
            {
                log.Append($"      rig: {RigFolder}/{hullRigStem}.js is not in the repository — the " +
                           "bundled rig has no counterpart to diverge from.\n");
                return;
            }

            byte[] inRepo = File.ReadAllBytes(mainPath);
            if (DeckSidecarReader.MatchRigHash(inRepo, DeckSidecarReader.Sha256Hex(bundled), out string _)
                != RigHashMatch.None)
            {
                log.Append($"      rig: the repository's {hullRigStem}.js is IDENTICAL to the bundled one.\n");
                return;
            }
            log.Append($"      rig: the repository's {hullRigStem}.js DIFFERS from the bundled one " +
                       $"({inRepo.Length} B vs {bundled.Length} B). Expected where the revision adds the " +
                       "door and the published loft — but see the S0 ledger before anything copies the " +
                       "bundled file over the repository's.\n");
        }

        /// <summary>The sidecar's <c>hull_stem</c> without a full parse — gate 1 runs before the reader
        /// so that a refused hull's geometry is never read at all.</summary>
        static string PeekHullStem(string json)
        {
            try { return DeckSidecarJson.String(DeckSidecarJson.Member(DeckSidecarJson.Parse(json), "hull_stem")) ?? ""; }
            catch (Exception) { return ""; }
        }

        static string RepoRoot() => Directory.GetParent(Application.dataPath).FullName;

        static string RepoRelative(string repo, string path)
            => path.Substring(repo.Length).TrimStart('/', '\\').Replace('\\', '/');

        static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
            string leaf = Path.GetFileName(assetFolder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
